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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using OSD = OpenMetaverse.StructuredData.OSD;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class AgentPreferencesModule : ISharedRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        #region ISharedRegionModule Members

        public void Initialize(IConfigSource source)
        {

        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
        }

        public void RegionLoaded(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void PostInitialize()
        {
        }

        public void Close()
        { 
        }

        public string Name
        {
            get { return "AgentPreferencesModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region AgentPrefs Storage

        private AgentPreferencesData FetchAgentPrefs(UUID agent)
        {
            AgentPreferencesData data = null;
            if (agent != UUID.Zero)
            {
                ScenePresence sp = m_scene.GetScenePresence(agent);
                if (sp != null)
                    data = new AgentPreferencesData(sp.AgentPrefs);
            }

            if (data == null)
                data = new AgentPreferencesData();
            
            return data;
        }

        private void StoreAgentPrefs(UUID agent, AgentPreferencesData data)
        {
            ScenePresence sp = m_scene.GetScenePresence(agent);
            if (sp != null)
                sp.AgentPrefs = data;
        }

        #endregion

        #region Region module

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capId = UUID.Random();
            caps.RegisterHandler("AgentPreferences",
                new RestStreamHandler("POST", "/CAPS/" + capId,
                    delegate(string request, string path, string param,
                        OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                    {
                        return UpdateAgentPreferences(request, path, param, agentID);
                    }));
            caps.RegisterHandler("UpdateAgentLanguage",
                new RestStreamHandler("POST", "/CAPS/" + capId,
                    delegate(string request, string path, string param,
                        OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                    {
                        return UpdateAgentPreferences(request, path, param, agentID);
                    }));
            caps.RegisterHandler("UpdateAgentInformation",
                new RestStreamHandler("POST", "/CAPS/" + capId,
                    delegate(string request, string path, string param,
                        OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                    {
                        return UpdateAgentPreferences(request, path, param, agentID);
                    }));
        }

        public string UpdateAgentPreferences(string request, string path, string param, UUID agent)
        {
            OSDMap resp = new OSDMap();
            // The viewer doesn't do anything with the return value. It never fetches these, so there's no need to persist them.
            // We'll store them for the session with the SP so that the values are available, e.g. to llGetAgentLanguage

            m_log.DebugFormat("[AgentPrefs]: UpdateAgentPreferences for {0}", agent.ToString());
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);

            AgentPreferencesData data = FetchAgentPrefs(agent);

            if (req.ContainsKey("access_prefs"))
            {
                OSDMap accessPrefs = (OSDMap)req["access_prefs"];  // We could check with ContainsKey...
                data.AccessPrefs = accessPrefs["max"].AsString();
            }
            if (req.ContainsKey("default_object_perm_masks"))
            {
                OSDMap permsMap = (OSDMap)req["default_object_perm_masks"];
                data.PermEveryone = permsMap["Everyone"].AsUInteger();
                data.PermGroup = permsMap["Group"].AsUInteger();
                data.PermNextOwner = permsMap["NextOwner"].AsUInteger();
            }
            if (req.ContainsKey("hover_height"))
            {
                data.HoverHeight = (float)req["hover_height"].AsReal();
            }
            if (req.ContainsKey("language"))
            {
                data.Language = req["language"].AsString();
            }
            if (req.ContainsKey("language_is_public"))
            {
                data.LanguageIsPublic = req["language_is_public"].AsBoolean();
            }

            StoreAgentPrefs(agent, data);

            OSDMap respAccessPrefs = new OSDMap();
            respAccessPrefs["max"] = data.AccessPrefs;
            resp["access_prefs"] = respAccessPrefs;
            OSDMap respDefaultPerms = new OSDMap();
            respDefaultPerms["Everyone"] = data.PermEveryone;
            respDefaultPerms["Group"] = data.PermGroup;
            respDefaultPerms["NextOwner"] = data.PermNextOwner;
            resp["default_object_perm_masks"] = respDefaultPerms;
            resp["god_level"] = 0; // *TODO: Add this
            resp["hover_height"] = data.HoverHeight;
            resp["language"] = data.Language;
            resp["language_is_public"] = data.LanguageIsPublic;

            return OSDParser.SerializeLLSDXmlString(resp);
        }

        #endregion Region module
    }
}
