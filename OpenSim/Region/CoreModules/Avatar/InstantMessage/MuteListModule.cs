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
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Data.SimpleDB;

namespace OpenSim.Region.CoreModules.Avatar.MuteList
{
    public class MuteListModule : IRegionModule, IMuteListModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;
        private List<Scene> m_SceneList = new List<Scene>();
        private ConnectionFactory _connectionFactory = null;

        // Legacy mutes are BY_NAME and have null UUID.
        enum MuteType { BY_NAME = 0, AGENT = 1, OBJECT = 2, GROUP = 3, COUNT = 4 };

        class MuteListEntry
        {
            public int m_Type;      // MuteType
            public string m_Name;
            public uint m_Flags;
            public MuteListEntry()
            {
                m_Type = (int)MuteType.AGENT; m_Name = String.Empty; m_Flags = 0;
            }
            public MuteListEntry(int type, string name, uint flags)
            {
                m_Type = type; m_Name = name; m_Flags = flags;
            }

        };
        Dictionary<UUID, Dictionary<UUID, MuteListEntry>> MuteListCache = new Dictionary<UUID, Dictionary<UUID, MuteListEntry>>();

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (!enabled)
                return;

            IConfig cnf = config.Configs["Messaging"];
            if (cnf == null)
            {
                enabled = false;
                return;
            }

            if (cnf != null && cnf.GetString(
                    "MuteListModule", "None") !=
                    "MuteListModule")
            {
                enabled = false;
                return;
            }

            string dbType = cnf.GetString("MuteListDBType");
            if (dbType == null)
                dbType = "MySQL";   // default
            string connStr = cnf.GetString("MuteListConnString");
            if (connStr == null)
            {   // If the MuteListConnString INI option isn't found, fall back to the one used for profiles.
                cnf = config.Configs["Profile"];
                connStr = cnf.GetString("ProfileConnString");
            }

            _connectionFactory = new ConnectionFactory(dbType, connStr);

