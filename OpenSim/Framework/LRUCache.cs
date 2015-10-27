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

        /// <summary>
        /// The default value to use for the expiration timer if no value
        /// is specified or its <= 0 and maxAge was set > 0).
        /// </summary>
        ///
        public const int DEFAULT_EXPIRATION_TIMER_INTERVAL = 1 * 60 * 1000;

        /// <summary>
        /// The default value to use for the max age time if no value is specified.
        /// </summary>
        ///
        public const int DEFAULT_MAX_AGE = 5 * 60 * 1000;


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
         /// A system timer and interval values used to age the cache;
         /// </summary>
        private System.Threading.Timer _expireTimer = null;
        private Dictionary<K, DateTime> _lastAccessedTime = null;
        private int _minSize;
        private int _maxAge;
        private int _expireInterval;


        /// <summary>
        /// Constructs an LRUCache with the given maximum size
        /// </summary>
        /// <param name="capacity"></param>
        public LRUCache(int capacity) : this(capacity, false, 0, 0, 0)
        {
        }

        /// <summary>
        /// Constructs an LRUCache with the given maximum size
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="useSizing">Whether or not to use explicit object sizes</param>
        public LRUCache(int capacity, bool useSizing) : this(capacity, useSizing, 0, 0, 0)
        {
        }
        /// <summary>
        /// Constructs an LRUCache with the given maximum size, maximum age and expiration interval
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="useSizing">Whether or not to use explicit object sizes</param>
        /// <param name="minSize">Minimum size in bytes in the cache. Below this level and no aging is performed.</param>
        /// <param name="maxAge">The maximum age in milliseconds an an entry should live in cache 
        ///     before it's a candidate to be removed.</param>
        public LRUCache(int capacity, bool useSizing, int minSize, int maxAge) : this(capacity, useSizing, minSize, maxAge, 0)
        {
        }

        /// <summary>
        /// Constructs an LRUCache with the given maximum size, maximum age and expiration interval
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="useSizing">Whether or not to use explicit object sizes</param>
        /// <param name="minSize">Minimum size in bytes in the cache. Below this level and no aging is performed.</param>
        /// <param name="maxAge">The maximum age in milliseconds an an entry should live in cache 
        ///     before it's a candidate to be removed.</param>
        /// <param name="expireInterval">Time in milliseconds between checks for expired entries.</param>
        public LRUCache(int capacity, bool useSizing, int minSize, int maxAge, int expireInterval)
        {
            _storage = new C5.HashedLinkedList<KeyValuePair<K, T>>(new KVPComparer<K, T>());
            _capacity = capacity;
            _totalSize = 0;

            if (useSizing)
            {
                _objectSizes = new Dictionary<K, int>();
            }

            _maxAge = maxAge;
            _minSize = (minSize <= 0 ? 0 : minSize);
            _expireInterval = (expireInterval > 0 ? expireInterval : DEFAULT_EXPIRATION_TIMER_INTERVAL);
            _lastAccessedTime = null;

            if (_maxAge > 0)
            {
                _lastAccessedTime = new Dictionary<K, DateTime>();

                // Create a timer and set the interval to _expiry.
                _expireTimer = new Timer(OnTimedEvent, null, _expireInterval, Timeout.Infinite);
            }
        }

        #region TimerDrivenAging

        private void OnTimedEvent(Object state)
        {
            var entries = new List<KeyValuePair<K, T>> ();

            lock (_storage)
            {
                foreach (var entry in _storage)
                {
                    if ((_storage.Count - entries.Count) <= _minSize)
                        break;

                    DateTime lastAccess;
                    if (_lastAccessedTime.TryGetValue(entry.Key, out lastAccess) == false)
                        continue;
                    var age = DateTime.Now - lastAccess;
                    if (age.TotalMilliseconds > (double)_maxAge)
                        entries.Add(entry);
                }

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

            _expireTimer.Change(_expireInterval, Timeout.Infinite);
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

            lock (_storage)
            {
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
            lock (_storage)
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

                if (_lastAccessedTime != null)
                {
                    _lastAccessedTime.Remove(key);
                    _lastAccessedTime.Add(key, DateTime.Now);
                }

                return removed;
            }
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
            lock (_storage)
            {
                _storage.Clear();
                _totalSize = 0;
                if (_objectSizes != null) _objectSizes.Clear();
                if (_lastAccessedTime != null) _lastAccessedTime.Clear();
            }
        }

        public bool Contains(K key)
        {
            lock (_storage)
            {
                KeyValuePair<K, T> kvp = new KeyValuePair<K, T>(key, default(T));
                return _storage.Contains(kvp);
            }
        }

        public int Count
        {
            get
            {
                lock (_storage)
                {
                    return _storage.Count;
                }
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

        public bool IsReadOnly
        {
            get
            {
                lock (_storage)
                {
                    return _storage.IsReadOnly;
                }
            }
        }

        public bool Remove(K key)
        {
            lock (_storage)
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
            lock (_storage)
            {
                return _storage.GetEnumerator();
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            lock (_storage)
            {
                return _storage.GetEnumerator();
            }
        }
    }
}
