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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Nini.Config;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using OpenSim.Data;
using OpenSim.Framework.Communications.Cache;
using CSJ2K;

// using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Framework.Communications.Capabilities
{
    public class Caps
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This is the uuid portion of every CAPS path.  It is used to make capability urls private to the requester.
        /// </summary>
        private string m_capsObjectPath;
        private CapsHandlers m_capsHandlers;
        private IHttpServer m_httpListener;
        private UUID m_agentID;
        private string m_regionName;

        public string CapsObjectPath 
        { 
            get { return m_capsObjectPath; } 
        }

        public string CapsBase
        {
            get { return ("/CAPS/" + CapsObjectPath); }
        }

        public UUID AgentID
        {
            get { return m_agentID; }
        }

        public string RegionName
        {
            get { return m_regionName; }
        }

        public string HostName
        {
            get { return m_httpListener.FullHostName; }
        }

        public uint Port
        {
            get { return m_httpListener.Port; }
        }

        public IHttpServer HttpListener
        {
            get { return m_httpListener; }
        }
        
        public bool SSLCaps
        {
            get { return m_httpListener.Secure; }
        }

        public CapsHandlers CapsHandlers
        {
            get { return m_capsHandlers; }
        }

        public Caps(IHttpServer httpServer, string capsPath, UUID agent, string regionName)
        {
            m_capsObjectPath = capsPath;
            m_httpListener = httpServer;
            m_agentID = agent;
            m_regionName = regionName;
            m_capsHandlers = new CapsHandlers(httpServer);
        }

        /// <summary>
        /// Register a handler.  This allows modules to register handlers.
        /// </summary>
        /// <param name="capName"></param>
        /// <param name="handler"></param>
        public void RegisterHandler(string capName, IRequestHandler handler)
        {
            m_capsHandlers[capName] = new CapsHandler(capName, handler, false);
            //m_log.DebugFormat("[CAPS]: Registering handler for \"{0}\": path {1}", capName, handler.Path);
        }

        /// <summary>
        /// Register an external handler. The service for this capability is somewhere else
        /// given by the URL.
        /// </summary>
        /// <param name="capsName"></param>
        /// <param name="url"></param>
        public void RegisterHandler(string capsName, string url)
        {
            m_capsHandlers[capsName] = new CapsHandler(capsName, url, false);
        }

        /// <summary>
        /// Registers an external handler that also can respond to pause and resume commands
        /// </summary>
        /// <param name="capsName"></param>
        /// <param name="url"></param>
        /// <param name="pauseTrafficHandler"></param>
        /// <param name="resumeTrafficHandler"></param>
        public void RegisterHandler(string capsName, string url, Action pauseTrafficHandler, 
            Action resumeTrafficHandler, Action<int> setMaxBandwidthHandler)
        {
            var handler = new CapsHandler(capsName, url, true);
            handler.PauseTrafficHandler = pauseTrafficHandler;
            handler.ResumeTrafficHandler = resumeTrafficHandler;
            handler.MaxBandwidthHandler = setMaxBandwidthHandler;

            m_capsHandlers[capsName] = handler;
        }

        /// <summary>
        /// Remove a named caps handler
        /// </summary>
        /// <param name="capsname"></param>
        public void DeregisterHandler(string capsname)
        {
            if (m_capsHandlers.ContainsCap(capsname))
                m_capsHandlers.Remove(capsname);

            //m_log.DebugFormat("[CAPS]: Deregistering handler for {0}", capName);
        }

        /// <summary>
        /// Remove all CAPS service handlers.
        ///
        /// </summary>
        /// <param name="httpListener"></param>
        /// <param name="path"></param>
        /// <param name="restMethod"></param>
        public void DeregisterHandlers()
        {
            if (m_capsHandlers != null)
            {
                foreach (string capsName in m_capsHandlers.Caps)
                {
                    m_capsHandlers.Remove(capsName);
                }
            }
        }


        public void PauseTraffic()
        {
            foreach (string capsName in m_capsHandlers.Caps)
            {
                var handler = m_capsHandlers[capsName];
                handler.PauseTraffic();
            }
        }

        public void ResumeTraffic()
        {
            foreach (string capsName in m_capsHandlers.Caps)
            {
                var handler = m_capsHandlers[capsName];
                handler.ResumeTraffic();
            }
        }

        public void SetMaxBandwidth(int bwMax)
        {
            foreach (string capsName in m_capsHandlers.Caps)
            {
                var handler = m_capsHandlers[capsName];
                handler.SetMaxBandwidth(bwMax);
            }
        }
    }
}
