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
    public class ObjectPool<T> where T : new()
    {
        //private static readonly ILog s_Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Maximum memory to allocate in all pools in bytes
        /// </summary>
        private readonly int _maxAllocatedObjects;

        /// <summary>
        /// The number of bytes currently allocated to all pools
        /// </summary>
        private volatile int _currAllocatedObjects;

        /// <summary>
        /// Stores byte arrays of varying sizes
        /// </summary>
        private LocklessQueue<T> _storage = new LocklessQueue<T>();

        private Func<T> _ctorFunc;

        /// <summary>
        /// Creates a new buffer pool with the given maximums
        /// </summary>
        /// <param name="maxAllocatedObjects">Maximum number of objects to allow</param>
        public ObjectPool(int maxAllocatedObjects)
        {
            _maxAllocatedObjects = maxAllocatedObjects;
        }

        /// <summary>
        /// Creates a new buffer pool with the given maximums
        /// </summary>
        /// <param name="maxAllocatedBytes">Maximum number of objects to allow</param>
        /// <param name="ctorFunc">Function to construct a new T</param>
        public ObjectPool(int maxAllocatedObjects, Func<T> ctorFunc)
        {
            _maxAllocatedObjects = maxAllocatedObjects;
            _ctorFunc = ctorFunc;
        }

        public T LeaseObject() 
        {
            T obj;
            if (_storage.Dequeue(out obj))
            {
                Interlocked.Decrement(ref _currAllocatedObjects);
                return obj;
            }
            else
            {
                if (_ctorFunc != null)
                {
                    return _ctorFunc();
                }
                else
                {
                    return new T();
                }
            }
        }

        public void ReturnObject(T obj)
        {
            if (obj == null) throw new ArgumentNullException("Object can not be null");

            if (_currAllocatedObjects < _maxAllocatedObjects)
            {
                Interlocked.Increment(ref _currAllocatedObjects);
                _storage.Enqueue(obj);
            }
        }
    }
}
#pragma warning restore 420
