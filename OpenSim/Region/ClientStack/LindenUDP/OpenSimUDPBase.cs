/*
 * Copyright (c) 2006, Clutch, Inc.
 * Original Author: Jeff Cesnik
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.org nor the names 
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Base UDP server
    /// </summary>
    public abstract class OpenSimUDPBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This method is called when an incoming packet is received
        /// </summary>
        /// <param name="buffer">Incoming packet buffer</param>
        protected abstract void PacketReceived(UDPPacketBuffer buffer);

        /// <summary>
        /// Called when the base is through with the given packet
        /// </summary>
        /// <param name="packet"></param>
        protected abstract void SendCompleted(OutgoingPacket packet);

        /// <summary>UDP port to bind to in server mode</summary>
        protected int m_udpPort;

        /// <summary>Local IP address to bind to in server mode</summary>
        protected IPAddress m_localBindAddress;

        /// <summary>UDP socket, used in either client or server mode</summary>
        private Socket m_udpSocket;

        /// <summary>Flag to process packets asynchronously or synchronously</summary>
        private bool m_asyncPacketHandling;

        /// <summary>The all important shutdown flag</summary>
        private volatile bool m_shutdownFlag = true;

        /// <summary>Returns true if the server is currently listening, otherwise false</summary>
        public bool IsRunning { get { return !m_shutdownFlag; } }

        /// <summary>
        /// About 8MB maximum for recv pools
        /// </summary>
        private const int MAX_POOLED_BUFFERS = 2048;
        /// <summary>
        /// Pool of UDP buffers to use for receive operations 
        /// </summary>
        private OpenSim.Framework.ObjectPool<UDPPacketBuffer> _recvBufferPool = new OpenSim.Framework.ObjectPool<UDPPacketBuffer>(MAX_POOLED_BUFFERS);

        /// <summary>
        /// UDP socket buffer size
        /// </summary>
        private const int UDP_BUFSZ = 32768;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="bindAddress">Local IP address to bind the server to</param>
        /// <param name="port">Port to listening for incoming UDP packets on</param>
        public OpenSimUDPBase(IPAddress bindAddress, int port)
        {
            m_localBindAddress = bindAddress;
            m_udpPort = port;
        }

        /// <summary>
        /// Start the UDP server
        /// </summary>
        /// <param name="recvBufferSize">The size of the receive buffer for 
        /// the UDP socket. This value is passed up to the operating system 
        /// and used in the system networking stack. Use zero to leave this
        /// value as the default</param>
        /// <param name="asyncPacketHandling">Set this to true to start
        /// receiving more packets while current packet handler callbacks are
        /// still running. Setting this to false will complete each packet
        /// callback before the next packet is processed</param>
        /// <remarks>This method will attempt to set the SIO_UDP_CONNRESET flag
        /// on the socket to get newer versions of Windows to behave in a sane
        /// manner (not throwing an exception when the remote side resets the
        /// connection). This call is ignored on Mono where the flag is not
        /// necessary</remarks>
        public void Start(int recvBufferSize, bool asyncPacketHandling)
        {
            m_asyncPacketHandling = asyncPacketHandling;

            if (m_shutdownFlag)
            {
                const int SIO_UDP_CONNRESET = -1744830452;

                IPEndPoint ipep = new IPEndPoint(m_localBindAddress, m_udpPort);

                m_udpSocket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Dgram,
                    ProtocolType.Udp);

                try
                {
                    // This udp socket flag is not supported under mono, 
                    // so we'll catch the exception and continue
                    m_udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                    m_log.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag set");
                }
                catch (SocketException)
                {
                    m_log.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag not supported on this platform, ignoring");
                }

                if (recvBufferSize != 0)
                {
                    m_udpSocket.ReceiveBufferSize = recvBufferSize;
                }
                else
                {
                    m_udpSocket.ReceiveBufferSize = UDP_BUFSZ;
                }

                m_udpSocket.SendBufferSize = UDP_BUFSZ;

                m_udpSocket.Bind(ipep);

                // we're not shutting down, we're starting up
                m_shutdownFlag = false;

                // kick off an async receive.  The Start() method will return, the
                // actual receives will occur asynchronously and will be caught in
                // AsyncEndRecieve().
                AsyncBeginReceive();
            }
        }

        /// <summary>
        /// Stops the UDP server
        /// </summary>
        public void Stop()
        {
            if (!m_shutdownFlag)
            {
                // wait indefinitely for a writer lock.  Once this is called, the .NET runtime
                // will deny any more reader locks, in effect blocking all other send/receive
                // threads.  Once we have the lock, we set shutdownFlag to inform the other
                // threads that the socket is closed.
                m_shutdownFlag = true;
                m_udpSocket.Close();
            }
        }

        private void AsyncBeginReceive()
        {
            // allocate a packet buffer
            //WrappedObject<UDPPacketBuffer> wrappedBuffer = Pool.CheckOut();
            UDPPacketBuffer buf = _recvBufferPool.LeaseObject();

            if (!m_shutdownFlag)
            {
                bool recvFromRunning = false;
                while (!recvFromRunning)
                {
                    try
                    {
                        // kick off an async read
                        m_udpSocket.BeginReceiveFrom(
                            //wrappedBuffer.Instance.Data,
                            buf.Data,
                            0,
                            UDPPacketBuffer.DEFAULT_BUFFER_SIZE,
                            SocketFlags.None,
                            ref buf.RemoteEndPoint,
                            AsyncEndReceive,
                            buf);

                        recvFromRunning = true;
                    }
                    catch (SocketException e)
                    {
                        m_log.ErrorFormat("[LLUDP] SocketException thrown from UDP BeginReceiveFrom, retrying: {0}", e);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[LLUDP] Exception thrown from UDP BeginReceiveFrom: {0}", e);
                        //UDP is toast and will not recover. The sim is for all intents and purposes, dead.
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Called when the packet is built and this buffer is no longer in use
        /// </summary>
        /// <param name="buffer"></param>
        protected void PacketBuildingFinished(UDPPacketBuffer buffer)
        {
            buffer.ResetEndpoint();
            _recvBufferPool.ReturnObject(buffer);
        }

        private void AsyncEndReceive(IAsyncResult iar)
        {
            // Asynchronous receive operations will complete here through the call
            // to AsyncBeginReceive
            if (!m_shutdownFlag)
            {
                // Asynchronous mode will start another receive before the
                // callback for this packet is even fired. Very parallel :-)
                if (m_asyncPacketHandling)
                    AsyncBeginReceive();

                // get the buffer that was created in AsyncBeginReceive
                // this is the received data
                //WrappedObject<UDPPacketBuffer> wrappedBuffer = (WrappedObject<UDPPacketBuffer>)iar.AsyncState;
                //UDPPacketBuffer buffer = wrappedBuffer.Instance;
                UDPPacketBuffer buffer = (UDPPacketBuffer)iar.AsyncState;

                try
                {
                    // get the length of data actually read from the socket, store it with the
                    // buffer
                    buffer.DataLength = m_udpSocket.EndReceiveFrom(iar, ref buffer.RemoteEndPoint);

                    // call the abstract method PacketReceived(), passing the buffer that
                    // has just been filled from the socket read.
                    PacketReceived(buffer);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[LLUDP] Exception thrown from UDP EndReceiveFrom: {0}", e);
                }
                finally
                {
                    //wrappedBuffer.Dispose();

                    // Synchronous mode waits until the packet callback completes
                    // before starting the receive to fetch another packet
                    if (!m_asyncPacketHandling)
                        AsyncBeginReceive();
                }

            }
        }

        public void AsyncBeginSend(OutgoingPacket packet)
        {
            if (!m_shutdownFlag)
            {
                try
                {
                    packet.AddRef();

                    m_udpSocket.BeginSendTo(
                        packet.Buffer.Data,
                        0,
                        packet.DataSize,
                        SocketFlags.None,
                        packet.Destination,
                        AsyncEndSend,
                        packet);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }

        void AsyncEndSend(IAsyncResult result)
        {
            OutgoingPacket packet = (OutgoingPacket)result.AsyncState;

            try
            {
                m_udpSocket.EndSendTo(result);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                this.SendCompleted(packet);
            }
        }
    }
}
