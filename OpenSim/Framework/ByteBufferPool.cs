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
        /// The max age in ms for an idle byte buffer before it is culled
        /// </summary>
        private ulong _idleBufferMaxAge;

        /// <summary>
        /// Stores byte arrays of varying sizes
        /// </summary>
        private List<KeyValuePair<int, LocklessQueue<ImmutableTimestampedItem<byte[]>>>> _storage 
            = new List<KeyValuePair<int, LocklessQueue<ImmutableTimestampedItem<byte[]>>>>();

        public int AllocatedBytes
        {
            get
            {
                return _currAllocatedBytes;
            }
        }

        /// <summary>
        /// Creates a new buffer pool with the given levels
        /// </summary>
        /// <param name="maxAllocatedBytes">Maximum bytes to allow to be allocated total for all pools</param>
        /// <param name="bufferSizes">List of buffer sizes in bytes to split the pool by</param>
        public ByteBufferPool(int maxAllocatedBytes, int[] bufferSizes) : this(maxAllocatedBytes, bufferSizes, 0)
        {
        }

        /// <summary>
        /// Creates a new buffer pool with the given levels
        /// </summary>
        /// <param name="maxAllocatedBytes">Maximum bytes to allow to be allocated total for all pools</param>
        /// <param name="bufferSizes">List of buffer sizes in bytes to split the pool by</param>
        /// <param name="idleBufferMaxAge">The maximum age a buffer (in ms) can sit idle before it will be purged</param>
        public ByteBufferPool(int maxAllocatedBytes, int[] bufferSizes, ulong idleBufferMaxAge)
        {
            _maxAllocatedBytes = maxAllocatedBytes;
            _idleBufferMaxAge = idleBufferMaxAge;

            foreach (int bufferSz in bufferSizes)
            {
                _storage.Add(
                    new KeyValuePair<int, LocklessQueue<ImmutableTimestampedItem<byte[]>>>(
                        bufferSz, new LocklessQueue<ImmutableTimestampedItem<byte[]>>()));
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

        /// <summary>
        /// Removes any buffer that has been idle for more than the max age
        /// specified during construction
        /// </summary>
        public void Maintain()
        {
            if (_idleBufferMaxAge == 0) return;

            foreach (var kvp in _storage)
            {
                var queue = kvp.Value;

                HashSet<byte[]> knownBuffers = new HashSet<byte[]>();

                //dequeue and requeue until we get false back (no items)
                //or we hit an item we've already seen

                //it would be nice if we could just rely on the implicit queue 
                //FIFO ordering to allow us to stop early, but the way this loop
                //works with enqueue/dequeue, it actually breaks the time sorted
                //ordering so we can't just rely on it to break out of the loop early
                ImmutableTimestampedItem<byte[]> item;
                while (queue.Dequeue(out item))
                {
                    //have we seen this item before?
                    if (knownBuffers.Contains(item.Item))
                    {
                        //yes, we're done
                        break;
                    }

                    if (item.ElapsedMilliseconds < _idleBufferMaxAge)
                    {
                        //track that we've seen the item
                        knownBuffers.Add(item.Item);
                        //put the item back
                        queue.Enqueue(item);
                    }
                    else
                    {
                        // this buffer is too old. mark it as deallocated
                        Interlocked.Add(ref _currAllocatedBytes, -item.Item.Length);
                    }
                }
            }
        }
    }
}
#pragma warning restore 420
