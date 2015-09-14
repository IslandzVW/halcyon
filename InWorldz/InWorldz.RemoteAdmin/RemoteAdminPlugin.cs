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
        private bool _enabled = false;
        private List<Scene> _scenes = new List<Scene>();
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

        public void Initialise()
        {
            m_log.Info("[RADMIN]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            m_app = openSim;
            m_admin = new RemoteAdmin();
        }

        public void PostInitialise()
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


#if false
        public XmlRpcResponse XmlRpcLoadHeightmapMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            m_log.Info("[RADMIN]: Load height maps request started");

            try
            {
                Hashtable requestData = (Hashtable)request.Params[0];

                m_log.DebugFormat("[RADMIN]: Load Terrain: XmlRpc {0}", request.ToString());
                // foreach (string k in requestData.Keys)
                // {
                //     m_log.DebugFormat("[RADMIN]: Load Terrain: XmlRpc {0}: >{1}< {2}",
                //                       k, (string)requestData[k], ((string)requestData[k]).Length);
                // }

                checkStringParameters(request, new string[] { "password", "filename", "regionid" });

                if (m_requiredPassword != String.Empty &&
                    (!requestData.Contains("password") || (string)requestData["password"] != m_requiredPassword))
                    throw new Exception("wrong password");

                string file = (string)requestData["filename"];
                UUID regionID = (UUID)(string)requestData["regionid"];
                m_log.InfoFormat("[RADMIN]: Terrain Loading: {0}", file);

                responseData["accepted"] = true;

                Scene region = null;

                if (!m_app.SceneManager.TryGetScene(regionID, out region))
                    throw new Exception("1: unable to get a scene with that name");

                ITerrainModule terrainModule = region.RequestModuleInterface<ITerrainModule>();
                if (null == terrainModule) throw new Exception("terrain module not available");
                terrainModule.LoadFromFile(file);

                responseData["success"] = false;

                response.Value = responseData;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[RADMIN] Terrain Loading: failed: {0}", e.Message);
                m_log.DebugFormat("[RADMIN] Terrain Loading: failed: {0}", e.ToString());

                responseData["success"] = false;
                responseData["error"] = e.Message;
            }

            m_log.Info("[RADMIN]: Load height maps request complete");

            return response;
        }



        /// <summary>
        /// Create a new region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcCreateRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// <item><term>region_x</term>
        ///       <description>desired region X coordinate (integer)</description></item>
        /// <item><term>region_y</term>
        ///       <description>desired region Y coordinate (integer)</description></item>
        /// <item><term>region_master_first</term>
        ///       <description>firstname of region master</description></item>
        /// <item><term>region_master_last</term>
        ///       <description>lastname of region master</description></item>
        /// <item><term>region_master_uuid</term>
        ///       <description>explicit UUID to use for master avatar (optional)</description></item>
        /// <item><term>listen_ip</term>
        ///       <description>internal IP address (dotted quad)</description></item>
        /// <item><term>listen_port</term>
        ///       <description>internal port (integer)</description></item>
        /// <item><term>external_address</term>
        ///       <description>external IP address</description></item>
        /// <item><term>persist</term>
        ///       <description>if true, persist the region info
        ///       ('true' or 'false')</description></item>
        /// <item><term>public</term>
        ///       <description>if true, the region is public
        ///       ('true' or 'false') (optional, default: true)</description></item>
        /// <item><term>enable_voice</term>
        ///       <description>if true, enable voice on all parcels,
        ///       ('true' or 'false') (optional, default: false)</description></item>
        /// </list>
        ///
        /// XmlRpcCreateRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the newly created region</description></item>
        /// <item><term>region_name</term>
        ///       <description>name of the newly created region</description></item>
        /// </list>
        /// </remarks>
        public XmlRpcResponse XmlRpcCreateRegionMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: CreateRegion: new request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            lock (rslock)
            {

                int m_regionLimit = m_config.GetInt("region_limit", 0);
                bool m_enableVoiceForNewRegions = m_config.GetBoolean("create_region_enable_voice", false);
                bool m_publicAccess = m_config.GetBoolean("create_region_public", true);

                try
                {
                    Hashtable requestData = (Hashtable)request.Params[0];

                    checkStringParameters(request, new string[]
                                                       {
                                                           "password",
                                                           "region_name",
                                                           "region_master_first", "region_master_last",
                                                           "region_master_password",
                                                           "listen_ip", "external_address"
                                                       });
                    checkIntegerParams(request, new string[] { "region_x", "region_y", "listen_port" });

                    // check password
                    if (!String.IsNullOrEmpty(m_requiredPassword) &&
                        (string)requestData["password"] != m_requiredPassword) throw new Exception("wrong password");

                    // check whether we still have space left (iff we are using limits)
                    if (m_regionLimit != 0 && m_app.SceneManager.Scenes.Count >= m_regionLimit)
                        throw new Exception(String.Format("cannot instantiate new region, server capacity {0} already reached; delete regions first",
                                                          m_regionLimit));
                    // extract or generate region ID now
                    Scene scene = null;
                    UUID regionID = UUID.Zero;
                    if (requestData.ContainsKey("region_id") &&
                        !String.IsNullOrEmpty((string)requestData["region_id"]))
                    {
                        regionID = (UUID)(string)requestData["region_id"];
                        if (m_app.SceneManager.TryGetScene(regionID, out scene))
                            throw new Exception(
                                String.Format("region UUID already in use by region {0}, UUID {1}, <{2},{3}>",
                                              scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                              scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));
                    }
                    else
                    {
                        regionID = UUID.Random();
                        m_log.DebugFormat("[RADMIN] CreateRegion: new region UUID {0}", regionID);
                    }

                    // create volatile or persistent region info
                    RegionInfo region = new RegionInfo();

                    region.RegionID = regionID;
                    region.RegionName = (string)requestData["region_name"];
                    region.RegionLocX = Convert.ToUInt32(requestData["region_x"]);
                    region.RegionLocY = Convert.ToUInt32(requestData["region_y"]);

                    // check for collisions: region name, region UUID,
                    // region location
                    if (m_app.SceneManager.TryGetScene(region.RegionName, out scene))
                        throw new Exception(
                            String.Format("region name already in use by region {0}, UUID {1}, <{2},{3}>",
                                          scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                          scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));

                    if (m_app.SceneManager.TryGetScene(region.RegionLocX, region.RegionLocY, out scene))
                        throw new Exception(
                            String.Format("region location <{0},{1}> already in use by region {2}, UUID {3}, <{4},{5}>",
                                          region.RegionLocX, region.RegionLocY,
                                          scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                          scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));

                    region.InternalEndPoint =
                        new IPEndPoint(IPAddress.Parse((string)requestData["listen_ip"]), 0);

                    region.InternalEndPoint.Port = Convert.ToInt32(requestData["listen_port"]);
                    if (0 == region.InternalEndPoint.Port) throw new Exception("listen_port is 0");
                    if (m_app.SceneManager.TryGetScene(region.InternalEndPoint, out scene))
                        throw new Exception(
                            String.Format(
                                "region internal IP {0} and port {1} already in use by region {2}, UUID {3}, <{4},{5}>",
                                region.InternalEndPoint.Address,
                                region.InternalEndPoint.Port,
                                scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));


                    region.ExternalHostName = (string)requestData["external_address"];

                    string masterFirst = (string)requestData["region_master_first"];
                    string masterLast = (string)requestData["region_master_last"];
                    string masterPassword = (string)requestData["region_master_password"];

                    UUID userID = UUID.Zero;
                    if (requestData.ContainsKey("region_master_uuid"))
                    {
                        // ok, client wants us to use an explicit UUID
                        // regardless of what the avatar name provided
                        userID = new UUID((string)requestData["region_master_uuid"]);
                    }
                    else
                    {
                        // no client supplied UUID: look it up...
                        CachedUserInfo userInfo
                            = m_app.CommunicationsManager.UserProfileCacheService.GetUserDetails(
                                masterFirst, masterLast);

                        if (null == userInfo)
                        {
                            m_log.InfoFormat("master avatar does not exist, creating it");
                            // ...or create new user
                            userID = m_app.CommunicationsManager.UserAdminService.AddUser(
                                masterFirst, masterLast, masterPassword, "", region.RegionLocX, region.RegionLocY);

                            if (userID == UUID.Zero)
                                throw new Exception(String.Format("failed to create new user {0} {1}",
                                                                  masterFirst, masterLast));
                        }
                        else
                        {
                            userID = userInfo.UserProfile.ID;
                        }
                    }

                    region.MasterAvatarFirstName = masterFirst;
                    region.MasterAvatarLastName = masterLast;
                    region.MasterAvatarSandboxPassword = masterPassword;
                    region.MasterAvatarAssignedUUID = userID;

                    bool persist = Convert.ToBoolean((string)requestData["persist"]);
                    if (persist)
                    {
                        // default place for region XML files is in the
                        // Regions directory of the config dir (aka /bin)
                        string regionConfigPath = Path.Combine(Util.configDir(), "Regions");
                        try
                        {
                            // OpenSim.ini can specify a different regions dir
                            IConfig startupConfig = (IConfig)m_configSource.Configs["Startup"];
                            regionConfigPath = startupConfig.GetString("regionload_regionsdir", regionConfigPath).Trim();
                        }
                        catch (Exception)
                        {
                            // No INI setting recorded.
                        }
                        string regionXmlPath = Path.Combine(regionConfigPath,
                                                            String.Format(
                                                                m_config.GetString("region_file_template",
                                                                                   "{0}x{1}-{2}.xml"),
                                                                region.RegionLocX.ToString(),
                                                                region.RegionLocY.ToString(),
                                                                regionID.ToString(),
                                                                region.InternalEndPoint.Port.ToString(),
                                                                region.RegionName.Replace(" ", "_").Replace(":", "_").
                                                                    Replace("/", "_")));
                        m_log.DebugFormat("[RADMIN] CreateRegion: persisting region {0} to {1}",
                                          region.RegionID, regionXmlPath);
                        region.SaveRegionToFile("dynamic region", regionXmlPath);
                    }

                    // Create the region and perform any initial initialization

                    IScene newscene;
                    m_app.CreateRegion(region, out newscene);

                    // If an access specification was provided, use it.
                    // Otherwise accept the default.
                    newscene.RegionInfo.EstateSettings.PublicAccess = getBoolean(requestData, "public", m_publicAccess);

                    // enable voice on newly created region if
                    // requested by either the XmlRpc request or the
                    // configuration
                    if (getBoolean(requestData, "enable_voice", m_enableVoiceForNewRegions))
                    {
                        List<ILandObject> parcels = ((Scene)newscene).LandChannel.AllParcels();

                        foreach (ILandObject parcel in parcels)
                        {
                            parcel.landData.Flags |= (uint)ParcelFlags.AllowVoiceChat;
                            parcel.landData.Flags |= (uint)ParcelFlags.UseEstateVoiceChan;
                        }
                    }

                    responseData["success"] = true;
                    responseData["region_name"] = region.RegionName;
                    responseData["region_uuid"] = region.RegionID.ToString();

                    response.Value = responseData;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[RADMIN] CreateRegion: failed {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] CreateRegion: failed {0}", e.ToString());

                    responseData["success"] = false;
                    responseData["error"] = e.Message;

                    response.Value = responseData;
                }

                m_log.Info("[RADMIN]: CreateRegion: request complete");
                return response;
            }
        }

        /// <summary>
        /// Delete a new region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcDeleteRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// </list>
        ///
        /// XmlRpcDeleteRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        public XmlRpcResponse XmlRpcDeleteRegionMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: DeleteRegion: new request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            lock (rslock)
            {
                try
                {
                    Hashtable requestData = (Hashtable)request.Params[0];
                    checkStringParameters(request, new string[] { "password", "region_name" });

                    Scene scene = null;
                    string regionName = (string)requestData["region_name"];
                    if (!m_app.SceneManager.TryGetScene(regionName, out scene))
                        throw new Exception(String.Format("region \"{0}\" does not exist", regionName));

                    m_app.RemoveRegion(scene, true);

                    responseData["success"] = true;
                    responseData["region_name"] = regionName;

                    response.Value = responseData;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[RADMIN] DeleteRegion: failed {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] DeleteRegion: failed {0}", e.ToString());

                    responseData["success"] = false;
                    responseData["error"] = e.Message;

                    response.Value = responseData;
                }

                m_log.Info("[RADMIN]: DeleteRegion: request complete");
                return response;
            }
        }

        /// <summary>
        /// Change characteristics of an existing region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcModifyRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// <item><term>public</term>
        ///       <description>if true, set the region to public
        ///       ('true' or 'false'), else to private</description></item>
        /// <item><term>enable_voice</term>
        ///       <description>if true, enable voice on all parcels of
        ///       the region, else disable</description></item>
        /// </list>
        ///
        /// XmlRpcModifyRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>

        public XmlRpcResponse XmlRpcModifyRegionMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: ModifyRegion: new request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            lock (rslock)
            {
                try
                {
                    Hashtable requestData = (Hashtable)request.Params[0];
                    checkStringParameters(request, new string[] { "password", "region_name" });

                    Scene scene = null;
                    string regionName = (string)requestData["region_name"];
                    if (!m_app.SceneManager.TryGetScene(regionName, out scene))
                        throw new Exception(String.Format("region \"{0}\" does not exist", regionName));

                    // Modify access 
                    scene.RegionInfo.EstateSettings.PublicAccess =
                        getBoolean(requestData, "public", scene.RegionInfo.EstateSettings.PublicAccess);

                    if (requestData.ContainsKey("enable_voice"))
                    {
                        bool enableVoice = getBoolean(requestData, "enable_voice", true);
                        List<ILandObject> parcels = ((Scene)scene).LandChannel.AllParcels();

                        foreach (ILandObject parcel in parcels)
                        {
                            if (enableVoice)
                            {
                                parcel.landData.Flags |= (uint)ParcelFlags.AllowVoiceChat;
                                parcel.landData.Flags |= (uint)ParcelFlags.UseEstateVoiceChan;
                            }
                            else
                            {
                                parcel.landData.Flags &= ~(uint)ParcelFlags.AllowVoiceChat;
                                parcel.landData.Flags &= ~(uint)ParcelFlags.UseEstateVoiceChan;
                            }
                        }
                    }

                    responseData["success"] = true;
                    responseData["region_name"] = regionName;

                    response.Value = responseData;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[RADMIN] ModifyRegion: failed {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] ModifyRegion: failed {0}", e.ToString());

                    responseData["success"] = false;
                    responseData["error"] = e.Message;

                    response.Value = responseData;
                }

                m_log.Info("[RADMIN]: ModifyRegion: request complete");
                return response;
            }
        }
#endif


    }
}
