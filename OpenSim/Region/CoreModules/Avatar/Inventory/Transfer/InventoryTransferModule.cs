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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Transfer
{
    public class InventoryTransferModule : IInventoryTransferModule, IRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        private List<Scene> m_Scenelist = new List<Scene>();
        private Dictionary<UUID, Scene> m_AgentRegions =
                new Dictionary<UUID, Scene>();

        private IMessageTransferModule m_TransferModule = null;

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                // Allow disabling this module in config
                //
                if (config.Configs["Messaging"].GetString(
                        "InventoryTransferModule", "InventoryTransferModule") !=
                        "InventoryTransferModule")
                    return;
            }

            if (!m_Scenelist.Contains(scene))
            {
                if (m_Scenelist.Count == 0)
                {
                    m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule>();
                    if (m_TransferModule == null)
                        m_log.Error("[INVENTORY TRANSFER]: No Message transfer module found, transfers will be local only");
                }

                m_Scenelist.Add(scene);

                scene.RegisterModuleInterface<IInventoryTransferModule>(this);

                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnClientClosed += ClientLoggedOut;
                scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

                m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule>();
            }
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InventoryModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            // Inventory giving is conducted via instant message
            client.OnInstantMessage += OnInstantMessage;
        }

//////////////////////////////////////////////// 
// Utility functions for inventory offer/accept/decline

        private Scene FindScene(UUID agentId)
        {
            lock (m_Scenelist)
            {
                foreach (Scene scene in m_Scenelist)
                {
                    if (scene.GetScenePresence(agentId) != null)
                        return scene;
                }
            }
            return null;
        }

        private ScenePresence FindAgent(UUID agentId)
        {
            ScenePresence presence = null;

            lock (m_Scenelist)
            {
                foreach (Scene scene in m_Scenelist)
                {
                    presence = scene.GetScenePresence(agentId);
                    if (presence != null)
                        break;
                }
            }
            return presence;
        }

        private void RelayInventoryOfferIM(Scene scene, ScenePresence user, GridInstantMessage im)
        {
            if (user != null) // Local (root agent or child agent)
            {
//                m_log.WarnFormat("[INVENTORY_OFFER]: Relaying IM {0} locally to {1}", im.dialog, im.toAgentID);
                user.ControllingClient.SendInstantMessage(im);
            }
            else
            {
//                m_log.WarnFormat("[INVENTORY_OFFER]: Relaying IM {0} remotely to {1}", im.dialog, im.toAgentID);
                if (m_TransferModule == null)
                    m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

                if (m_TransferModule != null)
                    m_TransferModule.SendInstantMessage(im, delegate(bool success) { });
                else
                    m_log.ErrorFormat("[INVENTORY_OFFER]: Could not relay IM {0} remotely to {1}", im.dialog, im.toAgentID);
            }
        }

        private void RelayInventoryOfferIM(Scene scene, GridInstantMessage im)
        {
            UUID agentId = new UUID(im.toAgentID);
            ScenePresence user = FindAgent(agentId);
            RelayInventoryOfferIM(scene, user, im);
        }

