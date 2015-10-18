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


namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    public class XmlRpcGroupsMessaging : ISharedRegionModule
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_sceneList = new List<Scene>();

        private IMessageTransferModule m_msgTransferModule = null;

        private IGroupsModule m_groupsModule = null;

        // TODO: Move this off to the xmlrpc server
        public Dictionary<Guid, List<Guid>> m_agentsInGroupSession = new Dictionary<Guid, List<Guid>>();
        public Dictionary<Guid, List<Guid>> m_agentsDroppedSession = new Dictionary<Guid, List<Guid>>();


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

                if (groupsConfig.GetString("Module", "Default") != "XmlRpcGroups")
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

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // The instant message module will only deliver messages of dialog types:
            // MessageFromAgent, StartTyping, StopTyping, MessageFromObject
            //
            // Any other message type will not be delivered to a client by the 
            // Instant Message Module


            if (m_debugEnabled)
            {
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

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
                    break;

                case (byte)InstantMessageDialog.SessionDrop:
                    RemoveAgentFromGroupSession(msg.fromAgentID, msg.imSessionID);
                    break;

                case (byte)InstantMessageDialog.SessionSend:
                    if (!m_agentsInGroupSession.ContainsKey(msg.toAgentID)
                        && !m_agentsDroppedSession.ContainsKey(msg.toAgentID))
                    {
                        // Agent not in session and hasn't dropped from session
                        // Add them to the session for now, and Invite them
                        AddAgentToGroupSession(msg.toAgentID, msg.imSessionID);

                        UUID toAgentID = new UUID(msg.toAgentID);
                        IClientAPI activeClient = GetActiveClient(toAgentID);
                        if (activeClient != null)
                        {
                            UUID groupID = new UUID(msg.fromAgentID);

                            GroupRecord groupInfo = m_groupsModule.GetGroupRecord(groupID);
                            if (groupInfo != null)
                            {
                                if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Sending chatterbox invite instant message");

                                // Force? open the group session dialog???
                                IEventQueue eq = activeClient.Scene.RequestModuleInterface<IEventQueue>();
                                eq.ChatterboxInvitation(
                                    groupID
                                    , groupInfo.GroupName
                                    , new UUID(msg.fromAgentID)
                                    , msg.message, new UUID(msg.toAgentID)
                                    , msg.fromAgentName
                                    , msg.dialog
                                    , msg.timestamp
                                    , msg.offline == 1
                                    , (int)msg.ParentEstateID
                                    , msg.Position
                                    , 1
                                    , new UUID(msg.imSessionID)
                                    , msg.fromGroup
                                    , Utils.StringToBytes(groupInfo.GroupName)
                                    );

                                eq.ChatterBoxSessionAgentListUpdates(
                                    new UUID(groupID)
                                    , new UUID(msg.fromAgentID)
                                    , new UUID(msg.fromAgentID)
                                    , false //canVoiceChat
                                    , false //isModerator
                                    , false //text mute
                                    , msg.dialog
                                    );

                                ((Scene)activeClient.Scene).EventManager.TriggerOnChatToClient(msg.message,
                                    UUID.Parse(msg.fromAgentID.ToString()), UUID.Parse(msg.toAgentID.ToString()),
                                    activeClient.Scene.RegionInfo.RegionID, msg.timestamp,
                                    ChatToClientType.GroupMessage);
                            }
                        }
                    }
                    else if (!m_agentsDroppedSession.ContainsKey(msg.toAgentID))
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
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Dropped {1} from session {0}", sessionID, agentID);
                    m_agentsDroppedSession[sessionID].Add(agentID);
                }
            }
        }

        private void AddAgentToGroupSession(Guid agentID, Guid sessionID)
        {
            // Add Session Status if it doesn't exist for this session
            CreateGroupSessionTracking(sessionID);

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

        private void CreateGroupSessionTracking(Guid sessionID)
        {
            if (!m_agentsInGroupSession.ContainsKey(sessionID))
            {
                if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Creating session tracking for : {0}", sessionID);
                m_agentsInGroupSession.Add(sessionID, new List<Guid>());
                m_agentsDroppedSession.Add(sessionID, new List<Guid>());
            }
        }

        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled)
            {
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

                DebugGridInstantMessage(im);
            }

            // Start group IM session
            if ((im.dialog == (byte)InstantMessageDialog.SessionGroupStart))
            {
                UUID groupID = new UUID(im.toAgentID);

                GroupRecord groupInfo = m_groupsModule.GetGroupRecord(groupID);
                if (groupInfo != null)
                {
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Start Group Session for {0}", groupInfo.GroupName);

                    AddAgentToGroupSession(im.fromAgentID, im.imSessionID);

                    ChatterBoxSessionStartReplyViaCaps(remoteClient, groupInfo.GroupName, groupID);

                    IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();
                    queue.ChatterBoxSessionAgentListUpdates(
                        new UUID(groupID)
                        , new UUID(im.fromAgentID)
                        , new UUID(im.fromAgentID)
                        , false //canVoiceChat
                        , false //isModerator
                        , false //text mute
                        , im.dialog
                        );
                }
            }

            // Send a message from locally connected client to a group
            if ((im.dialog == (byte)InstantMessageDialog.SessionSend))
            {
                UUID groupID = new UUID(im.toAgentID);

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

            foreach (GroupMembersData member in m_groupsModule.GroupMembersRequest(null, null, UUID.Zero, groupID))
            {
                if (m_agentsDroppedSession[im.imSessionID].Contains(member.AgentID.Guid))
                {   // Don't deliver messages to people who have dropped this session
                    continue;
                }
                if (muters.Contains(member.AgentID))
                {   // Don't deliver messages to people who have the sender muted.
                    continue;
                }

                // Copy Message
                GridInstantMessage msg = new GridInstantMessage();
                msg.imSessionID = im.imSessionID;
                msg.fromAgentName = im.fromAgentName;
                msg.message = im.message;
                msg.dialog = im.dialog;
                msg.offline = im.offline;
                msg.ParentEstateID = im.ParentEstateID;
                msg.Position = im.Position;
                msg.RegionID = im.RegionID;
                msg.binaryBucket = im.binaryBucket;
                msg.timestamp = (uint)Util.UnixTimeSinceEpoch();

                // Updat Pertinate fields to make it a "group message"
                msg.fromAgentID = groupID.Guid;
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

        void ChatterBoxSessionStartReplyViaCaps(IClientAPI remoteClient, string groupName, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap moderatedMap = new OSDMap(4);
            moderatedMap.Add("voice", OSD.FromBoolean(false));

            OSDMap sessionMap = new OSDMap(4);
            sessionMap.Add("moderated_mode", moderatedMap);
            sessionMap.Add("session_name", OSD.FromString(groupName));
            sessionMap.Add("type", OSD.FromInteger(0));
            sessionMap.Add("voice_enabled", OSD.FromBoolean(false));

            OSDMap bodyMap = new OSDMap(4);
            bodyMap.Add("session_id", OSD.FromUUID(groupID));
            bodyMap.Add("temp_session_id", OSD.FromUUID(groupID));
            bodyMap.Add("success", OSD.FromBoolean(true));
            bodyMap.Add("session_info", sessionMap);

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
