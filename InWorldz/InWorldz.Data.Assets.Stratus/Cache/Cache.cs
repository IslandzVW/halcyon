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
using OpenSim.Framework;
using OpenMetaverse;
using System.IO;

namespace InWorldz.Data.Assets.Stratus.Cache
{
    /// <summary>
    /// An asset cache based on LRU that uses buffer pooling
    /// </summary>
    internal class Cache
    {
        /// <summary>
        /// We dont make the cache exactly the same size as the buffer pool. This tries to make sure
        /// we always have some room left in the buffer pool and that we're not constantly swapping
        /// buffers in and out
        /// </summary>
        private const float CACHE_VS_BUFFERPOOL_EXTENSION_FACTOR = 1.1f;

        /// <summary>
        /// Sizes of the byte buffers for assets
        /// </summary>
        internal static readonly int[] BUFFER_SIZES = new int[] 
        { 
            1024, 
            4096, 
            16384, 
            65536, 
            262144, 
            Config.Constants.MAX_CACHEABLE_ASSET_SIZE / 2, //note: if you change this size unit tests will need to be updated
            Config.Constants.MAX_CACHEABLE_ASSET_SIZE 
        };


        private LRUCache<UUID, CacheEntry> _assetCache;
        private ByteBufferPool _bufferPool;

        /// <summary>
        /// Returns the number of items in the cache
        /// </summary>
        public int ItemCount
        {
            get
            {
                return _assetCache.Count;
            }
        }

        /// <summary>
        /// Returns the size of the items in the cache
        /// </summary>
        public int Size
        {
            get
            {
                return _assetCache.Size;
            }
        }

        /// <summary>
        /// Constructs a new asset cache
        /// </summary>
        /// <param name="maxEntryAge">The maximum age for a cache entry before it becomes a candidate to be purged</param>
        public Cache(int maxEntryAge = 0)
        {
            _assetCache = new LRUCache<UUID, CacheEntry>(Config.Settings.Instance.CFCacheSize, true, 
                (int)(Config.Settings.Instance.CFCacheSize * 0.1f), maxEntryAge);

            _assetCache.OnItemPurged += _assetCache_OnItemPurged;

            int bufferPoolSize = (int)(Config.Settings.Instance.CFCacheSize * CACHE_VS_BUFFERPOOL_EXTENSION_FACTOR);
            _bufferPool = new ByteBufferPool(bufferPoolSize, BUFFER_SIZES, (ulong)maxEntryAge);
        }

        void _assetCache_OnItemPurged(CacheEntry item)
        {
            if (item.Data != null)
            {
                _bufferPool.ReturnBytes(item.Data);
            }
        }

        public void CacheAssetData(UUID assetId, byte[] data)
        {
            byte[] cacheData = _bufferPool.LeaseBytes(data.Length);

            Buffer.BlockCopy(data, 0, cacheData, 0, data.Length);

            _assetCache.Add(assetId, new CacheEntry { Data = cacheData, Size = data.Length }, cacheData.Length);
        }

        public void CacheAssetData(UUID assetId, Stream data)
        {
            int dataLen = (int)data.Length;

            byte[] cacheData = _bufferPool.LeaseBytes(dataLen);

            data.Position = 0;
            using (MemoryStream outStream = new MemoryStream(cacheData))
            {
                data.CopyTo(outStream);
            }

            _assetCache.Add(assetId, new CacheEntry { Data = cacheData, Size = dataLen }, cacheData.Length);
        }

        public void CacheAssetData(UUID assetId, StratusAsset asset)
        {
            _assetCache.Add(assetId, new CacheEntry { FullAsset = asset }, asset.Data.Length);
        }

        public bool TryGetAsset(UUID assetId, out CacheEntry cachedAsset)
        {
            return _assetCache.TryGetValue(assetId, out cachedAsset);
        }

        public bool HasAsset(UUID assetId)
        {
            return _assetCache.Contains(assetId);
        }

        /// <summary>
        /// Should be called periodically to run cleanup tasks on data
        /// that has aged out
        /// </summary>
        public void Maintain()
        {
            _assetCache.Maintain();
            _bufferPool.Maintain();
        }
    }
}
