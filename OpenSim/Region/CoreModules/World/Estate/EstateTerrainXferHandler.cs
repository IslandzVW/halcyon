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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.CoreModules.World.Estate
{

    public class EstateTerrainXferHandler
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private AssetBase m_asset;
        private int m_assetSize = 0;
        private int m_xferPacketSize = 1000;

        public delegate void TerrainUploadComplete(string name, byte[] filedata, IClientAPI remoteClient);

        public event TerrainUploadComplete TerrainUploadDone;

        //private string m_description = String.Empty;
        //private string m_name = String.Empty;
        //private UUID TransactionID = UUID.Zero;
        private sbyte type = 0;

        public ulong mXferID;
        private TerrainUploadComplete handlerTerrainUploadDone;

        public EstateTerrainXferHandler(IClientAPI pRemoteClient, string pClientFilename)
        {
            m_asset = new AssetBase();
            m_asset.FullID = UUID.Zero;
            m_asset.Type = type;
            m_asset.Data = new byte[0];
            m_asset.Name = pClientFilename;
            m_asset.Description = "empty";
            m_asset.Local = true;
            m_asset.Temporary = true;
        }

        public ulong XferID
        {
            get { return mXferID; }
        }

        public void RequestStartXfer(IClientAPI pRemoteClient)
        {
            mXferID = Util.GetNextXferID();
            pRemoteClient.SendXferRequest(mXferID, m_asset.Type, m_asset.FullID, 0, Utils.StringToBytes(m_asset.Name));
        }

        /// <summary>
        /// Process transfer data received from the client.
        /// </summary>
        /// <param name="xferID"></param>
        /// <param name="packetID"></param>
        /// <param name="data"></param>
        private const uint PACKETID_COMPLETE = 0x80000000;
        public void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            bool isComplete = false;
            if (XferID == xferID)
            {
                if ((packetID & PACKETID_COMPLETE) != 0)
                {
                    isComplete = true;
                    packetID &= ~PACKETID_COMPLETE;
                }

                if (packetID == 0)
                {
                    // First (special) header packet with length.
                    int headerLen = sizeof(uint);  // 'size' field below
                    m_assetSize = Utils.BytesToInt(data);
                    m_xferPacketSize = data.Length - headerLen;
                    // We must use any existing buffer, in case we didn't get the first packet first.
                    if ((m_asset.Data == null) || (m_asset.Data.Length < m_assetSize))
                        m_asset.Data = new byte[m_assetSize];
                    Array.Copy(data, headerLen, m_asset.Data, 0, data.Length - headerLen);
                }
                else
                {
                    // normal header, we already know how big it should be (m_xferPacketSize==1000).
                    if (m_asset.Data == null)
                        m_asset.Data = new byte[0]; // just in case we get the second packet first...

                    int offset = (int)packetID * m_xferPacketSize;  // destination for new data block
                    int datalen = data.Length;                  // additional data block size
                    if (datalen > m_xferPacketSize)
                    {
                        // new data packet won't fit based on m_xferPacketSize
                        datalen = m_xferPacketSize;
                    }

                    int newbuflen = offset + datalen;           // required size when inserting new block
                    int oldbuflen = m_asset.Data.Length;        // existing data size
                    if (newbuflen > oldbuflen)
                    {
                        // grow buffer
                        byte[] destinationArray = new byte[newbuflen];
                        Array.Copy(m_asset.Data, 0, destinationArray, 0, oldbuflen);
                        m_asset.Data = destinationArray;
                    }
                    // insert data at the correct offset
                    Array.Copy(data, 0, m_asset.Data, offset, datalen);
                }
                remoteClient.SendConfirmXfer(xferID, packetID);

                if (isComplete)
                {
                    SendCompleteMessage(remoteClient);
                }
            }
        }

        public void SendCompleteMessage(IClientAPI remoteClient)
        {
            handlerTerrainUploadDone = TerrainUploadDone;
            if (handlerTerrainUploadDone != null)
            {
                handlerTerrainUploadDone(m_asset.Name, m_asset.Data, remoteClient);
            }
        }
    }
}
