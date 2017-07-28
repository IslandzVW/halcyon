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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Communications.Local;
using System.Threading.Tasks;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1GridServices : IGridServices
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_useRemoteRegionCache = true;
        /// <summary>
        /// Encapsulate local backend services for manipulation of local regions
        /// </summary>
        private LocalBackEndServices m_localBackend = new LocalBackEndServices();

        struct RegionInfoCacheEntry
        {
            public RegionInfo Info;
            public DateTime CachedTime;
            public bool Exists;
        }

        private Dictionary<ulong, RegionInfoCacheEntry> m_remoteRegionInfoCache = new Dictionary<ulong, RegionInfoCacheEntry>();
        private Dictionary<string, string> m_queuedGridSettings = new Dictionary<string, string>();
        private List<RegionInfo> m_regionsOnInstance = new List<RegionInfo>();

        public BaseHttpServer httpListener;
        public NetworkServersInfo serversInfo;
        public BaseHttpServer httpServer;
        
        public string gdebugRegionName
        {
            get { return m_localBackend.gdebugRegionName; }
            set { m_localBackend.gdebugRegionName = value; }
        }  

        public string rdebugRegionName
        {
            get { return _rdebugRegionName; }
            set { _rdebugRegionName = value; }
        }
        private string _rdebugRegionName = String.Empty;
        
        public bool RegionLoginsEnabled
        {
            get { return m_localBackend.RegionLoginsEnabled; }
            set { m_localBackend.RegionLoginsEnabled = value; }
        }      

        /// <summary>
        /// Contructor.  Adds "expect_user" and "check" xmlrpc method handlers
        /// </summary>
        /// <param name="servers_info"></param>
        /// <param name="httpServe"></param>
        public OGS1GridServices(NetworkServersInfo servers_info, BaseHttpServer httpServe)
        {
            serversInfo = servers_info;
            httpServer = httpServe;

            //Respond to Grid Services requests
            httpServer.AddXmlRPCHandler("check", PingCheckReply);
            httpServer.AddXmlRPCHandler("land_data", LandData);

            // New Style
            httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("check"), PingCheckReply));
            httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("land_data"), LandData));
        }

        // see IGridServices
        public RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            if (m_regionsOnInstance.Contains(regionInfo))
            {
                m_log.Debug("[OGS1 GRID SERVICES]: Error - region already registered " + regionInfo.RegionName);
                Exception e = new Exception(String.Format("Unable to register region"));

                throw e;

            }

            m_regionsOnInstance.Add(regionInfo);

            m_log.InfoFormat(
                "[OGS1 GRID SERVICES]: Attempting to register region {0} with grid at {1}",
                regionInfo.RegionName, serversInfo.GridURL);

            Hashtable GridParams = new Hashtable();
            // Login / Authentication

            GridParams["authkey"] = serversInfo.GridSendKey;
            GridParams["recvkey"] = serversInfo.GridRecvKey;
            GridParams["UUID"] = regionInfo.RegionID.ToString();
            GridParams["sim_ip"] = regionInfo.ExternalHostName;
            GridParams["sim_port"] = regionInfo.InternalEndPoint.Port.ToString();
            GridParams["region_locx"] = regionInfo.RegionLocX.ToString();
            GridParams["region_locy"] = regionInfo.RegionLocY.ToString();
            GridParams["sim_name"] = regionInfo.RegionName;
            GridParams["http_port"] = serversInfo.HttpListenerPort.ToString();
            GridParams["remoting_port"] = ConfigSettings.DefaultRegionRemotingPort.ToString();
            GridParams["map-image-id"] = regionInfo.RegionSettings.TerrainImageID.ToString();
            GridParams["originUUID"] = regionInfo.originRegionID.ToString();
            GridParams["region_secret"] = regionInfo.regionSecret;
            GridParams["major_interface_version"] = VersionInfo.MajorInterfaceVersion.ToString();

            if (regionInfo.MasterAvatarAssignedUUID != UUID.Zero)
                GridParams["master_avatar_uuid"] = regionInfo.MasterAvatarAssignedUUID.ToString();
            else
                GridParams["master_avatar_uuid"] = regionInfo.EstateSettings.EstateOwner.ToString();

            GridParams["maturity"] = regionInfo.RegionSettings.Maturity.ToString();
            GridParams["product"] = Convert.ToInt32(regionInfo.Product).ToString();
            if (regionInfo.OutsideIP != null) GridParams["outside_ip"] = regionInfo.OutsideIP;

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            string methodName = "simulator_login";
            XmlRpcRequest GridReq = new XmlRpcRequest(methodName, SendParams);
            XmlRpcResponse GridResp;
            
            try
            {                
                // The timeout should always be significantly larger than the timeout for the grid server to request
                // the initial status of the region before confirming registration.
                GridResp = GridReq.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 90000);
            }
            catch (Exception e)
            {
                Exception e2
                    = new Exception(
                        String.Format(
                            "Unable to register region with grid at {0}. Grid service not running?", 
                            serversInfo.GridURL),
                        e);

                throw e2;
            }

            Hashtable GridRespData = (Hashtable)GridResp.Value;
            // Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string) GridRespData["error"];

                Exception e = new Exception(
                    String.Format("Unable to connect to grid at {0}: {1}", serversInfo.GridURL, errorstring));

                throw e;
            }
            else
            {
                // m_knownRegions = RequestNeighbours(regionInfo.RegionLocX, regionInfo.RegionLocY);
                if (GridRespData.ContainsKey("allow_forceful_banlines"))
                {
                    if ((string) GridRespData["allow_forceful_banlines"] != "TRUE")
                    {
                        //m_localBackend.SetForcefulBanlistsDisallowed(regionInfo.RegionHandle);
                        if (!m_queuedGridSettings.ContainsKey("allow_forceful_banlines"))
                            m_queuedGridSettings.Add("allow_forceful_banlines", "FALSE");
                    }
                }

                m_log.InfoFormat(
                    "[OGS1 GRID SERVICES]: Region {0} successfully registered with grid at {1}",
                    regionInfo.RegionName, serversInfo.GridURL);
            }
            
            return m_localBackend.RegisterRegion(regionInfo);
        }

        // see IGridServices
        public bool DeregisterRegion(RegionInfo regionInfo)
        {
            Hashtable GridParams = new Hashtable();

            GridParams["UUID"] = regionInfo.RegionID.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            string methodName = "simulator_after_region_moved";
            XmlRpcRequest GridReq = new XmlRpcRequest(methodName, SendParams);
            XmlRpcResponse GridResp = null;
            
            try
            {            
                GridResp = GridReq.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 10000);
            }
            catch (Exception e)
            {
                Exception e2
                    = new Exception(
                        String.Format(
                            "Unable to deregister region with grid at {0}. Grid service not running?", 
                            serversInfo.GridURL),
                        e);

                throw e2;
            }
        
            Hashtable GridRespData = (Hashtable) GridResp.Value;

            // Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData != null && GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];
                m_log.Error("Unable to connect to grid: " + errorstring);
                return false;
            }

            return m_localBackend.DeregisterRegion(regionInfo);
        }

        public virtual Dictionary<string, string> GetGridSettings()
        {
            Dictionary<string, string> returnGridSettings = new Dictionary<string, string>();
            lock (m_queuedGridSettings)
            {
                foreach (string Dictkey in m_queuedGridSettings.Keys)
                {
                    returnGridSettings.Add(Dictkey, m_queuedGridSettings[Dictkey]);
                }

                m_queuedGridSettings.Clear();
            }

            return returnGridSettings;
        }

        // see IGridServices
        public List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            Hashtable respData = MapBlockQuery((int) x - 1, (int) y - 1, (int) x + 1, (int) y + 1);

            return ExtractRegionInfoFromMapBlockQuery(x, y, respData);
        }

        private static List<SimpleRegionInfo> ExtractRegionInfoFromMapBlockQuery(uint x, uint y, Hashtable respData)
        {
            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();

            foreach (ArrayList neighboursList in respData.Values)
            {
                foreach (Hashtable neighbourData in neighboursList)
                {
                    uint regX = Convert.ToUInt32(neighbourData["x"]);
                    uint regY = Convert.ToUInt32(neighbourData["y"]);
                    if ((x != regX) || (y != regY))
                    {
                        string simIp = (string)neighbourData["sim_ip"];
                        uint port = Convert.ToUInt32(neighbourData["sim_port"]);


                        SimpleRegionInfo sri = new SimpleRegionInfo(regX, regY, simIp, port);
                        sri.RegionID = new UUID((string)neighbourData["uuid"]);
                        sri.RemotingPort = Convert.ToUInt32(neighbourData["remoting_port"]);

                        if (neighbourData.ContainsKey("http_port"))
                        {
                            sri.HttpPort = Convert.ToUInt32(neighbourData["http_port"]);
                        }

                        if (neighbourData.ContainsKey("outside_ip"))
                        {
                            sri.OutsideIP = (string)neighbourData["outside_ip"];
                        }

                        neighbours.Add(sri);
                    }
                }
            }

            return neighbours;
        }

        // More efficient call to see if there is a neighbour there, than fetching all neighbours and DNS lookups
        // Return true if region at (x,y) has (nx,ny) as a neighbour.
        public bool HasNeighbour(uint x, uint y, uint nx, uint ny)
        {
            Hashtable respData = MapBlockQuery((int)x - 1, (int)y - 1, (int)x + 1, (int)y + 1);

            foreach (ArrayList neighboursList in respData.Values)
            {
                foreach (Hashtable neighbourData in neighboursList)
                {
                    uint regX = Convert.ToUInt32(neighbourData["x"]);
                    uint regY = Convert.ToUInt32(neighbourData["y"]);
                    if ((nx == regX) && (ny == regY))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Request information about a region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns>
        /// null on a failure to contact or get a response from the grid server
        /// FIXME: Might be nicer to return a proper exception here since we could inform the client more about the
        /// nature of the faiulre.
        /// </returns>
        public RegionInfo RequestNeighbourInfo(UUID Region_UUID)
        {
            // don't ask the gridserver about regions on this instance...
            foreach (RegionInfo info in m_regionsOnInstance)
            {
                if (info.RegionID == Region_UUID) return info;
            }

            // didn't find it so far, we have to go the long way
            RegionInfo regionInfo;
            Hashtable requestData = new Hashtable();
            requestData["region_UUID"] = Region_UUID.ToString();
            requestData["authkey"] = serversInfo.GridSendKey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);

            string methodName = "simulator_data_request";
            XmlRpcRequest gridReq = new XmlRpcRequest(methodName, SendParams);
            XmlRpcResponse gridResp = null;

            try
            {
                gridResp = gridReq.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 3000);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[OGS1 GRID SERVICES]: Communication with the grid server at {0} failed, {1}",
                    serversInfo.GridURL, e);

                return null;
            }

            Hashtable responseData = (Hashtable)gridResp.Value;

            if (responseData.ContainsKey("error"))
            {
// this happens all the time normally and pollutes the log 
//                m_log.WarnFormat("[OGS1 GRID SERVICES]: Error received from grid server: {0}", responseData["error"]);

                if (m_useRemoteRegionCache)
                {
                    RegionInfoCacheEntry cacheEntry = new RegionInfoCacheEntry
                    {
                        CachedTime = DateTime.Now,
                        Info = null,
                        Exists = false
                    };

                    lock (m_remoteRegionInfoCache)
                    {
                        m_remoteRegionInfoCache[Convert.ToUInt64((string)requestData["regionHandle"])] = cacheEntry;
                    }
                }
                return null;
            }

            regionInfo = buildRegionInfo(responseData, String.Empty);
            if ((m_useRemoteRegionCache) && (requestData.ContainsKey("regionHandle")))
            {
                RegionInfoCacheEntry cacheEntry = new RegionInfoCacheEntry
                {
                    CachedTime = DateTime.Now,
                    Info = regionInfo,
                    Exists = true
                };

                lock (m_remoteRegionInfoCache)
                {
                    m_remoteRegionInfoCache[Convert.ToUInt64((string)requestData["regionHandle"])] = cacheEntry;
                }
            }

            return regionInfo;
        }

        /// <summary>
        /// Request information about a region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            RegionInfo regionInfo = m_localBackend.RequestNeighbourInfo(regionHandle);

            if (regionInfo != null)
            {
                return regionInfo;
            }

            if (m_useRemoteRegionCache)
            {
                lock (m_remoteRegionInfoCache)
                {
                    RegionInfoCacheEntry entry;
                    if (m_remoteRegionInfoCache.TryGetValue(regionHandle, out entry))
                    {
                        if (DateTime.Now - entry.CachedTime < TimeSpan.FromMinutes(15.0))
                        {
                            if (entry.Exists)
                            {
                                return entry.Info;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            m_remoteRegionInfoCache.Remove(regionHandle);
                        }
                    }
                }
            }

            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_handle"] = regionHandle.ToString();
                requestData["authkey"] = serversInfo.GridSendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);

                string methodName = "simulator_data_request";
                XmlRpcRequest GridReq = new XmlRpcRequest(methodName, SendParams);
                XmlRpcResponse GridResp = GridReq.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 3000);

                Hashtable responseData = (Hashtable) GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    m_log.Error("[OGS1 GRID SERVICES]: Error received from grid server: " + responseData["error"]);

                    if (m_useRemoteRegionCache)
                    {
                        lock (m_remoteRegionInfoCache)
                        {
                            if (!m_remoteRegionInfoCache.ContainsKey(regionHandle))
                            {
                                RegionInfoCacheEntry entry = new RegionInfoCacheEntry
                                {
                                    CachedTime = DateTime.Now,
                                    Info = null,
                                    Exists = false
                                };

                                m_remoteRegionInfoCache[regionHandle] = entry;
                            }
                        }
                    }

                    return null;
                }

                uint regX = Convert.ToUInt32((string) responseData["region_locx"]);
                uint regY = Convert.ToUInt32((string) responseData["region_locy"]);
                string externalHostName = (string) responseData["sim_ip"];
                uint simPort = Convert.ToUInt32(responseData["sim_port"]);
                string regionName = (string)responseData["region_name"];
                UUID regionID = new UUID((string)responseData["region_UUID"]);
                uint remotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                    
                uint httpPort = 9000;
                if (responseData.ContainsKey("http_port"))
                {
                    httpPort = Convert.ToUInt32((string)responseData["http_port"]);
                }

                string outsideIp = null;
                if (responseData.ContainsKey("outside_ip"))
                    outsideIp = (string)responseData["outside_ip"];

                //IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int) port);
                regionInfo = RegionInfo.Create(regionID, regionName, regX, regY, externalHostName, httpPort, simPort, remotingPort, outsideIp);

                if (requestData.ContainsKey("product"))
                    regionInfo.Product = (ProductRulesUse)Convert.ToInt32(requestData["product"]);
                
                if (m_useRemoteRegionCache)
                {
                    lock (m_remoteRegionInfoCache)
                    {
                        if (!m_remoteRegionInfoCache.ContainsKey(regionHandle))
                        {
                            RegionInfoCacheEntry entry = new RegionInfoCacheEntry
                            {
                                CachedTime = DateTime.Now,
                                Info = regionInfo, 
                                Exists = true
                            };

                            m_remoteRegionInfoCache[regionHandle] = entry;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: " +
                            "Region lookup failed for: " + regionHandle.ToString() +
                            " - Is the GridServer down?" + e.ToString());
                return null;
            }
            

            return regionInfo;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(string name)
        {
            // Not implemented yet
            return null;
        }

        public RegionInfo RequestClosestRegion(string regionName)
        {
            if (m_useRemoteRegionCache)
            {
                lock (m_remoteRegionInfoCache)
                {
                    foreach (RegionInfoCacheEntry ri in m_remoteRegionInfoCache.Values)
                    {
                        if (ri.Exists && ri.Info != null)
                            if (ri.Info.RegionName == regionName)
                                return ri.Info; // .Info is not valid if Exists is false
                    }
                }
            }

            RegionInfo regionInfo = null;
            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_name_search"] = regionName;
                requestData["authkey"] = serversInfo.GridSendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);

                string methodName = "simulator_data_request";
                XmlRpcRequest GridReq = new XmlRpcRequest(methodName, SendParams);
                XmlRpcResponse GridResp = GridReq.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 3000);

                Hashtable responseData = (Hashtable) GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    m_log.ErrorFormat("[OGS1 GRID SERVICES]: Error received from grid server: ", responseData["error"]);
