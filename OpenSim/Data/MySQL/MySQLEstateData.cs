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
using System.Data;
using System.Reflection;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.MySQL
{
    public class MySQLEstateStore : IEstateDataStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string m_waitTimeoutSelect = "select @@wait_timeout";

        private string m_connectionString;

        private FieldInfo[] m_Fields;
        private Dictionary<string, FieldInfo> m_FieldMap =
                new Dictionary<string, FieldInfo>();

        private MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(m_connectionString);
            conn.Open();

            return conn;
        }

        public void Initialize(string connectionString)
        {
            m_connectionString = connectionString;

            Type t = typeof(EstateSettings);
            m_Fields = t.GetFields(BindingFlags.NonPublic |
                                   BindingFlags.Instance |
                                   BindingFlags.DeclaredOnly);

            foreach (FieldInfo f in m_Fields)
            {
                if (f.Name.Substring(0, 2) == "m_")
                    m_FieldMap[f.Name.Substring(2)] = f;
            }
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
        }

        private EstateSettings LoadEstateSettingsOnly(uint estateID)
        {
            EstateSettings es = new EstateSettings();
            es.OnSave += StoreEstateSettings;

            string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) + " from estate_settings where estate_settings.EstateID = ?EstateID";

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("?EstateID", estateID.ToString());

                    IDataReader r = cmd.ExecuteReader();

                    if (!r.Read())
                    {
                        r.Close();
                        return null;
                    }

                    foreach (string name in FieldList)
                    {
                        if (m_FieldMap[name].GetValue(es) is bool)
                        {
                            int v = Convert.ToInt32(r[name]);
                            if (v != 0)
                                m_FieldMap[name].SetValue(es, true);
                            else
                                m_FieldMap[name].SetValue(es, false);
                        }
                        else if (m_FieldMap[name].GetValue(es) is UUID)
                        {
                            UUID uuid = UUID.Zero;

                            UUID.TryParse(r[name].ToString(), out uuid);
                            m_FieldMap[name].SetValue(es, uuid);
                        }
                        else
                        {
                            m_FieldMap[name].SetValue(es, r[name]);
                        }
                    }
                    r.Close();
                    return es;
                }
            }
        }

        private EstateSettings NewEstateSettings(uint estateID, string regionName, UUID masterAvatarID)
        {
            // 0 will be treated as "create estate settings with a new ID"

            EstateSettings es = new EstateSettings();
            es.OnSave += StoreEstateSettings;

            List<string> names = new List<string>(FieldList);

            using (MySqlConnection conn = GetConnection())
            {
                es.EstateName = regionName;
                if (estateID == 0)
                {
                    names.Remove("EstateID");   // generate one
                }
                else
                {
                    es.EstateID = estateID;     // use this one
                    es.ParentEstateID = estateID;   // default is same
                }

                // The EstateOwner is not filled in yet. It needs to be.
                es.EstateOwner = masterAvatarID;

                string sql = "insert into estate_settings (" + String.Join(",", names.ToArray()) + ") values ( ?" + String.Join(", ?", names.ToArray()) + ")";

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Clear();

                    foreach (string name in FieldList)
                    {
                        if (m_FieldMap[name].GetValue(es) is bool)
                        {
                            if ((bool)m_FieldMap[name].GetValue(es))
                                cmd.Parameters.AddWithValue("?" + name, "1");
                            else
                                cmd.Parameters.AddWithValue("?" + name, "0");
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es).ToString());
                        }
                    }
                    cmd.ExecuteNonQuery();

                    // If we generated a new estate ID, fetch it
                    if (estateID == 0)
                    {
                        // Now fetch the auto-generated estate ID.
                        cmd.CommandText = "select LAST_INSERT_ID() as id";
                        cmd.Parameters.Clear();
                        IDataReader r = cmd.ExecuteReader();
                        r.Read();
                        es.EstateID = Convert.ToUInt32(r["id"]);
                        r.Close();

                        // If we auto-generated the Estate ID, then ParentEstateID is still the default (100). Fix it to default to the same value.
                        es.ParentEstateID = es.EstateID;
                        cmd.CommandText = "UPDATE estate_settings SET ParentEstateID=?ParentEstateID WHERE EstateID=?EstateID";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("ParentEstateID", es.ParentEstateID.ToString());
                        cmd.Parameters.AddWithValue("EstateID", es.EstateID.ToString());
                        cmd.ExecuteNonQuery();
                    }

                    return es;
                }
            }
        }

        /// <summary>
        /// Returns the EstateID from the estate_map for a specified regionID.
        /// </summary>
        /// <param name="regionID">The UUID of the target region</param>
        /// <returns>The UUID of the estate from the estate_map.</returns>
        private bool GetEstateMap(UUID regionID, out uint estateID)
        {
            bool result = false;
            estateID = 0;

            string sql = "select * from estate_map where RegionID = ?RegionID";

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                    IDataReader r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        if (uint.TryParse(r["EstateID"].ToString(), out estateID))
                            result = true;
                    }
                    r.Close();

                    return result;
                }
            }
        }

        private bool NewEstateMap(UUID regionID, uint estateID)
        {
            bool rc = true;

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "insert into estate_map values (?RegionID, ?EstateID)";
                    cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());
                    cmd.Parameters.AddWithValue("?EstateID", estateID.ToString());

                    // This will throw on dupe key
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        rc = false;
                    }

                    return rc;
                }
            }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, string regionName, UUID masterAvatarID, bool writable)
        {
            EstateSettings es = null;
            uint estateID;

            if (GetEstateMap(regionID, out estateID))
            {
                if (estateID == 0)
                {
                    string message = "Region " + regionID.ToString() + " cannot use estate ID 0.";
                    if (writable)   // fatal startup error
                        throw new Exception(message);
                    else
                        m_log.ErrorFormat("[ESTATE]: {0}", message);
                    return null;
                }

                es = LoadEstateSettingsOnly(estateID);
                if (es == null)
                {
                    if (writable)
                    {
                        // estate settings don't exist yet for this estateID
                        es = NewEstateSettings(estateID, regionName, masterAvatarID);
                    }
                    else
                    {
                        // If writable is false, this is an after-startup error (or a different region).
                        m_log.ErrorFormat("[ESTATE]: Region {0} has estate map but no settings for estate {1}", regionID, estateID);
                        return null;
                    }
                }
                else
                {
                    // We successfully loaded an existing EstateSettings.
                    if (writable)
                    {
                        // And we own this data. Let's do a sanity check and repair invalid settings if possible.
                        if (es.EstateOwner == UUID.Zero)
                        {
                            // Assume the region.xml specifies a better user than no user.
                            es.EstateOwner = masterAvatarID;
                            StoreEstateSettingsOnly(es);
                        }
                    }
                }
            } 
            else
            {
                // Does not have an estate assigned yet.
                if (writable)
                {
                    // 0 will be treated as "create estate settings with a new estate ID"
                    es = NewEstateSettings(0, regionName, masterAvatarID);
                    m_log.WarnFormat("[ESTATE]: Region {0} does not have an estate assigned - repaired as estate {1}.", regionID, es.EstateID);
                    if (!NewEstateMap(regionID, es.EstateID))
                        m_log.ErrorFormat("[ESTATE]: Region {0} could not save estate_map for estate {1}.", regionID, es.EstateID);
                }
                else
                {
                    // If writable is false, this is an after-startup error (or a different region).
                    m_log.ErrorFormat("[ESTATE]: Region {0} error - region has no estate.", regionID);
                    return null;
                }
            }

            // Load the four estate lists.
            LoadBanList(es);
            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");
            m_log.InfoFormat("[ESTATE]: Region {0} loaded estate {1} with {2} managers, {3} users, {4} groups, {5} bannned.",
                regionID, es.EstateID, es.EstateManagers.Length, es.EstateAccess.Length, es.EstateGroups.Length, es.EstateBans.Length);

            return es;
        }

        public EstateSettings LoadEstateSettings(UUID regionID)
        {
            return LoadEstateSettings(regionID, String.Empty, UUID.Zero, false);
        }

        private void StoreEstateSettingsOnly(EstateSettings es)
        {
            string sql = "replace into estate_settings (" + String.Join(",", FieldList) + ") values ( ?" + String.Join(", ?", FieldList) + ")";

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;

                    foreach (string name in FieldList)
                    {
                        if (m_FieldMap[name].GetValue(es) is bool)
                        {
                            if ((bool)m_FieldMap[name].GetValue(es))
                                cmd.Parameters.AddWithValue("?" + name, "1");
                            else
                                cmd.Parameters.AddWithValue("?" + name, "0");
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es).ToString());
                        }
                    }

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void StoreEstateSettings(EstateSettings es, uint listsToSave)
        {
            StoreEstateSettingsOnly(es);
            if ((listsToSave & (uint)EstateSettings.EstateSaveOptions.EstateSaveBans) != 0)
                SaveBanList(es);
            if ((listsToSave & (uint)EstateSettings.EstateSaveOptions.EstateSaveManagers) != 0)
                SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            if ((listsToSave & (uint)EstateSettings.EstateSaveOptions.EstateSaveUsers) != 0)
                SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            if ((listsToSave & (uint)EstateSettings.EstateSaveOptions.EstateSaveGroups) != 0)
                SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        private void LoadBanList(EstateSettings es)
        {
            es.ClearBans();

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select bannedUUID from estateban where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", es.EstateID);

                    IDataReader r = cmd.ExecuteReader();

                    while (r.Read())
                    {
                        EstateBan eb = new EstateBan();

                        UUID uuid = new UUID();
                        UUID.TryParse(r["bannedUUID"].ToString(), out uuid);

                        eb.BannedUserID = uuid;
                        eb.BannedHostAddress = "0.0.0.0";
                        eb.BannedHostIPMask = "0.0.0.0";
                        es.AddBan(eb);
                    }
                    r.Close();
                }
            }
        }

        private void SaveBanList(EstateSettings es)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        cmd.CommandText = "delete from estateban where EstateID = ?EstateID";
                        cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());

                        cmd.ExecuteNonQuery();

                        cmd.Parameters.Clear();

                        cmd.CommandText = "insert into estateban (EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask) values ( ?EstateID, ?bannedUUID, '', '', '' )";

                        foreach (EstateBan b in es.EstateBans)
                        {
                            cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());
                            cmd.Parameters.AddWithValue("?bannedUUID", b.BannedUserID.ToString());

                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        void SaveUUIDList(uint EstateID, string table, UUID[] data)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        cmd.CommandText = "delete from " + table + " where EstateID = ?EstateID";
                        cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());

                        cmd.ExecuteNonQuery();

                        cmd.Parameters.Clear();

                        cmd.CommandText = "insert into " + table + " (EstateID, uuid) values ( ?EstateID, ?uuid )";

                        foreach (UUID uuid in data)
                        {
                            cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());
                            cmd.Parameters.AddWithValue("?uuid", uuid.ToString());

                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        UUID[] LoadUUIDList(uint EstateID, string table)
        {
            List<UUID> uuids = new List<UUID>();

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select uuid from " + table + " where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", EstateID);

                    IDataReader r = cmd.ExecuteReader();

                    while (r.Read())
                    {
                        // EstateBan eb = new EstateBan();

                        UUID uuid = new UUID();
                        UUID.TryParse(r["uuid"].ToString(), out uuid);

                        uuids.Add(uuid);
                    }
                    r.Close();

                    return uuids.ToArray();
                }
            }
        }

        public List<UUID> GetEstateRegions(uint estateID)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select RegionID from estate_map where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", estateID);

                    IDataReader r = cmd.ExecuteReader();

                    List<UUID> regions = new List<UUID>();
                    while (r.Read())
                    {
                        EstateBan eb = new EstateBan();

                        UUID uuid = new UUID();
                        UUID.TryParse(r["RegionID"].ToString(), out uuid);

                        regions.Add(uuid);
                    }
                    r.Close();
                    return regions;
                }
            }
        }


        #region Telehub pieces

        /// <summary>
        ///     Adds a new telehub in the region. Replaces an old one automatically.
        /// </summary>
        /// <param name="telehub"></param>
        /// <param name="regionhandle"> </param>
        public void AddTelehub(Telehub telehub)
        {
            string sql = "REPLACE into telehubs (RegionID, TelehubLoc, TelehubRot, ObjectUUID, Spawns, Name) VALUES (?RegionID, ?TelehubLoc, ?TelehubRot, ?ObjectUUID, ?Spawns, ?Name)";

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add("?RegionID", telehub.RegionID.ToString());
                    cmd.Parameters.Add("?TelehubLoc", telehub.TelehubLoc.ToString());
                    cmd.Parameters.Add("?TelehubRot", telehub.TelehubRot.ToString());
                    cmd.Parameters.Add("?ObjectUUID", telehub.ObjectUUID.ToString());
                    cmd.Parameters.Add("?Spawns", telehub.BuildFromList(telehub.SpawnPos));
                    cmd.Parameters.Add("?Name", telehub.Name);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        ///     Removes the telehub if it exists.
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="regionHandle"> </param>
        public void RemoveTelehub(UUID regionID)
        {
            string sql = "DELETE FROM telehubs WHERE RegionID = ?RegionID;";

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add("?RegionID", regionID.ToString());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        ///     Attempts to find a telehub in the region; if one is not found, returns false.
        /// </summary>
        /// <param name="regionID">Region ID</param>
        /// <returns></returns>
        public Telehub FindTelehub(UUID regionID)
        {
            string sql = "SELECT * FROM telehubs where RegionID=?RegionID";

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add("?RegionID", regionID.ToString());
                    using (IDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            if (r.FieldCount == 0)
                                return null;

                            return new Telehub()
                            {
                                RegionID = UUID.Parse(r["RegionID"].ToString()),
                                TelehubLoc = Vector3.Parse(r["TelehubLoc"].ToString()),
                                TelehubRot = Quaternion.Parse(r["TelehubRot"].ToString()),
                                Name = r["Name"].ToString(),
                                ObjectUUID = UUID.Parse(r["ObjectUUID"].ToString()),
                                SpawnPos = Telehub.BuildToList(r["Spawns"].ToString())
                            };
                        }
                        return null;
                    }
                }
            }
        }

        #endregion
    }
}
