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
using System.Net;

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
        private static string m_accountService; // http://ip:port/fsapi

        private static string m_forcedChannelName = String.Empty;
        private static readonly string EMPTY_RESPONSE = "<llsd><undef /></llsd>";

        // not sure why vivox module locks.  serialize calls? no shared stucture used in half of requests
        private static readonly Object vlock = new Object();

        private static Dictionary<string, string> m_parents = new Dictionary<string, string>();

        private IConfig m_config;

        // Freeswitch Config pieces
        private string m_realm = String.Empty;
        private string m_apiPrefix = String.Empty;

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
                m_accountService = m_config.GetString("account_service", String.Empty);
                m_enabled = m_config.GetBoolean("enabled", false);

                if (m_enabled)
                {
                    m_log.InfoFormat("[FreeSwitchVoice] using FreeSwitch MGMT gateway: {0}", m_accountService);
                }
                else
                {
                    m_log.Info("[FreeSwitchVoice] plugin enabled");
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice] plugin initialization failed: {0}", e.Message);
                m_log.DebugFormat("[FreeSwitchVoice] plugin initialization failed: {0}", e.ToString());
                return;
            }

            try
            {
                XmlElement resp = NetworkCall(m_accountService);
                XmlNodeList dirs = resp.GetElementsByTagName("Realm");
                XmlNode node = dirs[0];
                m_log.InfoFormat("[FreeSwitchVoice] using FreeSwitch realm {0}", node.InnerText);
                m_realm = node.InnerText;

                dirs = resp.GetElementsByTagName("APIPrefix");
                node = dirs[0];
                m_log.InfoFormat("[FreeSwitchVoice] using FreeSwitch client endpoint: {0}/{1}", m_realm, node.InnerText);
                m_apiPrefix = node.InnerText;
            }
            catch (Exception e)
            {
                m_enabled = false;
                m_log.ErrorFormat("[FreeSwitchVoice] plugin initialization failed: {0}", e.Message);
                

            }
            
        }

        public void PostInitialize()
        {
            // Do nothing.
        }

        public void AddRegion(Scene scene)
        {
            if (!m_enabled)
            {
                return;
            }
            //string channelId;

            string sceneUUID = String.IsNullOrEmpty(m_forcedChannelName) ? scene.RegionInfo.RegionID.ToString() : m_forcedChannelName;
            string sceneName = scene.RegionInfo.RegionName;

            // get region directory and wipe out all children
            // create directory if not exists
            //if(TryGetDirectory(sceneUUID + "D", out channelId))
            //{
                // TODO: Are we using children?? delete if we are
                //FreeSwitchDialplan.ListChildren(directory);

                // delete all children, we wont be re-using them
            //}
            //else
            //{
            //    TryCreateDirectory(sceneUUID + "D", sceneName, out channelId);

                // TODO: handle errors
            //}

            /*lock (m_parents)
            {
                if (m_parents.ContainsKey(sceneUUID))
                {
                    RemoveRegion(scene);
                    m_parents.Add(sceneUUID, channelId);
                }
                else
                {
                    m_parents.Add(sceneUUID, channelId);
                }
            }*/

            scene.EventManager.OnRegisterCaps += delegate (UUID agentID, Caps caps)
            {
                OnRegisterCaps(scene, agentID, caps);
            };

        }

        public void RegionLoaded(Scene scene)
        {
            // Do nothing.
        }

        public void RemoveRegion(Scene scene)
        {
            // get region directory and wipe out all children
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "FreeSwitchVoiceModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void Close()
        {

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
                string code = String.Empty;

                agentname = agentname.Replace('+', '-').Replace('/', '_');

                UserAccount account = null;
                
                if(GetVoiceAccountInfo(agentname, avatarName, out account))
                {
                    LLSDVoiceAccountResponse voiceAccountResponse =
                    new LLSDVoiceAccountResponse(agentname, account.password, account.realm, String.Format("http://{0}/{1}", m_realm, m_apiPrefix));

                    string r = LLSDHelpers.SerializeLLSDReply(voiceAccountResponse);

                    m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: avatar \"{0}\": {1}", avatarName, r);

                    return r;
                }

                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: Get Account Request failed for \"{0}\"", avatarName);
                throw new Exception("Unable to execute request");
                
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
                    channel_uri = RegionGetOrCreateChannel(scene, land);
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

        private bool TryGetDirectory(string directoryName, out string directoryId)
        {
            m_log.DebugFormat(
                "[FreeSwitchVoice][TryGetDirectory]: directoryName: {0}", directoryName);

            string requrl = String.Format("{0}/getDirectory?channame={1}", m_accountService, directoryName);
            XmlElement resp = NetworkCall(requrl);

            /*
                expected XML response:
                <Result>
                    <Directory>
                        ...
                    </Directory>
                </Result>
            */
            XmlNodeList dirs = resp.GetElementsByTagName("Directory");
            if(dirs.Count == 0)
            {
                directoryId = String.Empty;
                m_log.DebugFormat(
                           "[FreeSwitchVoice][TryGetDirectory]: Directory not found");
                return false;
            }
            XmlNode directory = dirs.Item(0);
            foreach ( XmlNode node in directory.ChildNodes)
            {
                switch (node.Name)
                {
                    case "ID":
                        directoryId = node.InnerText;
                        m_log.DebugFormat(
                            "[FreeSwitchVoice][TryGetDirectory]: Directory found: {0}", directoryId);
                        return true;
                }
            }

            // not there, return negative
            m_log.DebugFormat(
                            "[FreeSwitchVoice][TryGetDirectory]: Directory not found");
            directoryId = String.Empty;
            return false;
        }

        private bool TryCreateDirectory(string directoryId, string description, out string channelId)
        {
            m_log.InfoFormat("[FreeSwitchVoice][TryCreateDirectory]: name \"{0}\", description: \"{1}\"", directoryId, description);
            string requrl = String.Format("{0}/createDirectory?dirid={1}", m_accountService, directoryId);

            if (!String.IsNullOrEmpty(description))
            {
                requrl = String.Format("{0}&chan_desc={1}", requrl, description);
            }
            requrl = String.Format("{0}&chan_type={1}", requrl, "dir");

            XmlElement resp = NetworkCall(requrl);
            XmlNodeList dirs = resp.GetElementsByTagName("Directory");
            if (dirs.Count == 0)
            {
                channelId = String.Empty;
                m_log.DebugFormat(
                           "[FreeSwitchVoice][TryCreateDirectory]: Directory not created");
                return false;
            }

            XmlNode directory = dirs.Item(0);
            foreach (XmlNode node in directory.ChildNodes)
            {
                switch (node.Name)
                {
                    case "ID":
                        channelId = node.InnerText;
                        m_log.DebugFormat(
                            "[FreeSwitchVoice][TryCreateDirectory]: Directory created: {0}", directoryId);
                        return true;
                }
            }

            m_log.DebugFormat(
                            "[FreeSwitchVoice][TryCreateDirectory]: Directory not created...");

            channelId = String.Empty;
            return false;
        }

        private bool TryGetChannel(string channelParent, string channelName, out string channelId, out string channelUri)
        {
            m_log.InfoFormat("[FreeSwitchVoice][TryGetChannel]: parent \"{0}\", name: \"{1}\"", channelParent, channelName);

            string requrl = String.Format("{0}/getChannel?parent={1}&name={2}", m_accountService, channelParent, channelName);
            XmlElement resp = NetworkCall(requrl);

            /*
                expected XML response:
                <Result>
                    <Channel>
                        <ID></ID>
                        <URI></URI>
                        <Name></Name>
                        <Parent></Parent>
                    </Channel>
                </Result>
            */
            XmlNodeList chans = resp.GetElementsByTagName("Channel");
            if (chans.Count == 0)
            {
                channelId = String.Empty;
                m_log.DebugFormat(
                           "[FreeSwitchVoice][TryGetChannel]: Channel not found");
                channelId = String.Empty;
                channelUri = String.Empty;
                return false;
            }

            string id = String.Empty;
            string uri = String.Empty;

            XmlNode channel = chans.Item(0);
            foreach (XmlNode node in channel.ChildNodes)
            {
                switch (node.Name)
                {
                    case "ID":
                        id = node.InnerText;
                        break;
                    case "URI":
                        uri = node.InnerText;
                        break;
                }
            }

            if( !String.IsNullOrEmpty(id) && !String.IsNullOrEmpty(uri))
            {
                m_log.DebugFormat("[FreeSwitchVoice][TryGetChannel]: Channel found: {0}: {1}", id, uri);
                channelId = id;
                channelUri = uri;
                return true;
            }

            m_log.DebugFormat("[FreeSwitchVoice][TryGetChannel]: Channel not found");
            channelId = String.Empty;
            channelUri = String.Empty;
            return false;
        }

        private bool TryCreateChannel(string parent, string channelId, string name, out string channelUri)
        {
            m_log.InfoFormat("[FreeSwitchVoice][TryCreateChannel]: parent \"{0}\", id: \"{1}\", name: \"{2}\"", parent, channelId, name);

            string requrl = String.Format("{0}/createChannel?parent={1}&name={2}&id={3}", m_accountService, parent, name, channelId);
            XmlElement resp = NetworkCall(requrl);

            /*
                expected XML response:
                <Result>
                    <Channel>
                        <ID></ID>
                        <URI></URI>
                        <Name></Name>
                        <Parent></Parent>
                    </Channel>
                </Result>
            */
            XmlNodeList chans = resp.GetElementsByTagName("Channel");
            if (chans.Count == 0)
            {
                channelId = String.Empty;
                m_log.DebugFormat("[FreeSwitchVoice][TryCreateChannel]: Channel not created");
                channelId = String.Empty;
                channelUri = String.Empty;
                return false;
            }

            XmlNode channel = chans.Item(0);
            foreach (XmlNode node in channel.ChildNodes)
            {
                switch (node.Name)
                {
                    case "URI":
                        m_log.DebugFormat("[FreeSwitchVoice][TryCreateChannel]: Channel found: {0}", node.InnerText);
                        channelUri = node.InnerText;
                        return true;
                }
            }


            m_log.DebugFormat("[FreeSwitchVoice][TryCreateChannel]: Channel not created");
            channelUri = String.Empty;
            return false;
        }

        private bool GetVoiceAccountInfo(string user, string name, out UserAccount account)
        {
            m_log.InfoFormat("[FreeSwitchVoice][GetVoiceAccountInfo]: user \"{0}\"", user);
            string requrl = String.Format("{0}/getAccountInfo?user={1}&name={2}", m_accountService, user, name.Replace(' ','_'));

            /*
            <Result>
                <Account>
                    <UserID> </UserID>
                    <Password> </Password>
                    <Realm> </Realm>
                </Account>
            </Result>    
            */
            XmlElement resp = NetworkCall(requrl);
            m_log.DebugFormat("[FreeSwitchVoice][GetVoiceAccountInfo]: {0}", resp.OuterXml);
            XmlNodeList accounts = resp.GetElementsByTagName("Account");
            if (accounts.Count == 0)
            {
                account = null;
                m_log.DebugFormat(
                           "[FreeSwitchVoice][GetVoiceAccountInfo]: No Accounts found");
                return false;
            }

            XmlNode acc = accounts.Item(0);
            string realm = String.Empty;
            string password = String.Empty;
            foreach (XmlNode node in acc.ChildNodes)
            {
                m_log.DebugFormat(
                           "[FreeSwitchVoice][GetVoiceAccountInfo]: node: {0}:{1}", node.Name, node.InnerText);

                switch (node.Name)
                {
                    case "Password":
                        password = node.InnerText;
                        break;
                    case "Realm":
                        realm = node.InnerText;
                        break;
                }
            }

            if(!String.IsNullOrEmpty(realm) || !String.IsNullOrEmpty(password))
            {
                account = new UserAccount(user, password, realm);
                return true;
            }

            account = null;
            m_log.DebugFormat(
                       "[FreeSwitchVoice][GetVoiceAccountInfo]: Account missing or incomplete. Password: {0}, Realm: {1}", password, realm);
            return false;
        }

        private string RegionGetOrCreateChannel(Scene scene, LandData land)
        {
            string channelUri = null;
            //string channelId = null;

            string landUUID;
            string landName;
            //string parentId;

            //lock (m_parents) parentId = String.IsNullOrEmpty(m_forcedChannelName) ? m_parents[scene.RegionInfo.RegionID.ToString()] : m_parents[m_forcedChannelName];

            // Create parcel voice channel. If no parcel exists, then the voice channel ID is the same
            // as the directory ID. Otherwise, it reflects the parcel's ID.

            if (land.LocalID != 1 && (land.Flags & (uint)ParcelFlags.UseEstateVoiceChan) == 0)
            {
                landName = String.Format("{0}:{1}", scene.RegionInfo.RegionName, land.Name);
                landUUID = land.GlobalID.ToString();
                //m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}", 
                //                  landName, land.LocalID, landUUID);
            }
            else
            {
                landName = String.Format("{0}:{1}", scene.RegionInfo.RegionName, scene.RegionInfo.RegionName);
                landUUID = String.IsNullOrEmpty(m_forcedChannelName) ? scene.RegionInfo.RegionID.ToString() : m_forcedChannelName;
                //m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}", 
                //                  landName, land.LocalID, landUUID);
            }

            // abandon this for now.  We can try to fix it up once we have a partial solution
            // MGM does not persist channel settings anyways at this point.
            //lock (vlock)
            //{
            // Added by Adam to help debug channel not availible errors.
            //    if (!TryGetChannel(parentId, landUUID, out channelId, out channelUri))
            //    {
            //        if (!TryCreateChannel(parentId, landUUID, landName, out channelUri))
            //            throw new Exception("freeswitch channel uri not available");
            //else
            //    m_log.DebugFormat("[FreeSwitchVoice] Created new channel at " + channelUri);
            //    }
            //else 
            //    m_log.DebugFormat("[FreeSwitchVoice] Found existing channel at " + channelUri);

            //m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parent channel id {1}: retrieved parcel channel_uri {2} ", 
            //                  landName, parentId, channelUri);


            //}
            channelUri = String.Format("sip:conf-{0}@{1}", "x" + Convert.ToBase64String(Encoding.ASCII.GetBytes(landUUID)), m_realm);
            return channelUri;
        }

        private XmlElement NetworkCall(string requrl)
        {

            XmlDocument doc = null;

            doc = new XmlDocument();

            try
            {
                // Otherwise prepare the request
                // m_log.DebugFormat("[FreeSwitchVoice] Sending request <{0}>", requrl);

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(requrl);
                HttpWebResponse rsp = null;

                // We are sending just parameters, no content
                req.ContentLength = 0;

                // Send request and retrieve the response
                rsp = (HttpWebResponse)req.GetResponse();

                XmlTextReader rdr = new XmlTextReader(rsp.GetResponseStream());
                doc.Load(rdr);
                rdr.Close();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice] Error in network call to {1}: {0}", e.Message, requrl);
            }

            return doc.DocumentElement;
        }
    }

    public class UserAccount
    {
        public string userID { get; private set; }
        public string password { get; private set; }
        public string realm { get; private set;  }

        public UserAccount(string userID, string password, string realm)
        {
            this.userID = userID;
            this.password = password;
            this.realm = realm;
        }
    }
}
