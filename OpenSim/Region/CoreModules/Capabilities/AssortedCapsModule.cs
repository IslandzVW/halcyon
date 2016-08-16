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
using System.IO;
using System.Reflection;

using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class AssortedCapsModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;

        #region IRegionModuleBase Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialize(IConfigSource source)
        {
        }

        public void AddRegion(Scene pScene)
        {
            m_Scene = pScene;
            m_Scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_Scene.EventManager.OnDeregisterCaps += DeregisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
            m_Scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_Scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            m_Scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void Close() 
        { 
        }

        public string Name 
        { 
            get { return "AssortedCapsModule"; } 
        }

        #endregion

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            string homeLocationCap = CapsUtil.CreateCAPS("HomeLocation", String.Empty);

            // OpenSimulator CAPs infrastructure seems to be somewhat hostile towards any CAP that requires both GET
            // and POST handlers, so we first set up a POST handler normally and then add a GET/PUT handler via MainServer

            IRequestHandler homeLocationRequestHandler
                = new RestStreamHandler(
                    "POST", homeLocationCap,
                    (request, path, param, httpRequest, httpResponse) => HomeLocation(request, agentID),
                    "HomeLocation", null);

            MainServer.Instance.AddStreamHandler(homeLocationRequestHandler);
            caps.RegisterHandler("HomeLocation", homeLocationRequestHandler);
        }

        public void EnteringRegion()
        {
        }

        public void DeregisterCaps(UUID agentID, Caps caps)
        {
//            m_service.RemoveStreamHandler("HomeLocation", "POST");
        }

        #region Other CAPS

        string HomeLocation(string request, UUID agentID)
        {
            OSDMap rm = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap homeLocation = rm["HomeLocation"] as OSDMap;
            if (homeLocation != null)
            {
                OSDMap locationPosMap = homeLocation["LocationPos"] as OSDMap;
                Vector3 position = new Vector3(
                                       (float)locationPosMap["X"].AsReal(),
                                       (float)locationPosMap["Y"].AsReal(),
                                       (float)locationPosMap["Z"].AsReal());
                OSDMap locationLookAtMap = homeLocation["LocationLookAt"] as OSDMap;
                Vector3 lookAt = new Vector3(
                                     (float)locationLookAtMap["X"].AsReal(),
                                     (float)locationLookAtMap["Y"].AsReal(),
                                     (float)locationLookAtMap["Z"].AsReal());
                uint locationId = homeLocation["LocationId"].AsUInteger();

                var SP = m_Scene.GetScenePresence(agentID);
                var regionHandle = m_Scene.RegionInfo.RegionHandle;

                if (SP != null)
                {
                    m_Scene.SetHomeRezPoint(SP.ControllingClient, regionHandle, position, lookAt, locationId);
                }
            }

            rm.Add("success", OSD.FromBoolean(true));
            return OSDParser.SerializeLLSDXmlString(rm);
        }


        #endregion
    }
} 
