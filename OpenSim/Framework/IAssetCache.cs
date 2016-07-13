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
using OpenMetaverse.Packets;

namespace OpenSim.Framework
{
    public class RequestData
    {
        public byte requestSource;
        public bool isTexture;
        public AssetRequestCallback callBack;
        public object request;
    }

    /// <summary>
    /// Interface to the local asset cache.  This is the mechanism through which assets can be added and requested.
    /// </summary>    
    public interface IAssetCache : IAssetReceiver, IPlugin
    {
        /// <value>
        /// The 'server' from which assets can be requested and to which assets are persisted.
        /// </value>        
        IAssetServer AssetServer { get; }
        
        void Initialize(ConfigSettings cs, IAssetServer server);        

        /// <summary>
        /// Report statistical data to the log.
        /// </summary>        
        void ShowState(bool resetStats);
        
        /// <summary>
        /// Asynchronously retrieve an asset.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="callback">
        ///     A callback invoked when the asset has either been found or not found.
        ///     If the asset was found this is called with the asset UUID and the asset data
        ///     If the asset was not found this is still called with the asset UUID but with a null asset data reference</param>        
        /// </param>
        /// <param name="reqInfo">Identifies the source of the request and any other needed information</param>
        void GetAsset(UUID assetID, AssetRequestCallback callback, AssetRequestInfo reqInfo);
        
        /// <summary>
        /// Synchronously retreive an asset.  If the asset isn't in the cache, a request will be made to the persistent store to
        /// load it into the cache.
        /// </summary>
        ///
        /// <param name="assetID"></param>
        /// <param name="isTexture"></param>
        /// <returns>null if the asset could not be retrieved</returns>        
        AssetBase GetAsset(UUID assetID, AssetRequestInfo reqInfo);
        
        /// <summary>
        /// Add an asset to both the persistent store and the cache.
        /// </summary>
        /// <param name="asset"></param>        
        void AddAsset(AssetBase asset, AssetRequestInfo reqInfo);
        
        /// <summary>
        /// Handle an asset request from the client.  The result will be sent back asynchronously.
        /// </summary>
        /// <param name="userInfo"></param>
        /// <param name="transferRequest"></param>        
        void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest);
    }

    public class AssetCachePluginInitializer : PluginInitializerBase
    {
        private ConfigSettings config;
        private IAssetServer   server;

        public AssetCachePluginInitializer (ConfigSettings p_sv, IAssetServer p_as)
        {
            config = p_sv;
            server = p_as;
        }
        public override void Initialize (IPlugin plugin)
        {
            IAssetCache p = plugin as IAssetCache;
            p.Initialize (config, server);
        }
    }

}
