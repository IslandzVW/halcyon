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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Timers;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Communications.Capabilities.Caps;
using OSDArray=OpenMetaverse.StructuredData.OSDArray;
using OSDMap=OpenMetaverse.StructuredData.OSDMap;
using Amib.Threading;

namespace OpenSim.Region.CoreModules.World.WorldMap
{
    public class WorldMapModule : INonSharedRegionModule, IWorldMapModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string DEFAULT_WORLD_MAP_EXPORT_PATH = "exportmap.jpg";

        private static readonly string m_mapLayerPath = "0001/";

        //private IConfig m_config;
        protected Scene m_scene;
        private List<MapBlockData> cachedMapBlocks = new List<MapBlockData>();
        private int cachedTime = 0;
        private byte[] myMapImageJPEG;
        protected volatile bool m_Enabled = false;
        private Dictionary<UUID, MapRequestState> m_openRequests = new Dictionary<UUID, MapRequestState>();
        private Dictionary<string, int> m_blacklistedurls = new Dictionary<string, int>();
        private Dictionary<ulong, int> m_blacklistedregions = new Dictionary<ulong, int>();
        private Dictionary<ulong, string> m_cachedRegionMapItemsAddress = new Dictionary<ulong, string>();
        private List<UUID> m_rootAgents = new List<UUID>();

        private SmartThreadPool _blockRequestPool;
        private SmartThreadPool _infoRequestPool;

        private Dictionary<UUID, IWorkItemResult> _currentRequests = new Dictionary<UUID, IWorkItemResult>();

        /// <summary>
        /// The path to where the generated region tile will be saved and the start of the file name.  Comes from Halcyon.ini, section WorldMap, entry RegionMapTileExportFilename.
        /// </summary>
        private string regionTileExportFilename = "";

        private struct MapTileDataForExport {
            public string filename;
            public byte[] jpegData;
        }

        /// <summary>
        /// Whether or not the map has been tainted and the map tile file needs to be updated.
        /// </summary>
        private bool isMapTainted = true;
        private bool readyToDrawMap = false;
        /// <summary>
        /// The minimum amount of time required to pass before the next automatic write of a map tile file to the server.  Keeps the file from being constantly written to in busy situations.
        /// </summary>
        private TimeSpan minimumMapPushTime = new TimeSpan(1, 0, 0);
        /// <summary>
        /// The last time the map tile file was pushed to the map server.
        /// </summary>
        private DateTime lastMapPushTime = new DateTime(0); // Set to 0 initally to make sure the map tile gets drawn asap.
        /// <summary>
        /// Used to make sure the map tile file gets updated after a maximum amount of time if it has been tainted.
        /// </summary>
        private System.Timers.Timer mapTileUpdateTimer;

        private bool terrainTextureCanTaintMapTile = false;
        private bool primsCanTaintMapTile = true;

        //private int CacheRegionsDistance = 256;

        #region INonSharedRegionModule Members
        public virtual void Initialize(IConfigSource config)
        {
            IConfig startupConfig = config.Configs["Startup"]; // Location supported for legacy INI files.
            IConfig worldmapConfig = config.Configs["WorldMap"];
            if (
                (worldmapConfig != null && worldmapConfig.GetString("WorldMapModule", "WorldMap") == "WorldMap")
                ||
                (startupConfig != null && startupConfig.GetString("WorldMapModule", "WorldMap") == "WorldMap") // LEGACY
            )
            {
                m_Enabled = true;
            }

            if (worldmapConfig != null)
            {
                regionTileExportFilename = worldmapConfig.GetString("RegionMapTileExportFilename", regionTileExportFilename);

                // Low values could exhaust F&F pools or cause overdue amounts of CPU usage.  No need to refresh the map faster than once a minute anyway.
                int pushTimeSeconds = Math.Max(60, worldmapConfig.GetInt("MinimumTaintedMapTileWaitTime", (int) minimumMapPushTime.TotalSeconds));
                minimumMapPushTime = new TimeSpan(0, 0, pushTimeSeconds);
                m_log.DebugFormat("[WORLD MAP] Got min wait time of {0} seconds which resulted in a span of {1}", pushTimeSeconds, minimumMapPushTime);

                double timerSeconds = (double) Math.Max(0, worldmapConfig.GetInt("MaximumTaintedMapTileWaitTime", 0));
                m_log.DebugFormat("[WORLD MAP] Got max wait time of {0} seconds", timerSeconds);
                if (timerSeconds > 0d && regionTileExportFilename.Length > 0)
                {
                    mapTileUpdateTimer = new System.Timers.Timer(timerSeconds * 1000.0d);
                    mapTileUpdateTimer.Elapsed += HandleTaintedMapTimer;
                    mapTileUpdateTimer.AutoReset = true;
                    mapTileUpdateTimer.Enabled = false;
                }

                terrainTextureCanTaintMapTile = worldmapConfig.GetBoolean("TextureOnMapTile", terrainTextureCanTaintMapTile);
                primsCanTaintMapTile = worldmapConfig.GetBoolean("DrawPrimOnMapTile", primsCanTaintMapTile);
            }

            STPStartInfo reqPoolStartInfo = new STPStartInfo();
            reqPoolStartInfo.MaxWorkerThreads = 2;
            reqPoolStartInfo.IdleTimeout = 5 * 60 * 1000;
            reqPoolStartInfo.ThreadPriority = ThreadPriority.Lowest;

            STPStartInfo infoReqPoolStartInfo = new STPStartInfo();
            reqPoolStartInfo.MaxWorkerThreads = 4;
            reqPoolStartInfo.IdleTimeout = 5 * 60 * 1000;
            reqPoolStartInfo.ThreadPriority = ThreadPriority.Lowest;

            _blockRequestPool = new SmartThreadPool(reqPoolStartInfo);
            _blockRequestPool.Name = "Map Block Requests";
            _infoRequestPool = new SmartThreadPool(infoReqPoolStartInfo);
            _infoRequestPool.Name = "Map Info Requests";
        }

