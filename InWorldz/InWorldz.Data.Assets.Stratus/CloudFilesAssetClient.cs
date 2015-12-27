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
using OpenSim.Framework;
using Amib.Threading;
using System.Threading;
using log4net;
using System.Reflection;

namespace InWorldz.Data.Assets.Stratus
{

    /// <summary>
    /// Implements an asset connector for Rackspace Cloud Files
    /// </summary>
    public class CloudFilesAssetClient : IAssetServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// How long to let a thread sit in the pool without being used
        /// </summary>
        const int THREADPOOL_IDLE_TIMEOUT = 2 * 60 * 1000;

        /// <summary>
        /// When the simulator is being stopped, this is how long to wait on the threadpool 
        /// to process asset requests before forcing a shutdown
        /// </summary>
        const int SHUTDOWN_WAIT_TIMEOUT = 5 * 1000;

        /// <summary>
        /// The maximum amount of time to wait for a synchronous asset request
        /// </summary>
        const int ASSET_WAIT_TIMEOUT = 45 * 1000;

        /// <summary>
        /// The amount of time between cache maintenance checks
        /// </summary>
        const int MAINTENANCE_INTERVAL = 5 * 60 * 1000;

        /// <summary>
        /// The maximum age of an idle cache entry before it will be purged
        /// </summary>
        const int MAX_IDLE_CACHE_ENTRY_AGE = MAINTENANCE_INTERVAL * 2;



        /// <summary>
        /// We use a threadpool to enable communication with cloudfiles for multiple requests
        /// </summary>
        private SmartThreadPool _threadPool;

        /// <summary>
        /// Collection of available asset clients
        /// </summary>
        private ObjectPool<CloudFilesAssetWorker> _asyncAssetWorkers;

        /// <summary>
        /// Object that gets the callback for an async request
        /// </summary>
        private IAssetReceiver _receiver;

        /// <summary>
        /// Our asset cache
        /// </summary>
        private Cache.Cache _assetCache;

        /// <summary>
        /// Stores written assets on disk when a cloud files write times out
        /// </summary>
        private Cache.DiskWriteBackCache _diskWriteBack;

        private int _readTimeout = CloudFilesAssetWorker.DEFAULT_READ_TIMEOUT;
        private int _writeTimeout = CloudFilesAssetWorker.DEFAULT_WRITE_TIMEOUT;

        private Timer _maintTimer;

        public CloudFilesAssetClient()
        {
        }

        /// <summary>
        /// For unit testing
        /// </summary>
        internal Cache.DiskWriteBackCache DiskWriteBackCache
        {
            get
            {
                return _diskWriteBack;
            }
        }



        public void Initialize(ConfigSettings settings)
        {
            //if this is being called, we were loaded as a plugin instead of the StratusAssetClient
            //we shouldnt be loaded like this, throw.
            throw new Exception("CloudFilesAssetClient should not be loaded directly as a plugin");
        }

        public void Start()
        {
            STPStartInfo START_INFO = new STPStartInfo
            {
                WorkItemPriority = Amib.Threading.WorkItemPriority.Normal,
                MinWorkerThreads = 0,
                MaxWorkerThreads = Config.Settings.Instance.CFWorkerThreads,
                IdleTimeout = THREADPOOL_IDLE_TIMEOUT,
            };

            _threadPool = new SmartThreadPool(START_INFO);
            _threadPool.Name = "Cloudfiles";

            Func<CloudFilesAssetWorker> ctorFunc = () => { return new CloudFilesAssetWorker(_readTimeout, _writeTimeout); };
            _asyncAssetWorkers = new ObjectPool<CloudFilesAssetWorker>(Config.Settings.Instance.CFWorkerThreads * 2, ctorFunc);

            _assetCache = new Cache.Cache(MAX_IDLE_CACHE_ENTRY_AGE);

            if (!Config.Settings.Instance.DisableWritebackCache)
            {
                _diskWriteBack = new Cache.DiskWriteBackCache();
                _diskWriteBack.Start();
            }

            //start maintaining the cache
            _maintTimer = new Timer(this.Maintain, null, MAINTENANCE_INTERVAL, MAINTENANCE_INTERVAL);
        }

        /// <summary>
        /// Should be called by the maintenance timer to maintain the cache
        /// </summary>
        /// <param name="unused"></param>
        private void Maintain(object unused)
        {
            _maintTimer.Change(MAINTENANCE_INTERVAL, Timeout.Infinite);
            lock (_assetCache)
            {
                try
                {
                    _assetCache.Maintain();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[InWorldz.Stratus] Error during cache maintenance {0}", e);
                }
            }

            _maintTimer.Change(MAINTENANCE_INTERVAL, MAINTENANCE_INTERVAL);
        }

