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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;

using OpenSim.Framework.Communications.Messages;
using System.Threading.Tasks;

namespace OpenSim.Framework.Communications.Clients
{
    public class RegionClient
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const int CREATE_OBJECT_TIMEOUT = 15000; // how long the sending region waits for an object create
        public const int REZ_OBJECT_TIMEOUT = 10000;    // max time the receiving region can take to rez the object
        public const int AGENT_UPDATE_TIMEOUT = 10000;  // how long the sending region waits for an agent create

        string _gridSendKey;

        public RegionClient(string gridSendKey)
        {
            _gridSendKey = gridSendKey;
        }

        public bool DoCreateChildAgentCall(RegionInfo region, AgentCircuitData aCircuit, string authKey, out string reason)
        {
            // Eventually, we want to use a caps url instead of the agentID
            string uri = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/agent/" + aCircuit.AgentID + "/";
            //Console.WriteLine("   >>> DoCreateChildAgentCall <<< " + uri);

            HttpWebRequest AgentCreateRequest = (HttpWebRequest)WebRequest.Create(uri);
            AgentCreateRequest.Method = "POST";
            AgentCreateRequest.ContentType = "application/json";
            AgentCreateRequest.Timeout = 10000;
            //AgentCreateRequest.KeepAlive = false;

            AgentCreateRequest.Headers["authorization"] = GenerateAuthorization();

            reason = String.Empty;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = aCircuit.PackAgentCircuitData();
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: PackAgentCircuitData failed with exception: " + e.Message);
                reason = "PackAgentCircuitData exception";
                return false;
            }

            // Add the regionhandle of the destination region
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            args["destination_handle"] = OSD.FromString(regionHandle.ToString());

