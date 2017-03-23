/*
 * Copyright (c) 2015, InWorldz Halcyon Developers
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice, this
 *     list of conditions and the following disclaimer.
 * 
 *   * Redistributions in binary form must reproduce the above copyright notice,
 *     this list of conditions and the following disclaimer in the documentation
 *     and/or other materials provided with the distribution.
 * 
 *   * Neither the name of halcyon nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using OpenMetaverse;
using log4net;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Data.SimpleDB;

namespace OpenSim.Region.OptionalModules.Avatar.FlexiGroups
{
    class NativeGroupDataProvider : IGroupDataProvider
    {
        public readonly UUID EVERYONE_ROLEID = UUID.Zero;

        private ConnectionFactory _connectionFactory;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ISimpleDB GetConnection()
        {
            if (_connectionFactory == null) return null;

            try
            {
                return _connectionFactory.GetConnection();
            }
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
                m_log.Info("[GROUPS]: MySQL connection exception: " + e.ToString());
            }
            return null;
        }

        public NativeGroupDataProvider(ConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;

            m_log.Info("[GROUPS]: Testing database connection");

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                {
                    m_log.Info("[GROUPS]: Testing connection failed.");
                    return;
                }
            }

            m_log.Info("[GROUPS]: Testing connection succeeded");
        }

        #region IGroupDataProvider Members

        public OpenMetaverse.UUID CreateGroup(GroupRequestID requestID, string name, string charter, bool showInList, OpenMetaverse.UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish, OpenMetaverse.UUID founderID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return UUID.Zero;

                UUID groupID = UUID.Random();
                UUID ownerRoleID = UUID.Random();
                
                GroupPowers ownerPowers = Constants.OWNER_GROUP_POWERS;

                string query
                    =   "INSERT INTO osgroup " +
                            "(GroupID, Name, Charter, InsigniaID, FounderID, MembershipFee, OpenEnrollment, " +
                                "ShowInList, AllowPublish, MaturePublish, OwnerRoleID) " +
                        "VALUES(?groupid, ?name, ?charter, ?insigniaID, ?founderID, ?membershipFee, " +
                            "?openEnrollment, ?showInList, ?allowPublish, ?maturePublish, ?ownerRoleID)";
                
                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("?groupid", groupID);
                parms.Add("?name", name);
                parms.Add("?charter", charter);
                parms.Add("?insigniaID", insigniaID);
                parms.Add("?founderID", founderID);
                parms.Add("?membershipFee", membershipFee.ToString());
                parms.Add("?openEnrollment", openEnrollment);
                parms.Add("?showInList", showInList);
                parms.Add("?allowPublish", allowPublish);
                parms.Add("?maturePublish", maturePublish);
                parms.Add("?ownerRoleID", ownerRoleID);

                db.QueryNoResults(query, parms);

                this.AddGroupRole(requestID, groupID, EVERYONE_ROLEID, "Everyone", "Everyone in the group is in the everyone role.", "Member of " + name, (ulong)Constants.DefaultEveryonePowers, true);
                this.AddGroupRole(requestID, groupID, ownerRoleID, "Owners", "Owners of " + name, "Owner of " + name, (ulong) ownerPowers, true);
                this.AddAgentToGroup(requestID, founderID, groupID, ownerRoleID, true);
                this.SetAgentGroupSelectedRole(db, founderID, groupID, ownerRoleID);
                this.SetAgentActiveGroup(requestID, founderID, groupID);

                return groupID;
            }
        }

        private void SetAgentGroupSelectedRole(ISimpleDB db, OpenMetaverse.UUID agentID, OpenMetaverse.UUID groupID, OpenMetaverse.UUID roleID)
        {
            string query 
                =   "UPDATE osgroupmembership " +
                    "SET SelectedRoleID = ?roleID " +
                    "WHERE AgentID = ?agentID AND GroupID = ?groupID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?roleID", roleID);
            parms.Add("?agentID", agentID);
            parms.Add("?groupID", groupID);

            db.QueryNoResults(query, parms);
        }

        bool TestForPower(GroupRequestID requestID, OpenMetaverse.UUID agentID, OpenMetaverse.UUID groupID, ulong power)
        {
            GroupMembershipData membershipData = GetAgentGroupMembership(requestID, agentID, groupID);
            if (membershipData == null)
            {
                return false;
            }

            if ((membershipData.GroupPowers & power) == 0)
            {
                return false;
            }

            return true;
        }

        public void UpdateGroup(GroupRequestID requestID, OpenMetaverse.UUID groupID, string charter, bool showInList, OpenMetaverse.UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (!TestForPower(requestID, requestID.AgentID, groupID, (ulong)GroupPowers.ChangeIdentity))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to change group options", requestID.AgentID);
                return;
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                string query
                    = "UPDATE osgroup " +
                        "SET " +
                        "    Charter = ?charter" +
                        "    , InsigniaID = ?insigniaID" +
                        "    , MembershipFee = ?membershipFee" +
                        "    , OpenEnrollment= ?openEnrollment" +
                        "    , ShowInList    = ?showInList" +
                        "    , AllowPublish  = ?allowPublish" +
                        "    , MaturePublish = ?maturePublish" +
                        " WHERE " +
                        "    GroupID = ?groupID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?charter", charter);
                parms.Add("?insigniaID", insigniaID);
                parms.Add("?membershipFee", membershipFee);
                parms.Add("?openEnrollment", openEnrollment);
                parms.Add("?showInList", showInList);
                parms.Add("?allowPublish", allowPublish);
                parms.Add("?maturePublish", maturePublish);
                parms.Add("?groupID", groupID);

                db.QueryNoResults(query, parms);
            }
            
        }

        public OpenSim.Framework.GroupRecord GetGroupRecord(GroupRequestID requestID, OpenMetaverse.UUID GroupID, string GroupName)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return null;

                string query =  " SELECT osgroup.GroupID, osgroup.Name, Charter, InsigniaID, FounderID, MembershipFee, " +
                                    "OpenEnrollment, ShowInList, AllowPublish, MaturePublish, OwnerRoleID " +
                                " , count(osrole.RoleID) as GroupRolesCount, count(osgroupmembership.AgentID) as GroupMembershipCount " + 
                                " FROM osgroup " +
                                " LEFT JOIN osrole ON (osgroup.GroupID = osrole.GroupID)" +
                                " LEFT JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID)" +
                                " WHERE ";

                Dictionary<string, object> parms = new Dictionary<string,object>();

                if (GroupID != UUID.Zero)
                {
                    query += " osgroup.GroupID = ?groupID ";
                    parms.Add("?groupID", GroupID);
                }
                else if (!String.IsNullOrEmpty(GroupName))
                {
                    query += " osgroup.Name = ?groupName ";
                    parms.Add("?groupName", GroupName);
                }
                else
                {
                    throw new InvalidOperationException("Group name, or group id must be specified");
                }

                query += " GROUP BY osgroup.GroupID, osgroup.name, charter, insigniaID, founderID, membershipFee, openEnrollment, showInList, allowPublish, maturePublish, ownerRoleID ";

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                if (results.Count == 1 && results[0].ContainsKey("GroupID"))
                {
                    return this.MapGroupRecordFromResult(results[0]);
                }

                // string trace = Environment.StackTrace;
                // m_log.WarnFormat("[GROUPS]: GetGroupRecord: Could not find exact match for {0} '{1}':", GroupID, GroupName);
                // m_log.Warn(trace);
                return null;
            }
        }

        private OpenSim.Framework.GroupRecord MapGroupRecordFromResult(Dictionary<string, string> result)
        {
            //osgroup.GroupID, osgroup.Name, Charter, InsigniaID, FounderID, MembershipFee, " +
            //"OpenEnrollment, ShowInList, AllowPublish, MaturePublish, OwnerRoleID

            OpenSim.Framework.GroupRecord record = new OpenSim.Framework.GroupRecord();
            record.GroupID = new UUID(result["GroupID"]);
            record.GroupName = result["Name"];
            record.Charter = result["Charter"];
            record.GroupPicture = new UUID(result["InsigniaID"]);
            record.FounderID = new UUID(result["FounderID"]);
            record.MembershipFee = Int32.Parse(result["MembershipFee"]);
            record.OpenEnrollment = result["OpenEnrollment"] == "1";
            record.ShowInList = result["ShowInList"] == "1";
            record.AllowPublish = result["AllowPublish"] == "1";
            record.MaturePublish = result["MaturePublish"] == "1";
            record.OwnerRoleID = new UUID(result["OwnerRoleID"]);

            return record;
        }

        public List<OpenSim.Framework.DirGroupsReplyData> FindGroups(GroupRequestID requestID, string search)
        {
            List<OpenSim.Framework.DirGroupsReplyData> replyData = new List<OpenSim.Framework.DirGroupsReplyData>();

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return replyData;

                string query = " SELECT osgroup.GroupID, osgroup.Name, count(osgroupmembership.AgentID) as Members " +
                                " FROM osgroup " +
                                    "LEFT JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID) " +
                                /*" WHERE MATCH (osgroup.name) AGAINST (?search IN BOOLEAN MODE) " +*/
                                " WHERE osgroup.name LIKE ?likeSearch " +
                                " OR osgroup.name REGEXP ?search " +
                                " OR osgroup.charter LIKE ?likeSearch " +
                                " OR osgroup.charter REGEXP ?search " +
                                " GROUP BY osgroup.GroupID, osgroup.Name ";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?search", search);
                parms.Add("?likeSearch", "%" + search + "%");

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    replyData.Add(MapGroupReplyDataFromResult(result));
                }
            }

            return replyData;
        }

        private OpenSim.Framework.DirGroupsReplyData MapGroupReplyDataFromResult(Dictionary<string, string> result)
        {
            OpenSim.Framework.DirGroupsReplyData replyData = new OpenSim.Framework.DirGroupsReplyData();
            replyData.groupID = new UUID(result["GroupID"]);
            replyData.groupName = result["Name"];
            replyData.members = Int32.Parse(result["Members"]);

            return replyData;
        }

        public List<OpenSim.Framework.GroupMembersData> GetGroupMembers(GroupRequestID requestID, OpenMetaverse.UUID GroupID, bool ownersOnly)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return new List<OpenSim.Framework.GroupMembersData>();

                string query = " SELECT osgroupmembership.AgentID" +
                                " , osgroupmembership.Contribution, osgroupmembership.ListInProfile, osgroupmembership.AcceptNotices" +
                                " , osgroupmembership.SelectedRoleID, osrole.Title" +
                                " , CASE WHEN OwnerRoleMembership.AgentID IS NOT NULL THEN 1 ELSE 0 END AS IsOwner " +
                                " , agents.agentOnline AS OnlineStatus, agents.logoutTime as LastLogout " +
                                " FROM osgroup JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID)" +
                                "              JOIN osrole ON (osgroupmembership.SelectedRoleID = osrole.RoleID AND osgroupmembership.GroupID = osrole.GroupID)" +
                                "              JOIN osrole AS OwnerRole ON (osgroup.OwnerRoleID  = OwnerRole.RoleID AND osgroup.GroupID  = OwnerRole.GroupID)" +
                                "         LEFT JOIN osgrouprolemembership AS OwnerRoleMembership ON (osgroup.OwnerRoleID       = OwnerRoleMembership.RoleID " +
                                                                              " AND (osgroup.GroupID           = OwnerRoleMembership.GroupID) " +
                                                                              " AND (osgroupmembership.AgentID = OwnerRoleMembership.AgentID))" +
                                " INNER JOIN agents ON agents.uuid = osgroupmembership.AgentID " +
                                " WHERE osgroup.GroupID = ?groupID ";
                
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?groupID", GroupID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                //map the group member data first into a dict so that it'll be easy to search to fill
                //the agent powers
                Dictionary<string, OpenSim.Framework.GroupMembersData> memberData = new Dictionary<string, OpenSim.Framework.GroupMembersData>();
                foreach (Dictionary<string, string> result in results)
                {
                    GroupMembersData member = this.MapGroupMemberFromResult(result);
                    if (member.IsOwner || !ownersOnly)
                        memberData.Add(result["AgentID"], member);
                }


                //now, assign the agent powers returned
                query = " SELECT BIT_OR(osrole.Powers) AS AgentPowers, osgrouprolemembership.AgentID " +
                        " FROM osgrouprolemembership " +
                            "JOIN osrole ON (osgrouprolemembership.GroupID = osrole.GroupID AND osgrouprolemembership.RoleID = osrole.RoleID) " +
                        " WHERE osgrouprolemembership.GroupID = ?groupID " +
                        " GROUP BY osgrouprolemembership.AgentID ";

                results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    if (memberData.ContainsKey(result["AgentID"]))
                    {
                        OpenSim.Framework.GroupMembersData currMemberData = memberData[result["AgentID"]];
                        currMemberData.AgentPowers = ulong.Parse(result["AgentPowers"]);
                        memberData[result["AgentID"]] = currMemberData;
                    }
                }

                return new List<OpenSim.Framework.GroupMembersData>(memberData.Values);
            }
        }

        private OpenSim.Framework.GroupMembersData MapGroupMemberFromResult(Dictionary<string, string> result)
        {
            //SELECT osgroupmembership.AgentID" +
            //" , osgroupmembership.Contribution, osgroupmembership.ListInProfile, osgroupmembership.AcceptNotices" +
            //" , osgroupmembership.SelectedRoleID, osrole.Title
            
            OpenSim.Framework.GroupMembersData membersData = new OpenSim.Framework.GroupMembersData();
            membersData.AgentID = new UUID(result["AgentID"]);
            membersData.Contribution = Int32.Parse(result["Contribution"]);
            membersData.ListInProfile = result["ListInProfile"] == "1";
            membersData.AcceptNotices = result["AcceptNotices"] == "1";
            membersData.Title = result["Title"];
            membersData.IsOwner = result["IsOwner"] == "1";
            membersData.OnlineStatus = result["OnlineStatus"] == "1";
            membersData.LastLogout = UInt32.Parse(result["LastLogout"]);

            return membersData;
        }

        public void AddGroupRole(GroupRequestID requestID, OpenMetaverse.UUID groupID, OpenMetaverse.UUID roleID, string name, 
            string description, string title, ulong powers, bool skipPermissionTests)
        {
            if (!skipPermissionTests)
            {
                if (!TestForPower(requestID, requestID.AgentID, groupID, (ulong)GroupPowers.CreateRole))
                {
                    m_log.WarnFormat("[GROUPS]: {0} No permission to add group roles", requestID.AgentID);
                    return;
                }
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                string query
                    =   "INSERT INTO osrole (GroupID, RoleID, Name, Description, Title, Powers) " +
                        "VALUES(?groupID, ?roleID, ?name, ?desc, ?title, ?powers)";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?groupID", groupID);
                parms.Add("?roleID", roleID);
                parms.Add("?name", name);
                parms.Add("?desc", description);
                parms.Add("?title", title);
                parms.Add("?powers", powers);

                db.QueryNoResults(query, parms);
            }
        }

        public void UpdateGroupRole(GroupRequestID requestID, OpenMetaverse.UUID groupID, OpenMetaverse.UUID roleID, 
            string name, string description, string title, ulong powers)
        {
            if (!TestForPower(requestID, requestID.AgentID, groupID, (ulong)GroupPowers.RoleProperties))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to change group roles", requestID.AgentID);
                return;
            }

            string query = "UPDATE osrole SET RoleID = ?roleID ";

            Dictionary<string, object> parms = new Dictionary<string,object>();

            if (name != null)
            {
                query += ", Name = ?name ";
                parms.Add("?name", name);
            }

            if (description != null)
            {
                query += ", Description = ?description ";
                parms.Add("?description", description);
            }

            if (title != null)
            {
                query += ", Title = ?title ";
                parms.Add("?title", title);
            }

            query += ", Powers = ?powers ";
            parms.Add("?powers", powers);
            
            query += " WHERE GroupID = ?groupID AND RoleID = ?roleID";
            parms.Add("?groupID", groupID);
            parms.Add("?roleID", roleID);

            using (ISimpleDB db = GetConnection())
            {
                if (db != null)
                    db.QueryNoResults(query, parms);
            }
        }

        public void RemoveGroupRole(GroupRequestID requestID, OpenMetaverse.UUID groupID, OpenMetaverse.UUID roleID)
        {
            if (!TestForPower(requestID, requestID.AgentID, groupID, (ulong)GroupPowers.DeleteRole))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to remove group roles", requestID.AgentID);
                return;
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("?groupID", groupID);
                parms.Add("?roleID", roleID);

                string query = "DELETE FROM osgrouprolemembership WHERE GroupID = ?groupID AND RoleID = ?roleID";
                db.QueryNoResults(query, parms);

                query = "UPDATE osgroupmembership SET SelectedRoleID = '" +
                    EVERYONE_ROLEID.ToString() + "' WHERE GroupID = ?groupID AND SelectedRoleID = ?roleID";
                db.QueryNoResults(query, parms);

                query = "DELETE FROM osrole WHERE GroupID = ?groupID AND RoleID = ?roleID";
                db.QueryNoResults(query, parms);
            }
        }

        public List<OpenSim.Framework.GroupRolesData> GetGroupRoles(GroupRequestID requestID, OpenMetaverse.UUID GroupID)
        {
            List<OpenSim.Framework.GroupRolesData> foundRoles = new List<OpenSim.Framework.GroupRolesData>();

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return foundRoles;

                string query 
                    =   " SELECT osrole.RoleID, osrole.Name, osrole.Title, osrole.Description, osrole.Powers, " +
                            " count(osgrouprolemembership.AgentID) as Members " +
                        " FROM osrole " +
                            " LEFT JOIN osgrouprolemembership ON (osrole.GroupID = osgrouprolemembership.GroupID " +
                                " AND osrole.RoleID = osgrouprolemembership.RoleID) " +
                        " WHERE osrole.GroupID = ?groupID " +
                        " GROUP BY osrole.RoleID, osrole.Name, osrole.Title, osrole.Description, osrole.Powers ";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?groupID", GroupID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    foundRoles.Add(this.MapGroupRolesDataFromResult(result));
                }

            }

            return foundRoles;
        }

        private OpenSim.Framework.GroupRolesData MapGroupRolesDataFromResult(Dictionary<string, string> result)
        {
            //osrole.RoleID, osrole.Name, osrole.Title, osrole.Description, osrole.Powers, " +
            //count(osgrouprolemembership.AgentID) as Members

            OpenSim.Framework.GroupRolesData rolesData = new OpenSim.Framework.GroupRolesData();
            rolesData.RoleID = new UUID(result["RoleID"]);
            rolesData.Name = result["Name"];
            rolesData.Title = result["Title"];
            rolesData.Description = result["Description"];
            rolesData.Powers = ulong.Parse(result["Powers"]);

            if (result.ContainsKey("Members"))
            {
                rolesData.Members = Int32.Parse(result["Members"]);
            }
            
            return rolesData;
        }

        public List<UUID> GetGroupMembersWithRole(GroupRequestID requestID, UUID GroupID, UUID RoleID)
        {
            string query = " SELECT * FROM osgrouprolemembership WHERE GroupID = ?groupID AND RoleID = ?roleID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?groupID", GroupID);
            parms.Add("?roleID", RoleID);

            List<UUID> roleMembersData = new List<UUID>();
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return roleMembersData;

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                    roleMembersData.Add(new UUID(result["AgentID"]));
            }
            return roleMembersData;
        }

        public List<OpenSim.Framework.GroupRoleMembersData> GetGroupRoleMembers(GroupRequestID requestID, OpenMetaverse.UUID GroupID)
        {
            string query
                =   " SELECT osrole.RoleID, osgrouprolemembership.AgentID " +
                    " FROM osrole " +
                        " INNER JOIN osgrouprolemembership ON (osrole.GroupID = osgrouprolemembership.GroupID " +
                            " AND osrole.RoleID = osgrouprolemembership.RoleID)" +
                    " WHERE osrole.GroupID = ?groupID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?groupID", GroupID);
            List<OpenSim.Framework.GroupRoleMembersData> foundMembersData = new List<OpenSim.Framework.GroupRoleMembersData>();

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return foundMembersData;

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    foundMembersData.Add(this.MapFoundMembersDataFromResult(result));
                }
            }

            return foundMembersData;
        }

        private OpenSim.Framework.GroupRoleMembersData MapFoundMembersDataFromResult(Dictionary<string, string> result)
        {
            OpenSim.Framework.GroupRoleMembersData mappedData = new OpenSim.Framework.GroupRoleMembersData();
            mappedData.MemberID = new UUID(result["AgentID"]);
            mappedData.RoleID = new UUID(result["RoleID"]);

            return mappedData;
        }

        private void DoInsertGroupMembership(ISimpleDB db, OpenMetaverse.UUID agentID, OpenMetaverse.UUID groupID, OpenMetaverse.UUID roleID)
        {
            string qInsMembership =
                "INSERT INTO osgroupmembership (GroupID, AgentID, Contribution, ListInProfile, AcceptNotices, SelectedRoleID) " +
                "VALUES (?groupID,?agentID, 0, 1, 1,?roleID)";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?agentID", agentID);
            parms.Add("?groupID", groupID);
            parms.Add("?roleID", roleID);

            db.QueryNoResults(qInsMembership, parms);
        }

        private bool IsAgentMemberOfGroup(ISimpleDB db, OpenMetaverse.UUID agentID, OpenMetaverse.UUID groupID)
        {
            // If the caller intends to allow a group operation for a null group ID (e.g. ActivateGroup),
            // then the caller is the one that needs to check that case and support it.
            if (groupID == UUID.Zero)
                return false;   // don't bother making the DB call

            string qIsAgentMember =
                    "SELECT count(AgentID) AS isMember " +
                    "FROM osgroupmembership " +
                    "WHERE AgentID = ?agentID AND GroupID = ?groupID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?agentID", agentID);
            parms.Add("?groupID", groupID);

            List<Dictionary<string, string>> isAgentMemberResults = db.QueryWithResults(qIsAgentMember, parms);

            if (isAgentMemberResults.Count == 0)
            {
                //this shouldnt happen
                m_log.Error("[GROUPS]: IsAgentMemberOfGroup: Expected at least one record");
                return false;
            }
            if (Convert.ToInt32(isAgentMemberResults[0]["isMember"]) > 0)
            {
                return true;
            }
            return false;
        }

        public bool IsAgentInGroup(UUID groupID, UUID agentID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return false;

                return IsAgentMemberOfGroup(db, agentID, groupID);
            }
        }

        private bool AgentHasBeenInvitedToGroup(UUID AgentID, UUID GroupID, UUID RoleID)
        {
            string query
                = "SELECT COUNT(*) as C " +
                    "FROM osgroupinvite " +
                    "WHERE GroupID = ?groupID AND RoleID = ?roleID AND AgentID = ?agentID ";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?groupID", GroupID);
            parms.Add("?roleID", RoleID);
            parms.Add("?agentID", AgentID);

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return false;

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                if (results.Count == 0)
                {
                    return false;
                }

                if (Convert.ToInt32(results[0]["C"]) > 0)
                {
                    return true;
                }

                return false;
            }
        }

        private bool AgentCanJoinGroup(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, OpenMetaverse.UUID RoleID)
        {
            GroupRecord record = GetGroupRecord(requestID, GroupID, null);
            if (record == null)
            {
                return false;
            }

            //is the group open enrollment?
            if (record.OpenEnrollment && RoleID == EVERYONE_ROLEID)
            {
                return true;
            }

            //has the agent been invited?
            if (this.AgentHasBeenInvitedToGroup(AgentID, GroupID, RoleID))
            {
                return true;
            }

            //no way, agent can't join
            return false;
        }

        public bool AddAgentToGroup(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, 
            OpenMetaverse.UUID RoleID, bool skipPermissionTests)
        {
            if (GroupID == UUID.Zero)
                return false;   // not a valid group

            if (!skipPermissionTests)
            {
                if (!AgentCanJoinGroup(requestID, AgentID, GroupID, RoleID))
                {
                    m_log.WarnFormat("[GROUPS]: {0} No permission to join group {1}", AgentID, GroupID);
                    return false;
                }
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return false;

                uint numGroups = GetAgentGroupCount(AgentID);
                if (numGroups >= Constants.MaxGroups)
                {
                    m_log.WarnFormat("[GROUPS]: {0} Already in {1} groups, cannot join group {2}", AgentID, numGroups, GroupID);
                    return false;
                }

                if (!IsAgentMemberOfGroup(db, AgentID, GroupID))
                {
                    this.DoInsertGroupMembership(db, AgentID, GroupID, RoleID);
                }

                //add to everyone role
                this.AddAgentToGroupRole(requestID, requestID.AgentID, AgentID, GroupID, EVERYONE_ROLEID, true);

                if (RoleID != EVERYONE_ROLEID)
                {
                    //specified role
                    this.AddAgentToGroupRole(requestID, requestID.AgentID, AgentID, GroupID, RoleID, true);
                }
            }

            return true;
        }

        public int RemoveAgentFromGroup(GroupRequestID requestID, OpenMetaverse.UUID RequesterID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID)
        {
            // Extra check needed for group owner removals - cannot remove the Owner role unless you are the user
            GroupRecord groupRec = GetGroupRecord(requestID, GroupID, null);
            if (groupRec == null) return (int)Constants.GenericReturnCodes.PARAMETER;

            // Is this the last owner in a group? Don't let group managers shoot themselves in the foot.
            List<UUID> groupOwners = GetGroupMembersWithRole(requestID, GroupID, groupRec.OwnerRoleID);
            if (groupOwners.Contains(AgentID))
            {
                // Sorry Dave, I can't allow you to remove yourself if you are the only owner.
                if (groupOwners.Count < 2)
                {
                    m_log.WarnFormat("[GROUPS]: {0} Cannot remove the only owner {1} in group {2}", RequesterID, AgentID, GroupID);
                    return (int)Constants.GenericReturnCodes.PERMISSION;
                }
                // An owner can only be removed by themselves...
                if (RequesterID != AgentID)
                {
                    m_log.WarnFormat("[GROUPS]: {0} Cannot remove owner {1} of group {2}", RequesterID, AgentID, GroupID);
                    return (int)Constants.GenericReturnCodes.PERMISSION;
                }
            }

            //user can always remove themselves from a group so skip tests in that case
            if (RequesterID != AgentID)
            {
                if (!this.TestForPower(requestID, RequesterID, GroupID, (ulong)GroupPowers.Eject))
                {
                    m_log.WarnFormat("[GROUPS]: {0} No permission to remove {1} from group {2}", RequesterID, AgentID, GroupID);
                    return (int)Constants.GenericReturnCodes.PERMISSION;
                }
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return (int)Constants.GenericReturnCodes.ERROR;

                if (!IsAgentMemberOfGroup(db, AgentID, GroupID))
                {
                    m_log.WarnFormat("[GROUPS]: {0} Cannot remove non-member {1} from group {2}", requestID.AgentID, AgentID, GroupID);
                    return (int)Constants.GenericReturnCodes.PERMISSION;
                }

                string query =  " UPDATE osagent " +
                                " SET ActiveGroupID = '" + UUID.Zero.ToString() + "' " +
                                " WHERE AgentID = ?agentID AND ActiveGroupID = ?groupID";

                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("?agentID", AgentID);
                parms.Add("?groupID", GroupID);


                db.QueryNoResults(query, parms);
                
                query = " DELETE FROM osgroupmembership " +
                        " WHERE AgentID = ?agentID AND GroupID = ?groupID";
              
                db.QueryNoResults(query, parms);
               
                query = " DELETE FROM osgrouprolemembership " +
                        " WHERE AgentID = ?agentID AND GroupID = ?groupID";

                db.QueryNoResults(query, parms);
            }

            return (int)Constants.GenericReturnCodes.SUCCESS;
        }

        public int AddAgentToGroupInvite(GroupRequestID requestID, OpenMetaverse.UUID inviterID, OpenMetaverse.UUID inviteID, OpenMetaverse.UUID groupID, 
            OpenMetaverse.UUID roleID, OpenMetaverse.UUID agentID, out string reason)
        {
            if (!this.TestForPower(requestID, inviterID, groupID, (ulong)GroupPowers.Invite))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to invite {1} to group {2}", inviterID, agentID, groupID);
                reason = "You do not have permission to invite others into that group.";
                return (int)Constants.GenericReturnCodes.PERMISSION;
            }

            if (roleID != EVERYONE_ROLEID)
            {
                if (!CanAddAgentToRole(requestID, inviterID, agentID, groupID, roleID, false))
                {
                    m_log.WarnFormat("[GROUPS]: {0} No permission to assign new role for user {1} in group {2}.", inviterID, agentID, groupID);
                    reason = "You do not have permission to invite others into that group role.";
                    return (int)Constants.GenericReturnCodes.PERMISSION;
                }
            }

            // In case they didn't get the IM or just closed it, offer them the invite again even if it's redundant.
            // if (AgentHasBeenInvitedToGroup(agentID, groupID, roleID))
            //    return (int)Constants.GenericReturnCodes.NOCHANGE;  // redundant request

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                {
                    reason = "MySQL connection failed";
                    return (int)Constants.GenericReturnCodes.ERROR;
                }

                if (IsAgentMemberOfGroup(db, agentID, groupID))
                {
                    // The target user is already a member of the group. See if they are already in that role.
                    if (roleID == EVERYONE_ROLEID)
                    {
                        reason = "That user is already a member of the group.";
                        return (int)Constants.GenericReturnCodes.NOCHANGE;   // redundant request
                    }
                    // See if they are already in that role.
                    if (IsAgentInRole(db, agentID, groupID, roleID))
                    {
                        reason = "The user is already in that group with that role.";
                        return (int)Constants.GenericReturnCodes.NOCHANGE;   // redundant request
                    }

                    // Otherwise, add the specified role.
                    AddAgentToGroupRole(requestID, inviterID, agentID, groupID, roleID, true);
                    reason = "The user has been assigned an additional new group role.";
                    return (int)Constants.GenericReturnCodes.SUCCESS;   // agent's group membership updated with specified role
                }

                // Remove any existing invites for this agent to this group
                string query =  " DELETE FROM osgroupinvite" +
                                " WHERE osgroupinvite.AgentID = ?agentID AND osgroupinvite.GroupID = ?groupID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", agentID);
                parms.Add("?groupID", groupID);

                db.QueryNoResults(query, parms);
               
                // Add new invite for this agent to this group for the specifide role
                query =     " INSERT INTO osgroupinvite (InviteID, GroupID, RoleID, AgentID) " +
                            " VALUES (?inviteID, ?groupID, ?roleID, ?agentID)";

                parms.Add("?inviteID", inviteID);
                parms.Add("?roleID", roleID);

                db.QueryNoResults(query, parms);
            }

            reason = "The user has been invited into the group.";
            return (int)Constants.GenericReturnCodes.SUCCESS;
        }

        public GroupInviteInfo GetAgentToGroupInvite(GroupRequestID requestID, OpenMetaverse.UUID inviteID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return null;

                string query =  " SELECT GroupID, RoleID, InviteID, AgentID FROM osgroupinvite" +
                                " WHERE osgroupinvite.InviteID = ?inviteID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?inviteID", inviteID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                if (results.Count == 1)
                {
                    return this.MapGroupInviteInfoFromResult(results[0]);
                }
                else
                {
                    return null;
                }
            }
        }

        private GroupInviteInfo MapGroupInviteInfoFromResult(Dictionary<string, string> result)
        {
            GroupInviteInfo inviteInfo = new GroupInviteInfo();
            inviteInfo.AgentID = new UUID(result["AgentID"]);
            inviteInfo.GroupID = new UUID(result["GroupID"]);
            inviteInfo.InviteID = new UUID(result["InviteID"]);
            inviteInfo.RoleID = new UUID(result["RoleID"]);

            return inviteInfo;
        }

        public void RemoveAgentToGroupInvite(GroupRequestID requestID, OpenMetaverse.UUID inviteID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                string query =  " DELETE FROM osgroupinvite " +
                                " WHERE osgroupinvite.InviteID = ?inviteID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?inviteID", inviteID);

                db.QueryNoResults(query, parms);
            }
        }

        private bool IsAgentInRole(ISimpleDB db, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, OpenMetaverse.UUID RoleID)
        {
            string query
                = "SELECT count(AgentID) as isMember " +
                    "FROM osgrouprolemembership " +
                    "WHERE AgentID = ?agentID AND RoleID = ?roleID AND GroupID = ?groupID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?agentID", AgentID);
            parms.Add("?groupID", GroupID);
            parms.Add("?roleID", RoleID);

            List<Dictionary<string, string>> agentInRoleResults = db.QueryWithResults(query, parms);

            if (agentInRoleResults.Count == 0)
            {
                //this shouldnt happen
                m_log.Error("[GROUPS]: IsAgentInRole: Expected at least one record");
                return false;
            }
            else if (agentInRoleResults[0]["isMember"] == "0")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool AgentHasOwnerRole(ISimpleDB db, GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID)
        {
            GroupRecord groupInfo = GetGroupRecord(requestID, GroupID, null);
            if (groupInfo == null) return false;

            return IsAgentInRole(db, AgentID, GroupID, groupInfo.OwnerRoleID);
        }

        public bool CanAddAgentToRole(GroupRequestID requestID, OpenMetaverse.UUID inviterID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, OpenMetaverse.UUID RoleID, bool bypassChecks)
        {
            bool success = false;

            if (bypassChecks)
                return true;

            if (TestForPower(requestID, inviterID, GroupID, (ulong)GroupPowers.AssignMember)) 
            {
                success = true;
            }
            else
            if (TestForPower(requestID, inviterID, GroupID, (ulong)GroupPowers.AssignMemberLimited))
            {
                using (ISimpleDB db = GetConnection())
                {
                    if (db != null)
                        if (IsAgentInRole(db, inviterID, GroupID, RoleID))
                            success = true;
                }
            }

            return success;
        }

        public void AddAgentToGroupRole(GroupRequestID requestID, OpenMetaverse.UUID inviterID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, OpenMetaverse.UUID RoleID,
            bool bypassChecks)
        {
            if (!CanAddAgentToRole(requestID, inviterID, AgentID, GroupID, RoleID, bypassChecks))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to assign new role for user {1} in group {2}.", inviterID, AgentID, GroupID);
                return;
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                if (! IsAgentInRole(db, AgentID, GroupID, RoleID))
                {
                    string query 
                        =   "INSERT INTO osgrouprolemembership (GroupID, RoleID, AgentID) " +
                            "VALUES (?groupID, ?roleID, ?agentID)";

                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    parms.Add("?agentID", AgentID);
                    parms.Add("?groupID", GroupID);
                    parms.Add("?roleID", RoleID);

                    db.QueryNoResults(query, parms);
                }
            }
        }

        public void RemoveAgentFromGroupRole(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, OpenMetaverse.UUID RoleID)
        {
            if (!this.TestForPower(requestID, requestID.AgentID, GroupID, (ulong)GroupPowers.RemoveMember))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to remove role from user {1} in group {2}", requestID.AgentID, AgentID, GroupID);
                return;
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                // Extra check needed for group owner removals - cannot remove the Owner role unless you are the user
                GroupRecord groupRec = GetGroupRecord(requestID, GroupID, null);
                if (groupRec == null) return;
                if (RoleID == groupRec.OwnerRoleID)
                {
                    // Is this the last owner in a group? Don't let group managers shoot themselves in the foot.
                    List<UUID> groupOwners = GetGroupMembersWithRole(requestID, GroupID, RoleID);
                    if (groupOwners.Count < 2)
                    {
                        m_log.WarnFormat("[GROUPS]: {0} Cannot remove the only owner {1} in group {2}", requestID.AgentID, AgentID, GroupID);
                        return;
                    }

                    // Is the requesting agent the one being removed? Allow them to remove their own Ownership (if not the last owner).
                    if (requestID.AgentID != AgentID)
                    {
                        m_log.WarnFormat("[GROUPS]: {0} No permission to modify group owner roles for {1} in {2}", requestID.AgentID, AgentID, GroupID);
                        return;
                    }
                }

                // If agent has this role selected, change their selection to everyone (uuidZero) role
                string query
                    = " UPDATE osgroupmembership SET SelectedRoleID = '" + EVERYONE_ROLEID + "'" +
                        " WHERE AgentID = ?agentID AND GroupID = ?groupID AND SelectedRoleID = ?roleID";

                Dictionary<string, object> parms = new Dictionary<string,object>();
                parms.Add("?agentID", AgentID);
                parms.Add("?groupID", GroupID);
                parms.Add("?roleID", RoleID);

                db.QueryNoResults(query, parms);

                query =     " DELETE FROM osgrouprolemembership " +
                            " WHERE AgentID = ?agentID AND GroupID = ?groupID AND RoleID = ?roleID";

                db.QueryNoResults(query, parms);
            }
        }

        public List<OpenSim.Framework.GroupRolesData> GetAgentGroupRoles(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID)
        {
            List<OpenSim.Framework.GroupRolesData> foundGroupRoles = new List<OpenSim.Framework.GroupRolesData>();

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return foundGroupRoles;

                string query
                    =   " SELECT osrole.RoleID, osrole.GroupID, osrole.Title, osrole.Name, osrole.Description, osrole.Powers" +
                            " , CASE WHEN osgroupmembership.SelectedRoleID = osrole.RoleID THEN 1 ELSE 0 END AS Selected" +
                        " FROM osgroupmembership " +
                            " JOIN osgrouprolemembership  ON (osgroupmembership.GroupID = osgrouprolemembership.GroupID AND osgroupmembership.AgentID = osgrouprolemembership.AgentID)" +
                            " JOIN osrole ON ( osgrouprolemembership.RoleID = osrole.RoleID AND osgrouprolemembership.GroupID = osrole.GroupID)" +
                            "                   LEFT JOIN osagent ON (osagent.AgentID = osgroupmembership.AgentID)" +
                        " WHERE osgroupmembership.AgentID = ?agentID ";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", AgentID);

				if (GroupID != UUID.Zero)
                {
                    query += " AND osgroupmembership.GroupID = ?groupID ";
                    parms.Add("?groupID", GroupID);
                }

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    foundGroupRoles.Add(this.MapGroupRolesDataFromResult(result));
                }

            }

            return foundGroupRoles;
        }

        private bool AgentHasActiveGroup(ISimpleDB db, OpenMetaverse.UUID agentID)
        {
            string query
                = "SELECT COUNT(*) AS agentCount " +
                    "FROM osagent " +
                    "WHERE AgentID = ?agentID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?agentID", agentID);

            List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
            if (results.Count > 0 && Int32.Parse(results[0]["agentCount"]) > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetAgentActiveGroup(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID)
        {
            //only the agent making the call can change their own group
            if (requestID.AgentID != AgentID)
            {
                m_log.WarnFormat("[GROUPS]: User {0} No permission to change the active group for {1}", requestID.AgentID, AgentID);
                return;
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return;

                //test to see if the user even exists in osagent
                if (AgentHasActiveGroup(db, AgentID))
                {
                    string query
                        = "UPDATE osagent " +
                            "SET ActiveGroupID = ?activeGroupID " +
                            "WHERE AgentID = ?agentID";

                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    parms.Add("?activeGroupID", GroupID);
                    parms.Add("?agentID", AgentID);

                    db.QueryNoResults(query, parms);
                }
                else
                {
                    string query
                        =   "INSERT INTO osagent (ActiveGroupID, AgentID) " +
                            "VALUES(?groupID, ?agentID)";

                    Dictionary<string, object> parms = new Dictionary<string, object>();
                    parms.Add("?groupID", GroupID);
                    parms.Add("?agentID", AgentID);

                    db.QueryNoResults(query, parms);
                }
            }
        }

        public OpenSim.Framework.GroupMembershipData GetAgentActiveMembership(GroupRequestID requestID, OpenMetaverse.UUID AgentID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return null;

                string query
                    = " SELECT osgroup.GroupID, osgroup.Name as GroupName, osgroup.Charter, osgroup.InsigniaID, " +
                            "osgroup.FounderID, osgroup.MembershipFee, osgroup.OpenEnrollment, osgroup.ShowInList, " +
                            "osgroup.AllowPublish, osgroup.MaturePublish, osgroupmembership.Contribution, " +
                            "osgroupmembership.ListInProfile, osgroupmembership.AcceptNotices, osgroupmembership.SelectedRoleID, osrole.Title" +
                            " , osagent.ActiveGroupID " +
                        " FROM osagent JOIN osgroup ON (osgroup.GroupID = osagent.ActiveGroupID)" +
                        "              JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID AND osagent.AgentID = osgroupmembership.AgentID)" +
                        "              JOIN osrole ON (osgroupmembership.SelectedRoleID = osrole.RoleID AND osgroupmembership.GroupID = osrole.GroupID)" +
                        " WHERE osagent.AgentID = ?agentID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", AgentID);

                List<Dictionary<string, string>> groupResults = db.QueryWithResults(query, parms);
                if (groupResults.Count == 0)
                {
                    //no active group? no groups, etc
                    return null;
                }

                UUID groupID = new UUID(groupResults[0]["GroupID"]);
                Dictionary<string, string> powersResult = FindGroupPowersForAgent(db, groupID, AgentID);

                OpenSim.Framework.GroupMembershipData foundMembership
                    = this.MapGroupMembershipDataFromResult(groupResults[0], powersResult);

                return foundMembership;
            }
        }

        private OpenSim.Framework.GroupMembershipData MapGroupMembershipDataFromResult(Dictionary<string, string> groupResult, 
            Dictionary<string, string> powerResult)
        {
            // osgroup.GroupID, osgroup.Name as GroupName, osgroup.Charter, osgroup.InsigniaID, " +
            //"osgroup.FounderID, osgroup.MembershipFee, osgroup.OpenEnrollment, osgroup.ShowInList, " + 
            //"osgroup.AllowPublish, osgroup.MaturePublish, osgroupmembership.Contribution, " + 
            //"osgroupmembership.ListInProfile, osgroupmembership.AcceptNotices, osgroupmembership.SelectedRoleID, osrole.Title

            OpenSim.Framework.GroupMembershipData foundMembership = new OpenSim.Framework.GroupMembershipData();
            foundMembership.GroupID = new UUID(groupResult["GroupID"]);
            foundMembership.GroupName = groupResult["GroupName"];
            foundMembership.Charter = groupResult["Charter"];
            foundMembership.GroupPicture = new UUID(groupResult["InsigniaID"]);
            foundMembership.FounderID = new UUID(groupResult["FounderID"]);
            foundMembership.MembershipFee = Int32.Parse(groupResult["MembershipFee"]);
            foundMembership.OpenEnrollment = groupResult["OpenEnrollment"] == "0" ? false : true;
            foundMembership.ShowInList = groupResult["ShowInList"] == "0" ? false : true;
            foundMembership.AllowPublish = groupResult["AllowPublish"] == "0" ? false : true;
            foundMembership.MaturePublish = groupResult["MaturePublish"] == "0" ? false : true;
            foundMembership.Contribution = Int32.Parse(groupResult["Contribution"]);
            foundMembership.ListInProfile = groupResult["ListInProfile"] == "0" ? false : true;
            foundMembership.AcceptNotices = groupResult["AcceptNotices"] == "0" ? false : true;
            foundMembership.ActiveRole = new UUID(groupResult["SelectedRoleID"]);
            foundMembership.GroupTitle = groupResult["Title"];

            if (powerResult != null)
            {
                foundMembership.GroupPowers = ulong.Parse(powerResult["GroupPowers"]);
            }

            return foundMembership;
        }

        public void SetAgentActiveGroupRole(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, OpenMetaverse.UUID RoleID)
        {
            //make sure the agent actually has the role they want to activate
            List<GroupRolesData> roles = GetAgentGroupRoles(requestID, AgentID, GroupID);
            bool foundRole = false;
            foreach (GroupRolesData role in roles)
            {
                if (role.RoleID == RoleID)
                {
                    foundRole = true;
                }
            }

            if (!foundRole)
            {
                //they are not in the role they selected
                return;
            }

            string query =
                "UPDATE osgroupmembership " +
                "SET SelectedRoleID = ?roleID " +
                "WHERE osgroupmembership.GroupID = ?groupID " +
                    "AND osgroupmembership.AgentID = ?agentID ";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?roleID", RoleID);
            parms.Add("?groupID", GroupID);
            parms.Add("?agentID", AgentID);

            using (ISimpleDB db = GetConnection())
            {
                if (db != null)
                    db.QueryNoResults(query, parms);
            }
        }

        public void SetAgentGroupInfo(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
            //the agent making the reuqest must be the target agent
            if (requestID.AgentID != AgentID)
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to change group info for {1}", requestID.AgentID, AgentID);
                return;
            }

            string query =
                "UPDATE osgroupmembership " +
                "SET AcceptNotices = ?acceptNotices, " +
                    "ListInProfile = ?listInProfile " +
                "WHERE osgroupmembership.GroupID = ?groupID " +
                    "AND osgroupmembership.AgentID = ?agentID ";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?acceptNotices", AcceptNotices);
            parms.Add("?listInProfile", ListInProfile);
            parms.Add("?groupID", GroupID);
            parms.Add("?agentID", AgentID);

            using (ISimpleDB db = GetConnection())
            {
                if (db != null)
                    db.QueryNoResults(query, parms);
            }
        }

        public OpenSim.Framework.GroupMembershipData GetAgentGroupMembership(GroupRequestID requestID, OpenMetaverse.UUID AgentID, OpenMetaverse.UUID GroupID)
        {
            //TODO?  Refactor?  This is very similar to another method, just different params and queries
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return null;

                string query
                    =   " SELECT osgroup.GroupID, osgroup.Name as GroupName, osgroup.Charter, osgroup.InsigniaID, " +
                            "osgroup.FounderID, osgroup.MembershipFee, osgroup.OpenEnrollment, osgroup.ShowInList, " +
                            "osgroup.AllowPublish, osgroup.MaturePublish, osgroupmembership.Contribution, " +
                            "osgroupmembership.ListInProfile, osgroupmembership.AcceptNotices, " +
                            "osgroupmembership.SelectedRoleID, osrole.Title" +
                        " FROM osgroup JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID)" +
                        "              JOIN osrole ON (osgroupmembership.SelectedRoleID = osrole.RoleID AND osgroupmembership.GroupID = osrole.GroupID)" +
                        " WHERE osgroup.GroupID = ?groupID AND osgroupmembership.AgentID = ?agentID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?groupID", GroupID);
                parms.Add("?agentID", AgentID);

                List<Dictionary<string, string>> groupResults = db.QueryWithResults(query, parms);
                if (groupResults.Count == 0)
                {
                    // no groups, etc?
                    return null;
                }

                Dictionary<string, string> powersResult = FindGroupPowersForAgent(db, GroupID, AgentID);

                OpenSim.Framework.GroupMembershipData foundMembership
                    = this.MapGroupMembershipDataFromResult(groupResults[0], powersResult);

                return foundMembership;
            }
        }

        private static Dictionary<string, string> FindGroupPowersForAgent(ISimpleDB db, UUID groupID, UUID agentID)
        {
            string query =  " SELECT BIT_OR(osrole.Powers) AS GroupPowers" +
                            " FROM osgrouprolemembership JOIN osrole ON (osgrouprolemembership.GroupID = osrole.GroupID AND osgrouprolemembership.RoleID = osrole.RoleID)" +
                            " WHERE osgrouprolemembership.GroupID = ?groupID AND osgrouprolemembership.AgentID = ?agentID";

            Dictionary<string, object> parms = new Dictionary<string, object>();
            parms.Add("?groupID", groupID);
            parms.Add("?agentID", agentID);

            List<Dictionary<string, string>> powersResults = db.QueryWithResults(query, parms);

            if (powersResults.Count == 0)
            {
                m_log.Error("[GROUPS]: Could not find any matching powers for agent");
                return null;
            }

            return powersResults[0];
        }

        public uint GetAgentGroupCount(OpenMetaverse.UUID AgentID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return 0;

                string query = "SELECT COUNT(osgroup.GroupID) AS GroupCount FROM osgroup" +
                                " JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID) " +
                                " WHERE osgroupmembership.AgentID = ?agentID";
                
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", AgentID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                if (results.Count == 0)
                    return 0;

                return Convert.ToUInt32(results[0]["GroupCount"]);
            }
        }

        public List<OpenSim.Framework.GroupMembershipData> GetAgentGroupMemberships(GroupRequestID requestID, OpenMetaverse.UUID AgentID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return new List<OpenSim.Framework.GroupMembershipData>();


                string query =  " SELECT osgroup.GroupID, osgroup.Name as GroupName, osgroup.Charter, osgroup.InsigniaID, osgroup.FounderID, osgroup.MembershipFee, osgroup.OpenEnrollment, osgroup.ShowInList, osgroup.AllowPublish, osgroup.MaturePublish" +
                                " , osgroupmembership.Contribution, osgroupmembership.ListInProfile, osgroupmembership.AcceptNotices" +
                                " , osgroupmembership.SelectedRoleID, osrole.Title" +
                                " , IFNULL(osagent.ActiveGroupID, '" + UUID.Zero + "') AS ActiveGroupID" +
                                " FROM osgroup JOIN osgroupmembership ON (osgroup.GroupID = osgroupmembership.GroupID)" +
                                "              JOIN osrole ON (osgroupmembership.SelectedRoleID = osrole.RoleID AND osgroupmembership.GroupID = osrole.GroupID)" +
                                "         LEFT JOIN osagent ON (osagent.AgentID = osgroupmembership.AgentID)" +
                                " WHERE osgroupmembership.AgentID = ?agentID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", AgentID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);

                //map the group data first into a dict so that it'll be easy to search to fill
                //the group powers
                Dictionary<string, OpenSim.Framework.GroupMembershipData> membershipData = new Dictionary<string, OpenSim.Framework.GroupMembershipData>();
                foreach (Dictionary<string, string> result in results)
                {
                    membershipData.Add(result["GroupID"], this.MapGroupMembershipDataFromResult(result, null));
                }


                //now, assign the group powers returned
                query = " SELECT BIT_OR(osrole.Powers) AS GroupPowers, osgrouprolemembership.GroupID" +
                        " FROM osgrouprolemembership JOIN osrole ON (osgrouprolemembership.GroupID = osrole.GroupID AND osgrouprolemembership.RoleID = osrole.RoleID)" +
                        " WHERE osgrouprolemembership.AgentID = ?agentID " + 
                        " GROUP BY GroupID ";

                results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    if (result.ContainsKey("GroupID") && membershipData.ContainsKey(result["GroupID"]))
                    {
                        OpenSim.Framework.GroupMembershipData currMembershipData = membershipData[result["GroupID"]];
                        currMembershipData.GroupPowers = ulong.Parse(result["GroupPowers"]);
                    }
                }

                return new List<OpenSim.Framework.GroupMembershipData>(membershipData.Values);
            }
        }

        // Returns null on error, empty list if not in any groups.
        public List<UUID> GetAgentGroupList(GroupRequestID requestID, UUID AgentID)
        {
            List<UUID> groups = new List<UUID>();

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return null;    // signal error

                string query = " SELECT osgroupmembership.GroupID FROM osgroupmembership WHERE osgroupmembership.AgentID = ?agentID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?agentID", AgentID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    groups.Add(new UUID(result["GroupID"]));
                }
            }

            return groups;
        }

        public bool AddGroupNotice(GroupRequestID requestID, OpenMetaverse.UUID groupID, OpenMetaverse.UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket)
        {
            if (!this.TestForPower(requestID, requestID.AgentID, groupID, (ulong)GroupPowers.SendNotices))
            {
                m_log.WarnFormat("[GROUPS]: {0} No permission to send notices for group {1}", requestID.AgentID, groupID);
                return false;
            }

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return false;

                string binBucketString = OpenMetaverse.Utils.BytesToHexString(binaryBucket, String.Empty);

                string query = " INSERT INTO osgroupnotice" +
                                " (GroupID, NoticeID, Timestamp, FromName, Subject, Message, BinaryBucket)" +
                                " VALUES " +
                                " (?groupID, ?noticeID, ?timeStamp, ?fromName, ?subject, ?message, ?binaryBucket)";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?groupID", groupID);
                parms.Add("?noticeID", noticeID);
                parms.Add("?timeStamp", ((uint)Util.UnixTimeSinceEpoch()).ToString());
                parms.Add("?fromName", fromName);
                parms.Add("?subject", subject);
                parms.Add("?message", message);
                parms.Add("?binaryBucket", binBucketString);

                db.QueryNoResults(query, parms);
            }

            return true;
        }

        // WARNING: This method does not initialize all notice fields, namely the fields stored in the binary bucket.
        // See FlexiGroups.cs method InitializeNoticeFromBucket() for an example.
        public GroupNoticeInfo GetGroupNotice(GroupRequestID requestID, OpenMetaverse.UUID noticeID)
        {
            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return null;

                string query 
                    =   " SELECT GroupID, NoticeID, Timestamp, FromName, Subject, Message, BinaryBucket" +
                        " FROM osgroupnotice" +
                        " WHERE osgroupnotice.NoticeID = ?noticeID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?noticeID", noticeID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                if (results.Count == 0)
                {
                    return null;
                }
                else
                {
                    return this.MapGroupNoticeFromResult(results[0]);
                }
            }
        }

        // This fetches a summary list of all notices in the group.
        public List<OpenSim.Framework.GroupNoticeData> GetGroupNotices(GroupRequestID requestID, OpenMetaverse.UUID GroupID)
        {
            List<OpenSim.Framework.GroupNoticeData> foundNotices = new List<OpenSim.Framework.GroupNoticeData>();

            using (ISimpleDB db = GetConnection())
            {
                if (db == null)
                    return foundNotices;

                string query
                    = " SELECT GroupID, NoticeID, Timestamp, FromName, Subject, Message, BinaryBucket" +
                        " FROM osgroupnotice" +
                        " WHERE osgroupnotice.GroupID = ?groupID";

                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?groupID", GroupID);

                List<Dictionary<string, string>> results = db.QueryWithResults(query, parms);
                foreach (Dictionary<string, string> result in results)
                {
                    foundNotices.Add(this.MapGroupNoticeDataFromResult(result));
                }
            }

            return foundNotices;
        }

        // This fetches a summary of a single notice for the notice list (no message body).
        private GroupNoticeData MapGroupNoticeDataFromResult(Dictionary<string, string> result)
        {
            GroupNoticeData data = new GroupNoticeData();
            byte[] bucket = Utils.HexStringToBytes(result["BinaryBucket"], true); 
            int bucketLen = bucket.GetLength(0);
            data.NoticeID = UUID.Parse(result["NoticeID"]);
            data.Timestamp = uint.Parse(result["Timestamp"]);
            data.FromName = result["FromName"];
            data.Subject = result["Subject"];
            if ((bucketLen < 35) || (bucket[0]==0)) {
                // no attachment data
                data.HasAttachment = false;
                data.AssetType = 0;
                data.OwnerID = UUID.Zero;
                data.ItemID = UUID.Zero;
                data.Attachment = String.Empty;
            } else {
                data.HasAttachment = true;
                data.AssetType = bucket[1];
                data.OwnerID = new UUID(bucket,2);
                data.ItemID = new UUID(bucket,18);
                data.Attachment = OpenMetaverse.Utils.BytesToString(bucket, 34, bucketLen - 34);
            }
            return data;
        }

        // This fetches a single notice in detail, including the message body.
        private GroupNoticeInfo MapGroupNoticeFromResult(Dictionary<string, string> result)
        {
            GroupNoticeInfo data = new GroupNoticeInfo();

            data.GroupID = UUID.Parse(result["GroupID"]);
            data.Message = result["Message"];
            data.noticeData.NoticeID = UUID.Parse(result["NoticeID"]);
            data.noticeData.Timestamp = uint.Parse(result["Timestamp"]);
            data.noticeData.FromName = result["FromName"];
            data.noticeData.Subject = result["Subject"];
            if (data.Message == null)
                data.Message = String.Empty;

            data.BinaryBucket = Utils.HexStringToBytes(result["BinaryBucket"], true);

            return data;
        }
        #endregion
    }
}
