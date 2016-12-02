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
using System.Net.Security;
using System.Web;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using System.Text.RegularExpressions;


namespace MOSES.FreeSwitchVoice
{
    public class FreeSwitchVoiceModule : ISharedRegionModule
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Capability strings
        private static readonly string m_parcelVoiceInfoRequestPath = "0007/";
        private static readonly string m_provisionVoiceAccountRequestPath = "0008/";
        private static readonly string m_chatSessionRequestPath = "0009/";

        private static bool m_pluginEnabled = false;

        private static string m_freeSwitchIP;
        private static string m_apiPrefix;
        private static string m_realm;
        private static string m_sipProxy;
        private static bool m_attempt_stun;
        private static string m_echo_server;
        private static int m_echo_port;
        private static int m_default_timeout;

        private Dictionary<string, string> id_to_name = new Dictionary<string, string>();

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

                if (String.IsNullOrEmpty(m_freeSwitchIP))
                {
                    m_log.Error("[FreeSwitchVoice] plugin mis-configured");
                    m_log.Info("[FreeSwitchVoice] plugin disabled: incomplete configuration, freeswitch_well_known_ip is empty or missing");
                    return;
                }

                m_log.InfoFormat("[FreeSwitchVoice] using FreeSwitch server {0}", m_freeSwitchIP);

                // Get admin rights and cleanup any residual channel definition

                m_pluginEnabled = true;

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
    }
}