            lock (m_SceneList)
            {
                if (!m_SceneList.Contains(scene))
                    m_SceneList.Add(scene);

                scene.RegisterModuleInterface<IMuteListModule>(this);
                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnRemovePresence += OnRemovePresence;

            }
        }

        public void PostInitialize()
        {
            if (!enabled)
                return;

            if (m_SceneList.Count == 0)
                return;

            // Test the db connection
            using (ISimpleDB db = _connectionFactory.GetConnection())
            {
            }

            m_log.Debug("[MUTE LIST] Mute list enabled.");
        }

        public string Name
        {
            get { return "MuteListModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
        
        public void Close()
        {
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnMuteListRequest += OnMuteListRequest;
            client.OnUpdateMuteListEntry += OnUpdateMuteListEntry;
            client.OnRemoveMuteListEntry += OnRemoveMuteListEntry;
        }

        private void OnRemovePresence(UUID AgentId)
        {
            if (MuteListCache.ContainsKey(AgentId))
                MuteListCache.Remove(AgentId);
        }

        private Dictionary<UUID, MuteListEntry> MapMuteListFromDBResults(List<Dictionary<string, string>> results)
        {
            Dictionary<UUID, MuteListEntry> MuteList = new Dictionary<UUID, MuteListEntry>();

            foreach (Dictionary<string, string> result in results) {
                MuteListEntry entry = new MuteListEntry();
                UUID MuteID = new UUID(result["MuteID"]);
                entry.m_Type = Convert.ToInt32(result["MuteType"]);
                entry.m_Name = result["MuteName"];
                entry.m_Flags = Convert.ToUInt32(result["MuteFlags"]);
                MuteList.Add(MuteID, entry);
            }

            return MuteList;
        }

        private Dictionary<UUID,MuteListEntry> DBLoadMuteList(UUID AgentID)
        {
            using (ISimpleDB db = _connectionFactory.GetConnection())
            {
                string query =  " SELECT AgentID, MuteType, MuteID, MuteName, MuteFlags" +
                                " FROM mutelist" +
                                " WHERE " +
                                "  AgentID = ?agentID";

                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("?agentID", AgentID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                if (results.Count < 1 || results[0]["AgentID"] == null)
                    return null;

                return MapMuteListFromDBResults(results);
            }
        }

        Dictionary<UUID,MuteListEntry> GetMuteList(UUID AgentID)
        {
            if (MuteListCache.ContainsKey(AgentID))
                return MuteListCache[AgentID];

            Dictionary<UUID,MuteListEntry> MuteList = DBLoadMuteList(AgentID);

            if (MuteList == null)  // Mute list is empty for user, return a new empty one
                MuteList = new Dictionary<UUID,MuteListEntry>();

            MuteListCache.Add(AgentID, MuteList);
            return MuteList;
        }

        /// <summary>
        /// This doesn't return the mutelist (mutees) for a user, it returns all users who have this user/object muted (muters).
        /// Implemented as a single DB call.
        /// </summary>
        /// <param name="id">The user or object that may be muted.</param>
        /// <returns>UUIDs of those who have it muted.</returns>
        public List<UUID> GetInverseMuteList(UUID muteID)
        {
            List<UUID> muters = new List<UUID>();
            using (ISimpleDB db = _connectionFactory.GetConnection())
            {
                string query = " SELECT * FROM mutelist WHERE MuteID = ?muteID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?muteID", muteID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                if (results.Count < 1 || results[0]["AgentID"] == null)
                    return muters;  // empty list

                foreach (Dictionary<string, string> result in results)
                {
                    UUID MuteID = new UUID(result["AgentID"]);
                    muters.Add(MuteID);
                }
            }
            return muters;
        }

        /// <summary>
        /// Don't use this for messages broadcast to more than one user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="target"></param>
        /// <returns>Returns true if target has sender muted.</returns>
        public bool IsMuted(UUID sender, UUID target)
        {
            using (ISimpleDB db = _connectionFactory.GetConnection())
            {
                string query = "SELECT COUNT(*) as MuteCount FROM mutelist WHERE AgentID = ?agentID AND MuteID = ?muteID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", target);
                parms.Add("?muteID", sender);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                return (Convert.ToInt32(results[0]["MuteCount"]) != 0);
            }
        }

        private void DBStoreMuteListEntry(UUID AgentID, UUID MuteID, MuteListEntry entry, bool isUpdate)
        {
            using (ISimpleDB db = _connectionFactory.GetConnection())
            {
                string query;
                if (isUpdate)
                    query = "UPDATE mutelist " +
                        "SET " +
                        "    MuteType = ?muteType" +
                        "    , MuteName = ?muteName" +
                        "    , MuteFlags= ?muteFlags" +
                        " WHERE " +
                        "    AgentID = ?agentID AND MuteID = ?muteID";
                else
                    query = "INSERT INTO mutelist " +
                            "(AgentID, MuteType, MuteID, MuteName, MuteFlags) " +
                            "VALUES(?AgentID, ?MuteType, ?MuteID, ?MuteName, ?MuteFlags)";
                
                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("?agentID", AgentID);
                parms.Add("?muteID", MuteID);
                parms.Add("?muteType", entry.m_Type);
                parms.Add("?muteName", entry.m_Name);
                parms.Add("?muteFlags", entry.m_Flags);

                db.QueryNoResults(query, parms);
            }
        }

        private void DBRemoveMuteListEntry(UUID AgentID, UUID MuteID)
        {
            using (ISimpleDB db = _connectionFactory.GetConnection())
            {
                Dictionary<string, object> parms = new Dictionary<string,object>();
                string query = "DELETE FROM mutelist WHERE AgentID = ?agentID AND MuteID = ?muteID";
                parms.Add("?agentID", AgentID);
                parms.Add("?muteID", MuteID);
                db.QueryNoResults(query, parms);
            }
        }

        private byte[] GetMuteListFileData(UUID AgentID)
        {
            Dictionary<UUID, MuteListEntry> MuteList;
            string data = String.Empty;
            int lines = 0;

            MuteList = GetMuteList(AgentID);
            foreach (KeyValuePair<UUID, MuteListEntry> pair in MuteList)
            {
                UUID MuteID = new UUID(pair.Key);
                MuteListEntry entry = pair.Value;

                if (lines++ != 0)
                    data += "\n";
                data += " "+entry.m_Type.ToString()+
                        " "+MuteID.ToString()+
                        " "+entry.m_Name.ToString()+
                        "|"+entry.m_Flags.ToString();
            }
            return Utils.StringToBytes(data);
        }

        private void OnMuteListRequest(IClientAPI client, uint crc)
        {
            m_log.DebugFormat("[MUTE LIST] List request for crc {0}", crc);
            string filename = "mutes"+client.AgentId.ToString();

            IXfer xfer = client.Scene.RequestModuleInterface<IXfer>();
            if (xfer != null)
            {
                xfer.AddNewFile(filename, GetMuteListFileData(client.AgentId));
                client.SendMuteListUpdate(filename);
            }
        }

        private void OnUpdateMuteListEntry(IClientAPI client, UUID AgentID, int muteType, UUID muteID, string Name, uint muteFlags)
        {
            Dictionary<UUID, MuteListEntry> MuteList;
            bool isUpdate = false;

            MuteList = GetMuteList(AgentID);
            isUpdate = MuteList.ContainsKey(muteID);
            if (isUpdate)
                MuteList.Remove(muteID);

            switch ((MuteType)muteType)
            {
                case MuteType.BY_NAME:
                    m_log.DebugFormat("[MUTE LIST] Update from {0} for name: {1} (ID {2}) flags={3}", AgentID, Name, muteID, muteFlags);
                    break;
                case MuteType.AGENT:
                    m_log.DebugFormat("[MUTE LIST] Update from {0} for agent: {1} ({2}) flags={3}", AgentID, muteID, Name, muteFlags);
                    break;
                case MuteType.OBJECT:
                    m_log.DebugFormat("[MUTE LIST] Update from {0} for object: {1} ({2}) flags={3}", AgentID, muteID, Name, muteFlags);
                    break;
                case MuteType.GROUP:
                    m_log.DebugFormat("[MUTE LIST] Update from {0} for group: {1} ({2}) flags={3}", AgentID, muteID, Name, muteFlags);
                    break;
                case MuteType.COUNT:
                    m_log.DebugFormat("[MUTE LIST] Update from {0} for count: {1} ({2}) flags={3}", AgentID, muteID, Name, muteFlags);
                    break;
                default:
                    m_log.ErrorFormat("[MUTE LIST] Update from {0} unknown type {1} with ID {2} Name {3} flags={4}", AgentID, muteType, muteID, Name, muteFlags);
                    break;
            }
            MuteListEntry entry = new MuteListEntry(muteType, Name, muteFlags);
            MuteList.Add(muteID, entry);
            DBStoreMuteListEntry(AgentID, muteID, entry, isUpdate);
        }

        private void OnRemoveMuteListEntry(IClientAPI client, UUID AgentID, UUID muteID, string name)
        {
            m_log.DebugFormat("[MUTE LIST] Remove from {0} for ID {1} Name {2}", AgentID, muteID, name);
            Dictionary<UUID, MuteListEntry> MuteList;
            MuteList = GetMuteList(AgentID);
            if (MuteList.ContainsKey(muteID))
            {
                MuteList.Remove(muteID);
                DBRemoveMuteListEntry(AgentID, muteID);
            }
        }

    }
}

