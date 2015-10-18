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
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Capabilities
{
    /// <summary>
    /// Seed Caps capability.
    /// </summary>
    /// <remarks>
    /// Handles the request by  client to fetch caps from a list available.
    /// This doesnt check the request, just returns what we know about.
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class SeedCapModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string m_requestPath = "0000/";
        private Scene m_scene;

        #region ISharedRegionModule Members

        public void Initialize(IConfigSource source)
        {
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
        }

        public void RegionLoaded(Scene s)
        {
        }

        public void PostInitialize()
        {
        }

        public void Close() 
        { 
        }

        public string Name 
        { 
            get { return "SeedCapModule"; } 
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion


        public void RegisterCaps(UUID agentID, Caps caps)
        {
            SeedCapsHandler handler = new SeedCapsHandler(m_scene, agentID, caps);
            IRequestHandler reqHandler = new RestStreamHandler("POST", caps.CapsBase + m_requestPath, handler.SeedCapRequest);
            caps.RegisterHandler("SEED", reqHandler); 

            m_log.DebugFormat("[CAPS]: Registered seed capability {0} for {1}", caps.CapsBase + m_requestPath, agentID);
        }

        private class SeedCapsHandler
        {
            private Caps m_Caps;
            private Scene m_Scene;

            public SeedCapsHandler(Scene scene, UUID agentID, Caps caps)
            {
                m_Caps = caps;
                m_Scene = scene;
            }

            /// <summary>
            /// Construct a client response detailing all the capabilities this server can provide.
            /// </summary>
            /// <param name="request"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <param name="httpRequest">HTTP request header object</param>
            /// <param name="httpResponse">HTTP response header object</param>
            /// <returns></returns>
            public string SeedCapRequest(string request, string path, string param,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                // m_log.DebugFormat("[CAPS]: Seed CAPS Request in region {0} = {1}", m_regionName, request);

                Hashtable capsDetails = m_Caps.CapsHandlers.GetCapsDetails(true);

                string result = LLSDHelpers.SerializeLLSDReply(capsDetails);

                // m_log.DebugFormat("[CAPS] CapsRequest {0}", result);

                return result;
            }

        }
    }
}
