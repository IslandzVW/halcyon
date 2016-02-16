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
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using Nini.Config;
using Mono.Addins;

//using Capabilities = OpenSim.Framework.Communications.Capabilities;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.World.LightShare
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EnvironmentModule")]

    public class EnvironmentModule : INonSharedRegionModule, IEnvironmentModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene = null;
        private UUID regionID = UUID.Zero;
        private static bool m_isEnabled = true;

        private static readonly string capsName = "EnvironmentSettings";
        private static readonly string capsBase = "/CAPS/0020/";

        // in-memory version to avoid fetching from db all the time.
        private string environString = null;

        #region INonSharedRegionModule
        public void Initialize(IConfigSource source)
        {
            m_isEnabled = true;
            IConfig config = source.Configs["Environment"];

            if (config != null)
                m_isEnabled = config.GetBoolean("Enabled", m_isEnabled);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EnvironmentModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_isEnabled)
                return;

            scene.RegisterModuleInterface<IEnvironmentModule>(this);
            m_scene = scene;
            regionID = scene.RegionInfo.RegionID;

            GetEnvironmentSettings(UUID.Zero);  // initialize the in-memory settings/cached copy.
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_isEnabled)
                return;

            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_isEnabled)
                return;

            scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene = null;
        }
        #endregion

        #region IEnvironmentModule
        public void ResetEnvironmentSettings(UUID regionUUID)
        {
            if (!m_isEnabled)
                return;

            SetEnvironmentSettings(EnvironmentSettings.EmptySettings(UUID.Zero, regionID), UUID.Zero);
//            m_scene.SimulationDataService.RemoveRegionEnvironmentSettings(regionUUID);
        }
        #endregion

        #region Events
        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            //            m_log.DebugFormat("[{0}]: Register capability for agentID {1} in region {2}",
            //                Name, agentID, caps.RegionName);

            string capsPath = capsBase + UUID.Random();

            // Get handler
            caps.RegisterHandler(
                capsName,
                new RestStreamHandler(
                    "GET",
                    capsPath,
                    delegate(string request, string path, string param,
                            OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                    {
                        return GetEnvironmentSettings(agentID);
                    }));


            // Set handler
            caps.RegisterHandler(
                capsName,
                new RestStreamHandler(
                    "POST",
                    capsPath,
                    delegate(string request, string path, string param,
                            OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                    {
                        string result = SetEnvironmentSettings(request, agentID);
                        if (result == null)
                            return String.Empty;
                        return result;
                    }));
        }
        #endregion

        private string GetEnvironmentSettings(UUID agentID)
        {
            m_log.WarnFormat("[{0}]: Environment GET handler for agentID {1}", Name, agentID);

            string response = String.Empty;

            // Check if we have cached the environment settings.
            if (environString == null)
            {
                // We'll need to fetch it from the db.
                try
                {
                    response = m_scene.StorageManager.DataStore.LoadRegionEnvironmentString(regionID);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[{0}]: Unable to load region environment settings, exception: {1} - {2}",
                        Name, e.Message, e.StackTrace);
                    response = String.Empty;
                }
                environString = response;
            }

            // Do we have environment settings, from storage or from cache?
            if (!String.IsNullOrEmpty(environString))
                return environString;

            // No environment settings.
            return EnvironmentSettings.EmptySettings(UUID.Zero, regionID);
        }

        private string SetEnvironmentSettings(string request, UUID agentID)
        {
            LLSDEnvironmentSetResponse setResponse = new LLSDEnvironmentSetResponse();

            m_log.WarnFormat("[{0}]: Environment POST handler for agentID {1}", Name, agentID);
            setResponse.regionID = regionID;
            setResponse.success = false;

            if (!m_scene.Permissions.CanIssueEstateCommand(agentID, false))
            {
                setResponse.fail_reason = "Insufficient estate permissions, settings has not been saved.";
                return LLSDHelpers.SerializeLLSDReply(setResponse);
            }

            try
            {
                m_scene.StorageManager.DataStore.StoreRegionEnvironmentString(regionID, request);
                setResponse.success = true;
                environString = request;
                m_log.InfoFormat("[{0}]: Environment settings updated by user {1}", Name, agentID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: Environment settings not been saved for user {1}, Exception: {2} - {3}",
                    Name, agentID, e.Message, e.StackTrace);

                setResponse.success = false;
                setResponse.fail_reason = String.Format("Environment settings for '{0}' could not be saved.", m_scene.RegionInfo.RegionName);
            }

            return LLSDHelpers.SerializeLLSDReply(setResponse);
        }
    }
}
