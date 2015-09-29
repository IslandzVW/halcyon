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

using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.ClientStack
{
    public abstract class RegionApplicationBase : BaseOpenSimServer
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<EndPoint, uint> m_clientCircuits = new Dictionary<EndPoint, uint>();
        protected NetworkServersInfo m_networkServersInfo;

        public NetworkServersInfo NetServersInfo
        {
            get { return m_networkServersInfo; }
        }

        protected uint m_httpServerPort;
        
        public CommunicationsManager CommunicationsManager 
        {
            get { return m_commsManager; }
            set { m_commsManager = value; }
        }
        protected CommunicationsManager m_commsManager;        

        protected StorageManager m_storageManager;
        
        protected ClientStackManager m_clientStackManager;

        public SceneManager SceneManager
        {
            get { return m_sceneManager; }
        }
        protected SceneManager m_sceneManager = new SceneManager();

        protected IAssetCache m_assetCache;

        public IAssetCache AssetCache
        {
            get { return m_assetCache; }
            set { m_assetCache = value; }
        }
        
        protected abstract void Initialize();
        
        /// <summary>
        /// Get a new physics scene.
        /// </summary>
        /// 
        /// <param name="osSceneIdentifier">
        /// The name of the OpenSim scene this physics scene is serving.  This will be used in log messages.  
        /// </param>
        /// <returns></returns>        
        protected abstract PhysicsScene GetPhysicsScene(string osSceneIdentifier, UUID regionId);
        
        protected abstract StorageManager CreateStorageManager();
        protected abstract ClientStackManager CreateClientStackManager();
        protected abstract Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager);        

        protected override void StartupSpecific()
        {
            m_storageManager = CreateStorageManager();

            m_clientStackManager = CreateClientStackManager();

            Initialize();

            // Main HTTP server first
            m_httpServer = new BaseHttpServer(m_httpServerPort, null);
            m_httpServer.HostName = m_networkServersInfo.HostName; 
            MainServer.AddHttpServer(m_httpServer);

            m_log.InfoFormat("[REGION]: Starting HTTP server on {0}:{1}", m_httpServer.HostName, m_httpServerPort);
            m_httpServer.Start();

            // If specified configure an ssl server for use as well
            // you need a Cert Request/Signed pair installed in the MY store with the CN specified
            if (m_networkServersInfo.HttpUsesSSL)
            {
                if (m_networkServersInfo.HttpListenerPort == m_networkServersInfo.httpSSLPort)
                {
                    m_log.Error("[HTTP]: HTTP Server config failed.   HTTP Server and HTTPS server must be on different ports");
                }
                else
                {
                    /// XXX Should also support specifying an IP Address
                    BaseHttpServer secureServer = new BaseHttpServer(m_networkServersInfo.httpSSLPort, null);
                    secureServer.HostName = m_networkServersInfo.HostName;

                    if (m_networkServersInfo.HttpSSLCN != null)
                        secureServer.SetSecureParams(m_networkServersInfo.HttpSSLCN, m_networkServersInfo.sslProtocol);
                    else
                        secureServer.SetSecureParams(m_networkServersInfo.HttpSSLCert, m_networkServersInfo.HttpSSLPassword, m_networkServersInfo.sslProtocol);

                    MainServer.AddHttpServer(secureServer);

                    m_log.Info("[REGION]: Starting HTTPS server");
                    secureServer.Start();
                }
            }

            base.StartupSpecific();
        }

        /// <summary>
        /// Get a new physics scene.
        /// </summary>
        /// <param name="engine">The name of the physics engine to use</param>
        /// <param name="meshEngine">The name of the mesh engine to use</param>
        /// <param name="config">The configuration data to pass to the physics and mesh engines</param>
        /// <param name="osSceneIdentifier">
        /// The name of the OpenSim scene this physics scene is serving.  This will be used in log messages.  
        /// </param>
        /// <returns></returns>
        protected PhysicsScene GetPhysicsScene(
            string engine, string meshEngine, IConfigSource config, string osSceneIdentifier,
            UUID regionId)
        {
            PhysicsPluginManager physicsPluginManager;
            physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPluginsFromAssemblies("Physics");
            
            return physicsPluginManager.GetPhysicsScene(engine, meshEngine, config, osSceneIdentifier, regionId);
        }
    }
}