        public void Stop()
        {
            _threadPool.WaitForIdle(SHUTDOWN_WAIT_TIMEOUT);
            _threadPool.Shutdown();

            if (_diskWriteBack != null) _diskWriteBack.Stop();
            _maintTimer.Dispose();
        }

        public void SetReceiver(IAssetReceiver receiver)
        {
            _receiver = receiver;
        }

        /// <summary>
        /// Requests and asset and responds asynchronously
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="args"></param>
        public void RequestAsset(OpenMetaverse.UUID assetID, AssetRequestInfo args)
        {
            _threadPool.QueueWorkItem(() => {
                try
                {
                    AssetBase asset = GetAssetInternal(assetID);

                    if (asset != null)
                    {
                        _receiver.AssetReceived(asset, args);
                    }
                    else
                    {
                        _receiver.AssetNotFound(assetID, args);
                    }
                }
                catch (Exception e)
                {
                    _receiver.AssetError(assetID, e, args);
                }
            });
        }

        /// <summary>
        /// Requests an asset and response synchronously
        /// </summary>
        /// <param name="assetID"></param>
        /// <returns></returns>
        public AssetBase RequestAssetSync(OpenMetaverse.UUID assetID)
        {
            ManualResetEventSlim syncEvent = new ManualResetEventSlim();
            AssetBase asset = null;
            Exception thrown = null;

            _threadPool.QueueWorkItem(() =>
            {
                try
                {
                    asset = GetAssetInternal(assetID);
                }
                catch (Exception e)
                {
                    thrown = e;
                }

                syncEvent.Set();
            });

            if (syncEvent.Wait(ASSET_WAIT_TIMEOUT))
            {
                syncEvent.Dispose();
            }
            else
            {
                m_log.WarnFormat("[InWorldz.Stratus]: Timout waiting for synchronous asset request for {0}", assetID);
            }


            if (thrown != null) throw new AssetServerException(thrown.Message, thrown);

            return asset;
        }

        /// <summary>
        /// Requests asset metadata and response synchronously
        /// </summary>
        /// <param name="assetID"></param>
        /// <returns></returns>
        public Dictionary<string, string> RequestAssetMetadataSync(OpenMetaverse.UUID assetID)
        {
            ManualResetEventSlim syncEvent = new ManualResetEventSlim();
            Dictionary<string,string> meta = null;
            Exception thrown = null;

            _threadPool.QueueWorkItem(() =>
            {
                try
                {
                    meta = RequestAssetMetadataInternal(assetID);
                }
                catch (Exception e)
                {
                    thrown = e;
                }

                syncEvent.Set();
            });

            if (syncEvent.Wait(ASSET_WAIT_TIMEOUT))
            {
                syncEvent.Dispose();
            }
            else
            {
                m_log.WarnFormat("[InWorldz.Stratus]: Timout waiting for synchronous metadata request for {0}", assetID);
            }


            if (thrown != null) throw new AssetServerException(thrown.Message, thrown);

            return meta;
        }

        private Dictionary<string, string> RequestAssetMetadataInternal(OpenMetaverse.UUID assetID)
        {
            Dictionary<string, string> meta = null;
            Util.Retry(2, new List<Type> { typeof(UnrecoverableAssetServerException) }, () =>
            {
                CloudFilesAssetWorker worker = null;
                try
                {
                    try
                    {
                        //nothing in the cache. request from CF
                        worker = _asyncAssetWorkers.LeaseObject();
                    }
                    catch (Exception e)
                    {
                        //bail out now if we couldn't lease a connection
                        throw new UnrecoverableAssetServerException(e.Message, e);
                    }

                    meta = worker.GetAssetMetadata(assetID);
                }
                catch (net.openstack.Core.Exceptions.Response.ItemNotFoundException)
                {
                    //not an exceptional case. this will happen
                    meta = null;
                }
                finally
                {
                    if (worker != null) _asyncAssetWorkers.ReturnObject(worker);
                }
            });

            return meta;
        }

