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

using OpenMetaverse;

using OpenSim.Framework;

namespace OpenSim.Region.OptionalModules.Avatar.FlexiGroups
{
    interface IGroupDataProvider
    {
        UUID CreateGroup(GroupRequestID requestID, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish, UUID founderID);
        void UpdateGroup(GroupRequestID requestID, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish);
        GroupRecord GetGroupRecord(GroupRequestID requestID, UUID GroupID, string GroupName);
        List<DirGroupsReplyData> FindGroups(GroupRequestID requestID, string search);
        List<GroupMembersData> GetGroupMembers(GroupRequestID requestID, UUID GroupID, bool ownersOnly);

        void AddGroupRole(GroupRequestID requestID, UUID groupID, UUID roleID, string name, 
            string description, string title, ulong powers, bool skipPermissionTests);
        void UpdateGroupRole(GroupRequestID requestID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers);
        void RemoveGroupRole(GroupRequestID requestID, UUID groupID, UUID roleID);
        List<GroupRolesData> GetGroupRoles(GroupRequestID requestID, UUID GroupID);
        List<GroupRoleMembersData> GetGroupRoleMembers(GroupRequestID requestID, UUID GroupID);

        bool AddAgentToGroup(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID, bool skipPermissionTests);

        // These return one of the values of Constants.GenericReturnCodes and are also used by LSL functions.
        int AddAgentToGroupInvite(GroupRequestID requestID, UUID inviterID, UUID inviteID, UUID groupID, UUID roleID, UUID agentID, out string reason);
        int RemoveAgentFromGroup(GroupRequestID requestID, UUID RequesterID, UUID AgentID, UUID GroupID);

        GroupInviteInfo GetAgentToGroupInvite(GroupRequestID requestID, UUID inviteID);
        void RemoveAgentToGroupInvite(GroupRequestID requestID, UUID inviteID);


        void AddAgentToGroupRole(GroupRequestID requestID, UUID inviterID, UUID AgentID, UUID GroupID, UUID RoleID, bool bypassChecks);
        void RemoveAgentFromGroupRole(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID);
        List<GroupRolesData> GetAgentGroupRoles(GroupRequestID requestID, UUID AgentID, UUID GroupID);

        void SetAgentActiveGroup(GroupRequestID requestID, UUID AgentID, UUID GroupID);
        GroupMembershipData GetAgentActiveMembership(GroupRequestID requestID, UUID AgentID);

        void SetAgentActiveGroupRole(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID);
        void SetAgentGroupInfo(GroupRequestID requestID, UUID AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile);

        GroupMembershipData GetAgentGroupMembership(GroupRequestID requestID, UUID AgentID, UUID GroupID);
        List<GroupMembershipData> GetAgentGroupMemberships(GroupRequestID requestID, UUID AgentID);
        List<UUID> GetAgentGroupList(GroupRequestID requestID, UUID AgentID); // Returns null on error, empty list if not in any groups.
        bool IsAgentInGroup(UUID groupID, UUID agentID);

        bool AddGroupNotice(GroupRequestID requestID, UUID groupID, UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket);
        GroupNoticeInfo GetGroupNotice(GroupRequestID requestID, UUID noticeID);
        List<GroupNoticeData> GetGroupNotices(GroupRequestID requestID, UUID GroupID);
    }

    public class GroupInviteInfo
    {
        public UUID GroupID  = UUID.Zero;
        public UUID RoleID   = UUID.Zero;
        public UUID AgentID  = UUID.Zero;
        public UUID InviteID = UUID.Zero;
    }

    public class GroupRequestID
    {
        public UUID AgentID = UUID.Zero;
        public string UserServiceURL = String.Empty;
        public UUID SessionID = UUID.Zero;
    }

    public class GroupNoticeInfo
    {
        public GroupNoticeData noticeData = new GroupNoticeData();
        public UUID GroupID = UUID.Zero;
        public string Message = String.Empty;
        public byte[] BinaryBucket = null;
    }

}
