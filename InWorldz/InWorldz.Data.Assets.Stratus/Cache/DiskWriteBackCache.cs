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
using System.Threading.Tasks;
using OpenMetaverse;
using System.IO;
using Amib.Threading;
using OpenSim.Framework;
using log4net;
using System.Reflection;
using System.Threading;

namespace InWorldz.Data.Assets.Stratus.Cache
{
    /// <summary>
    /// A writeback cache used to suppliment cloud files when writing is slow
    /// </summary>
    internal class DiskWriteBackCache
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// How long to wait on pending writes during a shutdown
        /// </summary>
        private const int SHUTDOWN_WAIT_TIMEOUT = 5 * 1000;

        /// <summary>
        /// The directory where we store the files that we're trying to send to CF
        /// </summary>
        private const string WRITEBACK_CACHE_DIR = "./cache/cf_writeback";

        /// <summary>
        /// Timeout before we delete assets that have recently been written to CF (see _recentlyWritten)
        /// </summary>
        private const ulong RECENT_WRITE_TIMEOUT = 60 * 1000;

        /// <summary>
        /// The number of worker threads we'll use to write the waiting assets to CF
        /// </summary>
        private const int NUM_WRITE_WORKERS = 4;

        /// <summary>
        /// How long to sleep between trying to write assets
        /// </summary>
        private const int WRITE_TIMER_PERIOD = 1000;

        /// <summary>
        /// In memory list of IDs that are currently stored in the writeback cache
        /// </summary>
        private C5.HashedLinkedList<Guid> _ids = new C5.HashedLinkedList<Guid>();

        /// <summary>
        /// Assets that were recently written to disk. This helps us to overcome a timing window whereby
        /// CF would report an asset missing while we were writing that asset to disk. Then, by the time
        /// the caller checks us, we would report the asset missing as well if we were able to write it to 
        /// CF. Instead, this list can be checked by a process that can clean up the written assets after 
        /// a timeout, getting rid of the timing window.
        /// </summary>
        private Dictionary<Guid, ulong> _recentlyWritten = new Dictionary<Guid, ulong>();

        /// <summary>
        /// Lock taken during any operation
        /// </summary>
        private object _oplock = new object();

        /// <summary>
        /// Write workers for copying our local assets to cloud files
        /// </summary>
        private CloudFilesAssetWorker[] _workers = new CloudFilesAssetWorker[NUM_WRITE_WORKERS];

        /// <summary>
        /// Threadpool to cover our write backs 
        /// </summary>
        private SmartThreadPool _writeBackPool = new SmartThreadPool(30 * 1000, NUM_WRITE_WORKERS);

        /// <summary>
        /// Timer that performs the write loop
        /// </summary>
        private Timer _writeTimer;

        /// <summary>
        /// Whether or not the cache should be running
        /// </summary>
        private volatile bool _stop;


        public DiskWriteBackCache()
        {
            CheckCacheDir();
            LoadAssetIdsFromCacheDir();
            _stop = true;
        }

        public void Start()
        {
            _stop = false;
            _writeTimer = new Timer(this.OnWriteTimer, null, WRITE_TIMER_PERIOD, Timeout.Infinite);
        }

        public void Stop()
        {
            _stop = true;
            _writeTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _writeTimer.Dispose();

            _writeBackPool.WaitForIdle(SHUTDOWN_WAIT_TIMEOUT);
        }