        public virtual void AddRegion (Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (scene)
            {
                m_scene = scene;

                m_scene.RegisterModuleInterface<IWorldMapModule>(this);

                m_scene.AddCommand(
                    this, "export-map",
                    "export-map [<path>]",
                    "Save an image of the world map", HandleExportWorldMapConsoleCommand);

                AddHandlers();
            }

            if (mapTileUpdateTimer != null)
            {
                mapTileUpdateTimer.Enabled = true;
            }
        }

        public virtual void RemoveRegion (Scene scene)
        {
            if (!m_Enabled)
                return;

            if (mapTileUpdateTimer != null)
            {
                mapTileUpdateTimer.Enabled = false;
            }

            lock (m_scene)
            {
                m_Enabled = false;
                RemoveHandlers();
                m_scene = null;
            }
        }

        public virtual void RegionLoaded (Scene scene)
        {
        }


        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "WorldMapModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        // this has to be called with a lock on m_scene
        protected virtual void AddHandlers()
        {
            myMapImageJPEG = new byte[0];

            string regionimage = "regionImage" + m_scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", String.Empty);
            m_log.Info("[WORLD MAP]: JPEG Map location: http://" + m_scene.RegionInfo.ExternalHostName + ":" + m_scene.RegionInfo.HttpPort.ToString() + "/index.php?method=" + regionimage);

            m_scene.CommsManager.HttpServer.AddHTTPHandler(regionimage, OnHTTPGetMapImage);
            m_scene.CommsManager.HttpServer.AddLLSDHandler(
                "/MAP/MapItems/" + m_scene.RegionInfo.RegionHandle.ToString(), HandleRemoteMapItemRequest);

            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.EventManager.OnNewClient += OnNewClient;
            m_scene.EventManager.OnClientClosed += ClientLoggedOut;
            m_scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            m_scene.EventManager.OnMakeRootAgent += MakeRootAgent;
        }

        // this has to be called with a lock on m_scene
        protected virtual void RemoveHandlers()
        {
            m_scene.EventManager.OnMakeRootAgent -= MakeRootAgent;
            m_scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
            m_scene.EventManager.OnClientClosed -= ClientLoggedOut;
            m_scene.EventManager.OnNewClient -= OnNewClient;
            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;

            string regionimage = "regionImage" + m_scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", String.Empty);
            m_scene.CommsManager.HttpServer.RemoveLLSDHandler("/MAP/MapItems/" + m_scene.RegionInfo.RegionHandle.ToString(),
                                                              HandleRemoteMapItemRequest);
            m_scene.CommsManager.HttpServer.RemoveHTTPHandler(String.Empty, regionimage);
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            //m_log.DebugFormat("[WORLD MAP]: OnRegisterCaps: agentID {0} caps {1}", agentID, caps);
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("MapLayer",
                                 new RestStreamHandler("POST", capsBase + m_mapLayerPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                           {
                                                               return MapLayerRequest(request, path, param,
                                                                                      agentID, caps);
                                                           }));
        }

