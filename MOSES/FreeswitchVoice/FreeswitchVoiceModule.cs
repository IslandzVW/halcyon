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
using System.Xml;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using OpenSim.Framework;
using System.Text;
using System.Collections;

namespace MOSES.FreeSwitchVoice
{
    public class FreeSwitchVoiceModule : ISharedRegionModule
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Capability strings
        private static readonly string m_parcelVoiceInfoRequestPath = "0007/";
        private static readonly string m_provisionVoiceAccountRequestPath = "0008/";
        private static readonly string m_chatSessionRequestPath = "0009/";

        private static bool m_enabled = false;

        // FreeSwitch Variables
        private static string m_freeSwitchIP;
        private static string m_apiPrefix;
        private static string m_realm;
        private static string m_sipProxy;
        private static bool m_attempt_stun;
        private static string m_echo_server;
        private static int m_echo_port;
        private static int m_default_timeout;

        private static string m_accountService;
        private static int m_accountServicePort;

        private static string m_forcedChannelName = String.Empty;
        private static readonly string EMPTY_RESPONSE = "<llsd><undef /></llsd>";

        private Dictionary<string, string> uuid_to_name = new Dictionary<string, string>();
        private Dictionary<string, string> m_ParcelAddress = new Dictionary<string, string>();

        private IConfig m_config;