            string strBuffer = String.Empty;
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                UTF8Encoding str = new UTF8Encoding();
                buffer = str.GetBytes(strBuffer);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of ChildCreate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                AgentCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = AgentCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted CreateChildAgent request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on ChildAgentUpdate {0}", ex.Message);
                reason = "cannot contact remote region";
                return false;
            }
            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");

            try
            {
                WebResponse webResponse = AgentCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoCreateChildAgentCall post");
                    reason = "response is null";
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                string response = sr.ReadToEnd().Trim();
                sr.Close();
                m_log.InfoFormat("[REST COMMS]: DoCreateChildAgentCall reply was {0} ", response);
                if (String.IsNullOrEmpty(response))
                {
                    reason = "response is empty";
                    return false;
                }

                try
                {
                    // we assume we got an OSDMap back
                    OSDMap r = GetOSDMap(response);
                    bool success = r["success"].AsBoolean();
                    reason = r["reason"].AsString();
                    return success;
                }
                catch (NullReferenceException e)
                {
                    m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", e.Message);

                    // check for old style response
                    if (response.ToLower().StartsWith("true"))
                        return true;

                    reason = "null reference exception";
                    return false;
                }
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", ex.Message);
                reason = "web exception";
                return false;
            }
        }

        public async Task<Tuple<bool, string>> DoCreateChildAgentCallAsync(SimpleRegionInfo regionInfo, AgentCircuitData aCircuit)
        {
            string uri = regionInfo.InsecurePublicHTTPServerURI + "/agent/" + aCircuit.AgentID + "/";

            HttpWebRequest agentCreateRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
            agentCreateRequest.Method = "POST";
            agentCreateRequest.ContentType = "application/json";
            agentCreateRequest.Timeout = AGENT_UPDATE_TIMEOUT;
            agentCreateRequest.ReadWriteTimeout = AGENT_UPDATE_TIMEOUT;
            agentCreateRequest.Headers["authorization"] = GenerateAuthorization();

            OSDMap args = null;
            try
            {
                args = aCircuit.PackAgentCircuitData();
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: PackAgentCircuitData failed with exception: " + e.Message);
                return Tuple.Create(false, "PackAgentCircuitData exception");
            }

            // Add the regionhandle of the destination region
            ulong regionHandle = GetRegionHandle(regionInfo.RegionHandle);
            args["destination_handle"] = OSD.FromString(regionHandle.ToString());

            string strBuffer = String.Empty;
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                UTF8Encoding str = new UTF8Encoding();
                buffer = str.GetBytes(strBuffer);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of ChildCreate: {0}", e.Message);
                return Tuple.Create(false, "Exception thrown on serialization of ChildCreate");
            }

            
            try
            { // send the Post
                agentCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                Stream os = await agentCreateRequest.GetRequestStreamAsync();

                await os.WriteAsync(buffer, 0, strBuffer.Length);         //Send it
                await os.FlushAsync();

                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted CreateChildAgent request to remote sim {0}", uri);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST COMMS]: Unable to contact remote region {0}: {1}", regionInfo.RegionHandle, e.Message);
                return Tuple.Create(false, "cannot contact remote region");
            }
            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");

            try
            {
                WebResponse webResponse = await agentCreateRequest.GetResponseAsync(AGENT_UPDATE_TIMEOUT);
                if (webResponse == null)
                {
                    m_log.Warn("[REST COMMS]: Null reply on DoCreateChildAgentCall post");
                    return Tuple.Create(false, "response from remote region was null");
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                string response = await sr.ReadToEndAsync();
                response.Trim();
                sr.Close();

                //m_log.InfoFormat("[REST COMMS]: DoCreateChildAgentCall reply was {0} ", response);
                if (String.IsNullOrEmpty(response))
                {
                    m_log.Info("[REST COMMS]: Empty response on DoCreateChildAgentCall post");
                    return Tuple.Create(false, "response from remote region was empty");
                }

                try
                {
                    // we assume we got an OSDMap back
                    OSDMap r = GetOSDMap(response);
                    bool success = r["success"].AsBoolean();
                    string reason = r["reason"].AsString();

                    return Tuple.Create(success, reason);
                }
                catch (NullReferenceException e)
                {
                    m_log.WarnFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", e.Message);

                    // check for old style response
                    if (response.ToLower().StartsWith("true"))
                        return Tuple.Create(true, String.Empty);

                    return Tuple.Create(false, "exception on reply of DoCreateChildAgentCall");
                }
            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", ex);
                return Tuple.Create(false, "web exception");
            }
            catch (Exception ex)
            {
                m_log.WarnFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", ex);
                return Tuple.Create(false, "web exception");
            }
        }

        public bool DoChildAgentUpdateCall(RegionInfo region, IAgentData cAgentData)
        {
            // Eventually, we want to use a caps url instead of the agentID
            string uri = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/agent/" + cAgentData.AgentID + "/";
            //Console.WriteLine("   >>> DoChildAgentUpdateCall <<< " + uri);

            HttpWebRequest ChildUpdateRequest = (HttpWebRequest)WebRequest.Create(uri);
            ChildUpdateRequest.Method = "PUT";
            ChildUpdateRequest.ContentType = "application/json";
            ChildUpdateRequest.Timeout = AGENT_UPDATE_TIMEOUT;
            //ChildUpdateRequest.KeepAlive = false;
            ChildUpdateRequest.Headers["authorization"] = GenerateAuthorization();

            // Fill it in
            OSDMap args = null;
            try
            {
                args = cAgentData.Pack();
            }
            catch (Exception e)
            {
                m_log.Error("[REST COMMS]: PackUpdateMessage failed with exception: " + e.Message);
                return false;
            }

            // Add the regionhandle of the destination region
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            args["destination_handle"] = OSD.FromString(regionHandle.ToString());

            string strBuffer = String.Empty;
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                UTF8Encoding str = new UTF8Encoding();
                buffer = str.GetBytes(strBuffer);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[REST COMMS]: Exception thrown on serialization of ChildUpdate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                ChildUpdateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = ChildUpdateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted ChildAgentUpdate request to remote sim {0}", uri);
            }
            catch (WebException)
            {
                // Normal case of network error connecting to a region (e.g. a down one)
                return false;
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[REST COMMS]: Bad send on ChildAgentUpdate {0}", ex.Message);
                return false;
            }

            // Let's wait for the response
