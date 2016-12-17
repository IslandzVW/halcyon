/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using OpenMetaverse;

namespace OpenSim.Framework
{
    ///
    public class AssetStats
    {
        public string Provider;
        // Asset FetchStore counters
        public ulong nTotal;
        // Reads/Fetches
        public ulong nGet;
        public ulong nGetInit;
        public ulong nGetHit;
        public ulong nGetFetches;
        public ulong nGetNotFound;
        // Writes/Stores
        public ulong nPut;
        public ulong nPutInit;
        public ulong nPutCached;
        public ulong nPutExists;
        public ulong nPutTO;     // timeout
        public ulong nPutNTO;    // .NET conn timeout
        public ulong nPutExceptWeb;
        public ulong nPutExceptIO;
        public ulong nPutExcept; // other exceptions
        // Update stats (ignored by CacheAssetIfAppropriate)
        public ulong nBigAsset;  // 
        public ulong nBigStream;
        public ulong nDupUpdate;


        public float[] allGets;
        public float[] allPuts;

        public AssetStats(string provider)
        {
            Provider = provider;
        }
    }

    /// <summary>
    /// Description of IAssetServer.
    /// </summary>
    public interface IAssetServer : IPlugin
    {
        void Initialize(ConfigSettings settings);
        
        /// <summary>
        /// Start the asset server
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stop the asset server
        /// </summary>
        void Stop();
        
        void SetReceiver(IAssetReceiver receiver);

        /// <summary>
        /// Async asset request
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="isTexture"></param>
        void RequestAsset(UUID assetID, AssetRequestInfo args);

        /// <summary>
        /// Synchronous asset request
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="isTexture"></param>
        AssetBase RequestAssetSync(UUID assetID);

        void StoreAsset(AssetBase asset);
        void UpdateAsset(AssetBase asset);

        AssetStats GetStats(bool resetStats);
    }

    public class AssetClientPluginInitializer : PluginInitializerBase
    {
        private ConfigSettings config;

        public AssetClientPluginInitializer (ConfigSettings p_sv)
        {
            config = p_sv;
        }
        public override void Initialize (IPlugin plugin)
        {
            IAssetServer p = plugin as IAssetServer;
            p.Initialize (config);
        }
    }

    public interface IAssetPlugin
    {
        IAssetServer GetAssetServer();
    }

}
