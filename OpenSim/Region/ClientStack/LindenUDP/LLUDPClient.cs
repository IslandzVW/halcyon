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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Net;
using System.Threading;
using log4net;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;

using TokenBucket = OpenSim.Region.ClientStack.LindenUDP.TokenBucket;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    #region Delegates

    /// <summary>
    /// Fired when updated networking stats are produced for this client
    /// </summary>
    /// <param name="inPackets">Number of incoming packets received since this
    /// event was last fired</param>
    /// <param name="outPackets">Number of outgoing packets sent since this
    /// event was last fired</param>
    /// <param name="unAckedBytes">Current total number of bytes in packets we
    /// are waiting on ACKs for</param>
    public delegate void PacketStats(int inPackets, int outPackets, int unAckedBytes);
    /// <summary>
    /// Fired when the queue for one or more packet categories is empty. This 
    /// event can be hooked to put more data on the empty queues
    /// </summary>
    /// <param name="category">Categories of the packet queues that are empty</param>
    public delegate void QueueEmpty(ThrottleOutPacketTypeFlags categories);

    #endregion Delegates

    /// <summary>
    /// Tracks state for a client UDP connection and provides client-specific methods
    /// </summary>
    public sealed class LLUDPClient
    {
        // TODO: Make this a config setting
        /// <summary>Percentage of the task throttle category that is allocated to avatar and prim
        /// state updates</summary>
        const float STATE_TASK_PERCENTAGE = 0.8f;

        /// <summary>
        /// 4 MB maximum outbound queue per avatar after this amount is reached non-reliable packets will
        /// be dropped
        /// </summary>
        const int MAX_TOTAL_QUEUE_SIZE = 4 * 1024 * 1024;


        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The number of packet categories to throttle on. If a throttle category is added
        /// or removed, this number must also change</summary>
        public const int THROTTLE_CATEGORY_COUNT = 8;

        /// <summary>Fired when updated networking stats are produced for this client</summary>
        public event PacketStats OnPacketStats;
        /// <summary>Fired when the queue for a packet category is empty. This event can be
        /// hooked to put more data on the empty queue</summary>
        public event QueueEmpty OnQueueEmpty;

        /// <summary>AgentID for this client</summary>
        public readonly UUID AgentID;
        /// <summary>The remote address of the connected client</summary>
        public readonly IPEndPoint RemoteEndPoint;
        /// <summary>Circuit code that this client is connected on</summary>
        public readonly uint CircuitCode;
        /// <summary>Sequence numbers of packets we've received (for duplicate checking)</summary>
        public readonly IncomingPacketHistoryCollection PacketArchive = new IncomingPacketHistoryCollection(384);
        /// <summary>Packets we have sent that need to be ACKed by the client</summary>
        public readonly UnackedPacketCollection NeedAcks;
        /// <summary>ACKs that are queued up, waiting to be sent to the client</summary>
        public readonly OpenSim.Framework.LocklessQueue<uint> PendingAcks = new OpenSim.Framework.LocklessQueue<uint>();

        /// <summary>Current packet sequence number</summary>
        public int CurrentSequence;
        /// <summary>Current ping sequence number</summary>
        public byte CurrentPingSequence;
        /// <summary>True when this connection is alive, otherwise false</summary>
        public bool IsConnected = true;
        /// <summary>True when this connection is paused, otherwise false</summary>
        public bool IsPaused;
        /// <summary>Environment.TickCount when the last packet was received for this client</summary>
        public int TickLastPacketReceived;

        /// <summary>Smoothed round-trip time. A smoothed average of the round-trip time for sending a
        /// reliable packet to the client and receiving an ACK</summary>
        public float SRTT;
        /// <summary>Round-trip time variance. Measures the consistency of round-trip times</summary>
        public float RTTVAR;
        /// <summary>Retransmission timeout. Packets that have not been acknowledged in this number of
        /// milliseconds or longer will be resent</summary>
        /// <remarks>Calculated from <seealso cref="SRTT"/> and <seealso cref="RTTVAR"/> using the
        /// guidelines in RFC 2988</remarks>
        public int RTO;
        /// <summary>Number of bytes received since the last acknowledgement was sent out. This is used
        /// to loosely follow the TCP delayed ACK algorithm in RFC 1122 (4.2.3.2)</summary>
        public int BytesSinceLastACK;
        /// <summary>Number of packets received from this client</summary>
        public int PacketsReceived;
        /// <summary>Number of packets sent to this client</summary>
        public int PacketsSent;
        /// <summary>Total byte count of unacked packets sent to this client</summary>
        public int UnackedBytes;

        /// <summary>Total number of received packets that we have reported to the OnPacketStats event(s)</summary>
        private int m_packetsReceivedReported;
        /// <summary>Total number of sent packets that we have reported to the OnPacketStats event(s)</summary>
        private int m_packetsSentReported;
        /// <summary>Holds the Environment.TickCount value of when the next OnQueueEmpty can be fired</summary>
        private int m_nextOnQueueEmpty = 1;

        /// <summary>Throttle bucket for this agent's connection</summary>
        private readonly TokenBucket m_throttle;
        /// <summary>Throttle buckets for each packet category</summary>
        private readonly TokenBucket[] m_throttleCategories;
        /// <summary>Outgoing queues for throttled packets</summary>
        private readonly OpenSim.Framework.LocklessQueue<OutgoingPacket>[] m_packetOutboxes = new OpenSim.Framework.LocklessQueue<OutgoingPacket>[THROTTLE_CATEGORY_COUNT];
        /// <summary>A container that can hold one packet for each outbox, used to store
        /// dequeued packets that are being held for throttling</summary>
        private readonly OutgoingPacket[] m_nextPackets = new OutgoingPacket[THROTTLE_CATEGORY_COUNT];
        /// <summary>A reference to the LLUDPServer that is managing this client</summary>
        private readonly LLUDPServer m_udpServer;

        

        /// <summary>Caches packed throttle information</summary>
        private UnpackedThrottles m_unpackedThrottles;

        private int m_defaultRTO = 3000;
        private int m_maxRTO = 60000;


        /// <summary>
        /// The current size of all packets on the outbound queue
        /// </summary>
        private int _currentOutboundQueueSize = 0;

        /// <summary>
        /// Records the last time an adjustment was made to the packet throttles
        /// </summary>
        private DateTime _lastDynamicThrottleAdjustment = DateTime.Now;

        /// <summary>
        /// The minmum amount of time to wait to report repeated dropped packets in seconds
        /// </summary>
        const int MIN_DROP_REPORT_INTERVAL = 60;

        /// <summary>
        /// The last time a packet drop was reported to the console
        /// </summary>
        private DateTime _lastDropReport = DateTime.Now;
        private object _dropReportLock = new object();

        /// <summary>
        /// Random number generator to select random streams to start sending data on
        /// during the dequeue phase
        /// </summary>
        private Random _rand = new Random();

        /// <summary>
        /// keeps tract of what buckets were empty on the last dequeue to inform
        /// the throttle adjustment code
        /// </summary>
        private List<int> _emptyBucketHints = new List<int>();

        /// <summary>
        /// The current size of our queue
        /// </summary>
        public int OutboundQueueSize
        {
            get { return _currentOutboundQueueSize; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="server">Reference to the UDP server this client is connected to</param>
        /// <param name="rates">Default throttling rates and maximum throttle limits</param>
        /// <param name="parentThrottle">Parent HTB (hierarchical token bucket)
        /// that the child throttles will be governed by</param>
        /// <param name="circuitCode">Circuit code for this connection</param>
        /// <param name="agentID">AgentID for the connected agent</param>
        /// <param name="remoteEndPoint">Remote endpoint for this connection</param>
        public LLUDPClient(LLUDPServer server, ThrottleRates rates, TokenBucket parentThrottle, uint circuitCode, UUID agentID, IPEndPoint remoteEndPoint, int defaultRTO, int maxRTO)
        {
            AgentID = agentID;
            RemoteEndPoint = remoteEndPoint;
            CircuitCode = circuitCode;
            m_udpServer = server;
            if (defaultRTO != 0)
                m_defaultRTO = defaultRTO;
            if (maxRTO != 0)
                m_maxRTO = maxRTO;

            // Create a token bucket throttle for this client that has the scene token bucket as a parent
            m_throttle = new TokenBucket(parentThrottle, rates.TotalLimit, rates.Total);
            // Create an array of token buckets for this clients different throttle categories
            m_throttleCategories = new TokenBucket[THROTTLE_CATEGORY_COUNT];

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                ThrottleOutPacketType type = (ThrottleOutPacketType)i;

                // Initialize the packet outboxes, where packets sit while they are waiting for tokens
                m_packetOutboxes[i] = new OpenSim.Framework.LocklessQueue<OutgoingPacket>();
                // Initialize the token buckets that control the throttling for each category
                m_throttleCategories[i] = new TokenBucket(m_throttle, rates.GetLimit(type), rates.GetRate(type));
            }

            // Default the retransmission timeout to three seconds
            RTO = m_defaultRTO;

            // Initialize this to a sane value to prevent early disconnects
            TickLastPacketReceived = Environment.TickCount & Int32.MaxValue;

            NeedAcks = new UnackedPacketCollection(server.ByteBufferPool);
        }

        /// <summary>
        /// Shuts down this client connection
        /// </summary>
        public void Shutdown()
        {
            IsConnected = false;
            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                m_packetOutboxes[i].Clear();
                m_nextPackets[i] = null;
            }
            OnPacketStats = null;
            OnQueueEmpty = null;
        }


        /// <summary>
        /// Modifies the UDP throttles
        /// </summary>
        /// <param name="info">New throttling values</param>
        public void SetClientInfo(ClientInfo info)
        {
            // TODO: Allowing throttles to be manually set from this function seems like a reasonable
            // idea. On the other hand, letting external code manipulate our ACK accounting is not
            // going to happen
            throw new NotImplementedException();
        }

        public string GetStats()
        {
            // TODO: ???
            return string.Format("{0,7} {1,7} {2,7} {3,7} {4,7} {5,7} {6,7} {7,7} {8,7} {9,7}",
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public void SendPacketStats()
        {
            PacketStats callback = OnPacketStats;
            if (callback != null)
            {
                int newPacketsReceived = PacketsReceived - m_packetsReceivedReported;
                int newPacketsSent = PacketsSent - m_packetsSentReported;

                callback(newPacketsReceived, newPacketsSent, UnackedBytes);

                m_packetsReceivedReported += newPacketsReceived;
                m_packetsSentReported += newPacketsSent;
            }
        }

        public bool SetThrottles(byte[] throttleData)
        {
            byte[] adjData;
            int pos = 0;

            if (!BitConverter.IsLittleEndian)
            {
                byte[] newData = new byte[7 * 4];
                Buffer.BlockCopy(throttleData, 0, newData, 0, 7 * 4);

                for (int i = 0; i < 7; i++)
                    Array.Reverse(newData, i * 4, 4);

                adjData = newData;
            }
            else
            {
                adjData = throttleData;
            }

            // These adjData values come in from the viewer in bits. (Not Kbits, not bytes, not Kbytes.)
            int resend = (int)(BitConverter.ToSingle(adjData, pos)); pos += 4;
            int land = (int)(BitConverter.ToSingle(adjData, pos)); pos += 4;
            int wind = (int)(BitConverter.ToSingle(adjData, pos)); pos += 4;
            int cloud = (int)(BitConverter.ToSingle(adjData, pos)); pos += 4;
            int task = (int)(BitConverter.ToSingle(adjData, pos)); pos += 4;
            int texture = (int)(BitConverter.ToSingle(adjData, pos)); pos += 4;
            int asset = (int)(BitConverter.ToSingle(adjData, pos));
            // m_log.DebugFormat("[LLUDPCLIENT]: throttles: Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, Total={7}", resend, land, wind, cloud, task, texture, asset, resend + land + wind + cloud + task + texture + asset);

            // undo LL's 50% hack first, this yields original bps rates (from viewer preferences).
            resend = (int)(resend / 1.5f);
            land = (int)(land / 1.5f);
            wind = (int)(wind / 1.5f);
            cloud = (int)(cloud / 1.5f);
            task = (int)(task / 1.5f);
            texture = (int)(texture / 1.5f);
            asset = (int)(asset / 1.5f);

            // convert from bits to bytes (sometimes not multiples of 8)
            resend = (resend + 7)/8;
            land = (land + 7) / 8;
            wind = (wind + 7) / 8;
            cloud = (cloud + 7) / 8;
            task = (task + 7) / 8;
            texture = (texture + 7) / 8;
            asset = (asset + 7) / 8;
            // Now these category sizes are in bytes, for server use, including matching units for MTU check.

            // State is a subcategory of task that we allocate a percentage to
            int state = (int)((float)task * STATE_TASK_PERCENTAGE);
            task -= state;

            // Make sure none of the throttles are set below our packet MTU,
            // otherwise a throttle could become permanently clogged
            resend = Math.Max(resend, LLUDPServer.MTU);
            land = Math.Max(land, LLUDPServer.MTU);
            wind = Math.Max(wind, LLUDPServer.MTU);
            cloud = Math.Max(cloud, LLUDPServer.MTU);
            task = Math.Max(task, LLUDPServer.MTU);
            texture = Math.Max(texture, LLUDPServer.MTU);
            asset = Math.Max(asset, LLUDPServer.MTU);
            state = Math.Max(state, LLUDPServer.MTU);

            // Let's calculate some convenience totals.
            int totalBytes = resend + land + wind + cloud + task + texture + asset + state;
            int totalBits = totalBytes * 8;
            int totalKBits = totalBits / 1024;
            // m_log.DebugFormat("[UDP]: Throttle task={0} of {1} Kbps", (task * 8) / 1024, totalKBits);

            //m_log.InfoFormat("[LLUDP] Client {0} throttle {1}", AgentID, total); 
            //m_log.DebugFormat("[LLUDPCLIENT]: {0} is setting throttles. Resend={1}, Land={2}, Wind={3}, Cloud={4}, Task={5}, Texture={6}, Asset={7}, State={8}, Total={9}",
            //    AgentID, resend, land, wind, cloud, task+state, texture, asset, state, total);

            // Update the token buckets with new throttle values
            TokenBucket bucket;
            int oldRate = m_throttle.DripRate;
            // DripRates are in bytes per second, throttles for encoding to viewer are in bits per second.
            int oldKBits = (oldRate * 8) / 1024;    // convert to bits then KBits
            bool throttleChanged = (m_throttle.DripRate != m_throttle.NormalizedDripRate(totalBytes));
            if (throttleChanged) m_log.InfoFormat("[LLUDPCLIENT]: Viewer agent bandwidth throttle request {0} kbps -> {1} kbps.", oldKBits, totalKBits);

            // Bucket drip/burst rates are in bytes per second (stored internally as bytes per millisecond)
            bucket = m_throttle;
            bucket.DripRate = totalBytes;
            bucket.MaxBurst = totalBytes;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Resend];
            bucket.DripRate = resend;
            bucket.MaxBurst = resend;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Land];
            bucket.DripRate = land;
            bucket.MaxBurst = land;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Wind];
            bucket.DripRate = wind;
            bucket.MaxBurst = wind;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Cloud];
            bucket.DripRate = cloud;
            bucket.MaxBurst = cloud;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Asset];
            bucket.DripRate = asset;
            bucket.MaxBurst = asset;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Task];
            bucket.DripRate = task + state;
            bucket.MaxBurst = task + state;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.State];
            bucket.DripRate = state;
            bucket.MaxBurst = state;

            bucket = m_throttleCategories[(int)ThrottleOutPacketType.Texture];
            bucket.DripRate = texture;
            bucket.MaxBurst = texture;

            // Reset the packed throttles cached data
            m_unpackedThrottles = null;

            return throttleChanged;
        }

        internal UnpackedThrottles GetThrottlesUnpacked()
        {
            UnpackedThrottles throttles = m_unpackedThrottles;

            if (throttles == null)
            {
                float[] fthrottles = new float[THROTTLE_CATEGORY_COUNT - 1];
                int i = 0;

                // DripRates are in bytes per second, throttles for encoding to viewer are in bits per second.
                fthrottles[i++] = (float)m_throttleCategories[(int)ThrottleOutPacketType.Resend].DripRate * 8;
                fthrottles[i++] = (float)m_throttleCategories[(int)ThrottleOutPacketType.Land].DripRate * 8;
                fthrottles[i++] = (float)m_throttleCategories[(int)ThrottleOutPacketType.Wind].DripRate * 8;
                fthrottles[i++] = (float)m_throttleCategories[(int)ThrottleOutPacketType.Cloud].DripRate * 8;
                fthrottles[i++] = (float)(m_throttleCategories[(int)ThrottleOutPacketType.Task].DripRate * 8 +
                                          m_throttleCategories[(int)ThrottleOutPacketType.State].DripRate * 8);
                fthrottles[i++] = (float)m_throttleCategories[(int)ThrottleOutPacketType.Texture].DripRate * 8;
                fthrottles[i++] = (float)m_throttleCategories[(int)ThrottleOutPacketType.Asset].DripRate * 8;

                throttles = new UnpackedThrottles(fthrottles);
                m_unpackedThrottles = throttles;
            }

            //m_log.DebugFormat("[THROTTLE]: Task throttle resend={0} and task={1} kpbs of {2} kbps.",
            //    m_throttleCategories[(int)ThrottleOutPacketType.Resend].DripRate * 8 / 1024,
            //    m_throttleCategories[(int)ThrottleOutPacketType.Task].DripRate * 8 / 1024,
            //    m_throttle.DripRate * 8 / 1024);
            return throttles;
        }

        public void TestReportPacketDrop(OutgoingPacket packet)
        {
            lock (_dropReportLock)
            {
                if (DateTime.Now - _lastDropReport > TimeSpan.FromSeconds(MIN_DROP_REPORT_INTERVAL))
                {
                    _lastDropReport = DateTime.Now;
                    m_log.WarnFormat("[LLUDP] Packets are being dropped for {0} due to overfilled outbound queue, last packet type {1}",
                        AgentID, packet.Type);
                }
            }
        }

        public void TestReportPacketShouldDrop(OutgoingPacket packet)
        {
            lock (_dropReportLock)
            {
                if (DateTime.Now - _lastDropReport > TimeSpan.FromSeconds(MIN_DROP_REPORT_INTERVAL))
                {
                    _lastDropReport = DateTime.Now;
                    m_log.WarnFormat("[LLUDP] Packet should've been dropped for {0} due to overfilled outbound queue, but was reliable. Last packet type {1}",
                        AgentID, packet.Type);
                }
            }
        }

        public void TestReportCriticalPacketDrop(OutgoingPacket packet)
        {
            lock (_dropReportLock)
            {
                if (DateTime.Now - _lastDropReport > TimeSpan.FromSeconds(MIN_DROP_REPORT_INTERVAL))
                {
                    _lastDropReport = DateTime.Now;
                    m_log.WarnFormat("[LLUDP] Reliable packets are being dropped for {0} due to overfilled outbound queue. Last packet type {1}",
                        AgentID, packet.Type);
                }
            }
        }

        public bool EnqueueOutgoing(OutgoingPacket packet)
        {
            int category = (int)packet.Category;

            if (category >= 0 && category < m_packetOutboxes.Length)
            {
                OpenSim.Framework.LocklessQueue<OutgoingPacket> queue = m_packetOutboxes[category];

                // Not enough tokens in the bucket, queue this packet

                //check the queue
                //Dont drop resends this can mess up the buffer pool as well as make the connection situation much worse
                if (_currentOutboundQueueSize > MAX_TOTAL_QUEUE_SIZE && (packet.Buffer.Data[0] & Helpers.MSG_RESENT) == 0)
                {
                    //queue already has too much data in it..
                    //can we drop this packet?
                    byte flags = packet.Buffer.Data[0];
                    bool isReliable = (flags & Helpers.MSG_RELIABLE) != 0;

                    if (!isReliable 
                        && packet.Type != PacketType.PacketAck
                        && packet.Type != PacketType.CompletePingCheck)
                    {
                        //packet is unreliable and will be dropped
                        this.TestReportPacketDrop(packet);
                        packet.DecRef(m_udpServer.ByteBufferPool);
                    }
                    else
                    {
                        if (_currentOutboundQueueSize < MAX_TOTAL_QUEUE_SIZE * 1.5)
                        {
                            this.TestReportPacketShouldDrop(packet);
                            Interlocked.Add(ref _currentOutboundQueueSize, packet.DataSize);
                            packet.AddRef();
                            queue.Enqueue(packet);
                        }
                        else
                        {
                            //this connection is in a pretty critical state and probably will never catch up.
                            //drop all packets until we start to catch up. This includes acks which will disconnect
                            //the client eventually anyways

                            this.TestReportCriticalPacketDrop(packet);
                            packet.DecRef(m_udpServer.ByteBufferPool);
                        }
                    }
                }
                else
                {
                    Interlocked.Add(ref _currentOutboundQueueSize, packet.DataSize);

                    packet.AddRef();
                    queue.Enqueue(packet);
                }

                return true;
            }
            else
            {
                // We don't have a token bucket for this category, so it will not be queued
                return false;
            }
        }

        /// <summary>
        /// Loops through all of the packet queues for this client and tries to send
        /// any outgoing packets, obeying the throttling bucket limits
        /// </summary>
        /// <remarks>This function is only called from a synchronous loop in the
        /// UDPServer so we don't need to bother making this thread safe</remarks>
        /// <returns>True if any packets were sent, otherwise false</returns>
        public bool DequeueOutgoing()
        {
            OutgoingPacket packet;
            OpenSim.Framework.LocklessQueue<OutgoingPacket> queue;
            TokenBucket bucket;
            bool packetSent = false;
            ThrottleOutPacketTypeFlags emptyCategories = 0;

            //string queueDebugOutput = String.Empty; // Serious debug business

            int randStart = _rand.Next(7);
            for (int j = 0; j < THROTTLE_CATEGORY_COUNT; j++)
            {
                int i = (j + randStart) % THROTTLE_CATEGORY_COUNT;

                bucket = m_throttleCategories[i];
                //queueDebugOutput += m_packetOutboxes[i].Count + " ";  // Serious debug business

                if (m_nextPackets[i] != null)
                {
                    // This bucket was empty the last time we tried to send a packet,
                    // leaving a dequeued packet still waiting to be sent out. Try to
                    // send it again
                    OutgoingPacket nextPacket = m_nextPackets[i];
                    if (bucket.RemoveTokens(nextPacket.DataSize))
                    {
                        // Send the packet
                        Interlocked.Add(ref _currentOutboundQueueSize, -nextPacket.DataSize);
                        m_udpServer.SendPacketFinal(nextPacket);
                        nextPacket.DecRef(m_udpServer.ByteBufferPool);
                        m_nextPackets[i] = null;
                        packetSent = true;
                        this.PacketsSent++;
                    }
                }
                else
                {
                    // No dequeued packet waiting to be sent, try to pull one off
                    // this queue
                    queue = m_packetOutboxes[i];
                    if (queue.Dequeue(out packet))
                    {
                        // A packet was pulled off the queue. See if we have
                        // enough tokens in the bucket to send it out
                        if (bucket.RemoveTokens(packet.DataSize))
                        {
                            // Send the packet
                            Interlocked.Add(ref _currentOutboundQueueSize, -packet.DataSize);
                            m_udpServer.SendPacketFinal(packet);
                            packet.DecRef(m_udpServer.ByteBufferPool);
                            packetSent = true;
                            this.PacketsSent++;
                        }
                        else
                        {
                            // Save the dequeued packet for the next iteration
                            m_nextPackets[i] = packet;
                            _emptyBucketHints.Add(i);
                        }

                        // If the queue is empty after this dequeue, fire the queue
                        // empty callback now so it has a chance to fill before we 
                        // get back here
                        if (queue.Count == 0)
                            emptyCategories |= CategoryToFlag(i);
                    }
                    else
                    {
                        // No packets in this queue. Fire the queue empty callback
                        // if it has not been called recently
                        emptyCategories |= CategoryToFlag(i);
                    }
                }
            }

            if (emptyCategories != 0)
                BeginFireQueueEmpty(emptyCategories);

            //m_log.Info("[LLUDPCLIENT]: Queues: " + queueDebugOutput); // Serious debug business
            return packetSent;
        }

        /// <summary>
        /// Called when an ACK packet is received and a round-trip time for a
        /// packet is calculated. This is used to calculate the smoothed
        /// round-trip time, round trip time variance, and finally the
        /// retransmission timeout
        /// </summary>
        /// <param name="r">Round-trip time of a single packet and its
        /// acknowledgement</param>
        public void UpdateRoundTrip(float r)
        {
            const float ALPHA = 0.125f;
            const float BETA = 0.25f;
            const float K = 4.0f;

            if (RTTVAR == 0.0f)
            {
                // First RTT measurement
                SRTT = r;
                RTTVAR = r * 0.5f;
            }
            else
            {
                // Subsequence RTT measurement
                RTTVAR = (1.0f - BETA) * RTTVAR + BETA * Math.Abs(SRTT - r);
                SRTT = (1.0f - ALPHA) * SRTT + ALPHA * r;
            }

            int rto = (int)(SRTT + Math.Max(m_udpServer.TickCountResolution, K * RTTVAR));

            // Clamp the retransmission timeout to manageable values
            rto = Utils.Clamp(rto, m_defaultRTO, m_maxRTO);

            RTO = rto;

            //m_log.Debug("[LLUDPCLIENT]: Setting agent " + this.Agent.FullName + "'s RTO to " + RTO + "ms with an RTTVAR of " +
            //    RTTVAR + " based on new RTT of " + r + "ms");
        }

        /// <summary>
        /// Exponential backoff of the retransmission timeout, per section 5.5
        /// of RFC 2988
        /// </summary>
        public void BackoffRTO()
        {
            // Reset SRTT and RTTVAR, we assume they are bogus since things
            // didn't work out and we're backing off the timeout
            SRTT = 0.0f;
            RTTVAR = 0.0f;

            // Double the retransmission timeout
            RTO = Math.Min(RTO * 2, m_maxRTO);
        }

        /// <summary>
        /// Does an early check to see if this queue empty callback is already
        /// running, then asynchronously firing the event
        /// </summary>
        /// <param name="throttleIndex">Throttle category to fire the callback
        /// for</param>
        private void BeginFireQueueEmpty(ThrottleOutPacketTypeFlags categories)
        {
            if (m_nextOnQueueEmpty != 0 && (Environment.TickCount & Int32.MaxValue) >= m_nextOnQueueEmpty)
            {
                // Use a value of 0 to signal that FireQueueEmpty is running
                m_nextOnQueueEmpty = 0;
                // Asynchronously run the callback
                Util.FireAndForget(FireQueueEmpty, categories);
            }
        }

        /// <summary>
        /// Fires the OnQueueEmpty callback and sets the minimum time that it
        /// can be called again
        /// </summary>
        /// <param name="o">Throttle categories to fire the callback for,
        /// stored as an object to match the WaitCallback delegate
        /// signature</param>
        private void FireQueueEmpty(object o)
        {
            const int MIN_CALLBACK_MS = 30;

            ThrottleOutPacketTypeFlags categories = (ThrottleOutPacketTypeFlags)o;
            QueueEmpty callback = OnQueueEmpty;
            
            int start = Environment.TickCount & Int32.MaxValue;

            if (callback != null)
            {
                try { callback(categories); }
                catch (Exception e) { m_log.Error("[LLUDPCLIENT]: OnQueueEmpty(" + categories + ") threw an exception: " + e.Message, e); }
            }

            m_nextOnQueueEmpty = start + MIN_CALLBACK_MS;
            if (m_nextOnQueueEmpty == 0)
                m_nextOnQueueEmpty = 1;
        }

        /// <summary>
        /// Converts a <seealso cref="ThrottleOutPacketType"/> integer to a
        /// flag value
        /// </summary>
        /// <param name="i">Throttle category to convert</param>
        /// <returns>Flag representation of the throttle category</returns>
        private static ThrottleOutPacketTypeFlags CategoryToFlag(int i)
        {
            ThrottleOutPacketType category = (ThrottleOutPacketType)i;

            /*
             * Land = 1,
        /// <summary>Wind data</summary>
        Wind = 2,
        /// <summary>Cloud data</summary>
        Cloud = 3,
        /// <summary>Any packets that do not fit into the other throttles</summary>
        Task = 4,
        /// <summary>Texture assets</summary>
        Texture = 5,
        /// <summary>Non-texture assets</summary>
        Asset = 6,
        /// <summary>Avatar and primitive data</summary>
        /// <remarks>This is a sub-category of Task</remarks>
        State = 7,
             */

            switch (category)
            {
                case ThrottleOutPacketType.Land:
                    return ThrottleOutPacketTypeFlags.Land;
                case ThrottleOutPacketType.Wind:
                    return ThrottleOutPacketTypeFlags.Wind;
                case ThrottleOutPacketType.Cloud:
                    return ThrottleOutPacketTypeFlags.Cloud;
                case ThrottleOutPacketType.Task:
                    return ThrottleOutPacketTypeFlags.Task;
                case ThrottleOutPacketType.Texture:
                    return ThrottleOutPacketTypeFlags.Texture;
                case ThrottleOutPacketType.Asset:
                    return ThrottleOutPacketTypeFlags.Asset;
                case ThrottleOutPacketType.State:
                    return ThrottleOutPacketTypeFlags.State;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// if we have any leftover bandwidth, check for queues that have packets still in them
        /// and equally distribute remaining bandwidth among queues
        /// </summary>
        /// <returns>True if a dynamic adjustment was made, false if not</returns>
        public bool PerformDynamicThrottleAdjustment()
        {
            //return false;
            if (m_throttle.Content == 0 || _emptyBucketHints.Count == 0)
            {
                _emptyBucketHints.Clear();
                return false;
            }

            int addnlAmount = m_throttle.Content / _emptyBucketHints.Count;
            foreach (int i in _emptyBucketHints)
            {
                m_throttleCategories[i].SpareBurst = addnlAmount;
            }

            _emptyBucketHints.Clear();
            return true;
        }
    }
}
