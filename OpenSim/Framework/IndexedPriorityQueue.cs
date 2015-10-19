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
using System.Linq;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// A priority queue that also supports O(1) lookups of contained items based on a key
    /// </summary>
    /// <typeparam name="I"></typeparam>
    /// <typeparam name="T"></typeparam>
    public class IndexedPriorityQueue<I, T> where T : IComparable<T>
    {
        private struct IndexedItem : IComparable<IndexedItem>
        {
            public I Key;
            public T Value;
        
            #region IComparable<IndexedItem> Members

            public int CompareTo(IndexedItem other)
            {
                 return Value.CompareTo(other.Value);
            }

            #endregion
        }

        private C5.IntervalHeap<IndexedItem> _priQueue = new C5.IntervalHeap<IndexedItem>();
        private Dictionary<I, C5.IPriorityQueueHandle<IndexedItem>> _index = new Dictionary<I, C5.IPriorityQueueHandle<IndexedItem>>();

        public IndexedPriorityQueue()
        {
        }

        /// <summary>
        /// Returns the number of items in the collection
        /// </summary>
        public int Count
        {
            get
            {
                return _index.Count;
            }
        }

        /// <summary>
        /// Adds an item to the collection
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Add(I key, T value)
        {
            if (!_index.ContainsKey(key))
            {
                C5.IPriorityQueueHandle<IndexedItem> handle = null;
                _priQueue.Add(ref handle, new IndexedItem { Key = key, Value = value});
                _index.Add(key, handle);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes and returns the minimum item
        /// </summary>
        /// <returns></returns>
        public T DeleteMin()
        {
            IndexedItem item = _priQueue.DeleteMin();
            _index.Remove(item.Key);

            return item.Value;
        }

        /// <summary>
        /// Removes and returns the maximum item
        /// </summary>
        /// <returns></returns>
        public T DeleteMax()
        {
            IndexedItem item = _priQueue.DeleteMax();
            _index.Remove(item.Key);
            
            return item.Value;
        }

        /// <summary>
        /// Returns the minimum item
        /// </summary>
        /// <returns></returns>
        public T FindMin()
        {
            IndexedItem item = _priQueue.FindMin();

            return item.Value;
        }

        /// <summary>
        /// Returns the maximum item
        /// </summary>
        /// <returns></returns>
        public T FindMax()
        {
            IndexedItem item = _priQueue.FindMax();

            return item.Value;
        }

        /// <summary>
        /// Returns the minimum item and its index
        /// </summary>
        /// <returns></returns>
        public KeyValuePair<I, T> FindMinItemAndIndex()
        {
            IndexedItem item = _priQueue.FindMin();

            return new KeyValuePair<I, T>(item.Key, item.Value);
        }

        /// <summary>
        /// Returns the maximum item and its index
        /// </summary>
        /// <returns></returns>
        public KeyValuePair<I, T> FindMaxItemAndIndex()
        {
            IndexedItem item = _priQueue.FindMax();

            return new KeyValuePair<I, T>(item.Key, item.Value);
        }

        /// <summary>
        /// Removes the item with the given key
        /// </summary>
        /// <param name="key"></param>
        public bool Remove(I key)
        {
            C5.IPriorityQueueHandle<IndexedItem> handle;
            if (_index.TryGetValue(key, out handle))
            {
                _priQueue.Delete(handle);
                _index.Remove(key);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to find the value identified by the given key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>True if the value is found, false if not</returns>
        public bool TryGetValue(I key, out T value)
        {
            C5.IPriorityQueueHandle<IndexedItem> handle;
            if (_index.TryGetValue(key, out handle))
            {
                IndexedItem item;
                _priQueue.Find(handle, out item);

                value = item.Value;

                return true;
            }

            value = default(T);
            return false;
        }

        public bool ContainsKey(I key)
        {
            return _index.ContainsKey(key);
        }

        public T this[I key]
        {
            get
            {
                C5.IPriorityQueueHandle<IndexedItem> handle;
                if (_index.TryGetValue(key, out handle))
                {
                    IndexedItem item;
                    _priQueue.Find(handle, out item);

                    return item.Value;
                }

                throw new KeyNotFoundException();
            }

            set
            {
                C5.IPriorityQueueHandle<IndexedItem> handle;
                if (_index.TryGetValue(key, out handle))
                {
                    _priQueue.Replace(handle, new IndexedItem { Key = key, Value = value });
                }
                else
                {
                    this.Add(key, value);
                }
            }
        }
    }
}
