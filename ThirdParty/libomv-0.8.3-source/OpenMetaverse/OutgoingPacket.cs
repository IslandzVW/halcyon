/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
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
using OpenMetaverse;
using System.Net;
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    /// <summary>
    /// Holds a reference to the <seealso cref="LLUDPClient"/> this packet is
    /// destined for, along with the serialized packet data, sequence number
    /// (if this is a resend), number of times this packet has been resent,
    /// the time of the last resend, and the throttling category for this
    /// packet
    /// </summary>
    public sealed class OutgoingPacket
    {
        /// <summary>
        /// The client to send the packet to. is "object" to break a circular reference
        /// </summary>
        public object Client;
        /// <summary>Packet data to send</summary>
        public byte[] Buffer;
        /// <summary>Sequence number of the wrapped packet</summary>
        public uint SequenceNumber;
        /// <summary>Number of times this packet has been resent</summary>
        public int ResendCount;
        /// <summary>Environment.TickCount when this packet was last sent over the wire</summary>
        public int TickCount;
        /// <summary>Category this packet belongs to</summary>
        public int Category;
        /// <summary>
        /// Size of the data in the buffer
        /// </summary>
        public int DataSize;
        /// <summary>
        /// The endpoint this data is headed for
        /// </summary>
        public IPEndPoint Destination;
        /// <summary>
        /// Was this packet marked as reliable
        /// </summary>
        public bool IsReliable;
        /// <summary>
        /// Was this buffer acquired from a pool
        /// </summary>
        public bool BufferFromPool;
        /// <summary>
        /// Packet type
        /// </summary>
        public Packets.PacketType Type;

        

        /// <summary>
        /// Reference count to be used on this packet to know when it's safe to return to the pool
        /// </summary>
        private int _refCount;

        /// <summary>
        /// Lock to protect the reference count
        /// </summary>
        private object _refCountLock = new object();


        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">Reference to the client this packet is destined for</param>
        /// <param name="buffer">Serialized packet data. If the flags or sequence number
        /// need to be updated, they will be injected directly into this binary buffer</param>
        /// <param name="category">Throttling category for this packet</param>
        public OutgoingPacket(object client, byte[] buffer, int category, int dataSize, IPEndPoint destination,
            bool fromBufferPool, Packets.PacketType type)
        {
            SequenceNumber = 0;
            ResendCount = 0;
            TickCount = 0;

            Client = client;
            Buffer = buffer;
            Category = category;
            DataSize = dataSize;
            Destination = destination;

            byte flags = buffer[0];
            IsReliable = (flags & Helpers.MSG_RELIABLE) != 0;

            BufferFromPool = fromBufferPool;
            Type = type;
        }

        public void AddRef()
        {
            if (BufferFromPool)
            {
                lock (_refCountLock)
                {
                    ++_refCount;
                }
            }
        }

        public void DecRef(Interfaces.IByteBufferPool returnPool)
        {
            if (BufferFromPool)
            {
                bool returnToBuffer = false;
                lock (_refCountLock)
                {
                    if (--_refCount <= 0)
                    {
                        returnToBuffer = true;
                    }
                }

                if (returnToBuffer)
                {
                    returnPool.ReturnBytes(Buffer);
                }
            }
        }
    }
}