        private void OnWriteTimer(object state)
        {
            try
            {
                while (! _stop && this.DoWriteCycle())
                {
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[InWorldz.Stratus] Error when executing asset writeback {0}", e);
            }
            finally
            {
                if (!_stop)
                {
                    _writeTimer.Change(WRITE_TIMER_PERIOD, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Loads all the asset IDs into memory from the file names in the cache directory
        /// </summary>
        private void LoadAssetIdsFromCacheDir()
        {
            foreach (string fileName in Directory.EnumerateFiles(WRITEBACK_CACHE_DIR, "*.asset"))
            {
                _ids.Add(Guid.Parse(Path.GetFileNameWithoutExtension(fileName)));
            }
        }

        /// <summary>
        /// Auto-repair the case where we don't really have a cached item
        /// Uses the lock but it should be called from within the lock by caller as well.
        /// </summary>
        /// <param name="assetId">The ID of the asset that isn't actually cached.</param>
        /// <returns>Returns true if asset cache file is missing or empty.</returns>
        private bool RepairEmpty(Guid assetId)
        {
            string assetFileName = GetAssetFileName(assetId);
            lock (_oplock)
            {
                FileInfo fileInfo = new System.IO.FileInfo(assetFileName);
                if (fileInfo.Exists)
                {
                    if (fileInfo.Length > 0)
                        return false; // this one is actually okay
                    // we have an empty asset cache file (disk full?)
                    fileInfo.Delete();
                }
                if (_ids.Contains(assetId))
                    _ids.Remove(assetId);
                if (_recentlyWritten.ContainsKey(assetId))
                    _recentlyWritten.Remove(assetId);

                return true;    // missing or empty asset cache file
            }
        }

        /// <summary>
        /// Puts an asset into the writeback cache
        /// </summary>
        /// <param name="asset"></param>
        public void StoreAsset(StratusAsset asset)
        {
            CheckCacheDir();

            string assetFileName = GetAssetFileName(asset.Id);

            lock (_oplock)
            {
                if (_ids.Contains(asset.Id) || _recentlyWritten.ContainsKey(asset.Id))
                {
                    // Auto-repair the case where we don't really have a cached item.
                    // Only call this when we think we already have an asset file.
                    if (!RepairEmpty(asset.Id))
                    {
                        //we already have this asset scheduled to write
                        throw new AssetAlreadyExistsException("Asset " + asset.Id.ToString() + " already cached for writeback");
                    }
                    // else fall through to write the item to disk (repair the cached item)
                }

                try
                {
                    using (FileStream fstream = File.OpenWrite(assetFileName))
                    {
                        ProtoBuf.Serializer.Serialize(fstream, asset);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("There was an error writing an asset back to disk. The process will be terminated. {0}", e);
                    Environment.Exit(-1);
                }

                _ids.Add(asset.Id);
            }
        }

        /// <summary>
        /// Tries to read an asset from the disk
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns></returns>
        public StratusAsset GetAsset(Guid assetId)
        {
            lock (_oplock)
            {
                if (!_ids.Contains(assetId) && !_recentlyWritten.ContainsKey(assetId))
                {
                    return null;
                }

                // Auto-repair the case where we don't really have a cached item
                if (RepairEmpty(assetId))
                {
                    // No asset: we shouldn't have gotten this far, but cleaned up tracking above.
                    // Returning null here avoids using the cache which doesn't have it anyway.
                    return null;
                }

                using (FileStream fstream = File.OpenRead(GetAssetFileName(assetId)))
                {
                    return ProtoBuf.Serializer.Deserialize<StratusAsset>(fstream);
                }
            }
        }

        /// <summary>
        /// Returns the file name and path where we can find the given asset in the cache
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        private static string GetAssetFileName(Guid assetId)
        {
            return String.Format("{0}/{1}.asset", WRITEBACK_CACHE_DIR, assetId.ToString());
        }

        /// <summary>
        /// Makes sure the cache directory exists, and creates it if not
        /// </summary>
        private void CheckCacheDir()
        {
            if (!Directory.Exists(WRITEBACK_CACHE_DIR))
            {
                Directory.CreateDirectory(WRITEBACK_CACHE_DIR);
            }
        }

        /// <summary>
        /// Performs a single write cycle. Attempts to write at most NUM_WRITE_WORKERS assets at a time to CF
        /// </summary>
        internal bool DoWriteCycle()
        {
            int count = 0;
            lock (_oplock)
            {
                count = _ids.Count;
            }

            if (count == 0)
            {
                CheckAndCleanOldWrites();
                return false;
            }

            CheckPopulateWorkers();

            int num = Math.Min(NUM_WRITE_WORKERS, count);
            List<Guid> idsToTry = GetNextAssetsWaitingForWrite(num);

            //fire up the writes
            for (int i = 0; i < num; ++i)
            {
                int capture = i;
                _writeBackPool.QueueWorkItem(() =>
                    {
                        Guid assetId = idsToTry[capture];
                        StratusAsset asset = this.GetAsset(assetId);

                        try
                        {
                            var stream = _workers[capture].StoreAsset(asset);
                            stream.Dispose();

                            MarkAssetWritten(assetId);
                        }
                        catch (AssetAlreadyExistsException)
                        {
                            //this is ok, consider this a success
                            MarkAssetWritten(assetId);
                        }
                        catch (Exception e)
                        {
                            //asset could not be written
                            m_log.ErrorFormat("[InWorldz.Stratus] Error when retrying write for {0}: {1}", assetId, e);
                        }
                    }
                );
            }

            _writeBackPool.WaitForIdle();

            CheckAndCleanOldWrites();

            lock (_oplock)
            {
                count = _ids.Count;
            }

            return count != 0;
        }

        private List<Guid> GetNextAssetsWaitingForWrite(int num)
        {
            List<Guid> idsToTry = new List<Guid>(num);

            lock (_oplock)
            {
                IEnumerator<Guid> walker = _ids.GetEnumerator();
                int i = 0;
                while (walker.MoveNext() && i < num)
                {
                    idsToTry.Add(walker.Current);
                    ++i;
                }
            }
            return idsToTry;
        }

        /// <summary>
        /// Marks the given ID as having been delay written to CF
        /// </summary>
        /// <param name="assetId"></param>
        private void MarkAssetWritten(Guid assetId)
        {
            //asset was written
            lock (_oplock)
            {
                _ids.Remove(assetId);
                _recentlyWritten.Add(assetId, Util.GetLongTickCount());
            }
        }

        /// <summary>
        /// Checks to make sure we have CF workers waiting in our collection, and if not, 
        /// creates a new set
        /// </summary>
        private void CheckPopulateWorkers()
        {
            if (_workers[0] == null)
            {
                for (int i = 0; i < NUM_WRITE_WORKERS; i++)
                {
                    _workers[i] = new CloudFilesAssetWorker(CloudFilesAssetWorker.DEFAULT_READ_TIMEOUT, 70 * 1000);
                }
            }
        }

        /// <summary>
        /// Checks for files that were written to CF and are older than the RECENT_WRITE_TIMEOUT
        /// </summary>
        private void CheckAndCleanOldWrites()
        {
            lock (_oplock)
            {
                if (_recentlyWritten.Count == 0) return;

                List<Guid> needsDelete = new List<Guid>();
                foreach (var kvp in _recentlyWritten)
                {
                    if (Config.Settings.Instance.UnitTest_DeleteOldCacheFilesImmediately ||
                        Util.GetLongTickCount() - kvp.Value >= RECENT_WRITE_TIMEOUT)
                    {
                        //needs a delete
                        needsDelete.Add(kvp.Key);
                    }
                }

                foreach (var id in needsDelete)
                {
                    RemoveAssetFile(id);
                    _recentlyWritten.Remove(id);
                }
            }
        }

        private void RemoveAssetFile(Guid id)
        {
            try
            {
                File.Delete(GetAssetFileName(id));
            }
            catch (Exception e)
            {
                //this isnt a huge deal, but we want to log it
                m_log.ErrorFormat("[InWorldz.Stratus] Unable to remove disk cached asset {0}. {1}", id, e);
            }
        }
    }
}