//////////////////////////////////////////////// 
// User-to-user inventory offers

        private void UserInventoryOffer(IClientAPI client, Scene scene, GridInstantMessage im)
        {
            InventoryFolderBase folder = null;
            InventoryItemBase item = null;

            UUID toAgentID = new UUID(im.toAgentID);

            IMuteListModule m_muteListModule = scene.RequestModuleInterface<IMuteListModule>();
            if (m_muteListModule != null)
            {
                if (m_muteListModule.IsMuted(client.AgentId, toAgentID))
                {
                    client.SendAgentAlertMessage("Inventory offer was automatically declined.", false);
                    return; // recipient has sender muted
                }
            }

            // Unpack the binary bucket
            AssetType assetType = (AssetType)im.binaryBucket[0];
            UUID destID = new UUID(im.binaryBucket, 1);
            UUID copyID;
            bool isFolder = (assetType == AssetType.Folder);

            ScenePresence recipient = scene.GetScenePresence(toAgentID);

            if (recipient != null && recipient.IsBot)
            {
                client.SendAgentAlertMessage("Can't give inventory to bots.", false);
                return;//can't give objects to bots
            }

            if (assetType == AssetType.Folder)
            {
                folder = scene.GiveInventoryFolder(toAgentID, client.AgentId, destID, UUID.Zero);
                if (folder == null)
                {
                    client.SendAgentAlertMessage("Can't find folder to give. Nothing given.", false);
                    return;
                }
                copyID = folder.ID;
            }
            else
            {
                item = scene.GiveInventoryItem(toAgentID, client.AgentId, destID);
                if (item == null)
                {
                    client.SendAgentAlertMessage("Can't find item to give. Nothing given.", false);
                    return;
                }
                copyID = item.ID;
            }
//            m_log.InfoFormat("[AGENT INVENTORY]: Offering {0} {1} to user {2} inventory as {3}", isFolder ? "folder" : "item", destID, toAgentID, copyID);

            // Update the asset type and destination ID into the outgoing IM.
            im.binaryBucket = new byte[17];
            im.binaryBucket[0] = (byte)assetType;
            Array.Copy(copyID.GetBytes(), 0, im.binaryBucket, 1, 16);
            // Also stuff the destination ID into the session ID field for retrieval in accept/decline
            im.imSessionID = copyID.Guid;

            CachedUserInfo recipientInfo = scene.CommsManager.UserService.GetUserDetails(toAgentID);
            if (recipientInfo != null && recipient != null)
            {
                if ((!isFolder) && (item != null))
                {
                    // item offer?
                    recipient.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
                }

                if (isFolder && (folder != null))
                {
                    // folder offer?
                    folder = recipientInfo.GetFolder(folder.ID);
                    if (folder != null)
                    {
                        recipient.ControllingClient.SendBulkUpdateInventory(folder);
                        recipientInfo.SendInventoryDecendents(recipient.ControllingClient, folder.ID, false, true);
                    }
                }
            }

            // Send the IM to the recipient. The item is already in their inventory, so
            // it will not be lost if they are offline. Transaction ID is the item ID.
            // We get that same ID back on the reply so we know what to act on.
            RelayInventoryOfferIM(scene, recipient, im);
        }

        private void UserInventoryOfferAccept(IClientAPI client, Scene scene, GridInstantMessage im)
        {
            // No extra work to do here.
            RelayInventoryOfferIM(scene, im);
        }

        private void UserInventoryOfferDecline(IClientAPI client, Scene scene, GridInstantMessage im)
        {
            // No extra work to do here.
            RelayInventoryOfferIM(scene, im);
        }

////////////////////////////////////////////////
// Object-to-user inventory offers

        private void TaskInventoryOffer(IClientAPI client, Scene scene, GridInstantMessage im)
        {
            IMuteListModule m_muteListModule = scene.RequestModuleInterface<IMuteListModule>();
            if (m_muteListModule != null)
            {
                UUID sender = new UUID(im.fromAgentID);
                UUID recipient = new UUID(im.toAgentID);
                if (m_muteListModule.IsMuted(sender, recipient))
                {
                    return; // recipient has sender muted
                }
            } 
            
            RelayInventoryOfferIM(scene, im);
        }

        private void TaskInventoryOfferAccept(IClientAPI client, Scene scene, GridInstantMessage im)
        {
            InventoryFolderBase folder = null;
            InventoryItemBase item = null;

            // The inventory item/folder, back from it's trip
            UUID inventoryEntityID = new UUID(im.imSessionID);

            // Here, the recipient is local and we can assume that the inventory is loaded.
            // Courtesy of the above bulk update, it will have been pushed to the client, too.
            CachedUserInfo userInfo = scene.CommsManager.UserService.GetUserDetails(client.AgentId);
            if (userInfo != null)
            {
                // Is it a folder or an item?
                if (userInfo.QueryItem(inventoryEntityID))
                {   // It's an item.
                    item = userInfo.FindItem(inventoryEntityID);
                    if (item == null)
                    {
                        client.SendAgentAlertMessage("Unable to accept received inventory: item/folder not found.", false);
                        return;
                    }
                    client.SendInventoryItemCreateUpdate(item, 0);
                }
                else
                {   // It's a folder.
                    folder = userInfo.GetFolder(inventoryEntityID);
                    if (folder != null)
                    {
                        client.SendBulkUpdateInventory(folder);
                        // If we don't send the descendents, viewer shows "Loading..." on the trash item.
                        userInfo.SendInventoryDecendents(client, folder.ID, false, true);
                    }
                }
            }

            //            RelayInventoryOfferIM(scene, im); // we don't need to notify a box that the user accepted this
        }

        private void TaskInventoryOfferDecline(IClientAPI client, Scene scene, GridInstantMessage im)
        {
            // The inventory item/folder, back from it's trip
            UUID inventoryEntityID = new UUID(im.imSessionID);

            // Here, the recipient is local and we can assume that the inventory is loaded.
            // Courtesy of the above bulk update, it will have been pushed to the client, too.
            CachedUserInfo userInfo = scene.CommsManager.UserService.GetUserDetails(client.AgentId);
            if (userInfo != null)
            {
                InventoryFolderBase trashFolder = userInfo.FindFolderForType((int)FolderType.Trash);
                if (null == trashFolder)
                {
                    client.SendAgentAlertMessage("Unable to decline received inventory: Trash folder not found.", false);
                    return;
                }

                // Is it a folder or an item?
                if (userInfo.QueryItem(inventoryEntityID))
                {   // It's an item.
                    InventoryItemBase item = userInfo.FindItem(inventoryEntityID);
                    if (item == null)
                    {
                        client.SendAgentAlertMessage("Unable to decline received inventory: item/folder not found.", false);
                        return;
                    }
                    userInfo.MoveItemToTrash(item, trashFolder);
                    scene.AddInventoryItem(client, item);
                    client.SendInventoryItemCreateUpdate(item, 0);
                }
                else
                {   // It's a folder.
                    InventoryFolderBase folder = userInfo.GetFolderAttributes(inventoryEntityID);
                    userInfo.MoveFolder(inventoryEntityID, trashFolder.ID);
                    folder = userInfo.GetFolder(inventoryEntityID);
                    if (folder != null)
                    {
                        client.SendBulkUpdateInventory(folder);
                        // If we don't send the descendents, viewer shows "Loading..." on the trash item.
                        userInfo.SendInventoryDecendents(client, folder.ID, false, true);
                    }
                }
            }
        }

