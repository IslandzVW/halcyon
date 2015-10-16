/*
 * Copyright (c) Contributors, http://aurora-sim.org/
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
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
using System.IO;
using System.Reflection;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using log4net;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GroupCapsModule")]
    public class GroupCapsModule : INonSharedRegionModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        protected IGroupsModule m_groupService;

        public void Initialize(IConfigSource source)
        {
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "GroupCapsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void PostInitialize()
        {
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capID = UUID.Random();
            GroupMemberDataHandler handler = 
                new GroupMemberDataHandler("/CAPS/" + capID + "/", "GroupMemberData", agentID, m_scene);
            caps.RegisterHandler("GroupMemberData", handler);
        }

    }
        
    #region Group Members

    public class GroupMemberDataHandler : BaseStreamHandler
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly UUID m_agentID;
        private readonly Scene m_scene;

        public GroupMemberDataHandler(string path, string name, UUID agentID, Scene scene)
            : base("POST", path, name, agentID.ToString())
        {
            m_agentID = agentID;
            m_scene = scene;
        }

        private bool IsMyFriend(UUID otherUser, List<FriendListItem> friends)
        {
            FriendListItem friend =
                friends.Find(delegate(FriendListItem f)
                {
                    return f.Friend == otherUser;
                });
            return friend != null;
        }

        const string VIEWER_DATE_FORMAT = "MM/dd/yyyy";
        string FilterOnlineStatus(UUID otherUser, bool isOnline, uint lastLogout, List<FriendListItem> friends)
        {
            // Filter online status?
            if (isOnline)
            {
                if (otherUser == m_agentID)
                    return "Online";

                UserPreferencesData prefs = m_scene.CommsManager.UserService.RetrieveUserPreferences(otherUser);
                if (prefs != null)
                {
                    // This is where we check the "Only friend and groups know I'm online" option.
                    // Only applies to friends (not groups) in InWorldz (for now at least).
                    if (prefs.ListedInDirectory || IsMyFriend(otherUser, friends))
                        return "Online";    // everyone can see or these users are friends
                }

                // fake offline but "logged in today"
                return DateTime.Now.ToString(VIEWER_DATE_FORMAT);
            }

            if (lastLogout == 0)
                return "- Unknown -"; // '-' sorts as oldest

            DateTime when = Util.UnixToLocalDateTime(lastLogout);
            return when.ToString(VIEWER_DATE_FORMAT);
        }

        public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            IGroupsModule groupService = m_scene.RequestModuleInterface<IGroupsModule>();
            ScenePresence SP = m_scene.GetScenePresence(this.m_agentID);
            IClientAPI client = (SP == null) ? null : SP.ControllingClient;
            if ((groupService == null) || (client == null))
                return new byte[0];
            try
            {
                // Get *this* user's friends list (should be fast).
                List<FriendListItem> friends = m_scene.CommsManager.UserService.GetUserFriendList(this.m_agentID);

                OSDMap rm = (OSDMap) OSDParser.DeserializeLLSDXml(request);
                UUID groupID = rm["group_id"].AsUUID();

                OSDMap defaults = new OSDMap();
                defaults["default_powers"] = (ulong)Constants.DefaultEveryonePowers;

                OSDMap members = new OSDMap();
                List<string> titleList = new List<string>();
                int count = 0;
                foreach (GroupMembersData gmd in groupService.GroupMembersRequest(null, m_scene, m_agentID, groupID))
                {
                    OSDMap member = new OSDMap();
                    member["donated_square_meters"] = gmd.Contribution;
                    member["last_login"] = FilterOnlineStatus(gmd.AgentID, gmd.OnlineStatus, gmd.LastLogout, friends);
                    member["powers"] = gmd.AgentPowers;

                    if (gmd.IsOwner)
                        member["owner"] = "Y";  // mere presence of this means IsOwner to the viewer.

                    int titleIndex;
                    if (titleList.Contains(gmd.Title))
                    {
                        titleIndex = titleList.IndexOf(gmd.Title);
                    }
                    else
                    {
                        titleIndex = titleList.Count;   // before adding, 0-based
                        titleList.Add(gmd.Title);
                    }
                    member["title"] = titleIndex.ToString();

                    count++;
                    members[gmd.AgentID.ToString()] = member;
                }

                // Convert optimized list to OSDArray.
                OSDArray titles = new OSDArray();
                foreach (string title in titleList)
                    titles.Add(title);

                OSDMap map = new OSDMap();
                map["member_count"] = count;
                map["group_id"] = groupID;
                map["defaults"] = defaults;
                map["titles"] = titles;
                map["members"] = members;
                return OSDParser.SerializeLLSDXmlBytes(map);
            }
            catch (Exception e)
            {
                MainConsole.Instance.Error("[CAPS]: " + e);
            }

            return new byte[0];
        }

        #endregion
    }
}
