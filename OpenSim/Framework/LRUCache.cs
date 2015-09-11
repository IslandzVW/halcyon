/**
 * Least Recently Used cache
 * Caches the most recently used items up to the given capacity dumping
 * the least used items first
 * 
 * Initial Revision:  August 6, 2010 David C. Daeschler 
 * (c) 2010 InWorldz, LLC.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// Implements a least recently used cache.  Basically a linked list with hash indexes and a 
    /// limited size  
    /// 
    /// TODO:  Implement IDictionary
    /// </summary>
    public class LRUCache<K, T> : IEnumerable<KeyValuePair<K,T>>
    {
        public delegate void ItemPurgedDelegate(T item);
        public event ItemPurgedDelegate OnItemPurged;

        private class KVPComparer<CK, CT> : IEqualityComparer<KeyValuePair<CK, CT>> 
        {
            public bool Equals(KeyValuePair<CK, CT> x, KeyValuePair<CK, CT> y)
            {
                bool eq = EqualityComparer<CK>.Default.Equals(x.Key, y.Key);
                return eq;
            }

            public int GetHashCode(KeyValuePair<CK, CT> obj)
            {
                return EqualityComparer<CK>.Default.GetHashCode(obj.Key);
            }
        }

        private C5.HashedLinkedList<KeyValuePair<K,T>> _storage;
        private int _capacity;

        private Dictionary<K, int> _objectSizes;
        private int _totalSize;

        /// <summary>
        /// Constructs an LRUCache with the given maximum size
        /// </summary>
        /// <param name="capacity"></param>
        public LRUCache(int capacity) : this(capacity, false)
        {

        }

        /// <summary>
        /// Constructs an LRUCache with the given maximum size
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="useSizing">Whether or not to use explicit object sizes</param>
        public LRUCache(int capacity, bool useSizing)
        {
            _storage = new C5.HashedLinkedList<KeyValuePair<K, T>>(new KVPComparer<K, T>());
            _capacity = capacity;
            _totalSize = 0;

            if (useSizing)
            {
                _objectSizes = new Dictionary<K, int>();
            }
        }

        #region ICollection<T>

        /// <summary>
        /// Try to return the item that matches the hash of the given item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="foundItem"></param>
        /// <returns></returns>
        public bool TryGetValue(K key, out T foundItem)
        {
            KeyValuePair<K, T> kvp = new KeyValuePair<K,T>(key, default(T));

            if (_storage.Find(ref kvp))
            {
                foundItem = kvp.Value;

                //readd if we found it to update its position
                _storage.Remove(kvp);
                _storage.Add(kvp);

                return true;
            }
            else
            {
                foundItem = default(T);
                return false;
            }

        }

        /// <summary>
        /// Adds an item/moves an item up in the LRU cache and returns whether or not
        /// the item with the given key already existed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="item"></param>
        /// <returns>If the object already existed</returns>
        public bool Add(K key, T item)
        {
            return this.Add(key, item, 1);
        }

        /// <summary>
        /// Adds an item/moves an item up in the LRU cache and returns whether or not
        /// the item with the given key already existed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="item"></param>
        /// <param name="size">Size of the item</param>
        /// <returns>If the object already existed</returns>
        public bool Add(K key, T item, int size)
        {
            KeyValuePair<K, T> kvp = new KeyValuePair<K, T>(key, item);

            //does this list already contain the item?
            //if so remove it so it gets moved to the bottom
            bool removed = _storage.Remove(kvp);

            //are we at capacity?
            if ((!removed) && _totalSize >= _capacity)
            {
                EnsureCapacity(size);
            }

            //insert the new item
            _storage.Add(kvp);

            if (!removed)
            {
                _totalSize += size;
                if (_objectSizes != null) _objectSizes[key] = size;
            }

            return removed;
        }

        private void EnsureCapacity(int requiredSize)
        {
            while (this.RemainingCapacity < requiredSize && _storage.Count > 0)
            {
                //remove the top item
                KeyValuePair<K, T> pair = _storage.RemoveFirst();
                this.AccountForRemoval(pair.Key);

                if (this.OnItemPurged != null)
                {
                    OnItemPurged(pair.Value);
                }
            }
        }

        public void Clear()
        {
            _storage.Clear();
            _totalSize = 0;
            if (_objectSizes != null) _objectSizes.Clear();
        }

        public bool Contains(K key)
        {
            KeyValuePair<K, T> kvp = new KeyValuePair<K, T>(key, default(T));

            return _storage.Contains(kvp);
        }

        public int Count
        {
            get { return _storage.Count; }
        }

        public int Size
        {
            get { return _totalSize; }
        }

        public int RemainingCapacity
        {
            get { return _capacity - _totalSize; }
        }

        public bool IsReadOnly
        {
            get { return _storage.IsReadOnly; }
        }

        public bool Remove(K key)
        {
            KeyValuePair<K, T> kvp = new KeyValuePair<K, T>(key, default(T));
            if (_storage.Remove(kvp))
            {
                AccountForRemoval(key);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void AccountForRemoval(K key)
        {
            if (_objectSizes != null)
            {
                _totalSize -= _objectSizes[key];
                _objectSizes.Remove(key);
            }
            else
            {
                _totalSize--;
            }
        }

        #endregion

        public IEnumerator<KeyValuePair<K, T>> GetEnumerator()
        {
            return _storage.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _storage.GetEnumerator();
        }
    }
}
