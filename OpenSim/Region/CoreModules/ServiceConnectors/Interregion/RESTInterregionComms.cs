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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Messages;
using OpenSim.Framework.Communications.Clients;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using System.Threading.Tasks;

namespace OpenSim.Region.CoreModules.ServiceConnectors.Interregion
{
    public class RESTInterregionComms : IRegionModule, IInterregionCommsOut
    {
        private bool initialized = false;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_enabled = false;
        protected Scene m_aScene;
        // RESTInterregionComms does not care about local regions; it delegates that to the Local module
        protected LocalInterregionComms m_localBackend;

        protected CommunicationsManager m_commsManager;

        protected RegionToRegionClient m_regionClient;

        protected IPAddress m_thisIP;

        private string _gridSendKey;

        #region IRegionModule

        public void Initialize_Unittest(Scene scene)
        {
            _gridSendKey = "key";
            InitOnce(scene);
        }


        public virtual void Initialize(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;

                IConfig netConfig = config.Configs["Network"];

                if (netConfig != null)
                {
                    _gridSendKey = netConfig.GetString("grid_send_key");
                }


                IConfig startupConfig = config.Configs["Communications"];
                if (startupConfig != null && startupConfig.GetString("InterregionComms", "RESTComms") == "RESTComms")
                {
                    m_log.Info("[REST COMMS]: Enabling InterregionComms RESTComms module");
                    m_enabled = true;

                    InitOnce(scene);
                }
            }

            if (!m_enabled)
                return;

            InitEach(scene);

        }

