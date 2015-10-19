/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Text;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Caps=OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class CapabilitiesModule : INonSharedRegionModule, ICapabilitiesModule
    { 
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_showCapsCommandFormat = "   {0,-38} {1,-60}\n";
        
        protected Scene m_scene;
        
        /// <summary>
        /// Each agent has its own capabilities handler.
        /// </summary>
        private Dictionary<UUID, Caps> m_capsObjects = new Dictionary<UUID, Caps>();

        private object m_syncRoot = new object();

        public void Initialize(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<ICapabilitiesModule>(this);

            MainConsole.Instance.Commands.AddCommand("Comms", false, "show caps",
                "show caps [agentID]",
                "Shows all registered capabilities for one or all users", HandleShowCapsCommand);

            MainConsole.Instance.Commands.AddCommand("Comms", false, "show childseeds",
                "show childseeds [agentID]",
                "Shows all registered childseeds for one or all agents", HandleShowSeedCapsCommand);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.UnregisterModuleInterface<ICapabilitiesModule>(this);
        }
        
        public void PostInitialize() 
        {
        }

        public void Close() {}

        public string Name 
        { 
            get { return "Capabilities Module"; } 
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public ICapsControl CreateCaps(UUID agentId, string capsPath)
        {
            Caps caps = null; 
            lock (m_syncRoot)
            {
                if (m_capsObjects.TryGetValue(agentId, out caps))
                {
                    m_log.WarnFormat("[CAPS]: Recreating caps for agent {0}.  Old caps path {1}, new caps path {2}. ",
                                        agentId, caps.CapsObjectPath, capsPath);
                }

                caps = new Caps(MainServer.Instance, capsPath, agentId, m_scene.RegionInfo.RegionName);
                m_capsObjects[agentId] = caps;
            }

            m_scene.EventManager.TriggerOnRegisterCaps(agentId, caps);

            return new CapsControl(agentId, this);
        }

        public void RemoveCaps(UUID agentId)
        {
            lock (m_syncRoot)
            {
                if (m_capsObjects.ContainsKey(agentId))
                {
                    m_scene.EventManager.TriggerOnDeregisterCaps(agentId, m_capsObjects[agentId]);
                    m_capsObjects[agentId].DeregisterHandlers(); 
                    m_capsObjects.Remove(agentId);
                }
                else
                {
                    m_log.WarnFormat(
                        "[CAPS]: Received request to remove CAPS handler for root agent {0} in {1}, but no such CAPS handler found!",
                        agentId, m_scene.RegionInfo.RegionName);
                }
            }
        }
        
        public Caps GetCapsForUser(UUID agentId)
        {
            lock (m_syncRoot)
            {
                if (m_capsObjects.ContainsKey(agentId))
                {
                    return m_capsObjects[agentId];
                }
            }
            
            return null;
        }
        
        public string GetCapsPath(UUID agentId)
        {
            lock (m_syncRoot)
            {
                Caps userCaps;
                if (m_capsObjects.TryGetValue(agentId, out userCaps))
                {
                    return userCaps.CapsObjectPath;
                }
                else
                {
                    return null;
                }
            }
        }

        private void HandleShowSeedCapsCommand(string module, string[] cmdparams)
        {
            UUID agentID = UUID.Zero;
            StringBuilder caps = new StringBuilder();

            if (cmdparams.Length == 3)
            {
                if (! UUID.TryParse(cmdparams[2], out agentID))
                    MainConsole.Instance.OutputFormat("Usage: show childseeds [agentID]");
            }

            caps.AppendFormat("Region {0} ChildSeeds:\n", m_scene.RegionInfo.RegionName);

            lock (m_syncRoot)
            {
                foreach (KeyValuePair<UUID, Caps> kvp in m_capsObjects)
                {
                    if ((agentID != UUID.Zero) && (agentID != kvp.Key))
                        continue;

                    caps.AppendFormat("** User {0}:\n", kvp.Key);
                }
            }

            MainConsole.Instance.Output(caps.ToString());
        }

        private void HandleShowCapsCommand(string module, string[] cmdparams)
        {
            UUID agentID = UUID.Zero;
            StringBuilder caps = new StringBuilder();

            if (cmdparams.Length == 3)
            {
                if (!UUID.TryParse(cmdparams[2], out agentID))
                    MainConsole.Instance.OutputFormat("Usage: show caps [agentID]");
            }

            caps.AppendFormat("Region {0} Caps:\n", m_scene.RegionInfo.RegionName);

            lock (m_syncRoot)
            {
                foreach (KeyValuePair<UUID, Caps> kvp in m_capsObjects)
                {
                    if ((agentID != UUID.Zero) && (agentID != kvp.Key))
                        continue;

                    caps.AppendFormat("** User {0}:\n", kvp.Key);

                    for (IDictionaryEnumerator kvp2 = kvp.Value.CapsHandlers.GetCapsDetails(false).GetEnumerator(); kvp2.MoveNext(); )
                    {
                        Uri uri = new Uri(kvp2.Value.ToString());
                        caps.AppendFormat(m_showCapsCommandFormat, kvp2.Key, uri.PathAndQuery);
                    }

                }
            }

            MainConsole.Instance.Output(caps.ToString());
        }

    }

}