////////////////////////////////////////////////
// Incoming IM handlers

        /// I believe this is an IM from a local agent or object, rather than one from the grid somewhere.
        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            Scene scene = FindScene(client.AgentId);
            if (scene == null) // Something seriously wrong here.
                return;

            if (im.dialog == (byte)InstantMessageDialog.InventoryOffered)
            {
                UserInventoryOffer(client, scene, im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.InventoryAccepted)
            {
                UserInventoryOfferAccept(client, scene, im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.InventoryDeclined)
            {
                UserInventoryOfferDecline(client, scene, im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.TaskInventoryOffered)
            {
                TaskInventoryOffer(client, scene, im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.TaskInventoryAccepted)
            {
                TaskInventoryOfferAccept(client, scene, im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.TaskInventoryDeclined)
            {
                TaskInventoryOfferDecline(client, scene, im);
            }
        }

        private void GridInventoryOffer(GridInstantMessage im)
        {
            if (im.binaryBucket.Length < 1) // Invalid
            {
                m_log.WarnFormat("[INVENTORY TRANSFER]: Invalid IM (binary bucket length {0}).", im.binaryBucket.Length);
                return;
            }

            Scene scene = FindScene(new UUID(im.toAgentID));
            ScenePresence user = scene.GetScenePresence(new UUID(im.toAgentID));
            if (user == null) // Shouldn't happen
            {
                m_log.Warn("[INVENTORY TRANSFER]: Can't find recipient");
                return;
            }

            CachedUserInfo userInfo =
                    scene.CommsManager.UserService.
                    GetUserDetails(user.ControllingClient.AgentId);

            if (userInfo == null)
            {
                m_log.Warn("[INVENTORY TRANSFER]: Can't find user info of recipient");
                return;
            }

            AssetType assetType = (AssetType)im.binaryBucket[0];
            UUID inventoryEntityID = UUID.Zero;
            if (im.binaryBucket.Length >= 17) // includes UUID
                inventoryEntityID = new UUID(im.binaryBucket, 1);

            if (AssetType.Folder == assetType)
            {
                if (inventoryEntityID != UUID.Zero)
                {
                    // Get folder info
                    InventoryFolderBase folder = userInfo.GetFolder(inventoryEntityID);
                    if (folder == null)
                    {
                        m_log.WarnFormat("[INVENTORY TRANSFER]: Can't retrieve folder {0} to give.", inventoryEntityID);
                        return;
                    }

                    // Update folder to viewer (makes it appear to user)
                    user.ControllingClient.SendBulkUpdateInventory(folder);
                    userInfo.SendInventoryDecendents(user.ControllingClient, folder.ID, false, true);
                }

                // Deliver message
                user.ControllingClient.SendInstantMessage(im);
            }
            else
            {
                if (inventoryEntityID != UUID.Zero)
                {
                    // Get item info
                    InventoryItemBase item = userInfo.FindItem(inventoryEntityID);
                    if (item == null)
                    {
                        m_log.WarnFormat("[INVENTORY TRANSFER]: Can't retrieve item {0} to give.", inventoryEntityID);
                        return;
                    }

                    // Update item to viewer (makes it appear in proper folder)
                    user.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
                }

                // Deliver message
                user.ControllingClient.SendInstantMessage(im);
            }
        }

        // When we arrive here from a grid message, all the hard lifting has already been done.
        // All that is left is to notify the result to the sender of the offer.
        private void GridInventoryOfferAcceptDecline(GridInstantMessage im)
        {
            UUID agentId = new UUID(im.toAgentID);
            Scene scene = FindScene(agentId);
            if (scene == null)
                return; // recipient is not here
            ScenePresence user = scene.GetScenePresence(agentId);
            RelayInventoryOfferIM(scene, user, im);
        }

        /// <summary>
        /// This is an IM from the grid, as opposed to an IM from a local agent or object.
        /// </summary>
        /// <param name="msg"></param>
        private void OnGridInstantMessage(GridInstantMessage im)
        {
            if (im.dialog == (byte)InstantMessageDialog.InventoryOffered)
            {
                GridInventoryOffer(im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.InventoryAccepted)
            {
                //                m_log.Warn("[INVENTORY TRANSFER]: InventoryAccepted");
                GridInventoryOfferAcceptDecline(im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.InventoryDeclined)
            {
                //                m_log.Warn("[INVENTORY TRANSFER]: InventoryDeclined");
                GridInventoryOfferAcceptDecline(im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.TaskInventoryOffered)
            {
                //                m_log.Info("[INVENTORY TRANSFER]: TaskInventoryOffered");
                GridInventoryOffer(im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.TaskInventoryAccepted)
            {
                //                m_log.Info("[INVENTORY TRANSFER]: TaskInventoryAccepted");
                GridInventoryOfferAcceptDecline(im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.TaskInventoryDeclined)
            {
                //                m_log.Info("[INVENTORY TRANSFER]: TaskInventoryDeclined");
                GridInventoryOfferAcceptDecline(im);
            }
        }

////////////////////////////////////////////////

        public void SetRootAgentScene(UUID agentID, Scene scene)
        {
            lock (m_AgentRegions)
            {
                m_AgentRegions[agentID] = scene;
            }
        }

        public bool NeedSceneCacheClear(UUID agentID, Scene scene)
        {
            bool haveAgent;

            lock (m_AgentRegions)
            {
                haveAgent = m_AgentRegions.ContainsKey(agentID);
            }

            if (!haveAgent)
            {
                // Since we can get here two ways, we need to scan
                // the scenes here. This is somewhat more expensive
                // but helps avoid a nasty bug
                //

                foreach (Scene s in m_Scenelist)
                {
                    ScenePresence presence;

                    if (s.TryGetAvatar(agentID, out presence))
                    {
                        // If the agent is in this scene, then we
                        // are being called twice in a single
                        // teleport. This is wasteful of cycles
                        // but harmless due to this 2nd level check
                        //
                        // If the agent is found in another scene
                        // then the list wasn't current
                        //
                        // If the agent is totally unknown, then what
                        // are we even doing here??
                        //
                        if (s == scene)
                        {
                            //m_log.Debug("[INVTRANSFERMOD]: s == scene. Returning true in " + scene.RegionInfo.RegionName);
                            return true;
                        }
                        else
                        {
                            //m_log.Debug("[INVTRANSFERMOD]: s != scene. Returning false in " + scene.RegionInfo.RegionName);
                            return false;
                        }
                    }
                }
                //m_log.Debug("[INVTRANSFERMOD]: agent not in scene. Returning true in " + scene.RegionInfo.RegionName);
                return true;
            }

            // The agent is left in current Scene, so we must be
            // going to another instance
            //
            lock (m_AgentRegions)
            {
                if (m_AgentRegions[agentID] == scene)
                {
                    //m_log.Debug("[INVTRANSFERMOD]: m_AgentRegions[agentID] == scene. Returning true in " + scene.RegionInfo.RegionName);
                    m_AgentRegions.Remove(agentID);
                    return true;
                }
            }

            // Another region has claimed the agent
            //
            //m_log.Debug("[INVTRANSFERMOD]: last resort. Returning false in " + scene.RegionInfo.RegionName);
            return false;
        }

        public void ClientLoggedOut(UUID agentID, Scene scene)
        {
            lock (m_AgentRegions)
            {
                if (m_AgentRegions.ContainsKey(agentID))
                    m_AgentRegions.Remove(agentID);
            }
        }
    }
}
