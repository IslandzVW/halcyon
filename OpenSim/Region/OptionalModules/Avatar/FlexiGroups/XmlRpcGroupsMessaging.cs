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
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.CoreModules.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Avatar.FlexiGroups
{
    public class XmlRpcGroupsMessaging : ISharedRegionModule
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_sceneList = new List<Scene>();

        private IMessageTransferModule m_msgTransferModule = null;

        private IGroupsModule m_groupsModule = null;

        // TODO: Move this off to the xmlrpc server
        private object m_sessionLock = new object();
        private Dictionary<Guid, List<Guid>> m_agentsInGroupSession = new Dictionary<Guid, List<Guid>>();
        private Dictionary<Guid, List<Guid>> m_agentsDroppedSession = new Dictionary<Guid, List<Guid>>();


        // Config Options
        private bool m_groupMessagingEnabled = false;
        private bool m_debugEnabled = true;

        #region IRegionModuleBase Members

        public void Initialize(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            if (groupsConfig == null)
            {
                // Do not run this module by default.
                return;
            }
            else
            {
                if (!groupsConfig.GetBoolean("Enabled", false))
                {
                    return;
                }

                if (groupsConfig.GetString("Module", "Default") != "FlexiGroups")
                {
                    m_groupMessagingEnabled = false;

                    return;
                }

                m_groupMessagingEnabled = groupsConfig.GetBoolean("XmlRpcMessagingEnabled", true);

                if (!m_groupMessagingEnabled)
                {
                    return;
                }

                m_log.Info("[GROUPS-MESSAGING]: Initializing XmlRpcGroupsMessaging");

                m_debugEnabled = groupsConfig.GetBoolean("XmlRpcDebugEnabled", true);
            }

            m_log.Info("[GROUPS-MESSAGING]: XmlRpcGroupsMessaging starting up");

        }

        public void AddRegion(Scene scene)
        {
            // NoOp
        }
        public void RegionLoaded(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupsModule = scene.RequestModuleInterface<IGroupsModule>();

            // No groups module, no groups messaging
            if (m_groupsModule == null)
            {
                m_log.Error("[GROUPS-MESSAGING]: Could not get IGroupsModule, XmlRpcGroupsMessaging is now disabled.");
                Close();
                m_groupMessagingEnabled = false;
                return;
            }

            m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

            // No message transfer module, no groups messaging
            if (m_msgTransferModule == null)
            {
                m_log.Error("[GROUPS-MESSAGING]: Could not get MessageTransferModule");
                Close();
                m_groupMessagingEnabled = false;
                return;
            }


            m_sceneList.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnRemovePresence += OnRemovePresence;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_sceneList.Remove(scene);
        }

        public void Close()
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.Debug("[GROUPS-MESSAGING]: Shutting down XmlRpcGroupsMessaging module.");

            foreach (Scene scene in m_sceneList)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            }

            m_sceneList.Clear();

            m_groupsModule = null;
            m_msgTransferModule = null;
        }

        public string Name
        {
            get { return "XmlRpcGroupsMessaging"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region ISharedRegionModule Members

        public void PostInitialize()
        {
            // NoOp
        }

        #endregion

        #region SimGridEventHandlers

        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: OnInstantMessage registered for {0}", client.Name);

            client.OnInstantMessage += OnInstantMessage;
        }

        private void OnRemovePresence(UUID agentId)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: OnRemovePresence registered for {0}", agentId.ToString());

            RemoveAgentFromAllGroupSessions(agentId.Guid);
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // The instant message module will only deliver messages of dialog types:
            // MessageFromAgent, StartTyping, StopTyping, MessageFromObject
            //
            // Any other message type will not be delivered to a client by the 
            // Instant Message Module


            if (m_debugEnabled)
            {
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called with {1}", System.Reflection.MethodBase.GetCurrentMethod().Name,msg.dialog);

                DebugGridInstantMessage(msg);
            }

            // Incoming message from a group
            if ((msg.fromGroup == true) && 
                ((msg.dialog == (byte)InstantMessageDialog.SessionSend)
                 || (msg.dialog == (byte)InstantMessageDialog.SessionAdd)
                 || (msg.dialog == (byte)InstantMessageDialog.SessionDrop)))
            {
                ProcessMessageFromGroupSession(msg);
            }
        }

        private void ProcessMessageFromGroupSession(GridInstantMessage msg)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Session message from {0} going to agent {1}", msg.fromAgentName, msg.toAgentID);

            switch (msg.dialog)
            {
                case (byte)InstantMessageDialog.SessionAdd:
                    AddAgentToGroupSession(msg.fromAgentID, msg.imSessionID);
                    NotifyNewSessionUserOfExisting(msg.imSessionID, msg.fromAgentID);
                    NotifySessionUserTransition(msg.imSessionID, msg.fromAgentID, msg.dialog);
                    break;

                case (byte)InstantMessageDialog.SessionDrop:
                    RemoveAgentFromGroupSession(msg.fromAgentID, msg.imSessionID);
                    NotifySessionUserTransition(msg.imSessionID, msg.fromAgentID, msg.dialog);
                    break;

                case (byte)InstantMessageDialog.SessionSend:
                    bool needsSessionAdd = false;
                    bool agentDroppedSession = false;

                    lock (m_sessionLock)
                    {
                        needsSessionAdd = (!HasAgentBeenInvited(msg.toAgentID, msg.imSessionID)) && (!HasAgentDroppedSession(msg.toAgentID, msg.imSessionID));
                        agentDroppedSession = HasAgentDroppedSession(msg.toAgentID, msg.imSessionID);
                    }

                    if (needsSessionAdd)
                    {
                        // Agent not in session and hasn't dropped from session
                        // Add them to the session for now, and Invite them
                        AddAgentToGroupSession(msg.toAgentID, msg.imSessionID);

                        UUID toAgentID = new UUID(msg.toAgentID);
                        IClientAPI activeClient = GetActiveClient(toAgentID);
                        if (activeClient != null)
                        {
                            UUID groupID = new UUID(msg.imSessionID);   // caller passes the group ID here

                            GroupRecord groupInfo = m_groupsModule.GetGroupRecord(groupID);
                            if (groupInfo != null)
                            {
                                if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Sending chatterbox invite instant message");

                                // UUID sessionID = new UUID(groupInfo.GroupID);
                                // msg.imSessionID = sessionID.Guid;
                                UUID sessionID = new UUID(msg.imSessionID);

                                // Force? open the group session dialog???
                                IEventQueue eq = activeClient.Scene.RequestModuleInterface<IEventQueue>();
                                eq.ChatterboxInvitation(
                                    sessionID                       // session ID
                                    , groupInfo.GroupName           // session name
                                    , new UUID(msg.fromAgentID)     // from agent ID
                                    , msg.message                   // message text
                                    , new UUID(msg.toAgentID)       // to agent ID
                                    , msg.fromAgentName             // from agent name
                                    , msg.dialog                    // IM dialog type
                                    , msg.timestamp                 // time stamp
                                    , msg.offline == 1              // offline
                                    , 0                             // parent estate ID
                                    , Vector3.Zero                  // agent position
                                    , 1                             // TTL
                                    , groupID                       // transaction ID (caller passes the group ID here)
                                    , msg.fromGroup                 // from group boolean (true)
                                    , Utils.StringToBytes(groupInfo.GroupName)  // binary bucket
                                    );

                                NotifyNewSessionUserOfExisting(msg.imSessionID, msg.fromAgentID);
                                NotifySessionUserTransition(msg.imSessionID, msg.fromAgentID, (byte)InstantMessageDialog.SessionAdd);

                                ((Scene)activeClient.Scene).EventManager.TriggerOnChatToClient(msg.message,
                                    UUID.Parse(msg.fromAgentID.ToString()), UUID.Parse(msg.toAgentID.ToString()),
                                    activeClient.Scene.RegionInfo.RegionID, msg.timestamp,
                                    ChatToClientType.GroupMessage);
                            }
                        }
                    }
                    else if (!agentDroppedSession)
                    {
                        // User hasn't dropped, so they're in the session, 
                        // maybe we should deliver it.
                        IClientAPI client = GetActiveClient(new UUID(msg.toAgentID));
                        if (client != null)
                        {
                            // Deliver locally, directly
                            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Delivering to {0} locally", client.Name);
                            client.SendInstantMessage(msg);
                        }
                        else
                        {
                            m_log.WarnFormat("[GROUPS-MESSAGING]: Received a message over the grid for a client that isn't here: {0}", msg.toAgentID);
                        }
                    }
                    break;

                default:
                    m_log.WarnFormat("[GROUPS-MESSAGING]: I don't know how to proccess a {0} message.", ((InstantMessageDialog)msg.dialog).ToString());
                    break;
            }
        }

        #endregion

        #region ClientEvents

        private void RemoveAgentFromGroupSession(Guid agentID, Guid sessionID)
        {
            // Add Session Status if it doesn't exist for this session
            CreateGroupSessionTracking(sessionID);  // for dropped tracking

            lock (m_sessionLock)
            {
                if (m_agentsInGroupSession.ContainsKey(sessionID))
                {
                    // If in session remove
                    if (m_agentsInGroupSession[sessionID].Contains(agentID))
                    {
                        m_agentsInGroupSession[sessionID].Remove(agentID);
                    }

                    // If not in dropped list, add
                    if (!m_agentsDroppedSession[sessionID].Contains(agentID))
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Dropped {0} from session {1}", agentID, sessionID);
                        m_agentsDroppedSession[sessionID].Add(agentID);
                    }
                }
            }
        }
        private void RemoveAgentFromAllGroupSessions(Guid agentID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Dropped {0} from group IM sessions", agentID);

            lock (m_sessionLock)
            {
                foreach (KeyValuePair<Guid, List<Guid>> session in m_agentsInGroupSession)
                {
                    if (session.Value.Contains(agentID))
                    {
                        session.Value.Remove(agentID);
                    }
                }

                foreach (KeyValuePair<Guid, List<Guid>> session in m_agentsDroppedSession)
                {
                    if (session.Value.Contains(agentID))
                    {
                        session.Value.Remove(agentID);
                    }
                }
            }
        }

        private void AddAgentToGroupSession(Guid agentID, Guid sessionID)
        {
            // Add Session Status if it doesn't exist for this session
            CreateGroupSessionTracking(sessionID);

            lock (m_sessionLock)
            {
                // If nessesary, remove from dropped list
                if (m_agentsDroppedSession[sessionID].Contains(agentID))
                {
                    m_agentsDroppedSession[sessionID].Remove(agentID);
                }

                // If nessesary, add to in session list
                if (!m_agentsInGroupSession[sessionID].Contains(agentID))
                {
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Added {1} to session {0}", sessionID, agentID);
                    m_agentsInGroupSession[sessionID].Add(agentID);
                }
            }
        }

        private bool HasAgentBeenInvited(Guid agentID, Guid sessionID)
        {
            lock (m_sessionLock)
            {
                if (!m_agentsInGroupSession.ContainsKey(sessionID))
                    return false;

                return m_agentsInGroupSession[sessionID].Contains(agentID);
            }
        }

        private bool HasAgentDroppedSession(Guid agentID, Guid sessionID)
        {
            lock (m_sessionLock)
            {
                if (!m_agentsDroppedSession.ContainsKey(sessionID))
                    return false;

                return m_agentsDroppedSession[sessionID].Contains(agentID);
            }
        }

        private void CreateGroupSessionTracking(Guid sessionID)
        {
            lock (m_sessionLock)
            {
                if (!m_agentsInGroupSession.ContainsKey(sessionID))
                {
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Creating session tracking for : {0}", sessionID);
                    m_agentsInGroupSession.Add(sessionID, new List<Guid>());
                    m_agentsDroppedSession.Add(sessionID, new List<Guid>());
                }
            }
        }

        private void NotifySessionUserTransition(Guid sessionID, Guid userID, byte transition)
        {
            Guid[] participants;
            lock (m_sessionLock)
            {
                if (!m_agentsInGroupSession.ContainsKey(sessionID))
                    return;
                participants = new Guid[m_agentsInGroupSession[sessionID].Count];
                m_agentsInGroupSession[sessionID].CopyTo(participants);
            }

            IEventQueue queue = this.m_sceneList[0].RequestModuleInterface<IEventQueue>();
            foreach (Guid participant in participants)
            {
                // This condition disables participant list updates, except for the current user, 
                // because it only supports participants in the current region. 
                // When they support notification across regions, we can remove the condition.
                if (participant != userID)
                    continue;

                // Notify existing participant of user transition.
                queue.ChatterBoxSessionAgentListUpdates(
                    new UUID(sessionID)
                    , new UUID(userID)
                    , new UUID(participant)
                    , false //canVoiceChat
                    , false //isModerator
                    , false //text mute
                    , transition
                    );
            }
        }

        private void NotifyNewSessionUserOfExisting(Guid sessionID, Guid userID)
        {
            Guid[] participants;
            lock (m_sessionLock)
            {
                if (!m_agentsInGroupSession.ContainsKey(sessionID))
                    return;
                participants = new Guid[m_agentsInGroupSession[sessionID].Count];
                m_agentsInGroupSession[sessionID].CopyTo(participants);
            }

            IEventQueue queue = this.m_sceneList[0].RequestModuleInterface<IEventQueue>();
            foreach (Guid participant in participants)
            {
                // This condition disables participant list updates, except for the current user, 
                // because it only supports participants in the current region. 
                // When they support notification across regions, we can remove the condition.
                if (participant != userID)
                    continue;

                // Notify existing participant of user transition.
                queue.ChatterBoxSessionAgentListUpdates(
                    new UUID(sessionID)
                    , new UUID(participant)
                    , new UUID(userID)
                    , false //canVoiceChat
                    , false //isModerator
                    , false //text mute
                    , (byte)InstantMessageDialog.SessionGroupStart
                    );
            }
        }

        private bool CanJoinGroupIM(UUID agentID, UUID groupID)
        {
            GroupMembershipData data = m_groupsModule.GetMembershipData(groupID, agentID);
            if (data == null)
                return false;
            return ((data.GroupPowers & (ulong)GroupPowers.JoinChat) == (ulong)GroupPowers.JoinChat);
        }

        private bool VerifyIMSecurity(IClientAPI remoteClient, UUID groupID, ref GridInstantMessage im)
        {
            if (!CanJoinGroupIM(remoteClient.AgentId, groupID))
            {
                remoteClient.SendAgentAlertMessage("You do not have permission to send an instant messaging in that group.", true);
                return false;
            }
            if ((im.fromAgentID != remoteClient.AgentId.Guid) || (im.fromAgentName != remoteClient.Name))
            {
                m_log.ErrorFormat("[HACKER]: IM type {0} from {1} [{2}] misidentified as being from {3} [{4}]. Fixed.",
                    im.dialog.ToString(), remoteClient.AgentId, remoteClient.Name, im.fromAgentID, im.fromAgentName);
                remoteClient.SendAgentAlertMessage("You are not allowed to change your identity in an instant messaging.", true);
                return false;
            }
            return true;
        }

        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            UUID groupID = new UUID(im.imSessionID);

            if (m_debugEnabled)
            {
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

                DebugGridInstantMessage(im);
            }

            // Start group IM session
            if ((im.dialog == (byte)InstantMessageDialog.SessionGroupStart))
            {
                GroupRecord groupInfo = m_groupsModule.GetGroupRecord(groupID);
                if (groupInfo != null)
                {
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Start Group Session for {0}", groupInfo.GroupName);

                    im.imSessionID = groupInfo.GroupID.Guid;

                    bool success = CanJoinGroupIM(remoteClient.AgentId, groupID);
                    string error = null;
                    if (success)
                        AddAgentToGroupSession(im.fromAgentID, im.imSessionID);
                    else
                        error = "no_ability";

                    ChatterBoxSessionStartReplyViaCaps(remoteClient, groupInfo.GroupName, groupID, error);
                    if (success)
                    {
                        NotifyNewSessionUserOfExisting(im.imSessionID, remoteClient.AgentId.Guid);
                        NotifySessionUserTransition(im.imSessionID, remoteClient.AgentId.Guid, im.dialog);
                    }
                }
            }

            if (im.dialog == (byte)InstantMessageDialog.SessionDrop)
            {
                GroupRecord groupInfo = m_groupsModule.GetGroupRecord(groupID);
                if (groupInfo != null)
                {
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Drop Session with {0} for {1}", groupInfo.GroupName, im.fromAgentName);
                    RemoveAgentFromGroupSession(im.fromAgentID, im.imSessionID);
                    NotifySessionUserTransition(im.imSessionID, im.fromAgentID, im.dialog);
                }
            }

            // Send a message from locally connected client to a group
            if ((im.dialog == (byte)InstantMessageDialog.SessionSend))
            {
                if (!VerifyIMSecurity(remoteClient, groupID, ref im))
                    return;

                if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Send message to session for group {0} with session ID {1}", groupID, im.imSessionID.ToString());
                SendMessageToGroup(remoteClient, im, groupID);
            }
        }

        #endregion

        private void SendMessageToGroup(IClientAPI remoteClient, GridInstantMessage im, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<UUID> muters;
            IMuteListModule m_muteListModule = m_sceneList[0].RequestModuleInterface<IMuteListModule>();
            if (m_muteListModule != null)
                muters = m_muteListModule.GetInverseMuteList(remoteClient.AgentId);
            else
                muters = new List<UUID>();

            List<GroupMembersData> members = m_groupsModule.GroupMembersRequest(null, null, UUID.Zero, groupID);
            foreach (GroupMembersData member in members)
            {
                if (HasAgentDroppedSession(member.AgentID.Guid, im.imSessionID))
                {
                    // Don't deliver messages to people who have dropped this session
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} has dropped session, not delivering to them", member.AgentID);
                    continue;
                }

                if (member.OnlineStatus == false)
                {
                    continue;
                }
                if ((member.AgentPowers & (ulong)GroupPowers.JoinChat) != (ulong)GroupPowers.JoinChat)
                {
                    continue;
                }
                if (muters.Contains(member.AgentID))
                {   // Don't deliver messages to people who have the sender muted.
                    continue;
                }

                // Copy Message
                GridInstantMessage msg = new GridInstantMessage();
                msg.imSessionID = groupID.Guid;
                msg.fromAgentName = im.fromAgentName;
                msg.message = im.message;
                msg.dialog = im.dialog;
                msg.offline = im.offline;
                msg.ParentEstateID = im.ParentEstateID;
                msg.Position = im.Position;
                msg.RegionID = im.RegionID;
                msg.binaryBucket = im.binaryBucket;
                msg.timestamp = (uint)Util.UnixTimeSinceEpoch();

                // Update pertinent fields to make it a "group message"
                msg.fromAgentID = im.fromAgentID;
                msg.fromGroup = true;

                msg.toAgentID = member.AgentID.Guid;

                IClientAPI client = GetActiveClient(member.AgentID);
                if (client == null)
                {
                    // If they're not local, forward across the grid
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Delivering to {0} via Grid", member.AgentID);
                    m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
                }
                else
                {
                    // Deliver locally, directly
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Passing to ProcessMessageFromGroupSession to deliver to {0} locally", client.Name);
                    ProcessMessageFromGroupSession(msg);
                }
            }
        }

        void ChatterBoxSessionStartReplyViaCaps(IClientAPI remoteClient, string groupName, UUID groupID, string error)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap moderatedMap = new OSDMap(4);
            moderatedMap.Add("voice", OSD.FromBoolean(false));

            OSDMap sessionMap = new OSDMap(4);
            sessionMap.Add("moderated_mode", moderatedMap);
            sessionMap.Add("session_name", OSD.FromString(groupName));
            sessionMap.Add("type", OSD.FromInteger(0));
            sessionMap.Add("voice_enabled", OSD.FromBoolean(false));

            OSDMap bodyMap = new OSDMap((error==null)?4:5);
            bodyMap.Add("session_id", OSD.FromUUID(groupID));
            bodyMap.Add("temp_session_id", OSD.FromUUID(groupID));
            bodyMap.Add("success", OSD.FromBoolean(error == null));
            bodyMap.Add("session_info", sessionMap);
            if (error != null)
                bodyMap.Add("error", OSD.FromString(error));

            IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();

            if (queue != null)
            {
                queue.Enqueue(EventQueueHelper.BuildEvent("ChatterBoxSessionStartReply", bodyMap), remoteClient.AgentId);
            }
        }

        private void DebugGridInstantMessage(GridInstantMessage im)
        {
            // Don't log any normal IMs (privacy!)
            if (m_debugEnabled && im.dialog != (byte)InstantMessageDialog.MessageFromAgent)
            {
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: fromGroup({0})", im.fromGroup ? "True" : "False");
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: Dialog({0})", ((InstantMessageDialog)im.dialog).ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: fromAgentID({0})", im.fromAgentID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: fromAgentName({0})", im.fromAgentName.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: imSessionID({0})", im.imSessionID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: message({0})", im.message.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: offline({0})", im.offline.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: toAgentID({0})", im.toAgentID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: binaryBucket({0})", OpenMetaverse.Utils.BytesToHexString(im.binaryBucket, "BinaryBucket"));
            }
        }

        #region Client Tools

        /// <summary>
        /// Try to find an active IClientAPI reference for agentID giving preference to root connections
        /// </summary>
        private IClientAPI GetActiveClient(UUID agentID)
        {
            IClientAPI child = null;

            // Try root avatar first
            foreach (Scene scene in m_sceneList)
            {
                if (scene.Entities.ContainsKey(agentID) &&
                    scene.Entities[agentID] is ScenePresence)
                {
                    ScenePresence user = (ScenePresence)scene.Entities[agentID];
                    if (!user.IsChildAgent)
                    {
                        return user.ControllingClient;
                    }
                    else
                    {
                        child = user.ControllingClient;
                    }
                }
            }

            // If we didn't find a root, then just return whichever child we found, or null if none
            return child;
        }

        #endregion
    }
}
