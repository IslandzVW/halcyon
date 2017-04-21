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
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data.SimpleDB;
using System.Text;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    public class MySQLUserData : UserDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ConnectionFactory _connFactory;

        private string m_agentsTableName = "agents";
        private string m_usersTableName = "users";
        private string m_userFriendsTableName = "userfriends";
        private string m_appearanceTableName = "avatarappearance";
        private string m_attachmentsTableName = "avatarattachments";

        public override void Initialize()
        {
            m_log.Info("[MySQLUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitializedException(Name);
        }

        /// <summary>
        /// Initialize User Interface
        /// Loads and initializes the MySQL storage plugin
        /// Warns and uses the obsolete mysql_connection.ini if connect string is empty.
        /// Checks for migration
        /// </summary>
        /// <param name="connect">connect string.</param>
        public override void Initialize(string connect)
        {
            _connFactory = new ConnectionFactory("MySQL", connect);

            using (ISimpleDB conn = _connFactory.GetConnection())
            {
                m_log.Info("[MySQLUserData.InWorldz]: Started");
            }
        }

        public override void Dispose()
        {
        }

        public override void SaveUserPreferences(UserPreferencesData userPrefs)
        {
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms["?user_id"] = userPrefs.UserId;
                // RetrieveUserPreferences tests for "0" and "1"
                parms["?recv_ims_via_email"] = userPrefs.ReceiveIMsViaEmail ? "1" : "0";
                parms["?listed_in_directory"] = userPrefs.ListedInDirectory ? "1" : "0";

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "INSERT INTO userpreferences(user_id, recv_ims_via_email, listed_in_directory) ";
                    query += "VALUES (?user_id, ?recv_ims_via_email, ?listed_in_directory) ";
                    query += "ON DUPLICATE KEY UPDATE user_id=VALUES(user_id), recv_ims_via_email=VALUES(recv_ims_via_email), listed_in_directory=VALUES(listed_in_directory) ";

                    conn.QueryNoResults(query, parms);
                }
            }
            catch (Exception e)
            {
                m_log.Error("[MySQLUserData.InWorldz]: Could not save user preferences: " + e.ToString());
            }
        }

        public override UserPreferencesData RetrieveUserPreferences(UUID userId)
        {
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms["?userId"] = userId;

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT * FROM userpreferences WHERE user_id = ?userId";
                    List<Dictionary<string, string>> results = conn.QueryWithResults(query, parms);

                    if (results.Count > 0)
                    {
                        UserPreferencesData foundPrefs = new UserPreferencesData(userId,
                            Util.String2Bool(results[0]["recv_ims_via_email"]),
                            Util.String2Bool(results[0]["listed_in_directory"]));

                        return foundPrefs;
                    }
                    else
                    {
                        return UserPreferencesData.GetDefaultPreferences(userId);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[MySQLUserData.InWorldz]: Could not retrieve user preferences: " + e.ToString());
                return null;
            }
        }


        // see IUserDataPlugin

        /*
        public override UserInterestsData GetUserInterests(UUID avatarID)
        {
            MySQLSuperManager dbm = GetLockedConnection("GetUserInterests");

            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?userid"] = avatarID;

                IDbCommand result =
                    dbm.Manager.Query(
                        "Select skillsMask, skillsText, wantToMask, wantToText, languagesText from " + m_usersTableName + "where UUID = ?userid", param);
                IDataReader reader = result.ExecuteReader();
                UserInterestsData row = dbm.Manager.readUserInterests(reader);

                reader.Dispose();
                result.Dispose();
                return row;
            }
            catch (Exception e)
            {
                dbm.Manager.Reconnect();
                m_log.Error(e.ToString());
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }
        */

        /// <summary>
        /// Reads a user profile from an active data reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A user profile</returns>
        private UserProfileData readUserRow(IDataReader reader)
        {
            UserProfileData retval = new UserProfileData();

            if (reader.Read())
            {
                UUID id;
                if (!UUID.TryParse(Convert.ToString(reader["UUID"]), out id))
                    return null;

                retval.ID = id;
                retval.FirstName = (string)reader["username"];
                retval.SurName = (string)reader["lastname"];
                retval.Email = (reader.IsDBNull(reader.GetOrdinal("email"))) ? String.Empty : (string)reader["email"];

                retval.PasswordHash = (string)reader["passwordHash"];
                retval.PasswordSalt = (string)reader["passwordSalt"];

                retval.HomeRegion = Convert.ToUInt64(reader["homeRegion"].ToString());
                retval.HomeLocation = new Vector3(
                    Convert.ToSingle(reader["homeLocationX"].ToString()),
                    Convert.ToSingle(reader["homeLocationY"].ToString()),
                    Convert.ToSingle(reader["homeLocationZ"].ToString()));
                retval.HomeLookAt = new Vector3(
                    Convert.ToSingle(reader["homeLookAtX"].ToString()),
                    Convert.ToSingle(reader["homeLookAtY"].ToString()),
                    Convert.ToSingle(reader["homeLookAtZ"].ToString()));

                UUID regionID = UUID.Zero;
                UUID.TryParse(reader["homeRegionID"].ToString(), out regionID); // it's ok if it doesn't work; just use UUID.Zero
                retval.HomeRegionID = regionID;

                retval.Created = Convert.ToInt32(reader["created"].ToString());
                retval.LastLogin = Convert.ToInt32(reader["lastLogin"].ToString());

                retval.UserInventoryURI = (string)reader["userInventoryURI"];
                retval.UserAssetURI = (string)reader["userAssetURI"];

                if (reader.IsDBNull(reader.GetOrdinal("profileAboutText")))
                    retval.AboutText = String.Empty;
                else
                    retval.AboutText = (string)reader["profileAboutText"];

                if (reader.IsDBNull(reader.GetOrdinal("profileFirstText")))
                    retval.FirstLifeAboutText = String.Empty;
                else
                    retval.FirstLifeAboutText = (string)reader["profileFirstText"];

                if (reader.IsDBNull(reader.GetOrdinal("profileImage")))
                    retval.Image = UUID.Zero;
                else
                {
                    UUID tmp;
                    UUID.TryParse(Convert.ToString(reader["profileImage"]), out tmp);
                    retval.Image = tmp;
                }

                if (reader.IsDBNull(reader.GetOrdinal("profileFirstImage")))
                    retval.FirstLifeImage = UUID.Zero;
                else
                {
                    UUID tmp;
                    UUID.TryParse(Convert.ToString(reader["profileFirstImage"]), out tmp);
                    retval.FirstLifeImage = tmp;
                }

                if (reader.IsDBNull(reader.GetOrdinal("webLoginKey")))
                {
                    retval.WebLoginKey = UUID.Zero;
                }
                else
                {
                    UUID tmp;
                    UUID.TryParse(Convert.ToString(reader["webLoginKey"]), out tmp);
                    retval.WebLoginKey = tmp;
                }

                retval.UserFlags = Convert.ToInt32(reader["userFlags"].ToString());
                retval.GodLevel = Convert.ToInt32(reader["godLevel"].ToString());
                if (reader.IsDBNull(reader.GetOrdinal("customType")))
                    retval.CustomType = String.Empty;
                else
                    retval.CustomType = reader["customType"].ToString();

                if (reader.IsDBNull(reader.GetOrdinal("partner")))
                {
                    retval.Partner = UUID.Zero;
                }
                else
                {
                    UUID tmp;
                    UUID.TryParse(Convert.ToString(reader["partner"]), out tmp);
                    retval.Partner = tmp;
                }

                if (reader.IsDBNull(reader.GetOrdinal("profileURL")))
                    retval.ProfileURL = String.Empty;
                else
                    retval.ProfileURL = (string)reader["profileURL"];

            }
            else
            {
                return null;
            }
            return retval;
        }

        public override UserProfileData GetUserByName(string user, string last)
        {
            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?first"] = user;
                param["?second"] = last;

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT * FROM " + m_usersTableName + " WHERE username = ?first AND lastname = ?second";
                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        UserProfileData row = this.readUserRow(reader);
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

        #region User Friends List Data

        public override void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            int dtvalue = Util.UnixTimeSinceEpoch();

            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?ownerID"] = friendlistowner.ToString();
            param["?friendID"] = friend.ToString();
            param["?friendPerms"] = perms.ToString();
            param["?datetimestamp"] = dtvalue.ToString();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(
                        "INSERT INTO `" + m_userFriendsTableName + "` " +
                        "(`ownerID`,`friendID`,`friendPerms`,`datetimestamp`) " +
                        "VALUES " +
                        "(?ownerID,?friendID,?friendPerms,?datetimestamp)," +
                        "(?friendID,?ownerID,?friendPerms,?datetimestamp)",
                        param);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }
        }

        public override void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?ownerID"] = friendlistowner.ToString();
            param["?friendID"] = friend.ToString();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(
                        "delete from " + m_userFriendsTableName + " where " +
                        "(ownerID = ?ownerID and friendID = ?friendID) OR " +
                        "(ownerID = ?friendID and friendID = ?ownerID) ",
                        param);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }
        }

        public override void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?ownerID"] = friendlistowner.ToString();
            param["?friendID"] = friend.ToString();
            param["?friendPerms"] = perms.ToString();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(
                        "update " + m_userFriendsTableName +
                        " SET friendPerms = ?friendPerms " +
                        "where ownerID = ?ownerID and friendID = ?friendID",
                        param);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }
        }

        public override List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            List<FriendListItem> Lfli = new List<FriendListItem>();

            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?ownerID"] = friendlistowner.ToString();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "select a.ownerID,a.friendID,a.friendPerms,b.friendPerms as ownerperms from " +
                            m_userFriendsTableName + " as a, " + m_userFriendsTableName + " as b" +
                            " where a.ownerID = ?ownerID and b.ownerID = a.friendID and b.friendID = a.ownerID";

                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        while (reader.Read())
                        {
                            FriendListItem fli = new FriendListItem();
                            fli.FriendListOwner = new UUID(Convert.ToString(reader["ownerID"]));
                            fli.Friend = new UUID(Convert.ToString(reader["friendID"]));
                            fli.FriendPerms = (uint)Convert.ToInt32(reader["friendPerms"]);

                            // This is not a real column in the database table, it's a joined column from the opposite record
                            fli.FriendListOwnerPerms = (uint)Convert.ToInt32(reader["ownerperms"]);

                            Lfli.Add(fli);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return Lfli;
            }

            return Lfli;
        }

        private string GenerateInList(List<UUID> uuids)
        {
            StringBuilder inList = new StringBuilder("(");

            bool first = true;
            foreach (UUID uuid in uuids)
            {
                if (first) first = false;
                else inList.Append(",");

                inList.Append("'" + uuid.ToString() + "'");
            }

            inList.Append(")");

            return inList.ToString();
        }

        override public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids)
        {
            Dictionary<UUID, FriendRegionInfo> infos = new Dictionary<UUID, FriendRegionInfo>();
            if (uuids.Count == 0) return infos;

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "select UUID, agentOnline, currentHandle from " + m_agentsTableName +
                                          " where UUID IN " + GenerateInList(uuids);

                    using (IDataReader reader = conn.QueryAndUseReader(query))
                    {
                        while (reader.Read())
                        {
                            FriendRegionInfo fri = new FriendRegionInfo();
                            fri.isOnline = (sbyte)reader["agentOnline"] != 0;
                            fri.regionHandle = (ulong)reader["currentHandle"];

                            infos[new UUID((string)reader["UUID"])] = fri;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[MYSQL]: Got exception on trying to find friends regions:", e);
                m_log.Error(e.ToString());
            }

            return infos;
        }

        #endregion

        public override List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();

            Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9]");

            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length > 1 && !String.IsNullOrWhiteSpace(querysplit[1]))
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?first"] = objAlphaNumericPattern.Replace(querysplit[0], String.Empty) + "%";
                param["?second"] = objAlphaNumericPattern.Replace(querysplit[1], String.Empty) + "%";

                try
                {
                    using (ISimpleDB conn = _connFactory.GetConnection())
                    {
                        string squery = "SELECT UUID,username,lastname FROM " + m_usersTableName +
                                " WHERE username like ?first AND lastname like ?second LIMIT 100";

                        using (IDataReader reader = conn.QueryAndUseReader(squery, param))
                        {
                            while (reader.Read())
                            {
                                AvatarPickerAvatar user = new AvatarPickerAvatar();
                                user.AvatarID = new UUID(Convert.ToString(reader["UUID"]));
                                user.firstName = (string)reader["username"];
                                user.lastName = (string)reader["lastname"];
                                returnlist.Add(user);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            else
            {
                try
                {
                    Dictionary<string, object> param = new Dictionary<string, object>();
                    param["?first"] = objAlphaNumericPattern.Replace(querysplit[0], String.Empty) + "%";

                    using (ISimpleDB conn = _connFactory.GetConnection())
                    {
                        string squery = "SELECT UUID,username,lastname FROM " + m_usersTableName +
                            " WHERE username like ?first OR lastname like ?first LIMIT 100";

                        using (IDataReader reader = conn.QueryAndUseReader(squery, param))
                        {
                            while (reader.Read())
                            {
                                AvatarPickerAvatar user = new AvatarPickerAvatar();
                                user.AvatarID = new UUID(Convert.ToString(reader["UUID"]));
                                user.firstName = (string)reader["username"];
                                user.lastName = (string)reader["lastname"];
                                returnlist.Add(user);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            return returnlist;
        }

        /// <summary>
        /// See IUserDataPlugin
        /// </summary>
        /// <param name="uuid">User UUID</param>
        /// <returns>User profile data</returns>
        public override UserProfileData GetUserByUUID(UUID uuid)
        {
            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?uuid"] = uuid.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    using (IDataReader reader = conn.QueryAndUseReader("SELECT * FROM " + m_usersTableName + " WHERE UUID = ?uuid", param))
                    {
                        UserProfileData row = this.readUserRow(reader);

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
        /// Returns a user session searching by name
        /// </summary>
        /// <param name="name">The account name : "Username Lastname"</param>
        /// <returns>The users session</returns>
        public override UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user session by account name
        /// </summary>
        /// <param name="user">First part of the users account name</param>
        /// <param name="last">Second part of the users account name</param>
        /// <returns>The users session</returns>
        public override UserAgentData GetAgentByName(string user, string last)
        {
            UserProfileData profile = GetUserByName(user, last);
            return GetAgentByUUID(profile.ID);
        }

        /// <summary>
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="WebLoginKey"></param>
        /// <remarks>is it still used ?</remarks>

        public override void StoreWebLoginKey(UUID AgentID, UUID webLoginKey)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?UUID"] = AgentID.ToString();
            param["?webLoginKey"] = webLoginKey.ToString();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(
                         "update " + m_usersTableName + " SET webLoginKey = ?webLoginKey " +
                         "where UUID = ?UUID",
                         param);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }
        }

        /// <summary>
        /// Reads an agent row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A user session agent</returns>
        private UserAgentData readAgentRow(IDataReader reader)
        {
            UserAgentData retval = new UserAgentData();

            if (reader.Read())
            {
                // Agent IDs
                UUID tmp;
                if (!UUID.TryParse(Convert.ToString(reader["UUID"]), out tmp))
                    return null;
                retval.ProfileID = tmp;

                UUID.TryParse(Convert.ToString(reader["sessionID"]), out tmp);
                retval.SessionID = tmp;

                UUID.TryParse(Convert.ToString(reader["secureSessionID"]), out tmp);
                retval.SecureSessionID = tmp;

                // Agent Who?
                retval.AgentIP = (string)reader["agentIP"];
                retval.AgentPort = Convert.ToUInt32(reader["agentPort"].ToString());
                retval.AgentOnline = Convert.ToBoolean(Convert.ToInt16(reader["agentOnline"].ToString()));

                // Login/Logout times (UNIX Epoch)
                retval.LoginTime = Convert.ToInt32(reader["loginTime"].ToString());
                retval.LogoutTime = Convert.ToInt32(reader["logoutTime"].ToString());

                // Current position
                retval.Region = new UUID(Convert.ToString(reader["currentRegion"]));
                retval.Handle = Convert.ToUInt64(reader["currentHandle"].ToString());
                Vector3 tmp_v;
                Vector3.TryParse((string)reader["currentPos"], out tmp_v);
                retval.Position = tmp_v;
                Vector3.TryParse((string)reader["currentLookAt"], out tmp_v);
                retval.LookAt = tmp_v;
            }
            else
            {
                return null;
            }
            return retval;
        }

        /// <summary>
        /// Returns an agent session by account UUID
        /// </summary>
        /// <param name="uuid">The accounts UUID</param>
        /// <returns>The users session</returns>
        public override UserAgentData GetAgentByUUID(UUID uuid)
        {
            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?uuid"] = uuid.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    using (IDataReader reader = conn.QueryAndUseReader("SELECT * FROM " + m_agentsTableName + " WHERE UUID = ?uuid", param))
                    {
                        UserAgentData row = this.readAgentRow(reader);
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
        /// Creates a new user and inserts it into the database
        /// </summary>
        /// <param name="uuid">User ID</param>
        /// <param name="username">First part of the login</param>
        /// <param name="lastname">Second part of the login</param>
        /// <param name="passwordHash">A salted hash of the users password</param>
        /// <param name="passwordSalt">The salt used for the password hash</param>
        /// <param name="homeRegion">A regionHandle of the users home region</param>
        /// <param name="homeRegionID"> The UUID of the user's home region</param>
        /// <param name="homeLocX">Home region position vector</param>
        /// <param name="homeLocY">Home region position vector</param>
        /// <param name="homeLocZ">Home region position vector</param>
        /// <param name="homeLookAtX">Home region 'look at' vector</param>
        /// <param name="homeLookAtY">Home region 'look at' vector</param>
        /// <param name="homeLookAtZ">Home region 'look at' vector</param>
        /// <param name="created">Account created (unix timestamp)</param>
        /// <param name="lastlogin">Last login (unix timestamp)</param>
        /// <param name="inventoryURI">Users inventory URI</param>
        /// <param name="assetURI">Users asset URI</param>
        /// <param name="canDoMask">I can do mask</param>
        /// <param name="wantDoMask">I want to do mask</param>
        /// <param name="aboutText">Profile text</param>
        /// <param name="firstText">Firstlife text</param>
        /// <param name="profileImage">UUID for profile image</param>
        /// <param name="firstImage">UUID for firstlife image</param>
        /// <param name="webLoginKey">Ignored</param>
        /// <param name="profileURL">ProfileURL</param>
        /// <returns>Success?</returns>
        public bool insertUserRow(UUID uuid, string username, string lastname, string email, string passwordHash,
                                  string passwordSalt, UInt64 homeRegion, UUID homeRegionID, float homeLocX, float homeLocY, float homeLocZ,
                                  float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                  string inventoryURI, string assetURI, string aboutText, string firstText,
                                  UUID profileImage, UUID firstImage, UUID webLoginKey, int userFlags, int godLevel, string customType, UUID partner, string ProfileURL)
        {
            m_log.Debug("[MySQLManager]: Inserting profile for " + uuid.ToString());
            string sql =
                "INSERT INTO users (`UUID`, `username`, `lastname`, `email`, `passwordHash`, `passwordSalt`, `homeRegion`, `homeRegionID`, ";
            sql +=
                "`homeLocationX`, `homeLocationY`, `homeLocationZ`, `homeLookAtX`, `homeLookAtY`, `homeLookAtZ`, `created`, ";
            sql +=
                "`lastLogin`, `userInventoryURI`, `userAssetURI`, ";
            sql += "`profileFirstText`, `profileImage`, `profileFirstImage`, `webLoginKey`, `userFlags`, `godLevel`, `customType`, `partner`, `profileURL`) VALUES ";

            sql += "(?UUID, ?username, ?lastname, ?email, ?passwordHash, ?passwordSalt, ?homeRegion, ?homeRegionID, ";
            sql +=
                "?homeLocationX, ?homeLocationY, ?homeLocationZ, ?homeLookAtX, ?homeLookAtY, ?homeLookAtZ, ?created, ";
            sql +=
                "?lastLogin, ?userInventoryURI, ?userAssetURI, ";
            sql += "?profileFirstText, ?profileImage, ?profileFirstImage, ?webLoginKey, ?userFlags, ?godLevel, ?customType, ?partner, ?profileURL)";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?UUID"] = uuid.ToString();
            parameters["?username"] = username;
            parameters["?lastname"] = lastname;
            parameters["?email"] = email;
            parameters["?passwordHash"] = passwordHash;
            parameters["?passwordSalt"] = passwordSalt;
            parameters["?homeRegion"] = homeRegion;
            parameters["?homeRegionID"] = homeRegionID.ToString();
            parameters["?homeLocationX"] = homeLocX;
            parameters["?homeLocationY"] = homeLocY;
            parameters["?homeLocationZ"] = homeLocZ;
            parameters["?homeLookAtX"] = homeLookAtX;
            parameters["?homeLookAtY"] = homeLookAtY;
            parameters["?homeLookAtZ"] = homeLookAtZ;
            parameters["?created"] = created;
            parameters["?lastLogin"] = lastlogin;
            parameters["?userInventoryURI"] = inventoryURI;
            parameters["?userAssetURI"] = assetURI;
            parameters["?profileAboutText"] = aboutText;
            parameters["?profileFirstText"] = firstText;
            parameters["?profileImage"] = profileImage.ToString();
            parameters["?profileFirstImage"] = firstImage.ToString();
            parameters["?webLoginKey"] = webLoginKey.ToString();
            parameters["?userFlags"] = userFlags;
            parameters["?godLevel"] = godLevel;
            parameters["?customType"] = customType == null ? String.Empty : customType;
            parameters["?partner"] = partner.ToString();
            parameters["?profileURL"] = ProfileURL;
            bool returnval = false;

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parameters);
                    returnval = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Creates a new users profile
        /// </summary>
        /// <param name="user">The user profile to create</param>
        public override void AddNewUserProfile(UserProfileData user)
        {
            UUID zero = UUID.Zero;
            if (user.ID == zero)
            {
                return;
            }

            try
            {
                this.insertUserRow(user.ID, user.FirstName, user.SurName, user.Email, user.PasswordHash, user.PasswordSalt,
                                      user.HomeRegion, user.HomeRegionID, user.HomeLocation.X, user.HomeLocation.Y,
                                      user.HomeLocation.Z,
                                      user.HomeLookAt.X, user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created,
                                      user.LastLogin, user.UserInventoryURI, user.UserAssetURI,
                                      user.AboutText, user.FirstLifeAboutText, user.Image,
                                      user.FirstLifeImage, user.WebLoginKey, user.UserFlags, user.GodLevel, user.CustomType, user.Partner, user.ProfileURL);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Creates a new agent and inserts it into the database
        /// </summary>
        /// <param name="agentdata">The agent data to be inserted</param>
        /// <returns>Success?</returns>
        public bool insertAgentRow(UserAgentData agentdata)
        {
            // m_log.ErrorFormat("[MYSQL]: REPLACE AgentData: {0} at {1} {2}", agentdata.ProfileID, agentdata.Handle, agentdata.Position);
            string sql = String.Empty;
            sql += "REPLACE INTO ";
            sql += "agents (UUID, sessionID, secureSessionID, agentIP, agentPort, agentOnline, loginTime, logoutTime, currentRegion, currentHandle, currentPos, currentLookAt) VALUES ";
            sql += "(?UUID, ?sessionID, ?secureSessionID, ?agentIP, ?agentPort, ?agentOnline, ?loginTime, ?logoutTime, ?currentRegion, ?currentHandle, ?currentPos, ?currentLookAt);";
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["?UUID"] = agentdata.ProfileID.ToString();
            parameters["?sessionID"] = agentdata.SessionID.ToString();
            parameters["?secureSessionID"] = agentdata.SecureSessionID.ToString();
            parameters["?agentIP"] = agentdata.AgentIP.ToString();
            parameters["?agentPort"] = agentdata.AgentPort.ToString();
            parameters["?agentOnline"] = (agentdata.AgentOnline == true) ? "1" : "0";
            parameters["?loginTime"] = agentdata.LoginTime.ToString();
            parameters["?logoutTime"] = agentdata.LogoutTime.ToString();
            parameters["?currentRegion"] = agentdata.Region.ToString();
            parameters["?currentHandle"] = agentdata.Handle.ToString();
            parameters["?currentPos"] = "<" + (agentdata.Position.X).ToString().Replace(",", ".") + "," + (agentdata.Position.Y).ToString().Replace(",", ".") + "," + (agentdata.Position.Z).ToString().Replace(",", ".") + ">";
            parameters["?currentLookAt"] = "<" + (agentdata.LookAt.X).ToString().Replace(",", ".") + "," + (agentdata.LookAt.Y).ToString().Replace(",", ".") + "," + (agentdata.LookAt.Z).ToString().Replace(",", ".") + ">";

            bool returnval = false;

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parameters);
                    returnval = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Creates a new agent
        /// </summary>
        /// <param name="agent">The agent to create</param>
        public override void AddNewUserAgent(UserAgentData agent)
        {
            UUID zero = UUID.Zero;
            if (agent.ProfileID == zero || agent.SessionID == zero)
                return;

            try
            {
                this.insertAgentRow(agent);
                this.UpdateLoginHistory(agent);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        private void UpdateLoginHistory(UserAgentData agent)
        {
            if (agent.AgentOnline)
            {
                const string UPDATE_HISTORY =
                    @"REPLACE INTO LoginHistory(session_id, user_id, login_time, session_ip, last_region) 
                    VALUES(?sessionId, ?userId, ?loginTime, ?sessionIp, ?lastRegion)";

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters["?sessionId"] = agent.SessionID;
                parameters["?userId"] = agent.ProfileID;
                parameters["?loginTime"] = Util.UnixToLocalDateTime(agent.LoginTime);
                parameters["?sessionIp"] = agent.AgentIP;
                parameters["?lastRegion"] = agent.Region;

                try
                {
                    using (ISimpleDB conn = _connFactory.GetConnection())
                    {
                        conn.QueryNoResults(UPDATE_HISTORY, parameters);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[MySQLUserData.InWorldz] UpdateLoginHistory: " + e.ToString());
                }
            }
            else
            {
                const string RECORD_LOGOUT =
                    @"UPDATE LoginHistory SET last_region = ?lastRegion, logout_time = ?logoutTime
                    WHERE session_id = ?sessionId";

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters["?lastRegion"] = agent.Region;
                parameters["?logoutTime"] = DateTime.Now;
                parameters["?sessionId"] = agent.SessionID;

                try
                {
                    using (ISimpleDB conn = _connFactory.GetConnection())
                    {
                        conn.QueryNoResults(RECORD_LOGOUT, parameters);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[MySQLUserData.InWorldz] UpdateLoginHistory: " + e.ToString());
                }
            }
        }

        /// <summary>
        /// Update user data into the database where User ID = uuid
        /// </summary>
        /// <param name="uuid">User ID</param>
        /// <param name="username">First part of the login</param>
        /// <param name="lastname">Second part of the login</param>
        /// <param name="passwordHash">A salted hash of the users password</param>
        /// <param name="passwordSalt">The salt used for the password hash</param>
        /// <param name="homeRegion">A regionHandle of the users home region</param>
        /// <param name="homeLocX">Home region position vector</param>
        /// <param name="homeLocY">Home region position vector</param>
        /// <param name="homeLocZ">Home region position vector</param>
        /// <param name="homeLookAtX">Home region 'look at' vector</param>
        /// <param name="homeLookAtY">Home region 'look at' vector</param>
        /// <param name="homeLookAtZ">Home region 'look at' vector</param>
        /// <param name="created">Account created (unix timestamp)</param>
        /// <param name="lastlogin">Last login (unix timestamp)</param>
        /// <param name="inventoryURI">Users inventory URI</param>
        /// <param name="assetURI">Users asset URI</param>
        /// <param name="canDoMask">I can do mask</param>
        /// <param name="wantDoMask">I want to do mask</param>
        /// <param name="aboutText">Profile text</param>
        /// <param name="firstText">Firstlife text</param>
        /// <param name="profileImage">UUID for profile image</param>
        /// <param name="firstImage">UUID for firstlife image</param>
        /// <param name="webLoginKey">UUID for weblogin Key</param>
        /// <param name="profileURL">Profile  URL text</param>
        /// <returns>Success?</returns>
        private bool updateUserRow(UUID uuid, string username, string lastname, string email, string passwordHash,
                                  string passwordSalt, UInt64 homeRegion, UUID homeRegionID, float homeLocX, float homeLocY, float homeLocZ,
                                  float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                  string inventoryURI, string assetURI, string profileURL, string aboutText, string firstText,
                                  UUID profileImage, UUID firstImage, UUID webLoginKey, int userFlags, int godLevel, string customType, UUID partner)
        {
            string sql = "UPDATE users SET `username` = ?username , `lastname` = ?lastname, `email` = ?email ";
            sql += ", `passwordHash` = ?passwordHash , `passwordSalt` = ?passwordSalt , ";
            sql += "`homeRegion` = ?homeRegion , `homeRegionID` = ?homeRegionID, `homeLocationX` = ?homeLocationX , ";
            sql += "`homeLocationY`  = ?homeLocationY , `homeLocationZ` = ?homeLocationZ , ";
            sql += "`homeLookAtX` = ?homeLookAtX , `homeLookAtY` = ?homeLookAtY , ";
            sql += "`homeLookAtZ` = ?homeLookAtZ , `created` = ?created , `lastLogin` = ?lastLogin , ";
            sql += "`userInventoryURI` = ?userInventoryURI , `userAssetURI` = ?userAssetURI , ";
            sql += "`profileURL` =?profileURL , ";
            sql += "`profileAboutText` = ?profileAboutText , `profileFirstText` = ?profileFirstText, ";
            sql += "`profileImage` = ?profileImage , `profileFirstImage` = ?profileFirstImage , ";
            sql += "`userFlags` = ?userFlags , `godLevel` = ?godLevel , ";
            sql += "`customType` = ?customType , ";
            sql += "`webLoginKey` = ?webLoginKey WHERE UUID = ?UUID";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["?UUID"] = uuid.ToString();
            parameters["?username"] = username;
            parameters["?lastname"] = lastname;
            parameters["?email"] = email;
            parameters["?passwordHash"] = passwordHash;
            parameters["?passwordSalt"] = passwordSalt;
            parameters["?homeRegion"] = homeRegion;
            parameters["?homeRegionID"] = homeRegionID.ToString();
            parameters["?homeLocationX"] = homeLocX;
            parameters["?homeLocationY"] = homeLocY;
            parameters["?homeLocationZ"] = homeLocZ;
            parameters["?homeLookAtX"] = homeLookAtX;
            parameters["?homeLookAtY"] = homeLookAtY;
            parameters["?homeLookAtZ"] = homeLookAtZ;
            parameters["?created"] = created;
            parameters["?lastLogin"] = lastlogin;
            parameters["?userInventoryURI"] = inventoryURI;
            parameters["?userAssetURI"] = assetURI;
            parameters["?profileURL"] = profileURL;
            parameters["?profileAboutText"] = aboutText;
            parameters["?profileFirstText"] = firstText;
            parameters["?profileImage"] = profileImage.ToString();
            parameters["?profileFirstImage"] = firstImage.ToString();
            parameters["?webLoginKey"] = webLoginKey.ToString();
            parameters["?userFlags"] = userFlags;
            parameters["?godLevel"] = godLevel;
            parameters["?customType"] = customType == null ? String.Empty : customType;

            bool returnval = false;
            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parameters);
                    returnval = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Updates a user profile stored in the DB
        /// </summary>
        /// <param name="user">The profile data to use to update the DB</param>
        public override bool UpdateUserProfile(UserProfileData user)
        {

            this.updateUserRow(user.ID, user.FirstName, user.SurName, user.Email, user.PasswordHash, user.PasswordSalt,
                              user.HomeRegion, user.HomeRegionID, user.HomeLocation.X, user.HomeLocation.Y,
                              user.HomeLocation.Z, user.HomeLookAt.X,
                              user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created, user.LastLogin,
                              user.UserInventoryURI,
                              user.UserAssetURI, user.ProfileURL, user.AboutText,
                              user.FirstLifeAboutText, user.Image, user.FirstLifeImage, user.WebLoginKey,
                              user.UserFlags, user.GodLevel, user.CustomType, user.Partner);


            return true;
        }

        /*
        public override bool UpdateUserInterests(UserInterestsData user)
        {
            MySQLSuperManager dbm = GetLockedConnection("UpdateUserInterests");
            try
            {
                dbm.Manager.updateUserInterests(user.ID, user.SkillsMask, user.SkillsText, user.WantToMask, user.WantToText, user.LanguagesText);
            }
            finally
            {
                dbm.Release();
            }

            return true;
        }
         */

        /// <summary>
        /// Performs a money transfer request between two accounts
        /// </summary>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Success?</returns>
        public override bool MoneyTransferRequest(UUID from, UUID to, uint amount)
        {
            return false;
        }

        /// <summary>
        /// Performs an inventory transfer request between two accounts
        /// </summary>
        /// <remarks>TODO: Move to inventory server</remarks>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="item">The item to transfer</param>
        /// <returns>Success?</returns>
        public override bool InventoryTransferRequest(UUID from, UUID to, UUID item)
        {
            return false;
        }

        /// <summary>
        /// Reads an avatar appearence from an active data reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>An avatar appearence</returns>
        private AvatarAppearance readAppearanceRow(IDataReader reader)
        {
            AvatarAppearance appearance = null;
            if (reader.Read())
            {
                appearance = new AvatarAppearance();
                appearance.Owner = new UUID(Convert.ToString(reader["owner"]));
                appearance.Serial = Convert.ToInt32(reader["serial"]);
                appearance.VisualParams = (byte[])reader["visual_params"];

                if (reader["texture"] is DBNull)
                {
                    appearance.Texture = new Primitive.TextureEntry(null);
                }
                else
                {
                    appearance.Texture = new Primitive.TextureEntry((byte[])reader["texture"], 0, ((byte[])reader["texture"]).Length);
                }

                appearance.AvatarHeight = (float)Convert.ToDouble(reader["avatar_height"]);

                // This handles the v1 style wearables list with a 1:1 relationship.  
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.BODY, 
                        new UUID(Convert.ToString(reader["body_item"])), 
                        new UUID(Convert.ToString(reader["body_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.SKIN,
                        new UUID(Convert.ToString(reader["skin_item"])),
                        new UUID(Convert.ToString(reader["skin_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.HAIR,
                        new UUID(Convert.ToString(reader["hair_item"])),
                        new UUID(Convert.ToString(reader["hair_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.EYES,
                        new UUID(Convert.ToString(reader["eyes_item"])),
                        new UUID(Convert.ToString(reader["eyes_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.SHIRT,
                        new UUID(Convert.ToString(reader["shirt_item"])),
                        new UUID(Convert.ToString(reader["shirt_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.PANTS,
                        new UUID(Convert.ToString(reader["pants_item"])),
                        new UUID(Convert.ToString(reader["pants_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.SHOES,
                        new UUID(Convert.ToString(reader["shoes_item"])),
                        new UUID(Convert.ToString(reader["shoes_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.SOCKS,
                        new UUID(Convert.ToString(reader["socks_item"])),
                        new UUID(Convert.ToString(reader["socks_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.JACKET,
                        new UUID(Convert.ToString(reader["jacket_item"])),
                        new UUID(Convert.ToString(reader["jacket_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.GLOVES,
                        new UUID(Convert.ToString(reader["gloves_item"])),
                        new UUID(Convert.ToString(reader["gloves_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.UNDERSHIRT,
                        new UUID(Convert.ToString(reader["undershirt_item"])),
                        new UUID(Convert.ToString(reader["undershirt_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.UNDERPANTS,
                        new UUID(Convert.ToString(reader["underpants_item"])),
                        new UUID(Convert.ToString(reader["underpants_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.SKIRT,
                        new UUID(Convert.ToString(reader["skirt_item"])),
                        new UUID(Convert.ToString(reader["skirt_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.ALPHA,
                        new UUID(Convert.ToString(reader["alpha_item"])),
                        new UUID(Convert.ToString(reader["alpha_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.TATTOO,
                        new UUID(Convert.ToString(reader["tattoo_item"])),
                        new UUID(Convert.ToString(reader["tattoo_asset"]))));
                appearance.SetWearable(
                    new AvatarWearable(
                        AvatarWearable.PHYSICS,
                        new UUID(Convert.ToString(reader["physics_item"])),
                        new UUID(Convert.ToString(reader["physics_asset"]))));
            }
            return appearance;
        }

        /// <summary>
        /// Appearance
        /// TODO: stubs for now to get us to a compiling state gently
        /// override
        /// </summary>
        public override AvatarAppearance GetUserAppearance(UUID user)
        {
            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?owner"] = user.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT * FROM " + m_appearanceTableName + " WHERE owner = ?owner";
                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        AvatarAppearance appearance = this.readAppearanceRow(reader);

                        if (null == appearance)
                        {
                            m_log.WarnFormat("[USER DB] No appearance found for user {0}", user.ToString());
                            return null;
                        }

                        appearance.SetAttachments(GetUserAttachments(user));
                        return appearance;
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
        /// Appearance
        /// </summary>
        public override AvatarAppearance GetBotOutfit(UUID user, string outfitName)
        {
            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?owner"] = user.ToString();
                param["?outfitName"] = outfitName.ToString();

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT * FROM botappearance WHERE owner = ?owner AND outfitName = ?outfitName";
                    AvatarAppearance appearance = null;
                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        appearance = this.readAppearanceRow(reader);

                        if (null == appearance)
                        {
                            m_log.WarnFormat("[USER DB] No appearance found for bot {0} - {1}", user.ToString(), outfitName);
                            return null;
                        }

                        appearance.SetAttachments(GetBotAttachments(user, outfitName));
                    }

                    //Update the last updated column
                    param.Add("?lastUsed", Util.UnixTimeSinceEpoch());
                    string sql = "UPDATE botappearance SET `lastUsed` = ?lastUsed WHERE owner = ?owner AND outfitName = ?outfitName";
                    conn.QueryNoResults(sql, param);

                    return appearance;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Create (or replace if existing) an avatar appearence
        /// </summary>
        /// <param name="appearance"></param>
        /// <returns>Succes?</returns>
        public bool insertAppearanceRow(AvatarAppearance appearance)
        {
            string sql = String.Empty;
            sql += "REPLACE INTO ";
            sql += "avatarappearance (owner, serial, visual_params, texture, avatar_height, ";
            sql += "body_item, body_asset, skin_item, skin_asset, hair_item, hair_asset, eyes_item, eyes_asset, ";
            sql += "shirt_item, shirt_asset, pants_item, pants_asset, shoes_item, shoes_asset, socks_item, socks_asset, ";
            sql += "jacket_item, jacket_asset, gloves_item, gloves_asset, undershirt_item, undershirt_asset, underpants_item, underpants_asset, ";
            sql += "skirt_item, skirt_asset, alpha_item, alpha_asset, tattoo_item, tattoo_asset, physics_item, physics_asset) values (";
            sql += "?owner, ?serial, ?visual_params, ?texture, ?avatar_height, ";
            sql += "?body_item, ?body_asset, ?skin_item, ?skin_asset, ?hair_item, ?hair_asset, ?eyes_item, ?eyes_asset, ";
            sql += "?shirt_item, ?shirt_asset, ?pants_item, ?pants_asset, ?shoes_item, ?shoes_asset, ?socks_item, ?socks_asset, ";
            sql += "?jacket_item, ?jacket_asset, ?gloves_item, ?gloves_asset, ?undershirt_item, ?undershirt_asset, ?underpants_item, ?underpants_asset, ";
            sql += "?skirt_item, ?skirt_asset, ?alpha_item, ?alpha_asset, ?tattoo_item, ?tattoo_asset, ?physics_item, ?physics_asset)";

            bool returnval = false;

            // we want to send in byte data, which means we can't just pass down strings
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                CreateParametersForAppearance(appearance, parms);

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parms);
                    returnval = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;

        }

        /// <summary>
        /// Create (or replace if existing) an avatar appearence
        /// </summary>
        /// <param name="appearance"></param>
        /// <returns>Succes?</returns>
        public bool insertBotAppearanceRow(string outfitName, AvatarAppearance appearance)
        {
            string sql = String.Empty;
            sql += "REPLACE INTO ";
            sql += "botappearance (owner, outfitName, lastUsed, serial, visual_params, texture, avatar_height, ";
            sql += "body_item, body_asset, skin_item, skin_asset, hair_item, hair_asset, eyes_item, eyes_asset, ";
            sql += "shirt_item, shirt_asset, pants_item, pants_asset, shoes_item, shoes_asset, socks_item, socks_asset, ";
            sql += "jacket_item, jacket_asset, gloves_item, gloves_asset, undershirt_item, undershirt_asset, underpants_item, underpants_asset, ";
            sql += "skirt_item, skirt_asset, alpha_item, alpha_asset, tattoo_item, tattoo_asset, physics_item, physics_asset) values (";
            sql += "?owner, ?outfitName, ?lastUsed, ?serial, ?visual_params, ?texture, ?avatar_height, ";
            sql += "?body_item, ?body_asset, ?skin_item, ?skin_asset, ?hair_item, ?hair_asset, ?eyes_item, ?eyes_asset, ";
            sql += "?shirt_item, ?shirt_asset, ?pants_item, ?pants_asset, ?shoes_item, ?shoes_asset, ?socks_item, ?socks_asset, ";
            sql += "?jacket_item, ?jacket_asset, ?gloves_item, ?gloves_asset, ?undershirt_item, ?undershirt_asset, ?underpants_item, ?underpants_asset, ";
            sql += "?skirt_item, ?skirt_asset, ?alpha_item, ?alpha_asset, ?tattoo_item, ?tattoo_asset, ?physics_item, ?physics_asset)";

            bool returnval = false;

            // we want to send in byte data, which means we can't just pass down strings
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                CreateParametersForAppearance(appearance, parms);
                parms.Add("?outfitName", outfitName);
                parms.Add("?lastUsed", Util.UnixTimeSinceEpoch());

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parms);
                    returnval = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;

        }

        private static void CreateParametersForAppearance(AvatarAppearance appearance, Dictionary<string, object> parms)
        {
            parms.Add("?owner", appearance.Owner.ToString());
            parms.Add("?serial", appearance.Serial);
            parms.Add("?visual_params", appearance.VisualParams);
            parms.Add("?texture", appearance.Texture.GetBytes());
            parms.Add("?avatar_height", appearance.AvatarHeight);

            // The Old Mechanism. 1:1 point to wearable
            AvatarWearable wearable;
            wearable = appearance.GetWearableOfType(AvatarWearable.BODY);
            parms.Add("?body_item", wearable.ItemID.ToString());
            parms.Add("?body_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.SKIN);
            parms.Add("?skin_item", wearable.ItemID.ToString());
            parms.Add("?skin_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.HAIR);
            parms.Add("?hair_item", wearable.ItemID.ToString());
            parms.Add("?hair_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.EYES);
            parms.Add("?eyes_item", wearable.ItemID.ToString());
            parms.Add("?eyes_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.SHIRT);
            parms.Add("?shirt_item", wearable.ItemID.ToString());
            parms.Add("?shirt_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.PANTS);
            parms.Add("?pants_item", wearable.ItemID.ToString());
            parms.Add("?pants_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.SHOES);
            parms.Add("?shoes_item", wearable.ItemID.ToString());
            parms.Add("?shoes_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.SOCKS);
            parms.Add("?socks_item", wearable.ItemID.ToString());
            parms.Add("?socks_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.JACKET);
            parms.Add("?jacket_item", wearable.ItemID.ToString());
            parms.Add("?jacket_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.GLOVES);
            parms.Add("?gloves_item", wearable.ItemID.ToString());
            parms.Add("?gloves_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.UNDERSHIRT);
            parms.Add("?undershirt_item", wearable.ItemID.ToString());
            parms.Add("?undershirt_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.UNDERPANTS);
            parms.Add("?underpants_item", wearable.ItemID.ToString());
            parms.Add("?underpants_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.SKIRT);
            parms.Add("?skirt_item", wearable.ItemID.ToString());
            parms.Add("?skirt_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.ALPHA);
            parms.Add("?alpha_item", wearable.ItemID.ToString());
            parms.Add("?alpha_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.TATTOO);
            parms.Add("?tattoo_item", wearable.ItemID.ToString());
            parms.Add("?tattoo_asset", wearable.AssetID.ToString());
            wearable = appearance.GetWearableOfType(AvatarWearable.PHYSICS);
            parms.Add("?physics_item", wearable.ItemID.ToString());
            parms.Add("?physics_asset", wearable.AssetID.ToString());
        }

        /// <summary>
        /// Updates an avatar appearence
        /// </summary>
        /// <param name="user">The user UUID</param>
        /// <param name="appearance">The avatar appearance</param>
        // override
        public override void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            try
            {
                appearance.Owner = user;
                this.insertAppearanceRow(appearance);
                UpdateUserAttachments(user, appearance.GetAttachments());
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Adds an outfit into the database
        /// </summary>
        /// <param name="user">The user UUID</param>
        /// <param name="appearance">The avatar appearance</param>
        // override
        public override void AddOrUpdateBotOutfit(UUID userID, string outfitName, AvatarAppearance appearance)
        {
            try
            {
                appearance.Owner = userID;
                this.insertBotAppearanceRow(outfitName, appearance);
                UpdateBotAttachments(userID, outfitName, appearance.GetAttachments());
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        public override void RemoveBotOutfit(UUID userID, string outfitName)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?UUID"] = userID.ToString();
            param["?outfitName"] = outfitName;

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(
                        "delete from botattachments where " +
                        "(UUID = ?UUID and outfitName = ?outfitName) ",
                        param);
                    conn.QueryNoResults(
                        "delete from botappearance where " +
                        "(owner = ?UUID and outfitName = ?outfitName) ",
                        param);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }
        }

        public override List<string> GetBotOutfitsByOwner(UUID userID)
        {
            List<string> ret = new List<string>();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    Dictionary<string, object> param = new Dictionary<string, object>();
                    string query = "SELECT outfitName from botappearance WHERE owner = ?owner";
                    param["?owner"] = userID.ToString();

                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        while (reader.Read())
                        {
                            string outfitName = reader["outfitName"].ToString();
                            ret.Add(outfitName);
                        }

                        reader.Close();
                        return ret;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        public override void SetCachedBakedTextures(Dictionary<UUID, UUID> bakedTextures)
        {
            string sql = String.Empty;
            //Replace into, as if the clothing gets reworn, we want to be able to use
            //  the newest baked texture if something was corrupted the first time around
            sql += "REPLACE INTO cachedbakedtextures (cache, texture) values ";
            for(int i = 1; i < bakedTextures.Count + 1; i++)
                sql += "(?cache" + i + ", ?texture" + i + "), ";
            sql = sql.Remove(sql.Length - 2);//Remove the ", "
            try
            {
                Dictionary<string, object> parms = new Dictionary<string, object>();
                int i = 1;
                foreach(KeyValuePair<UUID, UUID> kvp in bakedTextures)
                {
                    parms.Add("?cache" + i, kvp.Key.ToString());
                    parms.Add("?texture" + i, kvp.Value.ToString());
                    i++;
                }

                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(sql, parms);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }

        public override List<CachedAgentArgs> GetCachedBakedTextures(List<CachedAgentArgs> args)
        {
            List<CachedAgentArgs> ret = new List<CachedAgentArgs>();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    Dictionary<string, object> param = new Dictionary<string, object>();
                    string query = "SELECT cache, texture from cachedbakedtextures WHERE ";
                    int i = 0;
                    foreach (CachedAgentArgs arg in args)
                    {
                        query += "cache = ?uuid" + i + " OR ";
                        param["?uuid" + i++] = arg.ID;
                    }
                    query = query.Remove(query.Length - 4); //Remove " OR "

                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        while (reader.Read())
                        {
                            UUID texture = new UUID(reader["texture"].ToString());
                            UUID cachedID = new UUID(reader["cache"].ToString());
                            CachedAgentArgs foundArg = args.Find((a) => a.ID == cachedID);
                            ret.Add(new CachedAgentArgs() { ID = texture, TextureIndex = foundArg.TextureIndex });
                        }

                        reader.Close();
                        return ret;
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
        /// Database provider name
        /// </summary>
        /// <returns>Provider name</returns>
        public override string Name
        {
            get { return "MySQL Userdata Interface"; }
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        public override string Version
        {
            get { return "0.1"; }
        }

        public List<AvatarAttachment> GetUserAttachments(UUID agentID)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?uuid"] = agentID.ToString();
            List<AvatarAttachment> ret = new List<AvatarAttachment>();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT attachpoint, item, asset from " + m_attachmentsTableName + " WHERE UUID = ?uuid";
                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        while (reader.Read())
                        {
                            int attachpoint = Convert.ToInt32(reader["attachpoint"]);
                            UUID itemID = new UUID(reader["item"].ToString());
                            UUID assetID = new UUID(reader["asset"].ToString());
                            AvatarAttachment attachment = new AvatarAttachment(attachpoint, itemID, assetID);
                            ret.Add(attachment);
                        }

                        reader.Close();
                        return ret;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        public List<AvatarAttachment> GetBotAttachments(UUID agentID, string outfitName)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?uuid"] = agentID.ToString();
            param["?outfitName"] = outfitName;
            List<AvatarAttachment> ret = new List<AvatarAttachment>();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    string query = "SELECT attachpoint, item, asset from botattachments WHERE UUID = ?uuid AND outfitName = ?outfitName";
                    using (IDataReader reader = conn.QueryAndUseReader(query, param))
                    {
                        while (reader.Read())
                        {
                            int attachpoint = Convert.ToInt32(reader["attachpoint"]);
                            UUID itemID = new UUID(reader["item"].ToString());
                            UUID assetID = new UUID(reader["asset"].ToString());
                            AvatarAttachment attachment = new AvatarAttachment(attachpoint, itemID, assetID);
                            ret.Add(attachment);
                        }

                        reader.Close();
                        return ret;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return null;
            }
        }

        private string GenerateAttachmentParams(int number, string outfitName)
        {
            StringBuilder parms = new StringBuilder();
            if (number != 0) parms.Append(",");
            parms.Append("(");
            parms.Append("?uuid" + Convert.ToSingle(number) + ",");
            if (outfitName != null)
                parms.Append("?outfitName" + Convert.ToSingle(number) + ",");
            parms.Append("?attachpoint" + Convert.ToSingle(number) + ",");
            parms.Append("?item" + Convert.ToSingle(number) + ",");
            parms.Append("?asset" + Convert.ToSingle(number));
            parms.Append(")");

            return parms.ToString();
        }

        public void UpdateUserAttachments(UUID agentID, List<AvatarAttachment> attachments)
        {
            using (ISimpleDB conn = _connFactory.GetConnection())
            {
                using (ITransaction transaction = conn.BeginTransaction())
                {
                    string sql = "delete from avatarattachments where UUID = ?uuid";

                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    parms.Add("?uuid", agentID.ToString());

                    conn.QueryNoResults(sql, parms);

                    if (attachments == null || attachments.Count == 0)
                    {
                        transaction.Commit();
                        return;
                    }

                    parms.Clear();
                    sql = "insert into avatarattachments (UUID, attachpoint, item, asset) values ";

                    int number = 0;
                    foreach (AvatarAttachment attachment in attachments)
                    {
                        sql += this.GenerateAttachmentParams(number, null);

                        parms.Add("?uuid" + Convert.ToString(number), agentID.ToString());
                        parms.Add("?attachpoint" + Convert.ToString(number), attachment.AttachPoint);
                        parms.Add("?item" + Convert.ToString(number), attachment.ItemID.ToString());
                        parms.Add("?asset" + Convert.ToString(number), attachment.AssetID.ToString());

                        number++;
                    }

                    conn.QueryNoResults(sql, parms);
                    transaction.Commit();
                }
            }
        }

        public void UpdateBotAttachments(UUID agentID, string outfitName, List<AvatarAttachment> attachments)
        {
            using (ISimpleDB conn = _connFactory.GetConnection())
            {
                using (ITransaction transaction = conn.BeginTransaction())
                {
                    string sql = "delete from botattachments where UUID = ?uuid and outfitName = ?outfitName";

                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    parms.Add("?uuid", agentID.ToString());
                    parms.Add("?outfitName", outfitName);

                    conn.QueryNoResults(sql, parms);

                    if (attachments == null || attachments.Count == 0)
                    {
                        transaction.Commit();
                        return;
                    }

                    parms.Clear();
                    sql = "insert into botattachments (UUID, outfitName, attachpoint, item, asset) values ";

                    int number = 0;
                    foreach (AvatarAttachment attachment in attachments)
                    {
                        sql += this.GenerateAttachmentParams(number, outfitName);

                        parms.Add("?uuid" + Convert.ToString(number), agentID.ToString());
                        parms.Add("?outfitName" + Convert.ToString(number), outfitName);
                        parms.Add("?attachpoint" + Convert.ToString(number), attachment.AttachPoint);
                        parms.Add("?item" + Convert.ToString(number), attachment.ItemID.ToString());
                        parms.Add("?asset" + Convert.ToString(number), attachment.AssetID.ToString());

                        number++;
                    }

                    conn.QueryNoResults(sql, parms);
                    transaction.Commit();
                }
            }
        }

        public override void ResetAttachments(UUID userID)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?uuid"] = userID.ToString();

            using (ISimpleDB conn = _connFactory.GetConnection())
            {
                conn.QueryNoResults(
                    "UPDATE " + m_attachmentsTableName +
                    " SET asset = '00000000-0000-0000-0000-000000000000' WHERE UUID = ?uuid",
                    param);
            }
        }

        public override void LogoutUsers(UUID regionID)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["?regionID"] = regionID.ToString();
            param["?logoutTime"] = Util.UnixTimeSinceEpoch();

            try
            {
                using (ISimpleDB conn = _connFactory.GetConnection())
                {
                    conn.QueryNoResults(
                         "update " + m_agentsTableName + " SET agentOnline = 0, logoutTime = ?logoutTime " +
                         "where currentRegion = ?regionID and agentOnline != 0",
                         param);
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return;
            }
        }
    }
}
