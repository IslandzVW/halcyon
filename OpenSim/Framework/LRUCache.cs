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
using System.Threading;
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
        private bool _useSizing = false;

         /// <summary>
         /// A system timer and interval values used to age the cache;
         /// </summary>
        private Dictionary<K, DateTime> _lastAccessedTime = null;
        private int _minSize;
        private int _maxAge;
        private int _expireInterval;

        /// <summary>
        /// Constructs an LRUCache with the given maximum size, maximum age and expiration interval
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="useSizing">Whether or not to use explicit object sizes</param>
        /// <param name="minSize">Minimum size in bytes in the cache. Below this level and no aging is performed.</param>
        /// <param name="maxAge">The maximum age in milliseconds an an entry should live in cache 
        ///     before it's a candidate to be removed.</param>
        /// <param name="expireInterval">Time in milliseconds between checks for expired entries.</param>
        public LRUCache(int capacity, bool useSizing = false, int minSize = 0, int maxAge = 0)
        {
            _storage = new C5.HashedLinkedList<KeyValuePair<K, T>>(new KVPComparer<K, T>());
            _capacity = capacity;
            _totalSize = 0;

            if (useSizing)
            {
                _objectSizes = new Dictionary<K, int>();
                _useSizing = true;
            }

            _maxAge = maxAge;
            _minSize = (minSize <= 0 ? 0 : minSize);
            _lastAccessedTime = null;

            if (_maxAge > 0)
            {
                _lastAccessedTime = new Dictionary<K, DateTime>();
            }
        }

        #region TimerDrivenAging

        /// <summary>
        /// Removes items that have not been accessed in maxAge from the list
        /// </summary>
        public void Maintain()
        {
            if (_maxAge == 0) return;
            
            var entries = new List<KeyValuePair<K, T>>();
            int entriesSize = 0;

            foreach (var entry in _storage)
            {   
                DateTime lastAccess;
                if (_lastAccessedTime.TryGetValue(entry.Key, out lastAccess) == false)
                    continue;
                var age = DateTime.Now - lastAccess;

                // Check to see if this is a candidate.  If not we move on because the cache
                // is in LRU order and we would have visited entries that are candidates already
                if (age.TotalMilliseconds <= (double)_maxAge)
                    break;

                // See if there is a reserve we are maintaining.  If so and we are below it
                // we'll break out and clean up.  This and subsequent entries should be preserved.
                int entrySize = (_useSizing ? _objectSizes[entry.Key] : 1);
                if ((RequiredReserve > 0) && 
                    ((Size - (entrySize + entriesSize)) < RequiredReserve))
                        break;

                entriesSize += entrySize;
                entries.Add(entry);
            }

            // Clean up the storage we identified
            foreach (var entry in entries)
            {
                _storage.Remove(entry);
                this.AccountForRemoval(entry.Key);

                if (this.OnItemPurged != null)
                {
                    OnItemPurged(entry.Value);
                }
            }
            
        }

        #endregion

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

                if (_lastAccessedTime != null)
                {
                    DateTime accessed;
                    if (_lastAccessedTime.TryGetValue(key, out accessed))
                    {
                        accessed = DateTime.Now;
                    }
                }

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

            if (_objectSizes != null)
            {
                if (_objectSizes.ContainsKey(key))  // replaced
                {
                    _totalSize -= _objectSizes[key];
                }
                _totalSize += size;
                _objectSizes[key] = size;
            }

            if (_lastAccessedTime != null)
            {
                _lastAccessedTime.Remove(key);
                _lastAccessedTime.Add(key, DateTime.Now);
            }

            return removed;
        }

        // Called with _storage already locked
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
            if (_lastAccessedTime != null) _lastAccessedTime.Clear();
        }

        public bool Contains(K key)
        {
            KeyValuePair<K, T> kvp = new KeyValuePair<K, T>(key, default(T));
            return _storage.Contains(kvp);
        }

        public int Count
        {
            get
            {
                return _storage.Count;
            }
        }

        public int Size
        {
            get { return _totalSize; }
        }

        public int RemainingCapacity
        {
            get { return _capacity - _totalSize; }
        }

        public int RequiredReserve
        {
            get { return _minSize; }
        }

        public bool IsReadOnly
        {
            get
            {
                return _storage.IsReadOnly;
            }
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

        // Called with _storage already locked
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

            if (_lastAccessedTime != null)
            {
                _lastAccessedTime.Remove(key);
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
