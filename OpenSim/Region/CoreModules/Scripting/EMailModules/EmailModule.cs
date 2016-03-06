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
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DotNetOpenMail;
using DotNetOpenMail.SmtpAuth;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Scripting.EmailModules
{
    public class EmailModule : IEmailModule
    {
        //
        // Log
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private IConfigSource m_Config;
        private string m_HostName = String.Empty;
        //private string m_RegionName = String.Empty;
        private string SMTP_SERVER_HOSTNAME = String.Empty;
        private int SMTP_SERVER_PORT = 25;
        private string SMTP_SERVER_LOGIN = String.Empty;
        private string SMTP_SERVER_PASSWORD = String.Empty;

        private Dictionary<UUID, DateTime> m_LastGetEmailCall = new Dictionary<UUID, DateTime>();
        private TimeSpan m_QueueTimeout = new TimeSpan(2, 0, 0); // 2 hours without llGetNextEmail drops the queue
        private string m_InterObjectHostname = "lsl.inworldz.local";

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes =
            new Dictionary<ulong, Scene>();

        private bool m_Enabled = false;

        public void Initialize(Scene scene, IConfigSource config)
        {
            m_Config = config;
            IConfig SMTPConfig;

            //FIXME: RegionName is correct??
            //m_RegionName = scene.RegionInfo.RegionName;

            IConfig startupConfig = m_Config.Configs["Startup"];

            m_Enabled = (startupConfig.GetString("emailmodule", "DefaultEmailModule") == "DefaultEmailModule");

            //Load SMTP SERVER config
            try
            {
                if ((SMTPConfig = m_Config.Configs["SMTP"]) == null)
                {
                    m_log.InfoFormat("[SMTP]: SMTP server not configured");
                    m_Enabled = false;
                    return;
                }

                if (!SMTPConfig.GetBoolean("enabled", false))
                {
                    m_log.InfoFormat("[SMTP]: module disabled in configuration");
                    m_Enabled = false;
                    return;
                }

                m_HostName = SMTPConfig.GetString("host_domain_header_from", m_HostName);
                m_InterObjectHostname = SMTPConfig.GetString("internal_object_host", m_InterObjectHostname);
                SMTP_SERVER_HOSTNAME = SMTPConfig.GetString("SMTP_SERVER_HOSTNAME", SMTP_SERVER_HOSTNAME);
                SMTP_SERVER_PORT = SMTPConfig.GetInt("SMTP_SERVER_PORT", SMTP_SERVER_PORT);
                SMTP_SERVER_LOGIN = SMTPConfig.GetString("SMTP_SERVER_LOGIN", SMTP_SERVER_LOGIN);
                SMTP_SERVER_PASSWORD = SMTPConfig.GetString("SMTP_SERVER_PASSWORD", SMTP_SERVER_PASSWORD);
            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL]: DefaultEmailModule not configured: "+ e.Message);
                m_Enabled = false;
                return;
            }

            // It's a go!
            if (m_Enabled)
            {
                lock (m_Scenes)
                {
                    // Claim the interface slot
                    scene.RegisterModuleInterface<IEmailModule>(this);

                    // Add to scene list
                    if (m_Scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    {
                        m_Scenes[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_Scenes.Add(scene.RegionInfo.RegionHandle, scene);
                    }
                }

                m_log.Info("[EMAIL]: Activated DefaultEmailModule");
            }
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "DefaultEmailModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        /// <summary>
        /// Delay function using thread in seconds
        /// </summary>
        /// <param name="seconds"></param>
        private void DelayInSeconds(int delay)
        {
            delay = (int)((float)delay * 1000);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        // This function returns the first region name found in ObjectRegionName even if the object is not found.
        private SceneObjectPart findPrim(UUID objectID, out string ObjectRegionName)
        {
            ObjectRegionName = String.Empty;
            lock (m_Scenes)
            {
                foreach (Scene s in m_Scenes.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if ((part != null) || (ObjectRegionName == String.Empty))
                    {
                        ObjectRegionName = s.RegionInfo.RegionName;
                        uint localX = (s.RegionInfo.RegionLocX * 256);
                        uint localY = (s.RegionInfo.RegionLocY * 256);
                        ObjectRegionName = ObjectRegionName + " (" + localX + ", " + localY + ")";
                        if (part != null)
                            return part;
                    }
                }
            }
            return null;
        }

        private bool resolveNamePositionRegionName(UUID objectID, out string ObjectName, out string ObjectAbsolutePosition, out string ObjectRegionName)
        {
            SceneObjectPart part = findPrim(objectID, out ObjectRegionName);
            // ObjectRegionName is initialized by findPrim either way.
            if (part == null)
            {
                lock (m_Scenes)
                {
                    ObjectName = "Object " + objectID.ToString();
                    ObjectAbsolutePosition = Vector3.Zero.ToString();
                }
                return false;
            }

            ObjectAbsolutePosition = String.Format("({0},{1},{2})", (int)part.AbsolutePosition.X, (int)part.AbsolutePosition.Y, (int)part.AbsolutePosition.Z);
            ObjectName = part.Name;
            return true;
        }

        /// <summary>
        /// SendMail function utilized by llEMail
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="address"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        public void SendEmail(UUID objectID, string address, string subject, string body)
        {
            //Check if address is empty
            if (String.IsNullOrWhiteSpace(address))
                return;

            //FIXED:Check the email is correct form in REGEX
            string EMailpatternStrict = @"^(([^<>()[\]\\.,;:\s@\""]+"
                + @"(\.[^<>()[\]\\.,;:\s@\""]+)*)|(\"".+\""))@"
                + @"((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}"
                + @"\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+"
                + @"[a-zA-Z]{2,}))$";
            Regex EMailreStrict = new Regex(EMailpatternStrict);
            bool isEMailStrictMatch = EMailreStrict.IsMatch(address);
            if (!isEMailStrictMatch)
            {
                m_log.Error("[EMAIL]: REGEX Problem in EMail Address: "+address);
                return;
            }

            string LastObjectName = String.Empty;
            string LastObjectPosition = String.Empty;
            string LastObjectRegionName = String.Empty;

            try
            {
                //Creation EmailMessage
                EmailMessage emailMessage = new EmailMessage();
                //From
                emailMessage.FromAddress = new EmailAddress(objectID.ToString() + "@" + m_HostName);
                //To - Only One
                emailMessage.AddToAddress(new EmailAddress(address));
                //Subject
                emailMessage.Subject = subject;
                //TEXT Body
                if (!resolveNamePositionRegionName(objectID, out LastObjectName, out LastObjectPosition, out LastObjectRegionName))
                    m_log.WarnFormat("[EMAIL]: Could not find sending object {0} in region.", objectID);
                emailMessage.BodyText = "Object-Name: " + LastObjectName +
                            "\nRegion: " + LastObjectRegionName + "\nLocal-Position: " +
                            LastObjectPosition + "\n\n" + body;

                int len = emailMessage.BodyText.Length;
                if ((address.Length + subject.Length + len) > 4096)
                {
                    len -= address.Length;
                    len -= subject.Length;
                    emailMessage.BodyText = emailMessage.BodyText.Substring(0, len);
                }

                //Config SMTP Server
                //Set SMTP SERVER config
                SmtpServer smtpServer=new SmtpServer(SMTP_SERVER_HOSTNAME,SMTP_SERVER_PORT);
                // Add authentication only when requested
                //
                if (!(String.IsNullOrEmpty(SMTP_SERVER_LOGIN) || String.IsNullOrEmpty(SMTP_SERVER_PASSWORD)))
                {
                    //Authentication
                    smtpServer.SmtpAuthToken=new SmtpAuthToken(SMTP_SERVER_LOGIN, SMTP_SERVER_PASSWORD);
                }
                //Send Email Message
                emailMessage.Send(smtpServer);

                //Log
                m_log.Info("[EMAIL]: EMail sent to: " + address + " from object: " + objectID.ToString() + "@" + m_HostName);
            }
            catch (Exception e)
            {
                m_log.Error("[EMAIL]: DefaultEmailModule Exception: " + e.Message);
            }
        }

    }
}
