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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Text;
using Nwc.XmlRpc;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.Linden
{
    /// <summary>
    /// Handles login user (expect user) and logoff user messages from the remote LL login server
    /// </summary>
    public class LLProxyLoginModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool RegionLoginsEnabled
        {
            get
            {
                if (m_firstScene != null)
                {
                    return m_firstScene.CommsManager.GridService.RegionLoginsEnabled;
                }
                else
                {
                    return false;
                }
            }
        }

        private List<Scene> m_scenes = new List<Scene>();
        private Scene m_firstScene;
        private string _gridSendKey;
        private bool m_enabled = false; // Module is only enabled if running in grid mode

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource source)
        {
            if (m_firstScene == null)
            {
                m_firstScene = scene;

                IConfig startupConfig = source.Configs["Startup"];
                if (startupConfig != null)
                {
                    m_enabled = startupConfig.GetBoolean("gridmode", false);
                }

                IConfig netConfig = source.Configs["Network"];

                if (netConfig != null)
                {
                    _gridSendKey = netConfig.GetString("grid_send_key");
                }
                else
                {
                    throw new Exception("LLProxyLoginModule: Network configuration not found");
                }

                if (m_enabled)
                {
                    AddHttpHandlers();
                }
            }

            if (m_enabled)
            {
                AddScene(scene);
            }
        }

        public void PostInitialize()
        {

        }

        public void Close()
        {

        }

        public string Name
        {
            get { return "LLProxyLoginModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        /// <summary>
        /// Adds "expect_user" and "logoff_user" xmlrpc method handlers
        /// </summary>
        protected void AddHttpHandlers()
        {
            //we will add our handlers to the first scene we received, as all scenes share a http server. But will this ever change?
            m_firstScene.CommsManager.HttpServer.AddXmlRPCHandler("expect_user", ExpectUser);
            m_firstScene.CommsManager.HttpServer.AddXmlRPCHandler("logoff_user", LogOffUser);

            // New Style service interface
            m_firstScene.CommsManager.HttpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("expect_user"), ExpectUser));
            m_firstScene.CommsManager.HttpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("logoff_user"), LogOffUser));
        }


        protected void AddScene(Scene scene)
        {
            lock (m_scenes)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                }
            }
        }
        /// <summary>
        /// Received from the user server when a user starts logging in.  This call allows
        /// the region to prepare for direct communication from the client.  Sends back an empty
        /// xmlrpc response on completion.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectUser(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse resp = new XmlRpcResponse();

            //check the send key
            if (request.Params.Count < 2 || (string)request.Params[1] != _gridSendKey)
            {
                Hashtable respdata = new Hashtable();
                respdata["success"] = "FALSE";
                respdata["reason"] = "Grid authorization failed";
                resp.Value = respdata;

                return resp;
            }

            Hashtable requestData = (Hashtable)request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new UUID((string)requestData["session_id"]);
            agentData.SecureSessionID = new UUID((string)requestData["secure_session_id"]);
            agentData.FirstName = (string)requestData["firstname"];
            agentData.LastName = (string)requestData["lastname"];
            agentData.AgentID = new UUID((string)requestData["agent_id"]);
            agentData.CircuitCode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string)requestData["caps_path"];
            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);
            
            if (requestData.ContainsKey("client_version"))
                agentData.ClientVersion = (string)requestData["client_version"];
            else
                m_log.Warn("[CLIENT]: ExpectUser missing client_version for " + agentData.AgentID.ToString());

            // Appearance
            if (requestData.ContainsKey("appearance"))
                agentData.Appearance = new AvatarAppearance((Hashtable)requestData["appearance"]);

            m_log.DebugFormat(
                "[CLIENT]: Incoming connection from {0} {1} {2}, circuit {3}: {4}",
                agentData.FirstName, agentData.LastName, agentData.AgentID, agentData.CircuitCode, agentData.ClientVersion);

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
                bool success = false;
                string denyMess = String.Empty;
        
                Scene scene;
                if (TryGetRegion(regionHandle, out scene))
                {
                    if (scene.IsBlacklistedUser(agentData.AgentID))
                    {
                        denyMess = "User is blacklisted from this region";
                        m_log.InfoFormat(
                            "[CLIENT]: Denying access for user {0} {1} because user is blacklisted",
                            agentData.FirstName, agentData.LastName);
                    }
                    else
                        if (scene.RegionInfo.EstateSettings.IsBanned(agentData.AgentID))
                    {
                        denyMess = "User is banned from this region";
                        m_log.InfoFormat(
                            "[CLIENT]: Denying access for user {0} {1} because user is banned",
                            agentData.FirstName, agentData.LastName);
                    }
                    else
                    {
                        string reason;
                        if (scene.NewUserLogin(agentData, out reason))
                        {
                            success = true;
                        }
                        else
                        {
                            denyMess = String.Format("Login refused by region: {0}", reason);
                            m_log.InfoFormat(
                                "[CLIENT]: Denying access for user {0} {1} because user connection was refused by the region",
                                agentData.FirstName, agentData.LastName);
                        }
                    }
                    
                }
                else
                {
                    denyMess = "Region not found";
                }

                if (success)
                {
                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "TRUE";
                    resp.Value = respdata;
                }
                else
                {
                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "FALSE";
                    respdata["reason"] = denyMess;
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
        public XmlRpcResponse LogOffUser(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: LogOff User Called");

            Hashtable requestData = (Hashtable)request.Params[0];
            string message = (string)requestData["message"];
            UUID agentID = UUID.Zero;
            UUID RegionSecret = UUID.Zero;
            UUID.TryParse((string)requestData["agent_id"], out agentID);
            UUID.TryParse((string)requestData["region_secret"], out RegionSecret);

            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);

            Scene scene;
            if (TryGetRegion(regionHandle, out scene))
            {
                scene.HandleLogOffUserFromGrid(agentID, RegionSecret, message);
            }

            return new XmlRpcResponse();
        }

        protected bool TryGetRegion(ulong regionHandle, out Scene scene)
        {
            lock (m_scenes)
            {
                foreach (Scene nextScene in m_scenes)
                {
                    if (nextScene.RegionInfo.RegionHandle == regionHandle)
                    {
                        scene = nextScene;
                        return true;
                    }
                }
            }

            scene = null;
            return false;
        }

    }
}