        /// <summary>
        /// Callback for a map layer request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string MapLayerRequest(string request, string path, string param,
                                      UUID agentID, Caps caps)
        {
            //try
            //{
                //m_log.DebugFormat("[MAPLAYER]: request: {0}, path: {1}, param: {2}, agent:{3}",
                                  //request, path, param,agentID.ToString());

            // this is here because CAPS map requests work even beyond the 10,000 limit.
            ScenePresence avatarPresence = null;

            m_scene.TryGetAvatar(agentID, out avatarPresence);

            if (avatarPresence != null)
            {
                bool lookup = false;

                lock (cachedMapBlocks)
                {
                    if (cachedMapBlocks.Count > 0 && ((cachedTime + 1800) > Util.UnixTimeSinceEpoch()))
                    {
                        List<MapBlockData> mapBlocks;

                        mapBlocks = cachedMapBlocks;
                        avatarPresence.ControllingClient.SendMapBlock(mapBlocks, 0);
                    }
                    else
                    {
                        lookup = true;
                    }
                }
                if (lookup)
                {
                    List<MapBlockData> mapBlocks;

                    mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks((int)m_scene.RegionInfo.RegionLocX - 8, (int)m_scene.RegionInfo.RegionLocY - 8, (int)m_scene.RegionInfo.RegionLocX + 8, (int)m_scene.RegionInfo.RegionLocY + 8);
                    avatarPresence.ControllingClient.SendMapBlock(mapBlocks,0);

                    lock (cachedMapBlocks)
                        cachedMapBlocks = mapBlocks;

                    cachedTime = Util.UnixTimeSinceEpoch();
                }
            }
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetOSDMapLayerResponse());
            return mapResponse.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="mapReq"></param>
        /// <returns></returns>
        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            m_log.Debug("[WORLD MAP]: MapLayer Request in region: " + m_scene.RegionInfo.RegionName);
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetOSDMapLayerResponse());
            return mapResponse;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected static OSDMapLayer GetOSDMapLayerResponse()
        {
            OSDMapLayer mapLayer = new OSDMapLayer();
            mapLayer.Right = 5000;
            mapLayer.Top = 5000;
            mapLayer.ImageID = new UUID("00000000-0000-1111-9999-000000000006");

            return mapLayer;
        }
        #region EventHandlers

        /// <summary>
        /// Registered for event
        /// </summary>
        /// <param name="client"></param>
        private void OnNewClient(IClientAPI client)
        {
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnMapItemRequest += HandleMapItemRequest;
        }

        /// <summary>
        /// Client logged out, check to see if there are any more root agents in the simulator
        /// If not, stop the mapItemRequest Thread
        /// Event handler
        /// </summary>
        /// <param name="AgentId">AgentID that logged out</param>
        private void ClientLoggedOut(UUID AgentId, Scene scene)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.Contains(AgentId))
                {
                    m_rootAgents.Remove(AgentId);
                }
            }
        }
        #endregion

        public virtual void HandleMapItemRequest(IClientAPI remoteClient, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            lock (m_rootAgents)
            {
                if (!m_rootAgents.Contains(remoteClient.AgentId))
                    return;
            }
            uint xstart = 0;
            uint ystart = 0;
            Utils.LongToUInts(m_scene.RegionInfo.RegionHandle, out xstart, out ystart);
            if (itemtype == 6) // we only sevice 6 right now (avatar green dots)
            {
                if (regionhandle == 0 || regionhandle == m_scene.RegionInfo.RegionHandle)
                {
                    // Local Map Item Request
                    List<ScenePresence> avatars = m_scene.GetAvatars();
                    int tc = Environment.TickCount;
                    List<mapItemReply> mapitems = new List<mapItemReply>();
                    mapItemReply mapitem = new mapItemReply();
                    if (avatars.Count == 0 || avatars.Count == 1)
                    {
                        mapitem = new mapItemReply();
                        mapitem.x = (uint)(xstart + 1);
                        mapitem.y = (uint)(ystart + 1);
                        mapitem.id = UUID.Zero;
                        mapitem.name = Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString());
                        mapitem.Extra = 0;
                        mapitem.Extra2 = 0;
                        mapitems.Add(mapitem);
                    }
                    else
                    {
                        foreach (ScenePresence avatar in avatars)
                        {
                            // Don't send a green dot for yourself
                            Vector3 avpos;
                            if ((avatar.UUID != remoteClient.AgentId) && avatar.HasSafePosition(out avpos))
                            {
                                mapitem = new mapItemReply();
                                mapitem.x = (uint)(xstart + avpos.X);
                                mapitem.y = (uint)(ystart + avpos.Y);
                                mapitem.id = UUID.Zero;
                                mapitem.name = Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString());
                                mapitem.Extra = 1;
                                mapitem.Extra2 = 0;
                                mapitems.Add(mapitem);
                            }
                        }
                    }
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                }
                else
                {
                    RequestMapItems(String.Empty,remoteClient.AgentId,flags,EstateID,godlike,itemtype,regionhandle);
                }
            }
        }

        /// <summary>
        /// Sends the mapitem response to the IClientAPI
        /// </summary>
        /// <param name="response">The OSDMap Response for the mapitem</param>
        private void RequestMapItemsCompleted(OSDMap response)
        {
            UUID requestID = response["requestID"].AsUUID();

            if (requestID != UUID.Zero)
            {
                MapRequestState mrs = new MapRequestState();
                mrs.agentID = UUID.Zero;
                lock (m_openRequests)
                {
                    if (m_openRequests.ContainsKey(requestID))
                    {
                        mrs = m_openRequests[requestID];
                        m_openRequests.Remove(requestID);
                    }
                }

                if (mrs.agentID != UUID.Zero)
                {
                    ScenePresence av = null;
                    m_scene.TryGetAvatar(mrs.agentID, out av);
                    if (av != null)
                    {
                        if (response.ContainsKey(mrs.itemtype.ToString()))
                        {
                            List<mapItemReply> returnitems = new List<mapItemReply>();
                            OSDArray itemarray = (OSDArray)response[mrs.itemtype.ToString()];
                            for (int i = 0; i < itemarray.Count; i++)
                            {
                                OSDMap mapitem = (OSDMap)itemarray[i];
                                mapItemReply mi = new mapItemReply();
                                mi.x = (uint)mapitem["X"].AsInteger();
                                mi.y = (uint)mapitem["Y"].AsInteger();
                                mi.id = mapitem["ID"].AsUUID();
                                mi.Extra = mapitem["Extra"].AsInteger();
                                mi.Extra2 = mapitem["Extra2"].AsInteger();
                                mi.name = mapitem["Name"].AsString();
                                returnitems.Add(mi);
                            }
                            av.ControllingClient.SendMapItemReply(returnitems.ToArray(), mrs.itemtype, mrs.flags);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enqueue the MapItem request for remote processing
        /// </summary>
        /// <param name="httpserver">blank string, we discover this in the process</param>
        /// <param name="id">Agent ID that we are making this request on behalf</param>
        /// <param name="flags">passed in from packet</param>
        /// <param name="EstateID">passed in from packet</param>
        /// <param name="godlike">passed in from packet</param>
        /// <param name="itemtype">passed in from packet</param>
        /// <param name="regionhandle">Region we're looking up</param>
        public void RequestMapItems(string httpserver, UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            bool dorequest = true;
            lock (m_rootAgents)
            {
                if (!m_rootAgents.Contains(id))
                    dorequest = false;
            }

            if (dorequest)
            {
                _infoRequestPool.QueueWorkItem(
                        delegate()
                        {
                            OSDMap response = RequestMapItemsAsync(httpserver, id, flags, EstateID, godlike, itemtype, regionhandle);
                            RequestMapItemsCompleted(response);
                        }
                    );
            }
        }

        /// <summary>
        /// Does the actual remote mapitem request
        /// This should be called from an asynchronous thread
        /// Request failures get blacklisted until region restart so we don't
        /// continue to spend resources trying to contact regions that are down.
        /// </summary>
        /// <param name="httpserver">blank string, we discover this in the process</param>
        /// <param name="id">Agent ID that we are making this request on behalf</param>
        /// <param name="flags">passed in from packet</param>
        /// <param name="EstateID">passed in from packet</param>
        /// <param name="godlike">passed in from packet</param>
        /// <param name="itemtype">passed in from packet</param>
        /// <param name="regionhandle">Region we're looking up</param>
        /// <returns></returns>
        private OSDMap RequestMapItemsAsync(string httpserver, UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            bool blacklisted = false;
            lock (m_blacklistedregions)
            {
                if (m_blacklistedregions.ContainsKey(regionhandle))
                    blacklisted = true;
            }

            if (blacklisted)
                return new OSDMap();

            UUID requestID = UUID.Random();
            lock (m_cachedRegionMapItemsAddress)
            {
                if (m_cachedRegionMapItemsAddress.ContainsKey(regionhandle))
                    httpserver = m_cachedRegionMapItemsAddress[regionhandle];
            }
            if (String.IsNullOrEmpty(httpserver))
            {
                RegionInfo mreg = m_scene.SceneGridService.RequestNeighbouringRegionInfo(regionhandle);

                if (mreg != null)
                {
                    httpserver = "http://" + mreg.ExternalHostName + ":" + mreg.HttpPort + "/MAP/MapItems/" + regionhandle.ToString();
                    lock (m_cachedRegionMapItemsAddress)
                    {
                        if (!m_cachedRegionMapItemsAddress.ContainsKey(regionhandle))
                            m_cachedRegionMapItemsAddress.Add(regionhandle, httpserver);
                    }
                }
                else
                {
                    lock (m_blacklistedregions)
                    {
                        if (!m_blacklistedregions.ContainsKey(regionhandle))
                            m_blacklistedregions.Add(regionhandle, Environment.TickCount);
                    }
                    m_log.InfoFormat("[WORLD MAP]: Blacklisted region {0}", regionhandle.ToString());
                }
            }

            blacklisted = false;
            lock (m_blacklistedurls)
            {
                if (m_blacklistedurls.ContainsKey(httpserver))
                    blacklisted = true;
            }

            // Can't find the http server
            if (String.IsNullOrEmpty(httpserver) || blacklisted)
                return new OSDMap();

            MapRequestState mrs = new MapRequestState();
            mrs.agentID = id;
            mrs.EstateID = EstateID;
            mrs.flags = flags;
            mrs.godlike = godlike;
            mrs.itemtype=itemtype;
            mrs.regionhandle = regionhandle;

            lock (m_openRequests)
                m_openRequests.Add(requestID, mrs);

            WebRequest mapitemsrequest = WebRequest.Create(httpserver);
            mapitemsrequest.Method = "POST";
            mapitemsrequest.ContentType = "application/xml+llsd";
            OSDMap RAMap = new OSDMap();

            // string RAMapString = RAMap.ToString();
            OSD LLSDofRAMap = RAMap; // RENAME if this works

            byte[] buffer = OSDParser.SerializeLLSDXmlBytes(LLSDofRAMap);
            OSDMap responseMap = new OSDMap();
            responseMap["requestID"] = OSD.FromUUID(requestID);

            Stream os = null;
            try
            { // send the Post
                mapitemsrequest.ContentLength = buffer.Length;   //Count bytes to send
                os = mapitemsrequest.GetRequestStream();
                os.Write(buffer, 0, buffer.Length);         //Send it
                os.Close();
                //m_log.DebugFormat("[WORLD MAP]: Getting MapItems from Sim {0}", httpserver);
            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[WORLD MAP]: Bad send on GetMapItems {0}", ex.Message);
                responseMap["connect"] = OSD.FromBoolean(false);
                lock (m_blacklistedurls)
                {
                    if (!m_blacklistedurls.ContainsKey(httpserver))
                        m_blacklistedurls.Add(httpserver, Environment.TickCount);
                }

                m_log.WarnFormat("[WORLD MAP]: Blacklisted {0}", httpserver);

                return responseMap;
            }

            string response_mapItems_reply = null;
            { // get the response
                try
                {
                    WebResponse webResponse = mapitemsrequest.GetResponse();
                    if (webResponse != null)
                    {
                        StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                        response_mapItems_reply = sr.ReadToEnd().Trim();
                    }
                    else
                    {
                        return new OSDMap();
                    }
                }
                catch (WebException)
                {
                    responseMap["connect"] = OSD.FromBoolean(false);
                    lock (m_blacklistedurls)
                    {
                        if (!m_blacklistedurls.ContainsKey(httpserver))
                            m_blacklistedurls.Add(httpserver, Environment.TickCount);
                    }

                    m_log.WarnFormat("[WORLD MAP]: Blacklisted {0}", httpserver);

                    return responseMap;
                }
                OSD rezResponse = null;
                try
                {
                    rezResponse = OSDParser.DeserializeLLSDXml(response_mapItems_reply);

                    responseMap = (OSDMap)rezResponse;
                    responseMap["requestID"] = OSD.FromUUID(requestID);
                }
                catch (Exception)
                {
                    //m_log.InfoFormat("[OGP]: exception on parse of rez reply {0}", ex.Message);
                    responseMap["connect"] = OSD.FromBoolean(false);

                    return responseMap;
                }
            }
            return responseMap;
        }

        private void RequestCompleted(IWorkItemResult workItem)
        {
            lock (_currentRequests)
            {
                UUID userId = (UUID)workItem.State;

                IWorkItemResult currentRequest;
                if (_currentRequests.TryGetValue(userId, out currentRequest))
                {
                    if (currentRequest == workItem)
                    {
                        _currentRequests.Remove(userId);
                    }
                }
            }
        }

        /// <summary>
        /// Requests map blocks in area of minX, maxX, minY, MaxY in world cordinates
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public virtual void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            IWorkItemResult newItem = null;

            if ((flag & 0x10000) != 0)  // user clicked on the map a tile that isn't visible
            {
                lock (_currentRequests)
                {
                    CancelCurrentRequestForUser(remoteClient);

                    newItem =
                        _blockRequestPool.QueueWorkItem(
                            delegate(object state)
                            {
                                RequestNonVisibleMapTile(remoteClient, minX, minY, maxX, maxY, flag);
                                return null;
                            },
                            remoteClient.AgentId,
                            this.RequestCompleted
                        );

                    _currentRequests[remoteClient.AgentId] = newItem;
                }
            }
            else
            {
                lock (_currentRequests)
                {
                    // normal mapblock request. Use the provided values
                    CancelCurrentRequestForUser(remoteClient);

                    newItem =
                        _blockRequestPool.QueueWorkItem(
                            delegate(object state)
                            {
                                GetAndSendBlocks(remoteClient, minX, minY, maxX, maxY, flag);
                                return null;
                            },
                            remoteClient.AgentId,
                            this.RequestCompleted
                        );

                    _currentRequests[remoteClient.AgentId] = newItem;
                }
            }
        }

        private void CancelCurrentRequestForUser(IClientAPI remoteClient)
        {
            IWorkItemResult foundResult;
            if (_currentRequests.TryGetValue(remoteClient.AgentId, out foundResult))
            {
                foundResult.Cancel();
            }
        }

        private void RequestNonVisibleMapTile(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            List<MapBlockData> response = new List<MapBlockData>();

            // this should return one mapblock at most. But make sure: Look whether the one we requested is in there
            List<MapBlockData> mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
            if (mapBlocks != null)
            {
                foreach (MapBlockData block in mapBlocks)
                {
                    if (block.X == minX && block.Y == minY)
                    {
                        // found it => add it to response
                        response.Add(block);
                        break;
                    }
                }
            }

            if (response.Count == 0)
            {
                // response still empty => couldn't find the map-tile the user clicked on => tell the client
                MapBlockData block = new MapBlockData();
                block.X = (ushort)minX;
                block.Y = (ushort)minY;
                block.Name = "(NONE)"; // The viewer insists that the region name never be empty.
                block.Access = 255; // == not there (254 is "region down", 255 is "region non-existent")
                response.Add(block);
            }
            //(flag & 0x10000) != 0 is sent by v2 viewers, and it expects flag 2 back
            remoteClient.SendMapBlock(response, flag & 0xffff);
        }

        protected virtual void GetAndSendBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            List<MapBlockData> mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX - 4, minY - 4, maxX + 4, maxY + 4);
            remoteClient.SendMapBlock(mapBlocks, flag);
        }

        /// <summary>
        /// The last time a mapimage was cached
        /// </summary>
        private DateTime _lastImageGenerationTime = DateTime.Now;

        /// <summary>
        /// Number of days to cache a mapimage
        /// </summary>
        private const int MAPIMAGE_CACHE_TIME = 2;

        public Hashtable OnHTTPGetMapImage(Hashtable keysvals)
        {
            bool forceRefresh = false;
            if (keysvals.ContainsKey("requestvars"))
            {
                Hashtable rvars = (Hashtable)keysvals["requestvars"];
                if (rvars.ContainsKey("forcerefresh")) forceRefresh = true;
            }

            if (forceRefresh)
            {
                // Delay/reset the timer as the map's getting updated now.
                mapTileUpdateTimer?.Stop();

                m_log.Debug("[WORLD MAP]: Forcing refresh of map tile");

                try
                {
                    //regenerate terrain
                    m_scene.CreateTerrainTexture(false);
                }
                finally // Make sure the timer actually gets restarted even in an Exceptional situation.
                {
                    // Do the reset after the update so that the above update's time delay cannot cause overlaps.
                    mapTileUpdateTimer?.Start();
                }
            }

            m_log.Debug("[WORLD MAP]: Sending map image jpeg");
            Hashtable reply = new Hashtable();
            int statuscode = 200;
            byte[] jpeg = new byte[0];

            if (myMapImageJPEG.Length == 0 || (DateTime.Now - _lastImageGenerationTime).TotalDays > MAPIMAGE_CACHE_TIME || forceRefresh)
            {
                MemoryStream imgstream = new MemoryStream();
                Bitmap mapTexture = new Bitmap(1,1);
                ManagedImage managedImage;
                Image image = mapTexture;

                try
                {
                    // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                    imgstream = new MemoryStream();

                    // non-async because we know we have the asset immediately.
                    AssetBase mapasset = m_scene.CommsManager.AssetCache.GetAsset(m_scene.RegionInfo.lastMapUUID, 
                        AssetRequestInfo.InternalRequest());

                    // Decode image to System.Drawing.Image
                    if (OpenJPEG.DecodeToImage(mapasset.Data, out managedImage, out image))
                    {
                        // Save to bitmap
                        mapTexture = new Bitmap(image);

                        EncoderParameters myEncoderParameters = new EncoderParameters();
                        myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                        // Save bitmap to stream
                        mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

                        // Write the stream to a byte array for output
                        jpeg = imgstream.ToArray();
                        myMapImageJPEG = jpeg;

                        _lastImageGenerationTime = DateTime.Now;
                    }
                }
                catch (Exception)
                {
                    // Dummy!
                    m_log.Warn("[WORLD MAP]: Unable to generate Map image");
                }
                finally
                {
                    // Reclaim memory, these are unmanaged resources
                    mapTexture.Dispose();
                    image.Dispose();
                    imgstream.Close();
                    imgstream.Dispose();
                }
            }
            else
            {
                // Use cached version so we don't have to loose our mind
                jpeg = myMapImageJPEG;
            }

            reply["str_response_string"] = Convert.ToBase64String(jpeg);
            reply["int_response_code"] = statuscode;
            reply["content_type"] = "image/jpeg";

            return reply;
        }

        // From msdn
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        /// <summary>
        /// Export the world map
        /// </summary>
        /// <param name="fileName"></param>
        public void HandleExportWorldMapConsoleCommand(string module, string[] cmdparams)
        {
            if (m_scene.ConsoleScene() == null)
            {
                // FIXME: If console region is root then this will be printed by every module.  Currently, there is no
                // way to prevent this, short of making the entire module shared (which is complete overkill).
                // One possibility is to return a bool to signal whether the module has completely handled the command
                m_log.InfoFormat("[WORLD MAP]: Please change to a specific region in order to export its world map");
                return;
            }

            if (m_scene.ConsoleScene() != m_scene)
                return;

            string exportPath;

            if (cmdparams.Length > 1)
                exportPath = cmdparams[1];
            else
                exportPath = DEFAULT_WORLD_MAP_EXPORT_PATH;

            m_log.InfoFormat(
                "[WORLD MAP]: Exporting world map for {0} to {1}", m_scene.RegionInfo.RegionName, exportPath);

            List<MapBlockData> mapBlocks =
                m_scene.CommsManager.GridService.RequestNeighbourMapBlocks(
                    (int)(m_scene.RegionInfo.RegionLocX - 9),
                    (int)(m_scene.RegionInfo.RegionLocY - 9),
                    (int)(m_scene.RegionInfo.RegionLocX + 9),
                    (int)(m_scene.RegionInfo.RegionLocY + 9));
            List<AssetBase> textures = new List<AssetBase>();
            List<Image> bitImages = new List<Image>();

            foreach (MapBlockData mapBlock in mapBlocks)
            {
                AssetBase texAsset = m_scene.CommsManager.AssetCache.GetAsset(mapBlock.MapImageId, AssetRequestInfo.InternalRequest());

                if (texAsset != null)
                {
                    textures.Add(texAsset);
                }
                else
                {
                    texAsset = m_scene.CommsManager.AssetCache.GetAsset(mapBlock.MapImageId, AssetRequestInfo.InternalRequest());
                    if (texAsset != null)
                    {
                        textures.Add(texAsset);
                    }
                }
            }

            foreach (AssetBase asset in textures)
            {
                ManagedImage managedImage;
                Image image;

                if (OpenJPEG.DecodeToImage(asset.Data, out managedImage, out image))
                    bitImages.Add(image);
            }

            Bitmap mapTexture = new Bitmap(2560, 2560);
            Graphics g = Graphics.FromImage(mapTexture);
            SolidBrush sea = new SolidBrush(Color.DarkBlue);
            g.FillRectangle(sea, 0, 0, 2560, 2560);

            for (int i = 0; i < mapBlocks.Count; i++)
            {
                ushort x = (ushort)((mapBlocks[i].X - m_scene.RegionInfo.RegionLocX) + 10);
                ushort y = (ushort)((mapBlocks[i].Y - m_scene.RegionInfo.RegionLocY) + 10);
                g.DrawImage(bitImages[i], (x * 128), (y * 128), 128, 128);
            }

            mapTexture.Save(exportPath, ImageFormat.Jpeg);

            m_log.InfoFormat(
                "[WORLD MAP]: Successfully exported world map for {0} to {1}",
                m_scene.RegionInfo.RegionName, exportPath);
        }

        public OSD HandleRemoteMapItemRequest(string path, OSD request, IPEndPoint endpoint)
        {
            uint xstart = 0;
            uint ystart = 0;

            Utils.LongToUInts(m_scene.RegionInfo.RegionHandle,out xstart,out ystart);

            OSDMap responsemap = new OSDMap();
            OSDMap responsemapdata = new OSDMap();
            int tc = Environment.TickCount;

            List<ScenePresence> avatars = m_scene.GetAvatars();
            OSDArray responsearr = new OSDArray(avatars.Count);

            if (avatars.Count == 0)
            {
                responsemapdata = new OSDMap();
                responsemapdata["X"] = OSD.FromInteger((int)(xstart + 1));
                responsemapdata["Y"] = OSD.FromInteger((int)(ystart + 1));
                responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                responsemapdata["Name"] = OSD.FromString(Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()));
                responsemapdata["Extra"] = OSD.FromInteger(0);
                responsemapdata["Extra2"] = OSD.FromInteger(0);
                responsearr.Add(responsemapdata);

                responsemap["6"] = responsearr;
            }
            else
            {
                responsearr = new OSDArray(avatars.Count);
                foreach (ScenePresence av in avatars)
                {
                    Vector3 avpos;
                    if (av.HasSafePosition(out avpos))
                    {
                        responsemapdata = new OSDMap();
                        responsemapdata["X"] = OSD.FromInteger((int)(xstart + avpos.X));
                        responsemapdata["Y"] = OSD.FromInteger((int)(ystart + avpos.Y));
                        responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                        responsemapdata["Name"] = OSD.FromString(Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()));
                        responsemapdata["Extra"] = OSD.FromInteger(1);
                        responsemapdata["Extra2"] = OSD.FromInteger(0);
                        responsearr.Add(responsemapdata);
                    }
                }
                responsemap["6"] = responsearr;
            }
            return responsemap;
        }

        public void LazySaveGeneratedMaptile(byte[] data, bool temporary)
        {
            // Overwrites the local Asset cache with new maptile data
            // Assets are single write, this causes the asset server to ignore this update,
            // but the local asset cache does not

            // this is on purpose!  The net result of this is the region always has the most up to date
            // map tile while protecting the (grid) asset database from bloat caused by a new asset each
            // time a mapimage is generated!

            UUID lastMapRegionUUID = m_scene.RegionInfo.lastMapUUID;

            int lastMapRefresh = 0;
            const int RefreshSeconds = 172800; // 172800 = two days in seconds.

            try
            {
                lastMapRefresh = Convert.ToInt32(m_scene.RegionInfo.lastMapRefresh);
            }
            catch (ArgumentException)
            {
            }
            catch (FormatException)
            {
            }
            catch (OverflowException)
            {
            }

            UUID TerrainImageUUID = UUID.Random();

            if (lastMapRegionUUID == UUID.Zero || (lastMapRefresh + RefreshSeconds) < Util.UnixTimeSinceEpoch())
            {
                m_scene.RegionInfo.SaveLastMapUUID(TerrainImageUUID);
                m_log.Debug("[MAPTILE]: STORING MAPTILE IMAGE");
            }
            else
            {
                TerrainImageUUID = lastMapRegionUUID;
                m_log.Debug("[MAPTILE]: REUSING OLD MAPTILE IMAGE ID");
            }

            m_scene.RegionInfo.RegionSettings.TerrainImageID = TerrainImageUUID;

            AssetBase asset = new AssetBase();
            asset.FullID = m_scene.RegionInfo.RegionSettings.TerrainImageID;
            asset.Data = data;
            asset.Name = "terrainImage_" + m_scene.RegionInfo.RegionID + "_" + lastMapRefresh;
            asset.Description = m_scene.RegionInfo.RegionName;

            asset.Type = 0;
            asset.Temporary = temporary;

            {
                int t = Environment.TickCount;
                try
                {
                    m_scene.CommsManager.AssetCache.AddAsset(asset, AssetRequestInfo.InternalRequest());
                }
                catch (AssetServerException)
                {
                }
                t = Environment.TickCount - t;
                m_log.InfoFormat("[MAPTILE] Attempted save to asset server took {0}ms", t);
            }

            readyToDrawMap = true; // This seems to be the most guaranteed place to detect that the region's got all its peices loaded up and is ready to render a map tile.

            if (regionTileExportFilename.Length > 0 && isMapTainted && lastMapPushTime + minimumMapPushTime < DateTime.Now)
            {
                lastMapPushTime = DateTime.Now;

                MapTileDataForExport exportData;

                exportData.filename = regionTileExportFilename
                    .Replace("{X}", String.Format("{0:D}", m_scene.RegionInfo.RegionLocX))
                    .Replace("{Y}", String.Format("{0:D}", m_scene.RegionInfo.RegionLocY));

                exportData.jpegData = data;

                Util.FireAndForget(ExportMapTileToDisk, exportData); // Just have the disk IO go off and do its thing on its own.
            }
        }

        /// <summary>
        /// Marks the world map as tainted and updates the map tile if enough time has passed.
        /// </summary>
        /// <param name="reason">What is the source of the taint?</param>
        public void MarkMapTileTainted(WorldMapTaintReason reason)
        {
            if (regionTileExportFilename.Length <= 0) // If the map tile export path isn't active, don't even worry about doing the work.
                return;

            if (
                (reason == WorldMapTaintReason.TerrainTextureChange && !terrainTextureCanTaintMapTile)
                ||
                (reason == WorldMapTaintReason.PrimChange && !primsCanTaintMapTile)
                // Elevation can always taint the map.
            )
            {
                return; // These are not set up in the ini file to be able to show, and therefore taint the map.
            }

            isMapTainted = true;

            //m_log.Debug("[WORLD MAP] Map tile tainted."); // Can happen A LOT.

            // Skip if the region isn't ready - aka hasn't finished loading initial objects, or if not enough time has passed since the last push to disk.
            if (readyToDrawMap && lastMapPushTime + minimumMapPushTime < DateTime.Now)
            {
                // Delay/reset the timer as the map's getting updated now.
                mapTileUpdateTimer?.Stop();

                m_log.Info("[WORLD MAP] Rebuilding map tile on taint as the minimum wait time has passed.");

                try
                {
                    // Update the map tile.
                    m_scene.CreateTerrainTexture(false);
                }
                finally // Make sure the timer actually gets restarted even in an Exceptional situation.
                {
                    // Do the reset after the update so that the above update's time delay cannot cause overlaps.
                    mapTileUpdateTimer?.Start();
                }
            }
        }

        /// <summary>
        /// Fired if the Halcyon.ini entry MaximumTaintedMapTileWaitTime is greater than zero
        /// </summary>
        private void HandleTaintedMapTimer(object source, ElapsedEventArgs e)
        {
            if (m_Enabled && isMapTainted && lastMapPushTime + minimumMapPushTime < DateTime.Now)
            {
                // Delay/reset the timer as the map's getting updated now.
                mapTileUpdateTimer?.Stop();

                m_log.Info("[WORLD MAP] Rebuilding map tile; map was tainted and the maximum wait time has expired.");

                try
                {
                    // Update the map tile.
                    m_scene.CreateTerrainTexture(false);
                }
                finally // Make sure the timer actually gets restarted even in an Exceptional situation.
                {
                    // Do the reset after the update so that the above update's time delay cannot cause overlaps.
                    mapTileUpdateTimer?.Start();
                }
            }
        }

        private readonly object ExportMapTileToDiskLock = new object();
        private void ExportMapTileToDisk(object o)
        {
            int t = Environment.TickCount;
            var exportData = (MapTileDataForExport)o;

            // Attempt to get a lock, but if that fails, just abort - another request to update the file will come soon enough.
            if (Monitor.TryEnter(ExportMapTileToDiskLock))
            {
                try
                {
                    ManagedImage managedImage;
                    Image image;

                    if (OpenJPEG.DecodeToImage(exportData.jpegData, out managedImage, out image))
                    {
                        image.Save(exportData.filename, ImageFormat.Jpeg);

                        isMapTainted = false;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[WORLD MAP]: Failed to export map tile to path '{0}': {1}", exportData.filename, e);
                }
                finally
                {
                    Monitor.Exit(ExportMapTileToDiskLock);
                }
            }
            t = Environment.TickCount - t;
            m_log.InfoFormat ("[WORLD MAP] disk write of map tile took {0} ms", t);
        }

        private void MakeRootAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (!m_rootAgents.Contains(avatar.UUID))
                {
                    m_rootAgents.Add(avatar.UUID);
                }
            }
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.Contains(avatar.UUID))
                {
                    m_rootAgents.Remove(avatar.UUID);
                }
            }
        }

    }

    public struct MapRequestState
    {
        public UUID agentID;
        public uint flags;
        public uint EstateID;
        public bool godlike;
        public uint itemtype;
        public ulong regionhandle;
    }
}