#if NEEDS_REPLACEMENT
                    lock (m_remoteRegionInfoCache)
                    {
                        m_remoteRegionInfoCache[regionInfo.RegionHandle] = new RegionInfoCacheEntry
                        {
                            CachedTime = DateTime.Now,
                            Info = null,
                            Exists = false
                        };
                    }
#endif

                    return null;
                }

                regionInfo = buildRegionInfo(responseData, String.Empty);

                if (m_useRemoteRegionCache)
                {
                    lock (m_remoteRegionInfoCache)
                    {
                        m_remoteRegionInfoCache[regionInfo.RegionHandle] = new RegionInfoCacheEntry {
                            CachedTime = DateTime.Now,
                            Exists = true,
                            Info = regionInfo
                        };
                    }
                }
            }
            catch
            {
                m_log.Error("[OGS1 GRID SERVICES]: " +
                            "Region lookup failed for: " + regionName +
                            " - Is the GridServer down?");
            }

            return regionInfo;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            int temp = 0;

            if (minX > maxX)
            {
                temp = minX;
                minX = maxX;
                maxX = temp;
            }
            if (minY > maxY)
            {
                temp = minY;
                minY = maxY;
                maxY = temp;
            }

            Hashtable respData = MapBlockQuery(minX, minY, maxX, maxY);

            List<MapBlockData> neighbours = new List<MapBlockData>();

            foreach (ArrayList a in respData.Values)
            {
                foreach (Hashtable n in a)
                {
                    MapBlockData neighbour = new MapBlockData();

                    neighbour.X = Convert.ToUInt16(n["x"]);
                    neighbour.Y = Convert.ToUInt16(n["y"]);

                    neighbour.Name = (string) n["name"];
                    neighbour.Access = Convert.ToByte(n["access"]);
                    neighbour.RegionFlags = Convert.ToUInt32(n["region-flags"]);
                    neighbour.WaterHeight = Convert.ToByte(n["water-height"]);
                    neighbour.MapImageId = new UUID((string) n["map-image-id"]);

                    neighbours.Add(neighbour);
                }
            }

            return neighbours;
        }

        /// <summary>
        /// Performs a XML-RPC query against the grid server returning mapblock information in the specified coordinates
        /// </summary>
        /// <param name="minX">Minimum X value</param>
        /// <param name="minY">Minimum Y value</param>
        /// <param name="maxX">Maximum X value</param>
        /// <param name="maxY">Maximum Y value</param>
        /// <returns>Hashtable of hashtables containing map data elements</returns>
        private Hashtable MapBlockQuery(int minX, int minY, int maxX, int maxY)
        {
            Hashtable param = new Hashtable();
            param["xmin"] = minX;
            param["ymin"] = minY;
            param["xmax"] = maxX;
            param["ymax"] = maxY;
            IList parameters = new ArrayList();
            parameters.Add(param);
            
            try
            {
                string methodName = "map_block";
                XmlRpcRequest req = new XmlRpcRequest(methodName, parameters);
                XmlRpcResponse resp = req.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 10000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null && respData.Contains("faultCode"))
                {
                    m_log.ErrorFormat("[OGS1 GRID SERVICES]: Got an error while contacting GridServer: {0}", respData["faultString"]);
                    return null;
                }

                return respData;
            }
            catch (Exception e)
            {
                m_log.Error("MapBlockQuery XMLRPC failure: " + e);
                return new Hashtable();
            }
        }

        /// <summary>
        /// A ping / version check
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse PingCheckReply(XmlRpcRequest request,IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable respData = new Hashtable();
            respData["online"] = "true";

            m_localBackend.PingCheckReply(respData);

            response.Value = respData;

            return response;
        }

        /// <summary>
        /// Received from the user server when a user starts logging in.  This call allows
        /// the region to prepare for direct communication from the client.  Sends back an empty
        /// xmlrpc response on completion.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {            
            Hashtable requestData = (Hashtable) request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new UUID((string) requestData["session_id"]);
            agentData.SecureSessionID = new UUID((string) requestData["secure_session_id"]);
            agentData.FirstName = (string) requestData["firstname"];
            agentData.LastName = (string) requestData["lastname"];
            agentData.AgentID = new UUID((string) requestData["agent_id"]);
            agentData.CircuitCode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string)requestData["caps_path"];
            ulong regionHandle = Convert.ToUInt64((string) requestData["regionhandle"]);

            // Appearance
            if (requestData.ContainsKey("appearance"))
                agentData.Appearance = new AvatarAppearance((Hashtable)requestData["appearance"]);

            m_log.DebugFormat(
                "[CLIENT]: Told by user service to prepare for a connection from {0} {1} {2}, circuit {3}",
                agentData.FirstName, agentData.LastName, agentData.AgentID, agentData.CircuitCode);            

            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                //m_log.Debug("[CLIENT]: Child agent detected");
                agentData.child = true;
            }
            else
            {
                //m_log.Debug("[CLIENT]: Main agent detected");
                agentData.startpos =
                    new Vector3((float)Convert.ToDouble((string)requestData["startpos_x"]),
                                  (float)Convert.ToDouble((string)requestData["startpos_y"]),
                                  (float)Convert.ToDouble((string)requestData["startpos_z"]));
                agentData.child = false;
            }

            XmlRpcResponse resp = new XmlRpcResponse();
                        
            if (!RegionLoginsEnabled)
            {
                m_log.InfoFormat(
                    "[CLIENT]: Denying access for user {0} {1} because region login is currently disabled",
                    agentData.FirstName, agentData.LastName);

                Hashtable respdata = new Hashtable();
                respdata["success"] = "FALSE";
                respdata["reason"] = "region login currently disabled";
                resp.Value = respdata;                
            }
            else
            {
                RegionInfo[] regions = m_regionsOnInstance.ToArray();
                bool banned = false;

                for (int i = 0; i < regions.Length; i++)
                {
                    if (regions[i] != null)
                    {
                        if (regions[i].RegionHandle == regionHandle)
                        {
                            if (regions[i].EstateSettings.IsBanned(agentData.AgentID))
                            {
                                banned = true;
                                break;
                            }
                        }
                    }
                }                
            
                if (banned)
                {
                    m_log.InfoFormat(
                        "[CLIENT]: Denying access for user {0} {1} because user is banned",
                        agentData.FirstName, agentData.LastName);

                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "FALSE";
                    respdata["reason"] = "banned";
                    resp.Value = respdata;
                }
                else
                {
                    m_localBackend.TriggerExpectUser(regionHandle, agentData);
                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "TRUE";
                    resp.Value = respdata;
                }
            }

            return resp;
        }
        
        // Grid Request Processing
        /// <summary>
        /// Ooops, our Agent must be dead if we're getting this request!
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LogOffUser(XmlRpcRequest request)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: LogOff User Called");
            
            Hashtable requestData = (Hashtable)request.Params[0];
            string message = (string)requestData["message"];
            UUID agentID = UUID.Zero;
            UUID RegionSecret = UUID.Zero;
            UUID.TryParse((string)requestData["agent_id"], out agentID);
            UUID.TryParse((string)requestData["region_secret"], out RegionSecret);

            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);

            m_localBackend.TriggerLogOffUser(regionHandle, agentID, RegionSecret,message);

            return new XmlRpcResponse();
        }

        private LandData RequestLandData(ulong regionHandle, uint x, uint y, int localLandID)
        {
            LandData landData = null;

            Hashtable hash = new Hashtable();
            hash["region_handle"] = regionHandle.ToString();
            hash["x"] = x.ToString();
            hash["y"] = y.ToString();
            hash["land_id"] = localLandID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            try
            {
                // this might be cached, as we probably requested it just a moment ago...
                RegionInfo info = RequestNeighbourInfo(regionHandle);
                if (info != null) // just to be sure
                {
                    string methodName = "land_data";
                    XmlRpcRequest request = new XmlRpcRequest(methodName, paramList);

                    string uri = "http://" + info.ExternalHostName + ":" + info.HttpPort + "/";
                    XmlRpcResponse response = request.Send(Util.XmlRpcRequestURI(uri, methodName), 10000);

                    if (response.IsFault)
                    {
                        m_log.ErrorFormat("[OGS1 GRID SERVICES]: remote call returned an error: {0}", response.FaultString);
                    }
                    else
                    {
                        hash = (Hashtable)response.Value;
                        try
                        {
                            landData = new LandData();
                            landData.AABBMax = Vector3.Parse((string)hash["AABBMax"]);
                            landData.AABBMin = Vector3.Parse((string)hash["AABBMin"]);
                            landData.Area = Convert.ToInt32(hash["Area"]);
                            landData.AuctionID = Convert.ToUInt32(hash["AuctionID"]);
                            landData.Description = (string)hash["Description"];
                            landData.Flags = Convert.ToUInt32(hash["Flags"]);
                            landData.GlobalID = new UUID((string)hash["GlobalID"]);
                            landData.Name = (string)hash["Name"];
                            landData.OwnerID = new UUID((string)hash["OwnerID"]);
                            landData.SalePrice = Convert.ToInt32(hash["SalePrice"]);
                            landData.SnapshotID = new UUID((string)hash["SnapshotID"]);
                            landData.UserLocation = Vector3.Parse((string)hash["UserLocation"]);
                            // LandingType and UserLookAt were not included in the hash data prior to R1670.
                            // LandingType 0 means blocked, so where communicating with an older server, return 2 (anywhere).
                            landData.LandingType = hash.ContainsKey("LandingType") ? Convert.ToByte(hash["LandingType"]) : (byte)2;
                            landData.UserLookAt = hash.ContainsKey("UserLookAt") ? Vector3.Parse((string)hash["UserLookAt"]) : Vector3.Zero;
                            m_log.DebugFormat("[OGS1 GRID SERVICES]: Got land data for parcel {0}", landData.Name);
                        }
                        catch (Exception e)
                        {
                            m_log.Error("[OGS1 GRID SERVICES]: Got exception while parsing land-data:", e);
                            landData = null;
                        }
                    }
                }
                else m_log.WarnFormat("[OGS1 GRID SERVICES]: Couldn't find region with handle {0}", regionHandle);
            }
            catch (WebException)
            {
                m_log.WarnFormat("[OGS1 GRID SERVICES]: Couldn't contact region {0}: Region may be down.", regionHandle);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[OGS1 GRID SERVICES]: Couldn't contact region {0}: {1}", regionHandle, e);
            }
            return landData;
        }

        public LandData RequestLandData(ulong regionHandle, uint x, uint y)
        {
//            m_log.DebugFormat("[OGS1 GRID SERVICES]: requests land data in {0}, at {1}, {2}", regionHandle, x, y);
            LandData landData = m_localBackend.RequestLandData(regionHandle, x, y);
            if (landData == null)
                landData = RequestLandData(regionHandle, x, y, -1);

            return landData;
        }

        public LandData RequestLandData(ulong regionHandle, int localLandID)
        {
//            m_log.DebugFormat("[OGS1 GRID SERVICES]: requests land data in {0}, with ID {1}", regionHandle, localLandID);
            LandData landData = m_localBackend.RequestLandData(regionHandle, localLandID);
            if (landData == null)
                landData = RequestLandData(regionHandle, 128, 128, localLandID);

            return landData;
        }

        // Grid Request Processing
        /// <summary>
        /// Someone asked us about parcel-information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LandData(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            LandData landData = null;
            Hashtable requestData = (Hashtable)request.Params[0];
            ulong regionHandle = Convert.ToUInt64(requestData["region_handle"]);
            uint x = Convert.ToUInt32(requestData["x"]);
            uint y = Convert.ToUInt32(requestData["y"]);

            int localLandID = -1;
            if (requestData.ContainsKey("land_id"))
            {
                localLandID = Convert.ToInt32(requestData["land_id"]);
            }

            if (localLandID == -1)
            {
                // look up by x/y coordinates
                m_log.DebugFormat("[OGS1 GRID SERVICES]: Got XML request for land data at {0}, {1} in region {2}", x, y, regionHandle);
                landData = m_localBackend.RequestLandData(regionHandle, x, y);
            }
            else
            {
                // look up by local land ID
                m_log.DebugFormat("[OGS1 GRID SERVICES]: Got XML request for land data with ID {0} in region {1}", localLandID, regionHandle);
                landData = m_localBackend.RequestLandData(regionHandle, localLandID);
            }

            Hashtable hash = new Hashtable();
            if (landData != null)
            {
                // for now, only push out the data we need for answering a ParcelInfoReqeust
                hash["AABBMax"] = landData.AABBMax.ToString();
                hash["AABBMin"] = landData.AABBMin.ToString();
                hash["Area"] = landData.Area.ToString();
                hash["AuctionID"] = landData.AuctionID.ToString();
                hash["Description"] = landData.Description;
                hash["Flags"] = landData.Flags.ToString();
                hash["GlobalID"] = landData.GlobalID.ToString();
                hash["LandingType"] = landData.LandingType.ToString();
                hash["Name"] = landData.Name;
                hash["OwnerID"] = landData.OwnerID.ToString();
                hash["SalePrice"] = landData.SalePrice.ToString();
                hash["SnapshotID"] = landData.SnapshotID.ToString();
                hash["UserLocation"] = landData.UserLocation.ToString();
                hash["UserLookAt"] = landData.UserLookAt.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        public List<RegionInfo> RequestNamedRegions (string name, int maxNumber)
        {
            // no asking of the local backend first, here, as we have to ask the gridserver anyway.
            Hashtable hash = new Hashtable();
            hash["name"] = name;
            hash["maxNumber"] = maxNumber.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            Hashtable result = XmlRpcSearchForRegionByName(paramList);
            if (result == null) return null;

            uint numberFound = Convert.ToUInt32(result["numFound"]);
            List<RegionInfo> infos = new List<RegionInfo>();
            for (int i = 0; i < numberFound; ++i)
            {
                string prefix = "region" + i + ".";
                RegionInfo info = buildRegionInfo(result, prefix);
                infos.Add(info);
            }
            return infos;
        }

        private RegionInfo buildRegionInfo(Hashtable responseData, string prefix)
        {
            uint regX = Convert.ToUInt32((string) responseData[prefix + "region_locx"]);
            uint regY = Convert.ToUInt32((string) responseData[prefix + "region_locy"]);
            string internalIpStr = (string) responseData[prefix + "sim_ip"];
            uint port = Convert.ToUInt32(responseData[prefix + "sim_port"]);

            IPEndPoint neighbourInternalEndPoint = new IPEndPoint(Util.GetHostFromDNS(internalIpStr), (int) port);

            RegionInfo regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);
            regionInfo.RemotingPort = Convert.ToUInt32((string) responseData[prefix + "remoting_port"]);
            regionInfo.RemotingAddress = internalIpStr;

            if (responseData.ContainsKey(prefix + "http_port"))
            {
                regionInfo.HttpPort = Convert.ToUInt32((string) responseData[prefix + "http_port"]);
            }

            regionInfo.RegionID = new UUID((string) responseData[prefix + "region_UUID"]);
            regionInfo.RegionName = (string) responseData[prefix + "region_name"];

            regionInfo.RegionSettings.TerrainImageID = new UUID((string) responseData[prefix + "map_UUID"]);
            return regionInfo;
        }

        private Hashtable XmlRpcSearchForRegionByName(IList parameters)
        {
            try
            {
                string methodName = "search_for_region_by_name";
                XmlRpcRequest request = new XmlRpcRequest(methodName, parameters);
                XmlRpcResponse resp = request.Send(Util.XmlRpcRequestURI(serversInfo.GridURL, methodName), 10000);

                Hashtable respData = (Hashtable) resp.Value;
                if (respData != null && respData.Contains("faultCode"))
                {
                    m_log.WarnFormat("[OGS1 GRID SERVICES]: Got an error while contacting GridServer: {0}", respData["faultString"]);
                    return null;
                }

                return respData;
            }
            catch (Exception e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: MapBlockQuery XMLRPC failure: ", e);
                return null;
            }
        }


        public System.Threading.Tasks.Task<List<SimpleRegionInfo>> RequestNeighbors2Async(uint x, uint y, int maxDD)
        {
            Task<List<SimpleRegionInfo>> requestTask = new Task<List<SimpleRegionInfo>>(() =>
                {
                    uint xmin, xmax, ymin, ymax;
                    Util.GetDrawDistanceBasedRegionRectangle((uint)maxDD, 0, x, y, out xmin, out xmax, out ymin, out ymax);

                    Hashtable block = MapBlockQuery((int)xmin, (int)ymin, (int)xmax, (int)ymax);
                    if (block == null)
                        return new List<SimpleRegionInfo>();

                    return ExtractRegionInfoFromMapBlockQuery(x, y, block);
                }
            );

            requestTask.Start();

            return requestTask;
        }
    }
}
