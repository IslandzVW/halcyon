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
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Net;
using System.Reflection;
using System.Timers;
using System.Threading.Tasks;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;

using OpenMetaverse;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace InWorldz.RemoteAdmin
{
    class RemoteAdminPlugin : IApplicationPlugin
    {

        #region Declares
        
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_name = "RemoteAdmin";
        private string m_version = "0.0";
        private static Object rslock = new Object();
        private RemoteAdmin m_admin = null;
        private OpenSimBase m_app = null;

        #endregion

        public string Version
        {
            get { return m_version; }
        }

        public string Name
        {
            get { return m_name; }
        }

        public void Initialize()
        {
            m_log.Info("[RADMIN]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitializedException(Name);
        }

        public void Initialize(OpenSimBase openSim)
        {
            m_app = openSim;
            m_admin = new RemoteAdmin();
        }

        public void PostInitialize()
        {
            m_admin.AddCommand("Region", "Restart", RegionRestartHandler);
            m_admin.AddCommand("Region", "SendAlert", RegionSendAlertHandler);
            m_admin.AddCommand("Region", "Shutdown", RegionShutdownHandler);
            m_admin.AddCommand("Region", "Backup", RegionBackupHandler);
            m_admin.AddCommand("Region", "Restore", RegionRestoreHandler);
            m_admin.AddCommand("Region", "LoadOAR", LoadOARHandler);
            m_admin.AddCommand("Region", "SaveOAR", SaveOARHandler);
            m_admin.AddCommand("Region", "ChangeParcelFlags", RegionChangeParcelFlagsHandler);

            m_admin.AddHandler(MainServer.Instance);    
        }

        public void Dispose()
        {
            m_admin.Dispose();
        }

        #region RemoteAdmin Region Handlers

        private object RegionRestartHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            UUID regionID = new UUID((string)args[1]);
            Scene rebootedScene;

            if (!m_app.SceneManager.TryGetScene(regionID, out rebootedScene))
                throw new Exception("region not found");

            rebootedScene.Restart(30);
            return (true);
        }

        public object RegionShutdownHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            try
            {
                Scene rebootedScene;
                string message;

                UUID regionID = new UUID(Convert.ToString(args[1]));
                int delay = Convert.ToInt32(args[2]);

                if (!m_app.SceneManager.TryGetScene(regionID, out rebootedScene))
                    throw new Exception("Region not found");

                message = GenerateShutdownMessage(delay);

                m_log.DebugFormat("[RADMIN] Shutdown: {0}", message);

                IDialogModule dialogModule = rebootedScene.RequestModuleInterface<IDialogModule>();
                if (dialogModule != null)
                    dialogModule.SendGeneralAlert(message);

                ulong tcNow = Util.GetLongTickCount();
                ulong endTime = tcNow + (ulong)(delay * 1000);
                ulong nextReport = tcNow + (ulong)(60 * 1000);

                // Perform shutdown
                if (delay > 0)
                {
                    while (true)
                    {
                        System.Threading.Thread.Sleep(1000);

                        tcNow = Util.GetLongTickCount();
                        if (tcNow >= endTime)
                        {
                            break;
                        }

                        if (tcNow >= nextReport)
                        {
                            delay -= 60;

                            if (delay >= 0)
                            {
                                GenerateShutdownMessage(delay);
                                nextReport = tcNow + (ulong)(60 * 1000);
                            }
                        }
                    }
                }

                // Do this on a new thread so the actual shutdown call returns successfully.
                Task.Factory.StartNew(() => 
                {
                   m_app.Shutdown();
                });
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[RADMIN] Shutdown: failed: {0}", e.Message);
                m_log.DebugFormat("[RADMIN] Shutdown: failed: {0}", e.ToString());
                throw e;
            }

            m_log.Info("[RADMIN]: Shutdown Administrator Request complete");
            return true;
        }

        private static string GenerateShutdownMessage(int delay)
        {
            string message;
            if (delay > 0)
            {
                if (delay <= 60)
                    message = "Region is going down in " + delay.ToString() + " second(s).";
                else
                    message = "Region is going down in " + (delay / 60).ToString() + " minute(s).";
            }
            else
            {
                message = "Region is going down now.";
            }
            return message;
        }
        
        public object RegionSendAlertHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            if (args.Count < 3)
                return false;

            Scene scene;
            if (!m_app.SceneManager.TryGetScene((string)args[1], out scene))
                throw new Exception("region not found");
            String message = (string)args[2];

            IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule>();
            if (dialogModule != null)
                dialogModule.SendGeneralAlert(message);

            return true;
        }

        /// <summary>
        /// Load an OAR file into a region..
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcLoadOARMethod takes the following XMLRPC parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>session</term>
        ///       <description>An authenticated session ID</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the region</description></item>
        /// <item><term>filename</term>
        ///       <description>file name of the OAR file</description></item>
        /// </list>
        ///
        /// Returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// </list>
        /// </remarks>
        public object LoadOARHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            Scene scene;
            if (!m_app.SceneManager.TryGetScene((string)args[1], out scene))
                throw new Exception("region not found");

            String filename = (string)args[2];
            bool allowUserReassignment = Convert.ToBoolean(args[3]);
            bool skipErrorGroups = Convert.ToBoolean(args[4]);

            m_log.Info("[RADMIN]: Received Load OAR Administrator Request");

            lock (rslock)
            {
                try
                {
                    IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
                    if (archiver != null)
                        archiver.DearchiveRegion(filename, allowUserReassignment, skipErrorGroups);
                    else
                        throw new Exception("Archiver module not present for scene");

                    m_log.Info("[RADMIN]: Load OAR Administrator Request complete");
                    return true;
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[RADMIN] LoadOAR: {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] LoadOAR: {0}", e.ToString());
                }

                return false;
            }
        }

        /// <summary>
        /// Load an OAR file into a region..
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcLoadOARMethod takes the following XMLRPC parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>session</term>
        ///       <description>An authenticated session ID</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the region</description></item>
        /// <item><term>filename</term>
        ///       <description>file name of the OAR file</description></item>
        /// </list>
        ///
        /// Returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// </list>
        /// </remarks>
        public object SaveOARHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            Scene scene;
            if (!m_app.SceneManager.TryGetScene((string)args[1], out scene))
                throw new Exception("region not found");

            String filename = (string)args[2];
            bool storeAssets = Convert.ToBoolean(args[3]);

            m_log.Info("[RADMIN]: Received Save OAR Administrator Request");

            lock (rslock)
            {
                try
                {
                    IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
                    if (archiver != null)
                        archiver.ArchiveRegion(filename, storeAssets);
                    else
                        throw new Exception("Archiver module not present for scene");

                    m_log.Info("[RADMIN]: Save OAR Administrator Request complete");
                    return true;
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[RADMIN] LoadOAR: {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] LoadOAR: {0}", e.ToString());
                }

                return false;
            }
        }

        /// <summary>
        /// Load an OAR file into a region..
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcLoadOARMethod takes the following XMLRPC parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>session</term>
        ///       <description>An authenticated session ID</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the region</description></item>
        /// <item><term>filename</term>
        ///       <description>file name of the OAR file</description></item>
        /// </list>
        ///
        /// Returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// </list>
        /// </remarks>
        public object RegionBackupHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            String regionName = (string)args[1];
            String filename = (string)args[2];
            bool storeAssets = Convert.ToBoolean(args[3]);

            m_log.Info("[RADMIN]: Received Region Backup (SaveExplicitOAR) Administrator Request");

            lock (rslock)
            {
                try
                {
                    m_app.SceneManager.SaveExplicitOar(regionName, filename, storeAssets);
                    m_log.Info("[RADMIN]: Save OAR Administrator Request complete");
                    return true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[RADMIN] SaveOAR: {0}", e.ToString());
                }

                return false;
            }
        }

        /// <summary>
        /// Load an OAR file into a region..
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcLoadOARMethod takes the following XMLRPC parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>session</term>
        ///       <description>An authenticated session ID</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the region</description></item>
        /// <item><term>filename</term>
        ///       <description>file name of the OAR file</description></item>
        /// </list>
        ///
        /// Returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// </list>
        /// </remarks>
        public object RegionRestoreHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            Scene scene;
            if (!m_app.SceneManager.TryGetScene((string)args[1], out scene))
                throw new Exception("region not found");

            String filename = (string)args[2];
            bool allowUserReassignment = Convert.ToBoolean(args[3]);
            bool skipErrorGroups = Convert.ToBoolean(args[4]);

            m_log.Info("[RADMIN]: Received Region Restore Administrator Request");

            lock (rslock)
            {
                try
                {
                    IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
                    if (archiver != null)
                        archiver.DearchiveRegion(filename, allowUserReassignment, skipErrorGroups);
                    else
                        throw new Exception("Archiver module not present for scene");

                    m_log.Info("[RADMIN]: Load OAR Administrator Request complete");
                    return true;
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[RADMIN] LoadOAR: {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] LoadOAR: {0}", e.ToString());
                }

                return false;
            }
        }

        /// <summary>
        /// Changes the flags for all parcels on a region
        /// <summary>
        public object RegionChangeParcelFlagsHandler(IList args, IPEndPoint remoteClient)
        {
            m_admin.CheckSessionValid(new UUID((string)args[0]));

            Scene scene;
            if (!m_app.SceneManager.TryGetScene((string)args[1], out scene))
                throw new Exception("region not found");

            bool enable = args[2].ToString().ToLower() == "enable";
            uint mask = Convert.ToUInt32(args[3]);

            m_log.Info("[RADMIN]: Received Region Change Parcel Flags Request");

            lock (rslock)
            {
                try
                {
                    ILandChannel channel = scene.LandChannel;
                    List<ILandObject> parcels = channel.AllParcels();

                    foreach (var parcel in parcels)
                    {
                        LandData data = parcel.landData.Copy();
                        if (enable)
                        {
                            data.Flags = data.Flags | mask;
                        }
                        else
                        {
                            data.Flags = data.Flags & ~mask;
                        }

                        scene.LandChannel.UpdateLandObject(parcel.landData.LocalID, data);
                    }
                    
                    m_log.Info("[RADMIN]: Change Parcel Flags Request complete");
                    return true;
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[RADMIN] ChangeParcelFlags: {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] ChangeParcelFlags: {0}", e.ToString());
                }

                return false;
            }
        }

        #endregion

    }
}
