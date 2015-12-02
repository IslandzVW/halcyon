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
using OpenSim.Data.SimpleDB;

namespace OpenSim.Region.CoreModules.Scripting.EmailModules
{
    public class GetEmailModule : IGetEmailModule
    {
        //
        // Log
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private IConfigSource m_Config;
        private int m_MaxQueueSize = 50; // maximum size of an object mail queue
        private Dictionary<UUID, List<Email>> m_MailQueues = new Dictionary<UUID, List<Email>>();
        private Dictionary<UUID, DateTime> m_LastGetEmailCall = new Dictionary<UUID, DateTime>();
        private TimeSpan m_QueueTimeout = new TimeSpan(2, 0, 0); // 2 hours without llGetNextEmail drops the queue

        // Database support
        private ConnectionFactory _connectionFactory;
        private string _connectString = String.Empty;
        private static long mTicksToEpoch = new DateTime(1970, 1, 1).Ticks;

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes =
            new Dictionary<ulong, Scene>();

        private bool m_Enabled = false;

        public void InsertEmail(UUID to, Email email)
        {
            // It's tempting to create the queue here.  Don't; objects which have
            // not yet called GetNextEmail should have no queue, and emails to them
            // should be silently dropped.

            lock (m_MailQueues)
            {
                if (m_MailQueues.ContainsKey(to))
                {
                    if (m_MailQueues[to].Count >= m_MaxQueueSize)
                    {
                        // fail silently
                        return;
                    }

                    lock (m_MailQueues[to])
                    {
                        m_MailQueues[to].Add(email);
                    }
                }
            }
        }

