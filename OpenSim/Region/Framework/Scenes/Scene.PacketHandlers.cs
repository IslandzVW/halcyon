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

using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.Framework.Scenes
{
    public partial class Scene
    {
        /// <summary>
        /// Sends a chat message to clients in the region
        /// </summary>
        /// <param name="message">The message to send to users</param>
        /// <param name="type">The type of message (say, shout, whisper, owner say, etc)</param>
        /// <param name="channel">The channel to speak the message on</param>
        /// <param name="part">The SceneObjectPart that is sending this chat message</param>
        public void SimChat(string message, ChatTypeEnum type, int channel, SceneObjectPart part)
        {
            SimChat(message, type, channel, part.AbsolutePosition, part.Name, part.UUID, UUID.Zero, part.OwnerID, false);
        }

        /// <summary>
        /// Sends a chat message to clients in the region
        /// </summary>
        /// <param name="message">The message to send to users</param>
        /// <param name="type">The type of message (say, shout, whisper, owner say, etc)</param>
        /// <param name="channel">The channel to speak the message on</param>
        /// <param name="part">The SceneObjectPart that is sending this chat message</param>
        /// <param name="destID">The user or object that is being spoken to (UUID.Zero specifies all users will get the message)</param>
        /// <param name="broadcast">Whether the message will be sent regardless of distance from the sender</param>
        public void SimChat(string message, ChatTypeEnum type, int channel, SceneObjectPart part,
            UUID destID, bool broadcast = false)
        {
            SimChat(message, type, channel, part.AbsolutePosition, part.Name, part.UUID, destID, part.OwnerID, broadcast);
        }

        /// <summary>
        /// Sends a chat message to clients in the region
        /// </summary>
        /// <param name="message">The message to send to users</param>
        /// <param name="type">The type of message (say, shout, whisper, owner say, etc)</param>
        /// <param name="channel">The channel to speak the message on</param>
        /// <param name="presence">The ScenePresence that is sending this chat message</param>
        public void SimChat(string message, ChatTypeEnum type, int channel, ScenePresence presence)
        {
            SimChat(message, type, channel, presence.AbsolutePosition, presence.Name, presence.UUID, UUID.Zero, presence.UUID, false);
        }

        /// <summary>
        /// Sends a chat message to clients in the region
        /// </summary>
        /// <param name="message">The message to send to users</param>
        /// <param name="type">The type of message (say, shout, whisper, owner say, etc)</param>
        /// <param name="channel">The channel to speak the message on</param>
        /// <param name="presence">The ScenePresence that is sending this chat message</param>
        /// <param name="destID">The user or object that is being spoken to (UUID.Zero specifies all users will get the message)</param>
        /// <param name="broadcast">Whether the message will be sent regardless of distance from the sender</param>
        public void SimChat(string message, ChatTypeEnum type, int channel, ScenePresence presence,
            UUID destID, bool broadcast = false)
        {
            SimChat(message, type, channel, presence.AbsolutePosition, presence.Name, presence.UUID, destID, presence.UUID, broadcast);
        }

        /// <summary>
        /// Sends a chat message to clients in the region
        /// </summary>
        /// <param name="message">The message to send to users</param>
        /// <param name="type">The type of message (say, shout, whisper, owner say, etc)</param>
        /// <param name="channel">The channel to speak the message on</param>
        /// <param name="fromPos">The position of the speaker (the SceneObjectPart or ScenePresence)</param>
        /// <param name="fromName">The name of the speaker (the SceneObjectPart or ScenePresence)</param>
        /// <param name="fromID">The UUID of the speaker (the SceneObjectPart or ScenePresence)</param>
        /// <param name="destID">The user or object that is being spoken to (UUID.Zero specifies all users will get the message)</param>
        /// <param name="generatingAvatarID">The avatar ID that has generated this message regardless of
        /// if it is a script owned by this avatar, or the avatar itself</param>
        /// <param name="broadcast">Whether the message will be sent regardless of distance from the sender</param>
        public void SimChat(string message, ChatTypeEnum type, int channel, Vector3 fromPos,
            string fromName, UUID fromID, UUID destID, UUID generatingAvatarID, bool broadcast)
        {
            OSChatMessage args = new OSChatMessage()
            {
                Channel = channel,
                DestinationUUID = destID,
                From = fromName,
                GeneratingAvatarID = generatingAvatarID,
                Message = message,
                Position = fromPos,
                Scene = this,
                SenderUUID = fromID,
                Type = type
            };

            if (broadcast)
                EventManager.TriggerOnChatBroadcast(this, args);
            else
                EventManager.TriggerOnChatFromWorld(this, args);
        }

        /// <summary>
        /// Invoked when the client requests a prim.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void RequestPrim(uint primLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup obj = this.GetGroupByPrim(primLocalID);

            if (obj != null)
            {
                //The viewer didn't have the cached prim like we thought - force a full update 
                // so that they will get the full prim
                obj.SendFullUpdateToClient(remoteClient, PrimUpdateFlags.ForcedFullUpdate);
            }

            return;
        }

        /// <summary>
        /// Invoked when the client selects a prim.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void SelectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = m_sceneGraph.GetGroupByPrim(primLocalID);
            if (group == null) return;

            if (group.LocalId == primLocalID)
            {
                group.GetProperties(remoteClient);
                group.IsSelected = true;

                // A prim is only tainted if it's allowed to be edited by the person clicking it.
                if (Permissions.CanEditObject(group.UUID, remoteClient.AgentId,0)
                    || Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
                {
                    EventManager.TriggerParcelPrimCountTainted();
                }
            }
            else
            {
                //it's one of the child prims
                SceneObjectPart part = group.GetChildPart(primLocalID);
                part.GetProperties(remoteClient);
            }
        }

        /// <summary>
        /// Handle the deselection of a prim from the client.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void DeselectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(primLocalID);
            if (part == null)
                return;
            
            // The prim is in the process of being deleted.
            if (null == part.ParentGroup.RootPart)
                return;
            
            // A deselect packet contains all the local prims being deselected.  However, since selection is still
            // group based we only want the root prim to trigger a full update - otherwise on objects with many prims
            // we end up sending many duplicate ObjectUpdates
            if (part.ParentGroup.RootPart.LocalId != part.LocalId)
                return;

            bool isAttachment = false;
            
            // This is wrong, wrong, wrong. Selection should not be
            // handled by group, but by prim. Legacy cruft.
            // TODO: Make selection flagging per prim!
            //
            part.ParentGroup.IsSelected = false;
            
            if (part.ParentGroup.IsAttachment)
                isAttachment = true;
            // Regardless of if it is an attachment or not, we need to resend the position in case it moved or changed.
            part.ParentGroup.ScheduleGroupForFullUpdate(PrimUpdateFlags.FindBest);

            // If it's not an attachment, and we are allowed to move it,
            // then we might have done so. If we moved across a parcel
            // boundary, we will need to recount prims on the parcels.
            // For attachments, that makes no sense.
            //
            if (!isAttachment)
            {
                if (Permissions.CanEditObject(
                        part.ParentGroup.UUID, remoteClient.AgentId, 0) 
                        || Permissions.CanMoveObject(
                        part.ParentGroup.UUID, remoteClient.AgentId))
                    EventManager.TriggerParcelPrimCountTainted();
            }
        }

        public virtual void ProcessMoneyTransferRequest(UUID source, UUID destination, int amount, 
                                                        int transactiontype, string description)
        {
            EventManager.MoneyTransferArgs args = new EventManager.MoneyTransferArgs(source, destination, amount, 
                                                                                     transactiontype, description);

            EventManager.TriggerMoneyTransfer(this, args);
        }

        public virtual void ProcessParcelBuy(UUID agentId, UUID groupId, bool final, bool groupOwned,
                bool removeContribution, int parcelLocalID, int parcelArea, int parcelPrice, bool authenticated)
        {
            EventManager.LandBuyArgs args = new EventManager.LandBuyArgs(agentId, groupId, final, groupOwned, 
                                                                         removeContribution, parcelLocalID, parcelArea, 
                                                                         parcelPrice, authenticated);

            // First, allow all validators a stab at it
            m_eventManager.TriggerValidateLandBuy(this, args);

            // Then, check validation and transfer
            m_eventManager.TriggerLandBuy(this, args);
        }

        public virtual void ProcessObjectGrab(uint localID, Vector3 offsetPos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            SceneObjectGroup obj = this.GetGroupByPrim(localID);

            if (obj != null)
            {
                // Is this prim part of the group
                if (obj.HasChildPrim(localID))
                {
                    // Currently only grab/touch for the single prim
                    // the client handles rez correctly
                    obj.ObjectGrabHandler(localID, offsetPos, remoteClient);

                    SceneObjectPart part = obj.GetChildPart(localID);

                    int touchEvents = ((int)ScriptEvents.touch_start | (int)ScriptEvents.touch | (int)ScriptEvents.touch_end);

                    bool hasTouchEvent = (((int)part.ScriptEvents & touchEvents) != 0);

                    // If the touched prim handles touches, deliver it
                    if (hasTouchEvent)
                        EventManager.TriggerObjectGrab(part.LocalId, 0, part.OffsetPosition, remoteClient, surfaceArg);

                    // If not, or if PassTouches and we haven't just delivered it to the root prim, deliver it there
                    if ((!hasTouchEvent) || (part.PassTouches && (part.LocalId != obj.RootPart.LocalId)))
                        EventManager.TriggerObjectGrab(obj.RootPart.LocalId, part.LocalId, part.OffsetPosition, remoteClient, surfaceArg);
                }
            }
        }

        public virtual void ProcessObjectDeGrab(uint localID, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            SceneObjectGroup obj = this.GetGroupByPrim(localID);

            // Is this prim part of the group
            if (obj != null && obj.HasChildPrim(localID))
            {
                SceneObjectPart part=obj.GetChildPart(localID);
                SceneObjectGroup group = part.ParentGroup;
                if (part != null)
                {
                    // If the touched prim handles touches, deliver it
                    // If not, deliver to root prim
                    ScriptEvents eventsThatNeedDegrab = (ScriptEvents.touch_end | ScriptEvents.touch);

                    if ((part.ScriptEvents & eventsThatNeedDegrab) != 0)
                    {
                        EventManager.TriggerObjectDeGrab(part.LocalId, 0, remoteClient, surfaceArg);
                    }
                    else if ((group.RootPart.ScriptEvents & eventsThatNeedDegrab) != 0)
                    {
                        EventManager.TriggerObjectDeGrab(obj.RootPart.LocalId, part.LocalId, remoteClient, surfaceArg);
                    }

                    // Always send an object degrab.
                    m_sceneGraph.DegrabObject(localID, remoteClient, surfaceArgs);

                    return;
                }
                return;
            }
        }

        public void ProcessAvatarPickerRequest(IClientAPI client, UUID avatarID, UUID RequestID, string query)
        {
            //EventManager.TriggerAvatarPickerRequest();

            List<AvatarPickerAvatar> AvatarResponses = new List<AvatarPickerAvatar>();
            AvatarResponses = m_sceneGridService.GenerateAgentPickerRequestResponse(RequestID, query);

            AvatarPickerReplyPacket replyPacket = (AvatarPickerReplyPacket) PacketPool.Instance.GetPacket(PacketType.AvatarPickerReply);
            // TODO: don't create new blocks if recycling an old packet

            AvatarPickerReplyPacket.DataBlock[] searchData =
                new AvatarPickerReplyPacket.DataBlock[AvatarResponses.Count];
            AvatarPickerReplyPacket.AgentDataBlock agentData = new AvatarPickerReplyPacket.AgentDataBlock();

            agentData.AgentID = avatarID;
            agentData.QueryID = RequestID;
            replyPacket.AgentData = agentData;
            //byte[] bytes = new byte[AvatarResponses.Count*32];

            int i = 0;
            foreach (AvatarPickerAvatar item in AvatarResponses)
            {
                UUID translatedIDtem = item.AvatarID;
                searchData[i] = new AvatarPickerReplyPacket.DataBlock();
                searchData[i].AvatarID = translatedIDtem;
                searchData[i].FirstName = Utils.StringToBytes((string) item.firstName);
                searchData[i].LastName = Utils.StringToBytes((string) item.lastName);
                i++;
            }
            if (AvatarResponses.Count == 0)
            {
                searchData = new AvatarPickerReplyPacket.DataBlock[0];
            }
            replyPacket.Data = searchData;

            AvatarPickerReplyAgentDataArgs agent_data = new AvatarPickerReplyAgentDataArgs();
            agent_data.AgentID = replyPacket.AgentData.AgentID;
            agent_data.QueryID = replyPacket.AgentData.QueryID;

            List<AvatarPickerReplyDataArgs> data_args = new List<AvatarPickerReplyDataArgs>();
            for (i = 0; i < replyPacket.Data.Length; i++)
            {
                AvatarPickerReplyDataArgs data_arg = new AvatarPickerReplyDataArgs();
                data_arg.AvatarID = replyPacket.Data[i].AvatarID;
                data_arg.FirstName = replyPacket.Data[i].FirstName;
                data_arg.LastName = replyPacket.Data[i].LastName;
                data_args.Add(data_arg);
            }
            client.SendAvatarPickerReply(agent_data, data_args);
        }

        public void ProcessScriptReset(IClientAPI remoteClient, UUID objectID,
                UUID itemID)
        {
            SceneObjectPart part=GetSceneObjectPart(objectID);
            if (part == null)
                return;

            if (Permissions.CanResetScript(part.ParentGroup.UUID, itemID, remoteClient.AgentId))
            {
                EventManager.TriggerScriptReset(part.LocalId, itemID);
            }
        }
        
        /// <summary>
        /// Handle a fetch inventory request from the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="ownerID"></param>
        public void HandleFetchInventory(IClientAPI remoteClient, UUID itemID, UUID ownerID)
        {
            if (ownerID == CommsManager.LibraryRoot.Owner)
            {
                //m_log.Debug("request info for library item");
                return;
            }

            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

            if (null == userProfile)
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
                return;
            }

            InventoryItemBase item = userProfile.FindItem(itemID);
            if (item != null)
            {
                remoteClient.SendInventoryItemDetails(ownerID, item);
            }
            
        }    
        
        /// <summary>
        /// Tell the client about the various child items and folders contained in the requested folder.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        public void HandleFetchInventoryDescendents(IClientAPI remoteClient, UUID folderID, UUID ownerID,
                                                    bool fetchFolders, bool fetchItems, int sortOrder)
        {
            // TODO: This code for looking in the folder for the library should be folded back into the
            // CachedUserInfo so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).
            InventoryFolderImpl fold = null;
            if ((fold = CommsManager.LibraryRoot.FindFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(
                    fold.Owner, fold, fold.RequestListOfItems(),
                    fold.RequestListOfFolders(), fetchFolders, fetchItems);
                return;
            }

            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

            if (null == userProfile)
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);  
                return;
            }

            userProfile.SendInventoryDecendents(remoteClient, folderID, fetchFolders, fetchItems);
        }        
        
        /// <summary>
        /// Handle the caps inventory descendents fetch.
        ///
        /// Since the folder structure is sent to the client on login, I believe we only need to handle items.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        /// <returns>null if the inventory look up failed</returns>
        public List<InventoryItemBase> HandleFetchInventoryDescendentsCAPS(UUID agentID, UUID folderID, UUID ownerID,
                                                   bool fetchFolders, bool fetchItems, int sortOrder)
        {
//            m_log.DebugFormat(
//                "[INVENTORY CACHE]: Fetching folders ({0}), items ({1}) from {2} for agent {3}",
//                fetchFolders, fetchItems, folderID, agentID);

            // FIXME MAYBE: We're not handling sortOrder!

            // TODO: This code for looking in the folder for the library should be folded back into the
            // CachedUserInfo so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).            
            InventoryFolderImpl fold;
            if ((fold = CommsManager.LibraryRoot.FindFolder(folderID)) != null)
            {
                return fold.RequestListOfItems();
            }
            
            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(agentID);
            
            if (null == userProfile)
            {
                m_log.ErrorFormat("[AGENT INVENTORY]: Could not find user profile for {0}", agentID);                
                return null;
            }

            if (UserDoesntOwnFolder(agentID, folderID))
            {
                m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Not fetching descendents of {0} for user {1}, user does not own the folder",
                        folderID, agentID);

                return null;
            }

            return null;

            /*if ((fold = userProfile.FindFolderAtt(folderID)) != null)
            {
                return fold.RequestListOfItems();
            }
            else
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Could not find folder {0} requested by user {1}",
                    folderID, agentID);
                return null;
            }*/
        }        
        
        /// <summary>
        /// Handle an inventory folder creation request from the client.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="folderType"></param>
        /// <param name="folderName"></param>
        /// <param name="parentID"></param>
        public void HandleCreateInventoryFolder(IClientAPI remoteClient, UUID folderID, ushort folderType,
                                                string folderName, UUID parentID)
        {
            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

            if (null == userProfile)
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
                return;
            }

            if (folderID == UUID.Zero)
            {
                m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Not creating zero uuid folder for {0}",
                        remoteClient.Name);

                return;
            }

            userProfile.CreateFolder(folderName, folderID, (short)folderType, parentID);
        }      

        /// <summary>
        /// Handle a client request to update the inventory folder
        /// </summary>
        ///
        /// FIXME: We call add new inventory folder because in the data layer, we happen to use an SQL REPLACE
        /// so this will work to rename an existing folder.  Needless to say, to rely on this is very confusing,
        /// and needs to be changed.
        ///  
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="parentID"></param>
        public void HandleUpdateInventoryFolder(IClientAPI remoteClient, UUID folderID, ushort type, string name,
                                                UUID parentID)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Updating inventory folder {0} {1} for {2} {3}", folderID, name, remoteClient.Name, remoteClient.AgentId);

            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

            if (null == userProfile)
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
                return;
            }

            InventoryFolderBase folder = userProfile.GetFolderAttributes(folderID);

            if (folder.Owner != remoteClient.AgentId)
            {
                m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Not updating folder {0} for user {2}, user does not own the folder",
                        folderID, remoteClient.Name);

                return;
            }

            folder.Name = name;
            folder.Type = (short)type;
            folder.ParentID = parentID;

            if (!userProfile.UpdateFolder(folder))
            {
                m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Failed to update folder for user {0} {1}",
                        remoteClient.Name, remoteClient.AgentId);
            }
            
        }

        private bool UserDoesntOwnFolder(UUID userId, UUID folderId)
        {
            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(userId);

            //make sure the user owns the source folder
            InventoryFolderBase folder = userProfile.GetFolderAttributes(folderId);
            if (folder.Owner != userId)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle an inventory folder move request from the client.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="parentID"></param>
        public void HandleMoveInventoryFolder(IClientAPI remoteClient, UUID folderID, UUID parentID)
        {
            CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

            if (null == userProfile)
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                    remoteClient.Name, remoteClient.AgentId);
                return;
            }

            m_log.InfoFormat(
                        "[AGENT INVENTORY]: Moving folder {0} to {1} for user {2}",
                        folderID, parentID, remoteClient.Name);

            if (!userProfile.MoveFolder(folderID, parentID))
            {
                m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Failed to move folder {0} to {1} for user {2}",
                        folderID, parentID, remoteClient.Name);
            }
            
        }
        
        /// <summary>
        /// This should recursively delete all the items and folders in the given directory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        public void HandlePurgeInventoryDescendents(IClientAPI remoteClient, UUID folderID)
        {
            Util.FireAndForget(Util.PoolSelection.LongIO, delegate(object obj)
            {
                CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

                if (null == userProfile)
                {
                    m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                        remoteClient.Name, remoteClient.AgentId);
                    return;
                }

                //make sure the user owns the source folder
                InventoryFolderBase folder = userProfile.GetFolderAttributes(folderID);

                if (folder.Owner != remoteClient.AgentId)
                {
                    m_log.ErrorFormat(
                            "[AGENT INVENTORY]: Not purging descendents of {0} for user {1}, user does not own the folder",
                            folderID, remoteClient.Name);

                    return;
                }

                //make sure the folder is either the lost and found, the trash, or a descendant of it
                if (!CheckFolderHeirarchyIsAppropriateForPurge(folder, userProfile))
                {
                    m_log.ErrorFormat(
                            "[AGENT INVENTORY]: Not purging descendents of {0} for user {1}, folder is not part of a purgeable heirarchy",
                            folderID, remoteClient.Name);
                    return;
                }

                m_log.InfoFormat("[AGENT INVENTORY]: Purging descendents of {0} {1} for user {2}", folderID, folder.Name, remoteClient.Name);

                if (!userProfile.PurgeFolderContents(folder))
                {
                    m_log.ErrorFormat(
                            "[AGENT INVENTORY]: Failed to purge folder for user {0} {1}",
                            remoteClient.Name, remoteClient.AgentId);
                }
            });
        }

        /// <summary>
        /// This should delete the given directory and all its descendents recursively.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        public void HandlePurgeInventoryFolder(IClientAPI remoteClient, UUID folderID)
        {
            Util.FireAndForget(Util.PoolSelection.LongIO, delegate(object obj)
            {
                CachedUserInfo userProfile = CommsManager.UserService.GetUserDetails(remoteClient.AgentId);

                if (null == userProfile)
                {
                    m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Could not find user profile for {0} {1}",
                        remoteClient.Name, remoteClient.AgentId);
                    return;
                }

                //make sure the user owns the source folder
                InventoryFolderBase folder = userProfile.GetFolderAttributes(folderID);

                if (folder.Owner != remoteClient.AgentId)
                {
                    m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Not purging descendents of {0} for user {1}, user does not own the folder",
                        folderID, remoteClient.Name);

                    return;
                }

                //make sure the folder is either the lost and found, the trash, or a descendant of it
                if (!CheckFolderHeirarchyIsAppropriateForPurge(folder, userProfile))
                {
                    m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Not purging descendents of {0} for user {1}, folder is not part of a purgeable heirarchy",
                        folderID, remoteClient.Name);
                    return;
                }

                m_log.InfoFormat("[AGENT INVENTORY]: Purging {0} {1} for user {2}", folderID, folder.Name, remoteClient.Name);

                if (!userProfile.PurgeFolder(folder))
                {
                    m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Failed to purge folder for user {0} {1}",
                        remoteClient.Name, remoteClient.AgentId);
                }
            });
        }

        private bool CheckFolderHeirarchyIsAppropriateForPurge(InventoryFolderBase folder, CachedUserInfo userProfile)
        {
            if (folder.Type == (short)FolderType.Trash||
                folder.Type == (short)FolderType.LostAndFound)
            {
                return true;
            }

            if (folder.ParentID == UUID.Zero ||
                folder.Type == (short)FolderType.Root)
            {
                //got to the top, didnt find squat
                return false;
            }

            InventoryFolderBase parent = userProfile.GetFolderAttributes(folder.ParentID);
            return CheckFolderHeirarchyIsAppropriateForPurge(parent, userProfile);
        }

        protected void GrabUpdate(UUID objectID, Vector3 startPos, Vector3 pos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SceneObjectGroup group = m_sceneGraph.GetGroupByPrim(objectID);
            if (group != null)
            {
                SceneObjectPart part = group.GetChildPart(objectID);

                if (part != null)
                {
                    SurfaceTouchEventArgs surfaceArg = null;
                    Vector3 grabOffset = Vector3.Zero;
                    if (surfaceArgs != null && surfaceArgs.Count > 0)
                    {
                        surfaceArg = surfaceArgs[0];
                        if (surfaceArg.FaceIndex >= 0)
                            grabOffset = surfaceArg.Position - startPos;
                        else
                            grabOffset = pos - startPos;
                    }

                    // If the touched prim handles touches, deliver it
                    // If not, deliver to root prim,if the root prim doesnt
                    // handle it, deliver a grab to the scene graph
                    if ((part.ScriptEvents & ScriptEvents.touch) != 0)
                    {
                        EventManager.TriggerObjectGrabUpdate(part.LocalId, 0, grabOffset, remoteClient, surfaceArg);
                    }
                    else if ((group.RootPart.ScriptEvents & ScriptEvents.touch) != 0)
                    {
                        EventManager.TriggerObjectGrabUpdate(group.RootPart.LocalId, part.LocalId, grabOffset, remoteClient, surfaceArg);
                    }
                    else
                    {
                        //no one can handle it, send a grab
                        m_sceneGraph.MoveObject(objectID, startPos, pos, remoteClient, surfaceArgs);
                    }
                }
                
            }
        }
    }
}