//            m_log.Info("[REST COMMS]: Waiting for a reply after ChildAgentUpdate");
            try
            {
                WebResponse webResponse = ChildUpdateRequest.GetResponse();
                if (webResponse != null)
                {
                    StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                    string reply = sr.ReadToEnd().Trim();
                    sr.Close();
                    //m_log.InfoFormat("[REST COMMS]: ChildAgentUpdate reply was {0} ", reply);
                    bool rc = false;
                    if (!bool.TryParse(reply, out rc))
                        rc = false;
                    return rc;
                }
                m_log.Info("[REST COMMS]: Null reply on ChildAgentUpdate post");
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of ChildAgentUpdate {0}", ex.Message);
            }
            return false;
        }

        public enum AgentUpdate2Ret
        {
            Ok,
            Error,
            NotFound,
            AccessDenied
        }

        public AgentUpdate2Ret DoChildAgentUpdateCall2(SimpleRegionInfo regInfo, AgentData data)
        {
            ulong regionHandle = GetRegionHandle(regInfo.RegionHandle);
            string uri = "http://" + regInfo.ExternalHostName + ":" + regInfo.HttpPort + "/agent2/" + data.AgentID + "/" + regionHandle + "/";

            HttpWebRequest agentPutRequest = (HttpWebRequest)WebRequest.Create(uri);
            agentPutRequest.Method = "PUT";
            agentPutRequest.ContentType = "application/octet-stream";
            agentPutRequest.Timeout = AGENT_UPDATE_TIMEOUT;

            agentPutRequest.Headers["authorization"] = GenerateAuthorization();

            AgentPutMessage message = AgentPutMessage.FromAgentData(data);

            try
            {
                // send the Post
                Stream os = agentPutRequest.GetRequestStream();
                ProtoBuf.Serializer.Serialize(os, message);

                os.Flush();
                os.Close();

                m_log.InfoFormat("[REST COMMS]: PUT DoChildAgentUpdateCall2 request to remote sim {0}", uri);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[REST COMMS]: DoChildAgentUpdateCall2 call failed {0}", e);
                return AgentUpdate2Ret.Error;
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");
            try
            {
                HttpWebResponse webResponse = (HttpWebResponse)agentPutRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Error("[REST COMMS]: Null reply on DoChildAgentUpdateCall2 put");
                    return AgentUpdate2Ret.Error;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                string reply = sr.ReadToEnd().Trim();
                sr.Close();

                //this will happen during the initial rollout and tells us we need to fall back to the 
                //old method
                if (webResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    m_log.InfoFormat("[REST COMMS]: NotFound on reply of DoChildAgentUpdateCall2");
                    return AgentUpdate2Ret.NotFound;
                }
                else if (webResponse.StatusCode == HttpStatusCode.OK)
                {
                    return AgentUpdate2Ret.Ok;
                }
                else
                {
                    m_log.ErrorFormat("[REST COMMS]: Error on reply of DoChildAgentUpdateCall2 {0}", reply);
                    return AgentUpdate2Ret.Error;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse webResponse = ex.Response as HttpWebResponse;
                if (webResponse != null)
                {
                    //this will happen during the initial rollout and tells us we need to fall back to the 
                    //old method
                    if (webResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        m_log.InfoFormat("[REST COMMS]: NotFound on reply of DoChildAgentUpdateCall2");
                        return AgentUpdate2Ret.NotFound;
                    }
                    if (webResponse.StatusCode == HttpStatusCode.Forbidden)
                    {
                        m_log.InfoFormat("[REST COMMS]: Forbidden returned on reply of DoChildAgentUpdateCall2");
                        return AgentUpdate2Ret.AccessDenied;
                    }
                }

                m_log.ErrorFormat("[REST COMMS]: exception on reply of DoChildAgentUpdateCall2 {0} Sz {1}", ex, agentPutRequest.ContentLength);
            }

            return AgentUpdate2Ret.Error;
        }

        public bool DoRetrieveRootAgentCall(RegionInfo region, UUID id, out IAgentData agent)
        {
            agent = null;
            // Eventually, we want to use a caps url instead of the agentID
            string uri = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/agent/" + id + "/" + region.RegionHandle.ToString() + "/";
            //Console.WriteLine("   >>> DoRetrieveRootAgentCall <<< " + uri);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Timeout = 10000;
            //request.Headers.Add("authorization", String.Empty); // coming soon
            request.Headers["authorization"] = GenerateAuthorization();

            HttpWebResponse webResponse = null;
            string reply = String.Empty;
            try
            {
                webResponse = (HttpWebResponse)request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent get ");
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                reply = sr.ReadToEnd().Trim();
                sr.Close();
                //Console.WriteLine("[REST COMMS]: ChildAgentUpdate reply was " + reply);
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent get {0}", ex.Message);
                // ignore, really
                return false;
            }

            if (webResponse.StatusCode == HttpStatusCode.OK)
            {
                // we know it's jason
                OSDMap args = GetOSDMap(reply);
                if (args == null)
                {
                    //Console.WriteLine("[REST COMMS]: Error getting OSDMap from reply");
                    return false;
                }

                agent = new CompleteAgentData();
                agent.Unpack(args);
                return true;
            }

            //Console.WriteLine("[REST COMMS]: DoRetrieveRootAgentCall returned status " + webResponse.StatusCode);
            return false;
        }

        public bool DoReleaseAgentCall(ulong regionHandle, UUID id, string uri)
        {
            //m_log.Debug("   >>> DoReleaseAgentCall <<< " + uri);

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;
            request.Headers["authorization"] = GenerateAuthorization();

            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent delete ");
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChildAgentUpdate reply was {0} ", reply);
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent delete {0}", ex.Message);
            }
            return false;
        }

        public bool DoCloseAgentCall(RegionInfo region, UUID id)
        {
            string uri = region.InsecurePublicHTTPServerURI + "/agent/" + id + "/" + region.RegionHandle.ToString() + "/";

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;
            request.Headers["authorization"] = GenerateAuthorization();

            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent delete ");
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChildAgentUpdate reply was {0} ", reply);
                return true;
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent delete {0}", ex.Message);
            }

            return false;
        }

        public async Task<bool> DoCloseAgentCallAsync(SimpleRegionInfo region, UUID id)
        {
            string uri = region.InsecurePublicHTTPServerURI + "/agent/" + id + "/" + region.RegionHandle.ToString() + "/";

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = AGENT_UPDATE_TIMEOUT;
            request.ReadWriteTimeout = AGENT_UPDATE_TIMEOUT;
            
            request.Headers["authorization"] = GenerateAuthorization();

            try
            {
                WebResponse webResponse = await request.GetResponseAsync(AGENT_UPDATE_TIMEOUT);
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent delete ");
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                string response = await sr.ReadToEndAsync();
                response.Trim();
                sr.Close();

                return true;
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent delete {0}", ex);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent delete {0}", ex);
            }

            return false;
        }

        public enum CreateObject2Ret
        {
            Ok,
            Error,
            NotFound,
            AccessDenied
        }

        private string GenerateAuthorization()
        {
            return Util.GenerateHttpAuthorization(_gridSendKey);
        }

        public CreateObject2Ret DoCreateObject2Call(RegionInfo region, UUID sogId, byte[] sogBytes, bool allowScriptCrossing,
            Vector3 pos, bool isAttachment, long nonceID, List<UUID> avatars)
        {
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            string uri = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/object2/" + sogId + "/" + regionHandle.ToString() + "/";

            HttpWebRequest objectCreateRequest = (HttpWebRequest)WebRequest.Create(uri);
            objectCreateRequest.Method = "POST";
            objectCreateRequest.ContentType = "application/octet-stream";
            objectCreateRequest.Timeout = CREATE_OBJECT_TIMEOUT;

            objectCreateRequest.Headers["authorization"] = GenerateAuthorization();
            objectCreateRequest.Headers["x-nonce-id"] = nonceID.ToString();

            Guid[] avatarsArray;
            if (avatars == null)
                avatarsArray = null;
            else
            {
                int count = 0;
                avatarsArray = new Guid[avatars.Count];
                foreach (UUID id in avatars)
                    avatarsArray[count++] = id.Guid;
            }

            ObjectPostMessage message = new ObjectPostMessage { NumAvatars = avatarsArray.Length, Pos = pos, Sog = sogBytes, Avatars = avatarsArray };

            try
            { 
                // send the Post
                Stream os = objectCreateRequest.GetRequestStream();
                ProtoBuf.Serializer.Serialize(os, message);

                os.Flush();
                os.Close();

                m_log.InfoFormat("[REST COMMS]: Posted DoCreateObject2 request to remote sim {0}", uri);
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[REST COMMS]: DoCreateObject2 call failed {0}", e);
                return CreateObject2Ret.Error;
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");
            try
            {
                HttpWebResponse webResponse = (HttpWebResponse)objectCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoCreateObject2 post");
                    return CreateObject2Ret.Error;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                string reply = sr.ReadToEnd().Trim();
                sr.Close();

                //this will happen during the initial rollout and tells us we need to fall back to the 
                //old method
                if (webResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    m_log.InfoFormat("[REST COMMS]: Entry denied on reply of DoCreateObject2");
                    return CreateObject2Ret.AccessDenied;
                }
                else if (webResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    m_log.InfoFormat("[REST COMMS]: NotFound on reply of DoCreateObject2");
                    return CreateObject2Ret.NotFound;
                }
                else if (webResponse.StatusCode == HttpStatusCode.OK)
                {
                    return CreateObject2Ret.Ok;
                }
                else
                {
                    m_log.WarnFormat("[REST COMMS]: Error on reply of DoCreateObject2 {0}", reply);
                    return CreateObject2Ret.Error;
                }
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateObject2 {0} Sz {1}", ex, objectCreateRequest.ContentLength);
                HttpWebResponse response = (HttpWebResponse)ex.Response;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        m_log.InfoFormat("[REST COMMS]: Entry denied on reply of DoCreateObject2");
                        return CreateObject2Ret.AccessDenied;
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        m_log.InfoFormat("[REST COMMS]: NotFound on reply of DoCreateObject2");
                        return CreateObject2Ret.NotFound;
                    }
                    else if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return CreateObject2Ret.Ok;
                    }
                    else
                    {
                        m_log.Error("[REST COMMS]: Error on reply of DoCreateObject2: " + ex.Message);
                        return CreateObject2Ret.Error;
                    }
                }
            }

            return CreateObject2Ret.Error;
        }

        public bool DoDeleteObject2Call(RegionInfo region, UUID sogId, long nonceID)
        {
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            string uri = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/object2/" + sogId + "/" + regionHandle.ToString() + "/";

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = CREATE_OBJECT_TIMEOUT;
            request.ReadWriteTimeout = CREATE_OBJECT_TIMEOUT;

            request.Headers["authorization"] = GenerateAuthorization();
            request.Headers["x-nonce-id"] = nonceID.ToString();

            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on remote object delete ");
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: DoDeleteObject2Call reply was {0} ", reply);
                return true;
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of remote object delete {0}", ex.Message);
            }

            return false;
        }

        public bool SendUpdateEstateInfo(RegionInfo region)
        {
            string uri = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/region/" + region.RegionID + "/" + region.RegionHandle + "/UpdateEstateInfo/";
            //m_log.Debug("   >>> DoSendUpdateEstateInfoCall <<< " + uri);

            WebRequest sendUpdateEstateInfoRequest = WebRequest.Create(uri);
            sendUpdateEstateInfoRequest.Method = "POST";
            sendUpdateEstateInfoRequest.ContentType = "application/json";
            sendUpdateEstateInfoRequest.Timeout = 10000;

            sendUpdateEstateInfoRequest.Headers["authorization"] = GenerateAuthorization();

            // Fill it in
            OSDMap args = new OSDMap();
            string strBuffer = String.Empty;
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                UTF8Encoding str = new UTF8Encoding();
                buffer = str.GetBytes(strBuffer);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of DoSendUpdateEstateInfoCall: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                sendUpdateEstateInfoRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = sendUpdateEstateInfoRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted DoSendUpdateEstateInfoCall request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on DoSendUpdateEstateInfoCall {0}", ex.Message);
                return false;
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoSendUpdateEstateInfoCall");
            try
            {
                WebResponse webResponse = sendUpdateEstateInfoRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoSendUpdateEstateInfoCall post");
                    return false;
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: DoSendUpdateEstateInfoCall reply was {0} ", reply);
                return true;
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoSendUpdateEstateInfoCall {0}", ex.Message);
            }

            return false;
        }

        #region Hyperlinks

        public virtual ulong GetRegionHandle(ulong handle)
        {
            return handle;
        }

        public virtual bool IsHyperlink(ulong handle)
        {
            return false;
        }

        #endregion /* Hyperlinks */

        public static OSDMap GetOSDMap(string data)
        {
            OSDMap args = null;
            try
            {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson(data);
                if (buffer.Type == OSDType.Map)
                {
                    args = (OSDMap)buffer;
                    return args;
                }
                else
                {
                    // uh?
                    System.Console.WriteLine("[REST COMMS]: Got OSD of type " + buffer.Type.ToString());
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[REST COMMS]: exception on parse of REST message " + ex.Message);
                return null;
            }
        }


    }
}