        public void Initialize(Scene scene, IConfigSource config)
        {
            m_Config = config;
            IConfig SMTPConfig;

            //FIXME: RegionName is correct??
            //m_RegionName = scene.RegionInfo.RegionName;

            IConfig startupConfig = m_Config.Configs["Startup"];

            m_Enabled = (startupConfig.GetString("getemailmodule", "DefaultGetEmailModule") == "DefaultGetEmailModule");

            //Load SMTP SERVER config
            try
            {
                if ((SMTPConfig = m_Config.Configs["SMTP"]) == null)
                {
                    m_log.ErrorFormat("[InboundEmail]: SMTP not configured");
                    m_Enabled = false;
                    return;
                }

                if (!SMTPConfig.GetBoolean("inbound", true))
                {
                    m_log.WarnFormat("[InboundEmail]: Inbound email module disabled in configuration");
                    m_Enabled = false;
                    return;
                }

                // Database support
                _connectString = SMTPConfig.GetString("inbound_storage_connection", String.Empty);
                if (String.IsNullOrEmpty(_connectString))
                {
                    m_log.ErrorFormat("[InboundEmail]: Could not find SMTP inbound_storage_connection.");
                    m_Enabled = false;
                    return;
                }
                _connectionFactory = new ConnectionFactory("MySQL", _connectString);
                if (_connectionFactory == null)
                {
                    m_log.ErrorFormat("[InboundEmail]: Inbound email module could not create MySQL connection.");
                    m_Enabled = false;
                    return;
                }
            }
            catch (Exception e)
            {
                m_log.Error("[InboundEmail]: Inbound email not configured: " + e.Message);
                m_log.Error(e.StackTrace);
                m_Enabled = false;
                return;
            }

            // It's a go!
            if (m_Enabled)
            {
                lock (m_Scenes)
                {
                    // Claim the interface slot
                    scene.RegisterModuleInterface<IGetEmailModule>(this);

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

                m_log.Info("[InboundEmail]: Activated inbound email.");
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
            get { return "DefaultGetEmailModule"; }
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

        public static uint DateTime2UnixTime(DateTime when)
        {
            long elapsed = DateTime.Now.Ticks - mTicksToEpoch;
            return Convert.ToUInt32(elapsed / 10000000);
        }

        public static DateTime UnixTime2DateTime(uint when)
        {
            return new DateTime(mTicksToEpoch + when);
        }

        private bool IsLocal(UUID objectID)
        {
            UUID unused;
            return (null != findPrim(objectID, out unused));
        }

        private SceneObjectPart findPrim(UUID objectID, out UUID regionUUID)
        {
            lock (m_Scenes)
            {
                foreach (Scene s in m_Scenes.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        regionUUID = s.RegionInfo.RegionID;
                        return part;
                    }
                }
            }

            regionUUID = UUID.Zero;
            return null;
        }

        private void UpdateRegistration(ISimpleDB db, UUID uuid, UUID regionUUID)
        {
            string sql = "INSERT INTO emailregistrations (`uuid`, `time`, `region`) VALUES (?uuid, ?time, ?region)";
            sql += " ON DUPLICATE KEY UPDATE `time`=?time";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?uuid"] = uuid.ToString();
            parameters["?region"] = regionUUID.ToString();
            parameters["?time"] = DateTime2UnixTime(DateTime.UtcNow);   // All storage dates are UTC

            try
            {
                db.QueryNoResults(sql, parameters);
            }
            catch (Exception e)
            {
                m_log.Error("[InboundEmail]: Exception during database call to store message: "+e.Message);
                m_log.Error(e.StackTrace);
            }
        }

        private void DeleteMessage(ISimpleDB db, uint ID)
        {
            string sql = "DELETE FROM emailmessages WHERE `ID`=?id";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?ID"] = ID.ToString();

            try
            {
                db.QueryNoResults(sql, parameters);
            }
            catch (Exception e)
            {
                m_log.Error("[InboundEmail]: Exception during database call to delete delivered message: " + e.Message);
                m_log.Error(e.StackTrace);
            }
        }

        private uint QueryEmailCount(ISimpleDB db, UUID uuid)
        {
            try
            {
                string query
                    = "SELECT COUNT(*) FROM emailmessages WHERE uuid = ?uuid";
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?uuid", uuid);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                if (results.Count != 1)
                    return 0;

                return Convert.ToUInt32(results[0]["COUNT(*)"]);
            }
            catch (Exception e)
            {
                m_log.Error("[InboundEmail]: Exception during database call to count messages: " + e.Message);
                m_log.Error(e.StackTrace);
                return 0;
            }
        }

        private Email QueryNextEmail(ISimpleDB db, UUID uuid, string sender, string subject)
        {
            Email msg = new Email();

            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                string query = "SELECT * FROM emailmessages WHERE uuid = ?uuid";
                parms.Add("?uuid", uuid);
                if (!String.IsNullOrEmpty(sender))
                {
                    query += " AND from = ?from";
                    parms.Add("?from", sender);
                }
                if (!String.IsNullOrEmpty(subject))
                {
                    query += " AND subject = ?subject";
                    parms.Add("?subject", subject);
                }
                query += " ORDER BY sent LIMIT 1";

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                if (results.Count != 1)
                    return null;

                uint ID = Convert.ToUInt32(results[0]["ID"]);
                uint unixtime = Convert.ToUInt32(results[0]["sent"]);
                msg.time = unixtime.ToString();     // Note: email event is documented as string form of *UTC* unix time
                msg.sender = results[0]["from"];
                msg.subject = results[0]["subject"];
                msg.message = results[0]["body"];

                // This one has been handled, remove it from those queued in the DB.
                DeleteMessage(db, ID);
            }
            catch (Exception e)
            {
                m_log.Error("[InboundEmail]: Exception during database call to store message: " + e.Message);
                m_log.Error(e.StackTrace);
                return null;
            }

            msg.numLeft = (int)QueryEmailCount(db, uuid);
            return msg;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="sender"></param>
        /// <param name="subject"></param>
        /// <returns></returns>
        public Email GetNextEmail(UUID objectID, string sender, string subject)
        {
            try
            {
                using (ISimpleDB db = _connectionFactory.GetConnection())
                {
                    UUID regionUUID;
                    SceneObjectPart part = findPrim(objectID, out regionUUID);
                    if (part != null)
                    {
                        UpdateRegistration(db, objectID, regionUUID);
                        return QueryNextEmail(db, objectID, sender, subject);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[InboundEmail]: Exception during database call to check messages: " + e.Message);
                m_log.Error(e.StackTrace);
            }
            m_log.Error("[InboundEmail]: Next email not available. Part " + objectID.ToString() + " not found.");
            return null;
        }
    }
}