        public virtual void PostInitialize()
        {
            if (m_enabled)
                AddHTTPHandlers();
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "RESTInterregionCommsModule"; }
        }

        public virtual bool IsSharedModule
        {
            get { return true; }
        }

        protected virtual void InitEach(Scene scene)
        {
            m_localBackend.Init(scene);
            scene.RegisterModuleInterface<IInterregionCommsOut>(this);
        }

        protected virtual void InitOnce(Scene scene)
        {
            m_localBackend = new LocalInterregionComms();
            m_commsManager = scene.CommsManager;
            m_aScene = scene;
            m_regionClient = new RegionToRegionClient(m_aScene, _gridSendKey);
            m_thisIP = Util.GetHostFromDNS(scene.RegionInfo.ExternalHostName);
        }

        protected virtual void AddHTTPHandlers()
        {
            m_aScene.CommsManager.HttpServer.AddHTTPHandler("/agent/", AgentHandler);
            m_aScene.CommsManager.HttpServer.AddHTTPHandler("/object/", ObjectHandler);

            //new handlers for the Thoosa/protobuf creation messages
            m_aScene.CommsManager.HttpServer.AddStreamHandler(new BinaryStreamHandler("POST", "/object2/", DoObject2Post));
            m_aScene.CommsManager.HttpServer.AddStreamHandler(new BinaryStreamHandler("DELETE", "/object2/", DoObject2Delete));
            m_aScene.CommsManager.HttpServer.AddStreamHandler(new BinaryStreamHandler("PUT", "/agent2/", DoAgent2Put));
            m_aScene.CommsManager.HttpServer.AddStreamHandler(new BinaryStreamHandler("GET", "/agent2/", DoWaitScenePresence));
        }

        #endregion /* IRegionModule */

        #region IInterregionComms

        /**
         * Agent-related communications 
         */

        public bool SendCreateChildAgent(ulong regionHandle, AgentCircuitData aCircuit, bool authorize, out string reason)
        {
            int createChildStart = Environment.TickCount;
            // authorize is always false from here.  Anyone calling the REST version (not local version) directly is doing it for a child agent.
            authorize = false;

            try
            {
                // Try local first
                if (m_localBackend.SendCreateChildAgent(regionHandle, aCircuit, authorize, out reason))
                    return true;

                // else do the remote thing
                if (!m_localBackend.IsLocalRegion(regionHandle))
                {
                    RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        return m_regionClient.DoCreateChildAgentCall(regInfo, aCircuit, "None", out reason);
                    }
                    else
                    {
                        reason = "Region not found";
                        m_log.Warn("[REST COMMS]: Region not found " + regionHandle.ToString());
                    }
                }
                return false;
            }
            finally
            {
                m_log.DebugFormat("[REST COMM] SendCreateChildAgent: {0} ms", Environment.TickCount - createChildStart);
            }
        }

        public async Task<Tuple<bool, string>> SendCreateRemoteChildAgentAsync(SimpleRegionInfo regionInfo, AgentCircuitData aCircuit)
        {
            if (regionInfo == null)
                throw new ArgumentNullException("regionInfo cannot be null");

            return await m_regionClient.DoCreateChildAgentCallAsync(regionInfo, aCircuit);
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentData cAgentData)
        {
            int childAgentUpdateStart = Environment.TickCount;
            try
            {
                // Try local first
                if (m_localBackend.SendChildAgentUpdate(regionHandle, cAgentData))
                    return true;

                // else do the remote thing
                if (!m_localBackend.IsLocalRegion(regionHandle))
                {
                    RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        return m_regionClient.DoChildAgentUpdateCall(regInfo, cAgentData);
                    }
                    //else
                    //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
                }
                return false;
            }
            finally
            {
                m_log.DebugFormat("[REST COMM] SendCreateChildAgent: {0} ms", Environment.TickCount - childAgentUpdateStart);
            }
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentPosition cAgentData)
        {
            // Try local first
            if (m_localBackend.SendChildAgentUpdate(regionHandle, cAgentData))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    return m_regionClient.DoChildAgentUpdateCall(regInfo, cAgentData);
                }
                else
                    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;

        }

        public bool SendRetrieveRootAgent(ulong regionHandle, UUID id, out IAgentData agent)
        {
            // Try local first
            if (m_localBackend.SendRetrieveRootAgent(regionHandle, id, out agent))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    return m_regionClient.DoRetrieveRootAgentCall(regInfo, id, out agent);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;

        }

        public bool SendReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            // Try local first
            if (m_localBackend.SendReleaseAgent(regionHandle, id, uri))
                return true;

            // else do the remote thing
            return m_regionClient.DoReleaseAgentCall(regionHandle, id, uri);
        }


        public bool SendCloseAgent(ulong regionHandle, UUID id)
        {
            // Try local first
            if (m_localBackend.SendCloseAgent(regionHandle, id))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    return m_regionClient.DoCloseAgentCall(regInfo, id);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;
        }

        /**
         * Object-related communications 
         */
        static long m_nonceID = 0;
        private long NextNonceID()
        {
            if (m_nonceID == 0)
            {
                var buffer = new byte[sizeof(Int64)];
                Util.RandomClass.NextBytes(buffer);
                m_nonceID = BitConverter.ToInt64(buffer, 0);
            }

            return System.Threading.Interlocked.Increment(ref m_nonceID);
        }

        public bool SendCreateObject(ulong regionHandle, SceneObjectGroup sog, List<UUID> avatars, bool isLocalCall, Vector3 posInOtherRegion,
            bool isAttachment)
        {
            int createObjectStart = Environment.TickCount;

            try
            {
                // Try local first
                if (m_localBackend.SendCreateObject(regionHandle, sog, avatars, true, posInOtherRegion, isAttachment))
                {
                    //m_log.Debug("[REST COMMS]: LocalBackEnd SendCreateObject succeeded");
                    return true;
                }

                // else do the remote thing
                if (!m_localBackend.IsLocalRegion(regionHandle))
                {
                    RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        var engine = ProviderRegistry.Instance.Get<ISerializationEngine>();
                        byte[] sogBytes = engine.SceneObjectSerializer.SerializeGroupToBytes(sog, SerializationFlags.SerializePhysicsShapes | SerializationFlags.SerializeScriptBytecode);

                        long nonceID = NextNonceID();

                        RegionClient.CreateObject2Ret ret = m_regionClient.DoCreateObject2Call(regInfo, sog.UUID, sogBytes, true, posInOtherRegion, isAttachment, nonceID, avatars);

                        if (ret == RegionClient.CreateObject2Ret.Ok)
                            return true;

                        // create failed, make sure the other side didn't get it late and create one
                        // we could check for a soecific range of "ret" here, representing the timeout
                        if (ret != RegionClient.CreateObject2Ret.AccessDenied)
                            m_regionClient.DoDeleteObject2Call(regInfo, sog.UUID, nonceID);
                    }
                }
                return false;
            }
            finally
            {
                m_log.DebugFormat("[REST COMM] SendCreateObject: {0} ms", Environment.TickCount - createObjectStart);
            }
        }

        public bool SendCreateObject(ulong regionHandle, UUID userID, UUID itemID)
        {
            // Not Implemented
            return false;
        }

        public bool SendUpdateEstateInfo(UUID regionID)
        {
            // Try local first
            if (m_localBackend.SendUpdateEstateInfo(regionID))
                return true;

            RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionID);
            // else do the remote thing
            if (regInfo != null)
                return m_regionClient.SendUpdateEstateInfo(regInfo);
            return false;
        }

        #endregion /* IInterregionComms */

        #region Incoming calls from remote instances

        private string GenerateAuthentication()
        {
            return Util.StringToBase64(_gridSendKey + ":" + _gridSendKey);
        }

        /**
         * Agent-related incoming calls
         */

        public Hashtable AgentHandler(Hashtable request)
        {
            Hashtable responsedata = new Hashtable();
            Hashtable headers = (Hashtable)request["headers"];

            if (!Util.CheckHttpAuthorization(_gridSendKey, headers))
            {
                m_log.WarnFormat("[REST COMMS]: /agent/ communication from untrusted peer {0}", headers["remote_addr"]);
                responsedata["int_response_code"] = 401;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Skip reporting of PUTs, because moving avatars generate a constant stream.
            // If we later want these for other cases, we should check if the "uri" starts with "/agent/" and eliminate only those.
            string method = (string)request["http-method"];
            if (method != "PUT")
                m_log.DebugFormat("[REST COMMS]: {0} type={1} uri={2}", method, request["content-type"], request["uri"]);

            responsedata["content_type"] = "text/html";

            UUID agentID;
            string action;
            ulong regionHandle;
            if (!GetParams((string)request["uri"], out agentID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for agent message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            if (method.Equals("PUT"))
            {
                DoAgentPut(request, responsedata);
                return responsedata;
            }
            else if (method.Equals("POST"))
            {
                DoAgentPost(request, responsedata, agentID);
                return responsedata;
            }
            else if (method.Equals("GET"))
            {
                DoAgentGet(request, responsedata, agentID, regionHandle);
                return responsedata;
            }
            else if (method.Equals("DELETE"))
            {
                DoAgentDelete(request, responsedata, agentID, action, regionHandle);
                return responsedata;
            }
            else
            {
                m_log.InfoFormat("[REST COMMS]: method {0} not supported in agent message", method);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

        }

        protected virtual void DoAgentPost(Hashtable request, Hashtable responsedata, UUID id)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            bool authorize = false;
            if (args.ContainsKey("destination_handle"))
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);
            if (args.ContainsKey("authorize_user")) // not implemented on the sending side yet
                bool.TryParse(args["authorize_user"].AsString(), out authorize);

            AgentCircuitData aCircuit = new AgentCircuitData();
            try
            {
                aCircuit.UnpackAgentCircuitData(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildCreate message {0}", ex.Message);
                return;
            }

            OSDMap resp = new OSDMap(2);
            string reason = String.Empty;

            // This is the meaning of POST agent
            bool result = m_localBackend.SendCreateChildAgent(regionhandle, aCircuit, authorize, out reason);

            resp["reason"] = OSD.FromString(reason);
            resp["success"] = OSD.FromBoolean(result);

            // TODO: add reason if not String.Empty?
            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp);
        }

        protected virtual void DoAgentPut(Hashtable request, Hashtable responsedata)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args.ContainsKey("destination_handle"))
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            string messageType;
            if (args.ContainsKey("message_type"))
                messageType = args["message_type"].AsString();
            else
            {
                m_log.Warn("[REST COMMS]: Agent Put Message Type not found. ");
                messageType = "AgentData";
            }

            bool result = true;
            if ("AgentData".Equals(messageType))
            {
                AgentData agent = new AgentData();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    return;
                }

                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_localBackend.SendChildAgentUpdate(regionhandle, agent);

            }
            else if ("AgentPosition".Equals(messageType))
            {
                AgentPosition agent = new AgentPosition();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    return;
                }
                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_localBackend.SendChildAgentUpdate(regionhandle, agent);

            }

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        protected virtual void DoAgentGet(Hashtable request, Hashtable responsedata, UUID id, ulong regionHandle)
        {
            IAgentData agent = null;
            bool result = m_localBackend.SendRetrieveRootAgent(regionHandle, id, out agent);
            OSDMap map = null;
            if (result)
            {
                if (agent != null) // just to make sure
                {
                    map = agent.Pack();
                    string strBuffer = String.Empty;
                    try
                    {
                        strBuffer = OSDParser.SerializeJsonString(map);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of CreateObject: {0}", e.Message);
                        // ignore. buffer will be empty, caller should check.
                    }

                    responsedata["content_type"] = "application/json";
                    responsedata["int_response_code"] = 200;
                    responsedata["str_response_string"] = strBuffer;
                }
                else
                {
                    responsedata["int_response_code"] = 500;
                    responsedata["str_response_string"] = "Internal error";
                }
            }
            else
            {
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "Not Found";
            }
        }

        protected virtual void DoAgentDelete(Hashtable request, Hashtable responsedata, UUID id, string action, ulong regionHandle)
        {
            //m_log.Debug(" >>> DoDelete action:" + action + "; regionHandle:" + regionHandle);
            
            if (action.Equals("release"))
                m_localBackend.SendReleaseAgent(regionHandle, id, String.Empty);
            else
                m_localBackend.SendCloseAgent(regionHandle, id);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = "Halcyon agent " + id.ToString();

            m_log.Debug("[REST COMMS]: Agent Deleted.");
        }

        /**
         * Object-related incoming calls
         */

        public Hashtable ObjectHandler(Hashtable request)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";
            Hashtable headers = (Hashtable)request["headers"];

            if (!Util.CheckHttpAuthorization(_gridSendKey, headers))
            {
                m_log.WarnFormat("[REST COMMS]: /object/ communication from untrusted peer {0}", headers["remote_addr"]);
                responsedata["int_response_code"] = 401;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            m_log.Debug(" >> "+request["http-method"]+" "+request["content-type"]+" uri=" + request["uri"]);

            UUID objectID;
            string action;
            ulong regionHandle;
            if (!GetParams((string)request["uri"], out objectID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for object message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("POST"))
            {
                DoObjectPost(request, responsedata, regionHandle);
                return responsedata;
            }
            else if (method.Equals("PUT"))
            {
                DoObjectPut(request, responsedata, regionHandle);
                return responsedata;
            }
            else
            {
                m_log.InfoFormat("[REST COMMS]: method {0} not supported in object message", method);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

        }

        protected virtual void DoObjectPost(Hashtable request, Hashtable responsedata, ulong regionhandle)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }
            
            Vector3 pos = Vector3.Zero;
            string sogXmlStr = String.Empty, extraStr = String.Empty, stateXmlStr = String.Empty;
            if (args.ContainsKey("sog"))
                sogXmlStr = args["sog"].AsString();
            if (args.ContainsKey("extra"))
                extraStr = args["extra"].AsString();
            if (args.ContainsKey("pos"))
                pos = args["pos"].AsVector3();

            UUID regionID = m_localBackend.GetRegionID(regionhandle);
            SceneObjectGroup sog = null; 
            try
            {
                sog = SceneObjectSerializer.FromXml2Format(sogXmlStr);
                sog.ExtraFromXmlString(extraStr);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on deserializing scene object {0}", ex.Message);
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            if (args.ContainsKey("pos"))
            {
                sog.AbsolutePosition = pos;
            }

            if (args.ContainsKey("avatars"))
            {
                sog.AvatarsToExpect = args["avatars"].AsInteger();
            }

            if ((args.ContainsKey("state")) && m_aScene.m_allowScriptCrossings)
            {
                stateXmlStr = args["state"].AsString();
                if (!String.IsNullOrEmpty(stateXmlStr))
                {
                    try
                    {
                        sog.SetState(stateXmlStr, regionID);
                    }
                    catch (Exception ex)
                    {
                        m_log.InfoFormat("[REST COMMS]: exception on setting state for scene object {0}", ex.Message);

                    }
                }
            }
            // This is the meaning of POST object
            bool result = m_localBackend.SendCreateObject(regionhandle, sog, null, false, pos, args["pos"] == null);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        protected virtual void DoObjectPut(Hashtable request, Hashtable responsedata, ulong regionhandle)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            UUID userID = UUID.Zero, itemID = UUID.Zero;
            if (args.ContainsKey("userid"))
                userID = args["userid"].AsUUID();
            if (args.ContainsKey("itemid"))
                itemID = args["itemid"].AsUUID();

            //UUID regionID = m_localBackend.GetRegionID(regionhandle);

            // This is the meaning of PUT object
            bool result = m_localBackend.SendCreateObject(regionhandle, userID, itemID);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        private void DoUpdateEstateInfo(UUID regionID)
        {
            m_localBackend.SendUpdateEstateInfo(regionID);
        }

        private long GetNonceID(NameValueCollection headers)
        {
            try
            {
                string nonceStr = headers["x-nonce-id"];
                if (!String.IsNullOrEmpty(nonceStr))
                    return Convert.ToInt64(nonceStr);
            }
            catch(Exception)
            {
            }
            return 0;
        }

        private IndexedPriorityQueue<long, DateTime> _pastDeletes = new IndexedPriorityQueue<long, DateTime>();

        private void FlushExpiredDeletes()
        {
            while (_pastDeletes.Count > 0)
            {
                KeyValuePair<long, DateTime> kvp = _pastDeletes.FindMinItemAndIndex();
                if (kvp.Value <= DateTime.Now)
                {
                    // m_log.WarnFormat("[REST COMMS]: Expiring deleted object nonce ID nonce ID {0}", kvp.Key);
                    _pastDeletes.DeleteMin();
                }
                else
                {
                    break;
                }
            }
        }

        private bool CheckNonceID(long nonceID)
        {
            lock (_pastDeletes)
            {
                FlushExpiredDeletes();
                if (_pastDeletes.ContainsKey(nonceID))
                {
                    _pastDeletes.Remove(nonceID);
                    return true;
                }
            }
            return false;
        }

        protected virtual string DoObject2Post(Stream request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            if (!Util.CheckHttpAuthorization(_gridSendKey, httpRequest.Headers))
            {
                m_log.WarnFormat("[REST COMMS]: /object2/ communication from untrusted peer {0}", httpRequest.RemoteIPEndPoint.Address.ToString());
                httpResponse.StatusCode = 401;
                return "Untrusted";
            }

            //m_log.Debug(" >> " + request["http-method"] + " " + request["content-type"] + " uri=" + request["uri"]);

            UUID objectID;
            string action;
            ulong regionHandle;
            if (!GetParams(path, out objectID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for object message {0}", path);
                httpResponse.StatusCode = 400;
                return "Invalid Parameters";
            }

            ObjectPostMessage message = ProtoBuf.Serializer.Deserialize<ObjectPostMessage>(request);

            if (message == null)
            {
                httpResponse.StatusCode = 400;
                return "Invalid Request";
            }

            // System.Threading.Thread.Sleep(16000);

            //used for interpolation
            ulong createTime = Util.GetLongTickCount();
            SceneObjectGroup sog = null;
            long nonceID = GetNonceID(httpRequest.Headers);
            try
            {
                var engine = ProviderRegistry.Instance.Get<ISerializationEngine>();
                sog = engine.SceneObjectSerializer.DeserializeGroupFromBytes(message.Sog);
                sog.TimeReceived = createTime;
                sog.NonceID = nonceID;
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on deserializing scene object {0}", ex);
                httpResponse.StatusCode = 500;
                return ex.Message;
            }

            if (CheckNonceID(nonceID))
            {
                // possibly would have succeeded but client side cancelled
                // m_log.WarnFormat("[REST COMMS]: Skipping object create for nonce ID nonce ID {0}", nonceID);
                httpResponse.StatusCode = 204;
                return "No Content";
            }
            // else m_log.WarnFormat("[REST COMMS]: Nonce ID {0} not found, allowing rez.", nonceID);

            if (message.Pos.HasValue)
            {
                sog.OriginalEnteringScenePosition = sog.AbsolutePosition;
                sog.AbsolutePosition = Util.GetValidRegionXYZ(message.Pos.Value);
            }

            List<UUID> avatarIDs = new List<UUID>();
            if (message.Avatars == null)
                sog.AvatarsToExpect = message.NumAvatars;
            else
            {
                sog.AvatarsToExpect = message.Avatars.Length;
                foreach (Guid id in message.Avatars)
                    avatarIDs.Add(new UUID(id));
            }

            // This is the meaning of POST object
            bool result = m_localBackend.SendCreateObject(regionHandle, sog, avatarIDs, false, message.Pos.HasValue ? message.Pos.Value : Vector3.Zero, message.Pos.HasValue);

            if (result)
            {
                httpResponse.StatusCode = 200;
            }
            else
            {
                httpResponse.StatusCode = 500;
            }
            
            return result.ToString();
        }

        protected virtual string DoObject2Delete(Stream request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            if (!Util.CheckHttpAuthorization(_gridSendKey, httpRequest.Headers))
            {
                m_log.WarnFormat("[REST COMMS]: /object2/ communication from untrusted peer {0}", httpRequest.RemoteIPEndPoint.Address.ToString());
                httpResponse.StatusCode = 401;
                return "Untrusted";
            }

            //m_log.Debug(" >> " + request["http-method"] + " " + request["content-type"] + " uri=" + request["uri"]);

            long nonceID = GetNonceID(httpRequest.Headers);
            lock (_pastDeletes)
            {
                _pastDeletes.Add(nonceID, DateTime.Now + TimeSpan.FromMinutes(5));
                FlushExpiredDeletes();
            }

            UUID objectID;
            string action;
            ulong regionHandle;
            if (!GetParams(path, out objectID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for object message {0}", path);
                httpResponse.StatusCode = 400;
                return "Invalid Parameters";
            }

            // This is the meaning of PUT object
            bool result = m_localBackend.SendDeleteObject(regionHandle, objectID, nonceID);
            if (result)
            {
                httpResponse.StatusCode = 200;
            }
            else
            {
                // the ID was not found, store it to block creates (for a while)
                // m_log.WarnFormat("[REST COMMS]: Delete object not found - adding nonce ID {0} for object {1}", nonceID, objectID);
                httpResponse.StatusCode = 404;
            }

            return result.ToString();
        }

        private string DoAgent2Put(Stream request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            if (! Util.CheckHttpAuthorization(_gridSendKey, httpRequest.Headers))
            {
                m_log.WarnFormat("[REST COMMS]: /agent2/ communication from untrusted peer {0}", httpRequest.RemoteIPEndPoint.Address.ToString());
                httpResponse.StatusCode = 401;
                return "Untrusted";
            }

            UUID agentId;
            ulong regionHandle;
            string action; //unused
            
            if (!GetParams(path, out agentId, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for agent message {0}", path);
                httpResponse.StatusCode = 400;
                return "Invalid Parameters";
            }

            ulong createdOn = Util.GetLongTickCount();
            AgentPutMessage message = ProtoBuf.Serializer.Deserialize<AgentPutMessage>(request);

            if (message == null)
            {
                httpResponse.StatusCode = 400;
                return "Invalid Request";
            }

            //CAU2 is the meaning of PUT 
            var agentData = message.ToAgentData();
            agentData.AgentDataCreatedOn = createdOn; //used to calculate interpolation values
            ChildAgentUpdate2Response status = m_localBackend.SendChildAgentUpdate2(m_localBackend.GetRegion(regionHandle), agentData);
            bool result = (status == ChildAgentUpdate2Response.Ok);
            switch (status) 
            {
                case ChildAgentUpdate2Response.Ok:
                    httpResponse.StatusCode = 200;
                    break;
                case ChildAgentUpdate2Response.AccessDenied:
                    httpResponse.StatusCode = 403;
                    break;
                default:
                    httpResponse.StatusCode = 500;
                    break;
            }
            return result.ToString();
        }

        private string DoWaitScenePresence(Stream request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            if (!Util.CheckHttpAuthorization(_gridSendKey, httpRequest.Headers))
            {
                m_log.WarnFormat("[REST COMMS]: /agent2/ communication from untrusted peer {0}", httpRequest.RemoteIPEndPoint.Address.ToString());
                httpResponse.StatusCode = 401;
                return "Untrusted";
            }

            UUID agentId;
            ulong regionHandle;
            string action; //unused

            if (!GetParams(path, out agentId, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for agent message {0}", path);
                httpResponse.StatusCode = 400;
                return "Invalid Parameters";
            }

            // This is the meaning of POST object
            const int MAX_SP_WAIT = 10000;
            bool result = m_localBackend.SendWaitScenePresence(regionHandle, agentId, MAX_SP_WAIT);

            if (result)
            {
                httpResponse.StatusCode = 200;
            }
            else
            {
                httpResponse.StatusCode = 500;
            }

            return result.ToString();
        }

        #endregion 

        #region Misc


        /// <summary>
        /// Extract the param from an uri.
        /// </summary>
        /// <param name="uri">Something like this: /agent/uuid/ or /agent/uuid/handle/release</param>
        /// <param name="uri">uuid on uuid field</param>
        /// <param name="action">optional action</param>
        public static bool GetParams(string uri, out UUID uuid, out ulong regionHandle, out string action)
        {
            uuid = UUID.Zero;
            action = String.Empty;
            regionHandle = 0;

            uri = uri.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
            if (parts.Length <= 1)
            {
                return false;
            }
            else
            {
                if (!UUID.TryParse(parts[1], out uuid))
                    return false;

                if (parts.Length >= 3)
                    UInt64.TryParse(parts[2], out regionHandle);
                if (parts.Length >= 4)
                    action = parts[3];
                
                return true;
            }
        }

        public static bool GetAuthentication(Hashtable request, out string authority, out string authKey)
        {
            authority = String.Empty;
            authKey = String.Empty;

            Uri authUri;
            Hashtable headers = (Hashtable)request["headers"];

            // Authorization keys look like this:
            // http://orgrid.org:8002/<uuid>
            if (headers.ContainsKey("authorization") && (string)headers["authorization"] != "None")
            {
                if (Uri.TryCreate((string)headers["authorization"], UriKind.Absolute, out authUri))
                {
                    authority = authUri.Authority;
                    authKey = authUri.PathAndQuery.Trim('/');
                    m_log.DebugFormat("[REST COMMS]: Got authority {0} and key {1}", authority, authKey);
                    return true;
                }
                else
                    m_log.Debug("[REST COMMS]: Wrong format for Authorization header: " + (string)headers["authorization"]);
            }
            else
                m_log.Debug("[REST COMMS]: Authorization header not found");

            return false;
        }

        bool VerifyKey(UUID userID, string authority, string key)
        {
            string[] parts = authority.Split(':');
            IPAddress ipaddr = IPAddress.None;
            uint port = 0;
            if (parts.Length <= 2)
                ipaddr = Util.GetHostFromDNS(parts[0]);
            if (parts.Length == 2)
                UInt32.TryParse(parts[1], out port);

            // local authority (standalone), local call
            if (m_thisIP.Equals(ipaddr) && (m_aScene.RegionInfo.HttpPort == port))
                return ((IAuthentication)m_aScene.CommsManager.UserAdminService).VerifyKey(userID, key);
            // remote call
            else
                return AuthClient.VerifyKey("http://" + authority, userID, key);
        }


        #endregion Misc 

        protected class RegionToRegionClient : RegionClient
        {
            Scene m_aScene = null;

            public RegionToRegionClient(Scene s, string gridSendKey) : base(gridSendKey)
            {
                m_aScene = s;
            }

            public override ulong GetRegionHandle(ulong handle)
            {
                return handle;
            }

            public override bool IsHyperlink(ulong handle)
            {
                return false;
            }
        }



        public ChildAgentUpdate2Response SendChildAgentUpdate2(SimpleRegionInfo regionInfo, AgentData data)
        {
            int childAgentUpdateStart = Environment.TickCount;
            try
            {
                // Try local first
                if (m_localBackend.SendChildAgentUpdate2(regionInfo, data) == ChildAgentUpdate2Response.Ok)
                    return ChildAgentUpdate2Response.Ok;

                // else do the remote thing
                if (!m_localBackend.IsLocalRegion(regionInfo.RegionHandle))
                {
                    RegionClient.AgentUpdate2Ret ret = m_regionClient.DoChildAgentUpdateCall2(regionInfo, data);
                    switch (ret)
                    {
                        case RegionClient.AgentUpdate2Ret.Ok:
                            return ChildAgentUpdate2Response.Ok;
                        case RegionClient.AgentUpdate2Ret.Error:
                            return ChildAgentUpdate2Response.Error;
                        case RegionClient.AgentUpdate2Ret.NotFound:
                            return ChildAgentUpdate2Response.MethodNotAvailalble;
                        case RegionClient.AgentUpdate2Ret.AccessDenied:
                            return ChildAgentUpdate2Response.AccessDenied;
                    }
                }

                return ChildAgentUpdate2Response.Error;
            }
            finally
            {
                m_log.DebugFormat("[REST COMM] SendChildAgentUpdateCall2: {0} ms", Environment.TickCount - childAgentUpdateStart);
            }
        }


        public async Task<bool> SendCloseAgentAsync(SimpleRegionInfo regionInfo, UUID id)
        {
            return await m_regionClient.DoCloseAgentCallAsync(regionInfo, id);
        }
    }
}