        public void Initialize(IConfigSource config)
        {
            m_config = config.Configs["FreeSwitchVoice"];

            if (null == m_config || !m_config.GetBoolean("enabled", false))
            {
                m_log.Info("[FreeSwitchVoice] config missing or disabled, disabling");
                return;
            }

            try
            {
                m_freeSwitchIP = m_config.GetString("freeswitch_well_known_ip", String.Empty);
                m_apiPrefix = m_config.GetString("api_prefix", "/fsapi");
                m_realm = m_config.GetString("realm", m_freeSwitchIP);
                m_sipProxy = m_config.GetString("sip_proxy", m_freeSwitchIP + ":5060");
                m_attempt_stun = m_config.GetBoolean("use_stun", false);
                m_echo_server = m_config.GetString("echo_server", m_freeSwitchIP);
                m_echo_port = m_config.GetInt("echo_port", 50505);
                m_default_timeout = m_config.GetInt("default_timeout", 5000);

                m_accountService = m_config.GetString("account_service", String.Empty);
                m_accountServicePort = m_config.GetInt("account_service_port", 3000);

                if (String.IsNullOrEmpty(m_freeSwitchIP) || String.IsNullOrEmpty(m_accountService))
                {
                    m_log.Error("[FreeSwitchVoice] plugin mis-configured");
                    m_log.Info("[FreeSwitchVoice] plugin disabled: incomplete configuration, freeswitch_well_known_ip is empty or missing");
                    return;
                }

                m_log.InfoFormat("[FreeSwitchVoice] using FreeSwitch server {0}", m_freeSwitchIP);

                m_enabled = true;

                m_log.Info("[FreeSwitchVoice] plugin enabled");
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice] plugin initialization failed: {0}", e.Message);
                m_log.DebugFormat("[FreeSwitchVoice] plugin initialization failed: {0}", e.ToString());
                return;
            }
        }

        public void PostInitialize()
        {
            // Do nothing.
        }

        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                //string channelId;

                //string sceneUUID = String.IsNullOrEmpty(m_forcedChannelName) ? scene.RegionInfo.RegionID.ToString() : m_forcedChannelName;
                //string sceneName = scene.RegionInfo.RegionName;

                scene.EventManager.OnRegisterCaps += delegate (UUID agentID, Caps caps)
                {
                    OnRegisterCaps(scene, agentID, caps);
                };
            }
        }

        public void RemoveRegion(Scene scene)
        {

        }

        public void RegionLoaded(Scene scene)
        {
            // Do nothing.
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "FreeSwitchVoiceModule"; }
        }

        public void Close()
        {
            //if (m_pluginEnabled)
            //    VivoxLogout();
        }

        private void OnRegisterCaps(Scene scene, UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[FreeSwitchVoice] OnRegisterCaps: agentID {0} caps {1}", agentID, caps);

            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("ProvisionVoiceAccountRequest",
                                 new RestStreamHandler("POST", capsBase + m_provisionVoiceAccountRequestPath,
                                                       delegate (string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return ProvisionVoiceAccountRequest(scene, request, path, param,
                                                                                               agentID, caps);
                                                       }));
            caps.RegisterHandler("ParcelVoiceInfoRequest",
                                 new RestStreamHandler("POST", capsBase + m_parcelVoiceInfoRequestPath,
                                                       delegate (string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return ParcelVoiceInfoRequest(scene, request, path, param,
                                                                                         agentID, caps);
                                                       }));
            caps.RegisterHandler("ChatSessionRequest",
                                 new RestStreamHandler("POST", capsBase + m_chatSessionRequestPath,
                                                       delegate (string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return ChatSessionRequest(scene, request, path, param,
                                                                                     agentID, caps);
                                                       }));
        }

        public string ProvisionVoiceAccountRequest(Scene scene, string request, string path, string param,
                                                   UUID agentID, Caps caps)
        {
            m_log.DebugFormat(
                "[FreeSwitchVoice][PROVISIONVOICE]: ProvisionVoiceAccountRequest() request: {0}, path: {1}, param: {2}", request, path, param);

            try
            {
                ScenePresence avatar = null;
                string avatarName = null;
                int avatarWait = 10000;   // milliseconds
                int sleepWait = 100;      // milliseconds

                if (scene == null) throw new Exception("[FreeSwitchVoice][PROVISIONVOICE] Invalid scene");

                avatar = scene.GetScenePresence(agentID);
                while (avatar == null || avatar.IsInTransit)
                {
                    if (avatarWait <= 0)
                    {
                        m_log.WarnFormat("[FreeSwitchVoice][PROVISIONVOICE]: Timeout waiting for agent {0} to enter scene.", agentID);
                        return EMPTY_RESPONSE;
                    }

                    Thread.Sleep(sleepWait);
                    avatarWait -= sleepWait;
                    avatar = scene.GetScenePresence(agentID);
                }

                avatarName = avatar.Name;

                if (!scene.EventManager.TriggerOnBeforeProvisionVoiceAccount(agentID, avatarName))
                {
                    return EMPTY_RESPONSE;
                }

                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: scene = {0}, agentID = {1}", scene, agentID);
                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: request: {0}, path: {1}, param: {2}",
                                  request, path, param);

                string agentname = "x" + Convert.ToBase64String(agentID.GetBytes());
                string password = new UUID(Guid.NewGuid()).ToString().Replace('-', 'Z').Substring(0, 16);
                string code = String.Empty;

                agentname = agentname.Replace('+', '-').Replace('/', '_');

                lock (uuid_to_name)
                {
                    if (uuid_to_name.ContainsKey(agentname))
                    {
                        uuid_to_name[agentname] = avatarName;
                    }
                    else
                    {
                        uuid_to_name.Add(agentname, avatarName);
                    }
                }

                LLSDVoiceAccountResponse voiceAccountResponse =
                    new LLSDVoiceAccountResponse(agentname, password, m_realm, String.Format("http://{0}:{1}{2}/", m_accountService,
                                                              m_accountServicePort, m_apiPrefix));

                string r = LLSDHelpers.SerializeLLSDReply(voiceAccountResponse);

                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: avatar \"{0}\": {1}", avatarName, r);

                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice][PROVISIONVOICE]: : {0}, retry later", e.Message);
                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: : {0} failed", e.ToString());
                return EMPTY_RESPONSE;
            }
        }

        public string ParcelVoiceInfoRequest(Scene scene, string request, string path, string param,
                                             UUID agentID, Caps caps)
        {
            m_log.DebugFormat(
                "[FreeSwitchVoice][PARCELVOICE]: ParcelVoiceInfoRequest() request: {0}, path: {1}, param: {2}", request, path, param);

            ScenePresence avatar = scene.GetScenePresence(agentID);
            if (avatar == null) // seen on the main grid, perhaps on user disconnect or viewer crash
                return EMPTY_RESPONSE;

            string avatarName = avatar.Name;

            // - check whether we have a region channel in our cache
            // - if not: 
            //       create it and cache it
            // - send it to the client
            // - send channel_uri: as "sip:regionID@m_sipDomain"
            try
            {
                LLSDParcelVoiceInfoResponse parcelVoiceInfo;
                string channel_uri;

                if (null == scene.LandChannel)
                    throw new Exception(String.Format("region \"{0}\": avatar \"{1}\": land data not yet available",
                                                      scene.RegionInfo.RegionName, avatarName));

                // If the avatar is in transit between regions, avatar.AbsolutePosition calls below can return undefined values.
                if (avatar.IsInTransit)
                {
                    m_log.WarnFormat("[FreeSwitchVoice][PARCELVOICE]: Cannot process voice info request - avatar {0} is still in transit between regions.", avatarName);
                    return EMPTY_RESPONSE;
                }

                // get channel_uri: check first whether estate
                // settings allow voice, then whether parcel allows
                // voice, if all do retrieve or obtain the parcel
                // voice channel
                Vector3 pos = avatar.AbsolutePosition;  // take a copy to avoid double recalc
                LandData land = scene.GetLandData(pos.X, pos.Y);
                if (land == null)
                {
                    // m_log.DebugFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": avatar\"{1}\" at ({2}): Land parcel not found.",
                    //                scene.RegionInfo.RegionName, avatarName, pos.ToString());
                    return EMPTY_RESPONSE;
                }

                // TODO: EstateSettings don't seem to get propagated...
                if (!scene.RegionInfo.EstateSettings.AllowVoice)
                {
                    m_log.InfoFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": voice not enabled in estate settings",
                                      scene.RegionInfo.RegionName);
                    channel_uri = String.Empty;
                }

                if ((land.Flags & (uint)ParcelFlags.AllowVoiceChat) == 0)
                {
                    m_log.InfoFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": voice not enabled for parcel",
                                      scene.RegionInfo.RegionName, land.Name, land.LocalID, avatarName);
                    channel_uri = String.Empty;
                }
                else
                {
                    channel_uri = ChannelUri(scene, land);
                }

                // fill in our response to the client
                Hashtable creds = new Hashtable();
                creds["channel_uri"] = channel_uri;

                parcelVoiceInfo = new LLSDParcelVoiceInfoResponse(scene.RegionInfo.RegionName, land.LocalID, creds);
                string r = LLSDHelpers.SerializeLLSDReply(parcelVoiceInfo);

                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": avatar \"{1}\": {2} failed",
                                  scene.RegionInfo.RegionName, avatarName, e.Message);
                m_log.ErrorFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": avatar \"{1}\": {2} exception",
                                  scene.RegionInfo.RegionName, avatarName, e.ToString());

                return EMPTY_RESPONSE;
            }
        }

        public string ChatSessionRequest(Scene scene, string request, string path, string param,
                                         UUID agentID, Caps caps)
        {
            return "<llsd>true</llsd>";
        }

        private string ChannelUri(Scene scene, LandData land)
        {
            string channelUri = null;

            string landUUID;
            string landName;

            // Create parcel voice channel. If no parcel exists, then the voice channel ID is the same
            // as the directory ID. Otherwise, it reflects the parcel's ID.

            lock (m_ParcelAddress)
            {
                if (m_ParcelAddress.ContainsKey(land.GlobalID.ToString()))
                {
                    m_log.DebugFormat("[FreeSwitchVoice]: parcel id {0}: using sip address {1}",
                                      land.GlobalID, m_ParcelAddress[land.GlobalID.ToString()]);
                    return m_ParcelAddress[land.GlobalID.ToString()];
                }
            }

            if (land.LocalID != 1 && (land.Flags & (uint)ParcelFlags.UseEstateVoiceChan) == 0)
            {
                landName = String.Format("{0}:{1}", scene.RegionInfo.RegionName, land.Name);
                landUUID = land.GlobalID.ToString();
                m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}",
                                  landName, land.LocalID, landUUID);
            }
            else
            {
                landName = String.Format("{0}:{1}", scene.RegionInfo.RegionName, scene.RegionInfo.RegionName);
                landUUID = scene.RegionInfo.RegionID.ToString();
                m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}",
                                  landName, land.LocalID, landUUID);
            }

            // slvoice handles the sip address differently if it begins with confctl, hiding it from the user in the friends list. however it also disables
            // the personal speech indicators as well unless some siren14-3d codec magic happens. we dont have siren143d so we'll settle for the personal speech indicator.
            channelUri = String.Format("sip:conf-{0}@{1}", "x" + Convert.ToBase64String(Encoding.ASCII.GetBytes(landUUID)), m_realm);

            lock (m_ParcelAddress)
            {
                if (!m_ParcelAddress.ContainsKey(land.GlobalID.ToString()))
                {
                    m_ParcelAddress.Add(land.GlobalID.ToString(), channelUri);
                }
            }

            return channelUri;
        }
    }
}
