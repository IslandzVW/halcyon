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
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;

using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace MOSES.FreeswitchVoice
{
    public class FreeswitchVoiceModule : ISharedRegionModule
    {

        public void Initialize(IConfigSource config)
        {

        }

        // Called to indicate that the module has been added to the region
        public void AddRegion(Scene scene)
        {

        }
        
        // Called to indicate that all loadable modules have now been added
        public void RegionLoaded(Scene scene)
        {
            // Do nothing.
        }

        // Called to indicate that the region is going away.
        public void RemoveRegion(Scene scene)
        {
        
	}

        public void PostInitialize()
        {
            // Do nothing.
        }

        public void Close()
        {

        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "FreeswitchVoiceModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        // <summary>
        // OnRegisterCaps is invoked via the scene.EventManager
        // everytime OpenSim hands out capabilities to a client
        // (login, region crossing). We contribute two capabilities to
        // the set of capabilities handed back to the client:
        // ProvisionVoiceAccountRequest and ParcelVoiceInfoRequest.
        // 
        // ProvisionVoiceAccountRequest allows the client to obtain
        // the voice account credentials for the avatar it is
        // controlling (e.g., user name, password, etc).
        // 
        // ParcelVoiceInfoRequest is invoked whenever the client
        // changes from one region or parcel to another.
        //
        // Note that OnRegisterCaps is called here via a closure
        // delegate containing the scene of the respective region (see
        // Initialize()).
        // </summary>
        public void OnRegisterCaps(Scene scene, UUID agentID, Caps caps)
        {

        }

        /// <summary>
        /// Callback for a client request for Voice Account Details
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ProvisionVoiceAccountRequest(Scene scene, string request, string path, string param,
                                                   UUID agentID, Caps caps)
        {
        	return "";
        }

        /// <summary>
        /// Callback for a client request for ParcelVoiceInfo
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ParcelVoiceInfoRequest(Scene scene, string request, string path, string param,
                                             UUID agentID, Caps caps)
        {
            return "";
        }


        /// <summary>
        /// Callback for a client request for a private chat channel
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ChatSessionRequest(Scene scene, string request, string path, string param,
                                         UUID agentID, Caps caps)
        {
            ScenePresence avatar = scene.GetScenePresence(agentID);
            string        avatarName = avatar.Name;

            //m_log.DebugFormat("[VivoxVoice][CHATSESSION]: avatar \"{0}\": request: {1}, path: {2}, param: {3}",
            //                  avatarName, request, path, param);
            return "<llsd>true</llsd>";
        }


        private string RegionGetOrCreateChannel(Scene scene, LandData land)
        {
            return "";
        }


        private static readonly string m_vivoxLoginPath = "http://{0}/api2/viv_signin.php?userid={1}&pwd={2}";

        private static readonly string m_vivoxLogoutPath = "http://{0}/api2/viv_signout.php?auth_token={1}";

        private static readonly string m_vivoxGetAccountPath = "http://{0}/api2/viv_get_acct.php?auth_token={1}&user_name={2}";

        private static readonly string m_vivoxNewAccountPath = "http://{0}/api2/viv_adm_acct_new.php?username={1}&pwd={2}&auth_token={3}";

        private static readonly string m_vivoxPasswordPath = "http://{0}/api2/viv_adm_password.php?user_name={1}&new_pwd={2}&auth_token={3}";

        private static readonly string m_vivoxChannelPath = "http://{0}/api2/viv_chan_mod.php?mode={1}&chan_name={2}&auth_token={3}";

        private static readonly string m_vivoxChannelSearchPath = "http://{0}/api2/viv_chan_search.php?cond_channame={1}&auth_token={2}";

        private static readonly string m_vivoxChannelDel = "http://{0}/api2/viv_chan_mod.php?mode={1}&chan_id={2}&auth_token={3}";

        private static readonly string m_vivoxChannelSearch = "http://{0}/api2/viv_chan_search.php?&cond_chanparent={1}&auth_token={2}";

        private static readonly char[] C_POINT = {'.'};

    }
}