        private AssetBase GetAssetInternal(OpenMetaverse.UUID assetID)
        {
            //cache?
            Cache.CacheEntry cacheObject = null;
            lock (_assetCache)
            {
                _assetCache.TryGetAsset(assetID, out cacheObject);
            }

            StratusAsset rawAsset = null;
            if (cacheObject != null)
            {
                //stream cache or asset cache?
                if (cacheObject.FullAsset != null)
                {
                    rawAsset = cacheObject.FullAsset;
                }
                else
                {
                    using (System.IO.MemoryStream stream = new System.IO.MemoryStream(cacheObject.Data, 0, cacheObject.Size))
                    {
                        rawAsset = DeserializeAssetFromStream(assetID, stream);
                    }
                }
            }
            else
            {
                StratusAsset diskAsset = null;
                if (! Config.Settings.Instance.DisableWritebackCache) diskAsset = _diskWriteBack.GetAsset(assetID.Guid);

                if (diskAsset != null)
                {
                    rawAsset = diskAsset;
                }
                else
                {
                    Util.Retry(2, new List<Type> { typeof(UnrecoverableAssetServerException) }, () =>
                    {
                        CloudFilesAssetWorker worker = null;
                        try
                        {
                            try
                            {
                                //nothing on the local disk, request from CF
                                worker = _asyncAssetWorkers.LeaseObject();
                            }
                            catch (Exception e)
                            {
                                //exception here is unrecoverable since this is construction
                                throw new UnrecoverableAssetServerException(e.Message, e);
                            }

                            using (System.IO.MemoryStream stream = worker.GetAsset(assetID))
                            {
                                stream.Position = 0;
                                rawAsset = DeserializeAssetFromStream(assetID, stream);
                            

                                //if we're using the cache, we need to put the raw data in there now
                                stream.Position = 0;
                                this.CacheAssetIfAppropriate(assetID, stream, rawAsset);
                            }
                        }
                        catch (net.openstack.Core.Exceptions.Response.ItemNotFoundException)
                        {
                            //not an exceptional case. this will happen
                            rawAsset = null;
                        }
                        finally
                        {
                            if (worker != null) _asyncAssetWorkers.ReturnObject(worker);
                        }
                    });
                }
            }
            

            //nothing?
            if (rawAsset == null) return null;

            //convert
            return rawAsset.ToAssetBase();
        }

        private static StratusAsset DeserializeAssetFromStream(OpenMetaverse.UUID assetId, System.IO.MemoryStream stream)
        {
            StratusAsset rawAsset = ProtoBuf.Serializer.Deserialize<StratusAsset>(stream);

            if (rawAsset == null || rawAsset.Data == null)
            {
                throw new InvalidOperationException(String.Format("Asset deserialization failed. Asset ID: {0}, Stream Len: {1}", assetId, stream.Length));
            }

            return rawAsset;
        }

        private void CacheAssetIfAppropriate(OpenMetaverse.UUID assetId, System.IO.MemoryStream stream, StratusAsset asset)
        {
            if (!Config.Settings.Instance.CFUseCache) return;
            if (stream.Length > Config.Constants.MAX_CACHEABLE_ASSET_SIZE) return;
            if (asset.Data.Length > Config.Constants.MAX_CACHEABLE_ASSET_SIZE) return;

            lock (_assetCache)
            {
                if (!_assetCache.HasAsset(assetId))
                {
                    //we do not yet have this asset. we need to make a determination if caching the stream
                    //or caching the asset would be more beneficial
                    if (stream.Length > Config.Constants.MAX_STREAM_CACHE_SIZE)
                    {
                        //asset is too big for caching the stream to have any theoretical benefit.
                        //instead we cache the asset itself
                        _assetCache.CacheAssetData(assetId, asset);
                    }
                    else
                    {
                        //caching the stream should make for faster retrival and collection
                        _assetCache.CacheAssetData(assetId, stream);
                    }
                }
            }
        }

        private Dictionary<string, string> GetAssetMetadata(OpenMetaverse.UUID assetId, CloudFilesAssetWorker worker)
        {
            try
            {
                return worker.GetAssetMetadata(assetId);
            }
            catch (net.openstack.Core.Exceptions.Response.ItemNotFoundException)
            {
                return null;
            }
        }

