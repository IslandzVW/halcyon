/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Diagnostics;
using System.Timers;
using log4net;
using System.Reflection;

namespace OpenSim.Region.CoreModules.Agent.Xfer
{
    public class XferModule : IRegionModule, IXfer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private Dictionary<string, FileData> NewFiles = new Dictionary<string, FileData>();
        private Dictionary<ulong, XferDownLoad> Transfers = new Dictionary<ulong, XferDownLoad>();

        private class FileData
        {
            public byte[] Data;
            public int Count;
        }

        private const int TIMEOUT_CHECK_INTERVAL = 1000;
        private Timer _timeoutTimer;

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;

            m_scene.RegisterModuleInterface<IXfer>(this);

            _timeoutTimer = new Timer(TIMEOUT_CHECK_INTERVAL);
            _timeoutTimer.Elapsed += new ElapsedEventHandler(_timeoutTimer_Elapsed);
            _timeoutTimer.Start();
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
            _timeoutTimer.Stop();
        }

        public string Name
        {
            get { return "XferModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region IXfer Members

        public bool AddNewFile(string fileName, byte[] data)
        {
            lock (NewFiles)
            {
                if (NewFiles.ContainsKey(fileName))
                {
                    NewFiles[fileName].Count++;
                    NewFiles[fileName].Data = data;
//                    m_log.WarnFormat("[Xfer]: Add({0}) {1}", NewFiles[fileName].Count, fileName);
                }
                else
                {
                    FileData fd = new FileData();
                    fd.Count = 1;
                    fd.Data = data;
                    NewFiles.Add(fileName, fd);
//                    m_log.WarnFormat("[Xfer]: Added {0}", fileName);
                }
            }

            return true;
        }


        #endregion

        private void _timeoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                //disable the timer so that we don't get reentry
                _timeoutTimer.Enabled = false;


                lock (NewFiles)
                {
                    List<ulong> deadXferIds = new List<ulong>();
                    foreach (XferModule.XferDownLoad download in Transfers.Values)
                    {
                        if (download.WindowTimeout)
                        {
                            if (download.XferTimeout || download.RetriesExceeded)
                            {
                                //the whole transfer is is taking too long and must be stopped
                                m_log.ErrorFormat("[Xfer] Canceling transfer {0} for {1} due to timeout", download.FileName, download.Client.Name);
                                //download.Client.SendAlertMessage("Transfer has failed to complete, please try again");
                                const int ERR_TIMEOUT = -23016;
                                download.Client.SendAbortXfer(download.XferID, ERR_TIMEOUT);
                                deadXferIds.Add(download.XferID);
                            }
                            else
                            {
                                //we need to resend the current window
                                download.ResendCurrentWindow();
                            }
                        }
                    }

                    foreach (ulong deadXferId in deadXferIds)
                    {
                        Transfers.Remove(deadXferId);
                    }
//                    m_log.WarnFormat("[Xfer]: Removed ID {0}", deadXferId);
                }
            }
            finally
            {
                //reenable the timer
                _timeoutTimer.Enabled = true;
            }
        }
        public void NewClient(IClientAPI client)
        {
            client.OnRequestXfer += RequestXfer;
            client.OnConfirmXfer += AckPacket;
            client.OnAbortXfer += AbortXfer;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="xferID"></param>
        /// <param name="fileName"></param>
        public void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName)
        {
            lock (NewFiles)
            {
                if (NewFiles.ContainsKey(fileName))
                {
                    if (!Transfers.ContainsKey(xferID))
                    {
                        byte[] fileData = NewFiles[fileName].Data;
                        XferDownLoad transaction = new XferDownLoad(fileName, fileData, xferID, remoteClient);

//                        m_log.WarnFormat("[Xfer]: Requested ID {0} {1}", xferID, fileName);

                        Transfers.Add(xferID, transaction);
                        transaction.StartSend();

                        // The transaction for this file is either complete or on its way
                        RemoveOrDecrement(fileName);
                    }
                }
                else
                {
                    m_log.WarnFormat("[Xfer]: {0} not found", fileName);
                }

            }
        }

        public void AbortXfer(IClientAPI remoteClient, ulong xferID)
        {
//            m_log.WarnFormat("[Xfer]: Abort ID {0}", xferID);
            lock (NewFiles)
            {
                if (Transfers.ContainsKey(xferID))
                    RemoveOrDecrement(Transfers[xferID].FileName);

                Transfers.Remove(xferID);
            }
        }



        public void AckPacket(IClientAPI remoteClient, ulong xferID, uint packet)
        {
            lock (NewFiles)
            {
                if (Transfers.ContainsKey(xferID))
                {
                    if (Transfers[xferID].AckPacket(packet))
                    {
                        Transfers.Remove(xferID);
                    }
                }
            }
        }

        private void RemoveOrDecrement(string fileName)
        {
            // NewFiles must be locked
            if (NewFiles.ContainsKey(fileName))
            {
//                m_log.WarnFormat("[Xfer]: Remove({0}) {1}", NewFiles[fileName].Count, fileName);
                if (NewFiles[fileName].Count == 1)
                    NewFiles.Remove(fileName);
                else
                    NewFiles[fileName].Count--;
            }
        }

        public class XferDownLoad : IComparable<XferDownLoad>
        {
            public const int WINDOW_SIZE = 8;
            public const int CHUNK_SIZE = 1000;
            public const int WINDOW_ACK_TIMEOUT = 3 * 1000;
            public const int MAX_WINDOW_RETRIES = 5;
            public const int MIN_TRANSFER_RATE_BYTES_SEC = 750;
            public const int MIN_XFER_TIMEOUT = 15000;

            private enum AckState : byte
            {
                UNACKED_UNSENT,
                UNACKED_SENT,
                ACKED
            }
            
            public IClientAPI Client;
            public byte[] Data = new byte[0];
            public string FileName = String.Empty;
            public uint Serial = 1;
            public ulong XferID = 0;

            private bool _sendComplete;
            private ulong _transferQueuedOn = Util.GetLongTickCount();
            private ulong _lastWindowTransmission = Util.GetLongTickCount();
            private ulong _xferTimeoutTime;
            private uint _windowLeftEdge = 0;
            private AckState[] _windowAcks = new AckState[WINDOW_SIZE];
            private int _windowRetryCount = 0;
            private uint _currentWindowSize = WINDOW_SIZE;
            private uint _desiredWindowSize = WINDOW_SIZE;



            public XferDownLoad(string fileName, byte[] data, ulong xferID, IClientAPI client)
            {
                // m_log.DebugFormat("[Xfer]: XferDownload ID {0} {1}", xferID, fileName);

                FileName = fileName;
                Data = data;
                XferID = xferID;
                Client = client;

                ulong calculatedXferTimeout = (ulong)((data.Length / MIN_TRANSFER_RATE_BYTES_SEC) * 1000);
                _xferTimeoutTime = Util.GetLongTickCount() + Math.Max(calculatedXferTimeout, MIN_XFER_TIMEOUT);
            }

            public XferDownLoad()
            {
            }

            public bool WindowTimeout
            {
                get
                {
                    return _lastWindowTransmission + WINDOW_ACK_TIMEOUT < Util.GetLongTickCount();
                }
            }

            public bool XferTimeout
            {
                get
                {
                    return _xferTimeoutTime < Util.GetLongTickCount();
                }
            }

            public bool RetriesExceeded
            {
                get
                {
                    return _windowRetryCount > MAX_WINDOW_RETRIES;
                }
            }

            /// <summary>
            /// Start a transfer
            /// </summary>
            /// <returns>True if the transfer is complete, false if not</returns>
            public void StartSend()
            {
                SendToWindowEdge(_currentWindowSize);
            }

            private uint DataPointerFromPacketNum(uint packetNum)
            {
                return packetNum * CHUNK_SIZE;
            }

            private void SendToWindowEdge(uint edgeRoom)
            {
                for (uint i = _currentWindowSize - edgeRoom; i < _currentWindowSize; i++)
                {
                    //for each unacked window slot, send the corresponding packet
                    bool finalPacket = SendChunk(i);
                    _windowAcks[i] = AckState.UNACKED_SENT;

                    if (finalPacket)
                    {
                        _sendComplete = true;
                        break;
                    }
                }

                _lastWindowTransmission = Util.GetLongTickCount();
            }

            private bool SendChunk(uint i)
            {
                uint packetNum = _windowLeftEdge + i;
                uint dataPointer = DataPointerFromPacketNum(packetNum);

                //determine the size of the chunk to be sent and if this is the final packet
                uint dataSize;
                bool finalPacket;
                if ((Data.Length - dataPointer) > CHUNK_SIZE)
                {
                    dataSize = CHUNK_SIZE;
                    finalPacket = false;
                }
                else
                {
                    //if the remaining size is less than or equal to the max chunk size, this
                    //is the final packet
                    dataSize = (uint)Data.Length - dataPointer;
                    finalPacket = true;
                }

                //use to simulate random packet drops
                /*if (rnd1.Next(10) == 0)
                {
                    return finalPacket;
                }*/
                //------------------------------------

                uint totalSize = dataSize;
                bool doEncodeSize = packetNum == 0;
                if (doEncodeSize)
                {
                    totalSize += 4;
                }

                byte[] transferData = new byte[totalSize];
                if (doEncodeSize) Array.Copy(Utils.IntToBytes(Data.Length), 0, transferData, 0, 4);
                Array.Copy(Data, dataPointer, transferData, doEncodeSize ? 4 : 0, dataSize);
                if (finalPacket) packetNum |= (uint)0x80000000;
                Client.SendXferPacket(XferID, packetNum, transferData);
                return finalPacket;
            }


            //use to simulate random packet drops
            //private Random rnd1 = new Random();

            /// <summary>
            /// Respond to an ack packet from the client
            /// </summary>
            /// <param name="packet"></param>
            /// <returns>True if the transfer is complete, false otherwise</returns>
            public bool AckPacket(uint packet)
            {
                //use to simulate random packet drops
                /*if (rnd1.Next(10) == 0)
                {
                    return false;
                }*/
                //------------------------------------

                //make sure the ack is inside the window
                if (packet >= _windowLeftEdge
                    && packet < _windowLeftEdge + _currentWindowSize)
                {
                    //slide the window forward to the last ack 
                    //and send more data
                    _windowAcks[packet - _windowLeftEdge] = AckState.ACKED;

                    if (! _sendComplete)
                    {
                        uint amt = SlideWindowForward(packet - _windowLeftEdge + 1);
                        if (amt > 0) SendToWindowEdge(amt);
                    }

                    if (_sendComplete && this.WindowFullyAcked())
                    {
                        // m_log.DebugFormat("[Xfer]: XferDownload ID {0} {1} completed", this.XferID, this.FileName);
                        return true;
                    }
                }

                return false;
            }

            private bool WindowFullyAcked()
            {
                if (Data.Length == 0)
                {
                    return true;
                }

                uint numPackets = (uint)Math.Ceiling(Data.Length / (float)CHUNK_SIZE);

                uint windowLastSlot = numPackets - _windowLeftEdge - 1;
                if (_windowAcks[windowLastSlot] == AckState.ACKED)
                {
                    return true;
                }

                return false;
            }

            private uint SlideWindowForward(uint slideAmount)
            {
                _windowLeftEdge += slideAmount;
                for (uint i = 0; i < _currentWindowSize; i++)
                {
                    if (i + slideAmount < _currentWindowSize)
                    {
                        _windowAcks[i] = _windowAcks[i+slideAmount];
                    }
                    else
                    {
                        _windowAcks[i] = AckState.UNACKED_UNSENT;
                    }
                }

                _windowRetryCount = 0;

                if (_currentWindowSize > _desiredWindowSize)
                {
                    //adjust the current window by either the difference in the current window and this
                    //window, or by the slide amount, whichever is lesser (shrinks the window from the left)
                    uint windowAdjustment = Math.Min(slideAmount, _currentWindowSize - _desiredWindowSize);

                    _currentWindowSize -= windowAdjustment;

                    //if the slide is greater than the window difference, we can send the new open right
                    //of the window
                    return slideAmount - windowAdjustment;
                }
                else
                {
                    return slideAmount;
                }
            }
        
            /// <summary>
            /// for use in a priority queue
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            public int CompareTo(XferDownLoad other)
            {
                if (_lastWindowTransmission < other._lastWindowTransmission)
                {
                    return -1;
                }

                if (_lastWindowTransmission > other._lastWindowTransmission)
                {
                    return 1;
                }

                return 0;
            }

            internal void ResendCurrentWindow()
            {
                _windowRetryCount++;
                // m_log.DebugFormat("[XFER]: Resending window (retry #{0}) for {1} after timeout. Window size: {2}", _windowRetryCount, this.Client.Name, _currentWindowSize);


                if (_currentWindowSize > 1)
                {
                    _desiredWindowSize = _currentWindowSize / 2;
                }

                for (int i = 0; i < _currentWindowSize; i++)
                {
                    _windowAcks[i] = AckState.UNACKED_UNSENT;
                }

                _sendComplete = false;
                SendToWindowEdge(_currentWindowSize);
            }
        }
    }
}
