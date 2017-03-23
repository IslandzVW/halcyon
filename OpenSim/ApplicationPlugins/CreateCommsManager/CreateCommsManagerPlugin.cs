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

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Osp;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
//using OpenSim.Region.Communications.Hypergrid;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;

namespace OpenSim.ApplicationPlugins.CreateCommsManager
{
    public class CreateCommsManagerPlugin : IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IApplicationPlugin Members

        // TODO: required by IPlugin, but likely not at all right
        private string m_name = "CreateCommsManagerPlugin";
        private string m_version = "0.0";

        public string Version
        {
            get { return m_version; }
        }

        public string Name
        {
            get { return m_name; }
        }

        protected OpenSimBase m_openSim;

        protected BaseHttpServer m_httpServer;

        protected CommunicationsManager m_commsManager;
        protected GridInfoService m_gridInfoService;
        protected IHyperlink HGServices = null;

        protected IRegionCreator m_regionCreator;

        public void Initialize()
        {
            m_log.Info("[LOADREGIONS]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitializedException(Name);
        }

        public void Initialize(OpenSimBase openSim)
        {
            m_openSim = openSim;
            m_httpServer = openSim.HttpServer;

            InitializeCommsManager(openSim);
            if (m_commsManager != null)
            {
                m_openSim.ApplicationRegistry.RegisterInterface<IUserService>(m_commsManager.UserService);
            }


        }

        public void PostInitialize()
        {
            if (m_openSim.ApplicationRegistry.TryGet<IRegionCreator>(out m_regionCreator))
            {
                m_regionCreator.OnNewRegionCreated += RegionCreated;
            }
        }

        public void Dispose()
        {
        }

        #endregion

        private void RegionCreated(IScene scene)
        {
            if (m_commsManager != null)
            {
                scene.RegisterModuleInterface<IUserService>(m_commsManager.UserService);
            }
        }

        protected void InitializeCommsManager(OpenSimBase openSim)
        {
            LibraryRootFolder libraryRootFolder = new LibraryRootFolder(m_openSim.ConfigurationSettings.LibrariesXMLFile, m_openSim.ConfigurationSettings.LibraryName);

            InitializeStandardServices(libraryRootFolder);

            openSim.CommunicationsManager = m_commsManager;
        }

        protected void InitializeStandardServices(LibraryRootFolder libraryRootFolder)
        {
            // Standalone mode is determined by !startupConfig.GetBoolean("gridmode", false)
            if (m_openSim.ConfigurationSettings.Standalone)
            {
                InitializeStandaloneServices(libraryRootFolder);
            }
            else
            {
                // We are in grid mode
                InitializeGridServices(libraryRootFolder);
            }
        }

        /// <summary>
        /// Initializes the backend services for standalone mode, and registers some http handlers
        /// </summary>
        /// <param name="libraryRootFolder"></param>
        protected virtual void InitializeStandaloneServices(LibraryRootFolder libraryRootFolder)
        {
            m_commsManager
                = new CommunicationsLocal(
                    m_openSim.ConfigurationSettings, m_openSim.NetServersInfo, 
                    m_httpServer, m_openSim.AssetCache, libraryRootFolder);

            CreateGridInfoService();
        }

        protected virtual void InitializeGridServices(LibraryRootFolder libraryRootFolder)
        {
            m_commsManager
                = new CommunicationsOGS1(m_openSim.NetServersInfo, m_httpServer, m_openSim.AssetCache, libraryRootFolder,
                    m_openSim.ConfigurationSettings);

            m_httpServer.AddStreamHandler(new OpenSim.SimStatusHandler());
            m_httpServer.AddStreamHandler(new OpenSim.XSimStatusHandler(m_openSim));
            if (!String.IsNullOrEmpty(m_openSim.userStatsURI))
                m_httpServer.AddStreamHandler(new OpenSim.UXSimStatusHandler(m_openSim));
        }

        private void CreateGridInfoService()
        {
            // provide grid info
            m_gridInfoService = new GridInfoService(m_openSim.ConfigSource.Source);

            // Old Style
            m_httpServer.AddXmlRPCHandler("get_grid_info", m_gridInfoService.XmlRpcGridInfoMethod);

            // New Style service interface
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_grid_info"), m_gridInfoService.XmlRpcGridInfoMethod));

            // REST Handler
            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/get_grid_info", m_gridInfoService.RestGetGridInfoMethod));
        }
    }
}