        public void StoreAsset(AssetBase asset)
        {
            if (asset == null) throw new ArgumentNullException("asset cannot be null");
            if (asset.FullID == OpenMetaverse.UUID.Zero) throw new ArgumentException("assets must not have a null ID");

            bool isRetry = false;

            StratusAsset wireAsset = StratusAsset.FromAssetBase(asset);

            //for now we're not going to use compression etc, so set to zero
            wireAsset.StorageFlags = 0;

            Util.Retry(2, new List<Type> { typeof(AssetAlreadyExistsException), typeof(UnrecoverableAssetServerException) }, () =>
            {
                CloudFilesAssetWorker worker;
                try
                {
                    worker = _asyncAssetWorkers.LeaseObject();
                }
                catch (Exception e)
                {
                    throw new UnrecoverableAssetServerException(e.Message, e);
                }

                try
                {
                    if (Config.Settings.Instance.UnitTest_ThrowTimeout)
                    {
                        throw new System.Net.WebException("Timeout for unit testing", System.Net.WebExceptionStatus.Timeout);
                    }

                    using (System.IO.MemoryStream assetStream = worker.StoreAsset(wireAsset))
                    {
                        //cache the stored asset to eliminate roudtripping when
                        //someone performs an upload
                        this.CacheAssetIfAppropriate(asset.FullID, assetStream, wireAsset);
                    }
                }
                catch (AssetAlreadyExistsException)
                {
                    if (!isRetry) //don't throw if this is a retry. this can happen if a write times out and then succeeds
                    {
                        throw;
                    }
                }
                catch (System.Net.WebException e)
                {
                    if (e.Status == System.Net.WebExceptionStatus.Timeout || e.Status == System.Net.WebExceptionStatus.RequestCanceled)
                    {
                        DoTimeout(asset, wireAsset, e);
                    }
                    else
                    {
                        ReportThrowStorageError(asset, e);
                    }
                }
                catch (System.IO.IOException e)
                {
                    //this sucks, i think timeouts on writes are causing .net to claim the connection
                    //was forcibly closed by the remote host.
                    if (e.Message.Contains("forcibly closed"))
                    {
                        DoTimeout(asset, wireAsset, e);
                    }
                    else
                    {
                        ReportThrowStorageError(asset, e);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[InWorldz.Stratus]: Unable to store asset {0}: {1}", asset.FullID, e);
                    throw new AssetServerException(String.Format("Unable to store asset {0}: {1}", asset.FullID, e.Message), e);
                }
                finally
                {
                    isRetry = true;
                    _asyncAssetWorkers.ReturnObject(worker);
                }
            });
        }

        private static void ReportThrowStorageError(AssetBase asset, Exception e)
        {
            m_log.ErrorFormat("[InWorldz.Stratus]: Unable to store asset {0}: {1}", asset.FullID, e);
            throw new AssetServerException(String.Format("Unable to store asset {0}: {1}", asset.FullID, e.Message), e);
        }

        private void DoTimeout(AssetBase asset, StratusAsset wireAsset, Exception e)
        {
            if (!Config.Settings.Instance.DisableWritebackCache)
            {
                //eat the exception and write locally
                m_log.ErrorFormat("[InWorldz.Stratus]: Timeout attempting to store asset {0}. Storing locally.", asset.FullID);
                _diskWriteBack.StoreAsset(wireAsset);
            }
            else
            {
                m_log.ErrorFormat("[InWorldz.Stratus]: Timeout attempting to store asset {0}: {1}", asset.FullID, e);
                throw new AssetServerException(String.Format("Timeout attempting to store asset {0}: {1}", asset.FullID, e.Message), e);
            }
        }

        public void UpdateAsset(AssetBase asset)
        {
            this.StoreAsset(asset);
        }

        /// <summary>
        /// Requests that an asset be removed from storage and does not return until the operation completes
        /// </summary>
        /// <param name="assetID"></param>
        /// <returns></returns>
        public void PurgeAssetSync(OpenMetaverse.UUID assetID)
        {
            ManualResetEventSlim syncEvent = new ManualResetEventSlim();
            Exception thrown = null;

            _threadPool.QueueWorkItem(() =>
            {
                CloudFilesAssetWorker worker = null;
                try
                {
                    worker = _asyncAssetWorkers.LeaseObject();
                    worker.PurgeAsset(assetID);
                }
                catch (Exception e)
                {
                    thrown = e;
                }
                finally
                {
                    if (worker != null) _asyncAssetWorkers.ReturnObject(worker);
                }

                syncEvent.Set();
            });

            if (syncEvent.Wait(ASSET_WAIT_TIMEOUT))
            {
                syncEvent.Dispose();
            }
            else
            {
                m_log.WarnFormat("[InWorldz.Stratus]: Timout waiting for synchronous asset purge for {0}", assetID);
            }

            if (thrown != null) throw new AssetServerException(thrown.Message, thrown);
        }

        public string Version
        {
            get { return "1.0"; }
        }

        public string Name
        {
            get { return "InWorldz.Data.Assets.Stratus.CloudFilesAssetClient"; }
        }

        public void Initialize()
        {
        }

        public void Dispose()
        {

        }
    }
}
