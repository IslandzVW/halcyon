/*
 * Copyright (c) 2015, InWorldz Halcyon Developers
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice, this
 *     list of conditions and the following disclaimer.
 * 
 *   * Redistributions in binary form must reproduce the above copyright notice,
 *     this list of conditions and the following disclaimer in the documentation
 *     and/or other materials provided with the distribution.
 * 
 *   * Neither the name of halcyon nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse.Interfaces;
using System.Threading;
using log4net;
using System.Reflection;

#pragma warning disable 420
namespace OpenSim.Framework
{
    /// <summary>
    /// Creates a collection of byte buffers that can be leased for use on long running
    /// operations to prevent unnecessary allocations and gc thrashing as well as preventing
    /// too much gen0 memory from being pinned in the case of network operations
    /// </summary>
    public class ByteBufferPool : IByteBufferPool
    {
        private static readonly ILog s_Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Maximum memory to allocate in all pools in bytes
        /// </summary>
        private readonly int _maxAllocatedBytes;

        /// <summary>
        /// The number of bytes currently allocated to all pools
        /// </summary>
        private volatile int _currAllocatedBytes;

        /// <summary>
        /// Stores byte arrays of varying sizes
        /// </summary>
        private List<KeyValuePair<int, LocklessQueue<ImmutableTimestampedItem<byte[]>>>> _storage 
            = new List<KeyValuePair<int, LocklessQueue<ImmutableTimestampedItem<byte[]>>>>();

        /// <summary>
        /// Creates a new buffer pool with the given levels
        /// </summary>
        /// <param name="maxAllocatedBytes">Maximum bytes to allow to be allocated total for all pools</param>
        /// <param name="bufferSizes">List of buffer sizes in bytes to split the pool by</param>
        public ByteBufferPool(int maxAllocatedBytes, int[] bufferSizes)
        {
            _maxAllocatedBytes = maxAllocatedBytes;

            foreach (int bufferSz in bufferSizes)
            {
                _storage.Add(new KeyValuePair<int, LocklessQueue<ImmutableTimestampedItem<byte[]>>>());
            }
        }

        private LocklessQueue<ImmutableTimestampedItem<byte[]>> FindContainer(int minSize)
        {
            foreach (var kvp in _storage)
            {
                if (kvp.Key >= minSize)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        private LocklessQueue<ImmutableTimestampedItem<byte[]>> FindExactContainer(int size)
        {
            foreach (var kvp in _storage)
            {
                if (kvp.Key == size)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        private int FindContainerSize(int minSize)
        {
            foreach (var kvp in _storage)
            {
                if (kvp.Key >= minSize)
                {
                    return kvp.Key;
                }
            }

            return 0;
        }

        #region IByteBufferPool Members

        public byte[] LeaseBytes(int minSize)
        {
            LocklessQueue<ImmutableTimestampedItem<byte[]>> container = this.FindContainer(minSize);
            if (container == null)
            {
                s_Log.WarnFormat("[ByteBufferPool] Not servicing request for {0} bytes", minSize);

                //we cant service a request for this size
                return new byte[minSize];
            }

            ImmutableTimestampedItem<byte[]> buffer;
            if (container.Dequeue(out buffer))
            {
                Interlocked.Add(ref _currAllocatedBytes, -buffer.Item.Length);
                return buffer.Item;
            }
            else
            {
                int closestContainerSize = this.FindContainerSize(minSize);
                return new byte[closestContainerSize];
            }
        }

        public void ReturnBytes(byte[] bytes)
        {
            LocklessQueue<ImmutableTimestampedItem<byte[]>> container = this.FindExactContainer(bytes.Length);
            if (container != null)
            {
                if (_currAllocatedBytes < _maxAllocatedBytes)
                {
                    Interlocked.Add(ref _currAllocatedBytes, bytes.Length);
                    container.Enqueue(new ImmutableTimestampedItem<byte[]>(bytes));
                }
            }
        }

        #endregion
    }
}
#pragma warning restore 420
