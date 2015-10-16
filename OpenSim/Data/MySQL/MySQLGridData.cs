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
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data.SimpleDB;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Grid Server
    /// </summary>
    public class MySQLGridData : GridDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ConnectionFactory _connFactory;

        override public void Initialize()
        {
            m_log.Info("[MySQLGridData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitializedException (Name);
        }

        /// <summary>
        /// <para>Initializes Grid interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initializes the MySQL storage plugin</item>
        /// <item>Warns and uses the obsolete mysql_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string.</param>
        override public void Initialize(string connect)
        {
            m_log.Info("[MySQLGridData.InWorldz]: Starting up");

            _connFactory = new ConnectionFactory("MySQL", connect);

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                m_log.Info("[MySQLGridData.InWorldz]: Sucessfully made connection to database");
            }
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        override public void Dispose()
        {

        }

        /// <summary>
        /// Returns the plugin name
        /// </summary>
        /// <returns>Plugin name</returns>
        override public string Name
        {
            get { return "MySql OpenGridData"; }
        }

        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>Plugin version</returns>
        override public string Version
        {
            get { return "0.1"; }
        }

        /// <summary>
        /// Reads a region row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A region profile</returns>
        private RegionProfileData readSimRow(IDataReader reader)
        {
            RegionProfileData retval = new RegionProfileData();

            if (reader.Read())
            {
                // Region Main gotta-have-or-we-return-null parts
                UInt64 tmp64;
                if (!UInt64.TryParse(reader["regionHandle"].ToString(), out tmp64))
                {
                    return null;
                }
                else
                {
                    retval.regionHandle = tmp64;
                }
                UUID tmp_uuid;
                if (!UUID.TryParse(Convert.ToString(reader["uuid"]), out tmp_uuid))
                {
                    return null;
                }
                else
                {
                    retval.UUID = tmp_uuid;
                }

                // non-critical parts
                retval.regionName = (string)reader["regionName"];
                retval.originUUID = new UUID(Convert.ToString(reader["originUUID"]));
                retval.product = (ProductRulesUse) Convert.ToInt32(reader["product"]);

                // Secrets
                retval.regionRecvKey = (string)reader["regionRecvKey"];
                retval.regionSecret = (string)reader["regionSecret"];
                retval.regionSendKey = (string)reader["regionSendKey"];

                // Region Server
                retval.regionDataURI = (string)reader["regionDataURI"];
                retval.regionOnline = false; // Needs to be pinged before this can be set.
                retval.serverHostName = (string)reader["serverIP"];
                retval.serverPort = (uint)reader["serverPort"];
                if (reader.IsDBNull(reader.GetOrdinal("outside_ip")))
                    retval.OutsideIP = null;
                else
                    retval.OutsideIP = (string)reader["outside_ip"];

                retval.httpPort = Convert.ToUInt32(reader["serverHttpPort"].ToString());
                retval.remotingPort = Convert.ToUInt32(reader["serverRemotingPort"].ToString());

                // Location
                retval.regionLocX = Convert.ToUInt32(reader["locX"].ToString());
                retval.regionLocY = Convert.ToUInt32(reader["locY"].ToString());
                retval.regionLocZ = Convert.ToUInt32(reader["locZ"].ToString());

                // Neighbours - 0 = No Override
                retval.regionEastOverrideHandle = Convert.ToUInt64(reader["eastOverrideHandle"].ToString());
                retval.regionWestOverrideHandle = Convert.ToUInt64(reader["westOverrideHandle"].ToString());
                retval.regionSouthOverrideHandle = Convert.ToUInt64(reader["southOverrideHandle"].ToString());
                retval.regionNorthOverrideHandle = Convert.ToUInt64(reader["northOverrideHandle"].ToString());

                // Assets
                retval.regionAssetURI = (string)reader["regionAssetURI"];
                retval.regionAssetRecvKey = (string)reader["regionAssetRecvKey"];
                retval.regionAssetSendKey = (string)reader["regionAssetSendKey"];

                // Userserver
                retval.regionUserURI = (string)reader["regionUserURI"];
                retval.regionUserRecvKey = (string)reader["regionUserRecvKey"];
                retval.regionUserSendKey = (string)reader["regionUserSendKey"];

                // World Map Addition
                UUID.TryParse(Convert.ToString(reader["regionMapTexture"]), out retval.regionMapTextureID);
                UUID.TryParse(Convert.ToString(reader["owner_uuid"]), out retval.owner_uuid);
                retval.maturity = Convert.ToUInt32(reader["access"]);
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Returns all the specified region profiles within coordates -- coordinates are inclusive
        /// </summary>
        /// <param name="xmin">Minimum X coordinate</param>
        /// <param name="ymin">Minimum Y coordinate</param>
        /// <param name="xmax">Maximum X coordinate</param>
        /// <param name="ymax">Maximum Y coordinate</param>
        /// <returns>Array of sim profiles</returns>
        override public RegionProfileData[] GetProfilesInRange(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    parms["?xmin"] = xmin.ToString();
                    parms["?ymin"] = ymin.ToString();
                    parms["?xmax"] = xmax.ToString();
                    parms["?ymax"] = ymax.ToString();

                    IDataReader reader = 
                        conn.QueryAndUseReader(
                            "SELECT * FROM regions WHERE locX >= ?xmin AND locX <= ?xmax AND locY >= ?ymin AND locY <= ?ymax",
                            parms);

                    using (reader)
                    {
                        RegionProfileData row;
                        List<RegionProfileData> rows = new List<RegionProfileData>();

                        while ((row = this.readSimRow(reader)) != null)
                        {
                            rows.Add(row);
                        }

                        return rows.ToArray();
                    }

                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns up to maxNum profiles of regions that have a name starting with namePrefix
        /// </summary>
        /// <param name="name">The name to match against</param>
        /// <param name="maxNum">Maximum number of profiles to return</param>
        /// <returns>A list of sim profiles</returns>
        override public List<RegionProfileData> GetRegionsByName(string namePrefix, uint maxNum)
        {
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms["?name"] = namePrefix + "%";


                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    using (IDataReader reader 
                        = conn.QueryAndUseReader("SELECT * FROM regions WHERE regionName LIKE ?name", parms))
                    {
                        RegionProfileData row;

                        List<RegionProfileData> rows = new List<RegionProfileData>();

                        while (rows.Count < maxNum && (row = this.readSimRow(reader)) != null)
                        {
                            rows.Add(row);
                        }

                        return rows;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a sim profile from it's location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        override public RegionProfileData GetProfileByHandle(ulong handle)
        {
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms["?handle"] = handle.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    using (IDataReader reader = conn.QueryAndUseReader("SELECT * FROM regions WHERE regionHandle = ?handle", parms))
                    {
                        RegionProfileData row = this.readSimRow(reader);
                        return row;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a sim profile from it's UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByUUID(UUID uuid)
        {
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms["?uuid"] = uuid.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    using (IDataReader reader = conn.QueryAndUseReader("SELECT * FROM regions WHERE uuid = ?uuid", parms))
                    {
                        RegionProfileData row = this.readSimRow(reader);
                        return row;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            } 
        }

        /// <summary>
        /// Returns a sim profile from it's Region name string
        /// </summary>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByString(string regionName)
        {
            if (regionName.Length > 2)
            {
                try
                {
                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    // Add % because this is a like query.
                    parms["?regionName"] = regionName + "%";

                    using (ISimpleDB conn = _connFactory.GetConnection())
                    {
                        string query = "SELECT * FROM regions WHERE regionName LIKE ?regionName ORDER BY LENGTH(regionName) ASC LIMIT 1";

                        using (IDataReader reader = conn.QueryAndUseReader(query, parms))
                        {
                            RegionProfileData row = this.readSimRow(reader);

                            return row;
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                    return null;
                }
            }

            m_log.Error("[GRID DB]: Searched for a Region Name shorter then 3 characters");
            return null;
        }

        /// <summary>
        /// Inserts a new region into the database
        /// </summary>
        /// <param name="regiondata">The region to insert</param>
        /// <returns>Success?</returns>
        public bool insertRegion(RegionProfileData regiondata)
        {
            bool GRID_ONLY_UPDATE_NECESSARY_DATA = false;

            string sql = String.Empty;
            if (GRID_ONLY_UPDATE_NECESSARY_DATA)
            {
                sql += "INSERT INTO ";
            }
            else
            {
                sql += "REPLACE INTO ";
            }

            sql += "regions (regionHandle, regionName, uuid, regionRecvKey, regionSecret, regionSendKey, regionDataURI, ";
            sql +=
                "serverIP, serverPort, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, ";

            // part of an initial brutish effort to provide accurate information (as per the xml region spec)
            // wrt the ownership of a given region
            // the (very bad) assumption is that this value is being read and handled inconsistently or
            // not at all. Current strategy is to put the code in place to support the validity of this information
            // and to roll forward debugging any issues from that point
            //
            // this particular section of the mod attempts to implement the commit of a supplied value
            // server for the UUID of the region's owner (master avatar). It consists of the addition of the column and value to the relevant sql,
            // as well as the related parameterization
            sql +=
                "regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, regionMapTexture, serverHttpPort, serverRemotingPort, owner_uuid, originUUID, access, product, outside_ip) VALUES ";

            sql += "(?regionHandle, ?regionName, ?uuid, ?regionRecvKey, ?regionSecret, ?regionSendKey, ?regionDataURI, ";
            sql +=
                "?serverIP, ?serverPort, ?locX, ?locY, ?locZ, ?eastOverrideHandle, ?westOverrideHandle, ?southOverrideHandle, ?northOverrideHandle, ?regionAssetURI, ?regionAssetRecvKey, ";
            sql +=
                "?regionAssetSendKey, ?regionUserURI, ?regionUserRecvKey, ?regionUserSendKey, ?regionMapTexture, ?serverHttpPort, ?serverRemotingPort, ?owner_uuid, ?originUUID, ?access, ?product, ?outside_ip)";

            if (GRID_ONLY_UPDATE_NECESSARY_DATA)
            {
                sql += "ON DUPLICATE KEY UPDATE serverIP = ?serverIP, serverPort = ?serverPort, owner_uuid - ?owner_uuid;";
            }
            else
            {
                sql += ";";
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["?regionHandle"] = regiondata.regionHandle.ToString();
            parameters["?regionName"] = regiondata.regionName.ToString();
            parameters["?uuid"] = regiondata.UUID.ToString();
            parameters["?regionRecvKey"] = regiondata.regionRecvKey.ToString();
            parameters["?regionSecret"] = regiondata.regionSecret.ToString();
            parameters["?regionSendKey"] = regiondata.regionSendKey.ToString();
            parameters["?regionDataURI"] = regiondata.regionDataURI.ToString();
            parameters["?serverIP"] = regiondata.serverHostName.ToString();
            parameters["?serverPort"] = regiondata.serverPort.ToString();
            parameters["?locX"] = regiondata.regionLocX.ToString();
            parameters["?locY"] = regiondata.regionLocY.ToString();
            parameters["?locZ"] = regiondata.regionLocZ.ToString();
            parameters["?eastOverrideHandle"] = regiondata.regionEastOverrideHandle.ToString();
            parameters["?westOverrideHandle"] = regiondata.regionWestOverrideHandle.ToString();
            parameters["?northOverrideHandle"] = regiondata.regionNorthOverrideHandle.ToString();
            parameters["?southOverrideHandle"] = regiondata.regionSouthOverrideHandle.ToString();
            parameters["?regionAssetURI"] = regiondata.regionAssetURI.ToString();
            parameters["?regionAssetRecvKey"] = regiondata.regionAssetRecvKey.ToString();
            parameters["?regionAssetSendKey"] = regiondata.regionAssetSendKey.ToString();
            parameters["?regionUserURI"] = regiondata.regionUserURI.ToString();
            parameters["?regionUserRecvKey"] = regiondata.regionUserRecvKey.ToString();
            parameters["?regionUserSendKey"] = regiondata.regionUserSendKey.ToString();
            parameters["?regionMapTexture"] = regiondata.regionMapTextureID.ToString();
            parameters["?serverHttpPort"] = regiondata.httpPort.ToString();
            parameters["?serverRemotingPort"] = regiondata.remotingPort.ToString();
            parameters["?owner_uuid"] = regiondata.owner_uuid.ToString();
            parameters["?originUUID"] = regiondata.originUUID.ToString();
            parameters["?access"] = regiondata.maturity.ToString();
            parameters["?product"] = Convert.ToInt32(regiondata.product).ToString();

            if (regiondata.OutsideIP != null)
            {
                parameters["?outside_ip"] = regiondata.OutsideIP;
            }
            else
            {
                parameters["?outside_ip"] = DBNull.Value;
            }

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parameters);
                    return true;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Adds a new profile to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>Successful?</returns>
        override public DataResponse AddProfile(RegionProfileData profile)
        {
            if (this.insertRegion(profile))
            {
                return DataResponse.RESPONSE_OK;
            }
            return DataResponse.RESPONSE_ERROR;
        }

        /// <summary>
        /// Update a sim profile
        /// </summary>
        /// <param name="profile">The profile to update</param>
        /// <returns>Sucessful?</returns>
        /// <remarks>Same as AddProfile</remarks>
        override public DataResponse UpdateProfile(RegionProfileData profile)
        {
            return AddProfile(profile);
        }

        /// <summary>
        /// Deletes a sim profile from the database
        /// </summary>
        /// <param name="uuid">the sim UUID</param>
        /// <returns>Successful?</returns>
        //public DataResponse DeleteProfile(RegionProfileData profile)
        override public DataResponse DeleteProfile(string uuid)
        {
            string sql = "DELETE FROM regions WHERE uuid = ?uuid;";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?uuid"] = uuid;

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parameters);
                    return DataResponse.RESPONSE_OK;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return DataResponse.RESPONSE_ERROR;
            }
        }

        /// <summary>
        /// DEPRECATED. Attempts to authenticate a region by comparing a shared secret.
        /// </summary>
        /// <param name="uuid">The UUID of the challenger</param>
        /// <param name="handle">The attempted regionHandle of the challenger</param>
        /// <param name="authkey">The secret</param>
        /// <returns>Whether the secret and regionhandle match the database entry for UUID</returns>
        override public bool AuthenticateSim(UUID uuid, ulong handle, string authkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            RegionProfileData data = GetProfileByUUID(uuid);

            return (handle == data.regionHandle && authkey == data.regionSecret);
        }

        /// <summary>
        /// NOT YET FUNCTIONAL. Provides a cryptographic authentication of a region
        /// </summary>
        /// <remarks>This requires a security audit.</remarks>
        /// <param name="uuid"></param>
        /// <param name="handle"></param>
        /// <param name="authhash"></param>
        /// <param name="challenge"></param>
        /// <returns></returns>
        public bool AuthenticateSim(UUID uuid, ulong handle, string authhash, string challenge)
        {
            // SHA512Managed HashProvider = new SHA512Managed();
            // Encoding TextProvider = new UTF8Encoding();

            // byte[] stream = TextProvider.GetBytes(uuid.ToString() + ":" + handle.ToString() + ":" + challenge);
            // byte[] hash = HashProvider.ComputeHash(stream);

            return false;
        }

        /// <summary>
        /// Reads a reservation row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A reservation data object</returns>
        public ReservationData readReservationRow(IDataReader reader)
        {
            ReservationData retval = new ReservationData();
            if (reader.Read())
            {
                retval.gridRecvKey = (string)reader["gridRecvKey"];
                retval.gridSendKey = (string)reader["gridSendKey"];
                retval.reservationCompany = (string)reader["resCompany"];
                retval.reservationMaxX = Convert.ToInt32(reader["resXMax"].ToString());
                retval.reservationMaxY = Convert.ToInt32(reader["resYMax"].ToString());
                retval.reservationMinX = Convert.ToInt32(reader["resXMin"].ToString());
                retval.reservationMinY = Convert.ToInt32(reader["resYMin"].ToString());
                retval.reservationName = (string)reader["resName"];
                retval.status = Convert.ToInt32(reader["status"].ToString()) == 1;
                UUID tmp;
                UUID.TryParse(Convert.ToString(reader["userUUID"]), out tmp);
                retval.userUUID = tmp;
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Adds a location reservation
        /// </summary>
        /// <param name="x">x coordinate</param>
        /// <param name="y">y coordinate</param>
        /// <returns></returns>
        override public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?x"] = x.ToString();
                param["?y"] = y.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT * FROM reservations WHERE resXMin <= ?x AND resXMax >= ?x AND resYMin <= ?y AND resYMax >= ?y";
                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        ReservationData row = this.readReservationRow(reader);
                        return row;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            } 
        }
    }
}
