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
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.AssetTransaction
{
    /// <summary>
    /// Manage asset transactions for a single agent.
    /// </summary>
    public class AgentAssetTransactions
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Fields
        private bool m_dumpAssetsToFile;
        public AssetTransactionModule Manager;
        public UUID UserID;
        public Dictionary<UUID, AssetXferUploader> XferUploaders = new Dictionary<UUID, AssetXferUploader>();

        // Methods
        public AgentAssetTransactions(UUID agentID, AssetTransactionModule manager, bool dumpAssetsToFile)
        {
            UserID = agentID;
            Manager = manager;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        private void CleanupCallback(object state)
        {
            lock (XferUploaders)
            {
                List<UUID> expiredUploaders = new List<UUID>();

                foreach (KeyValuePair<UUID,AssetXferUploader> kvp in XferUploaders)
                {
                    AssetXferUploader uploader = kvp.Value;
                    if ((DateTime.Now - uploader.LastAccess).TotalMinutes > 10)
                    {
                        UUID transactionID = kvp.Key;
                        expiredUploaders.Add(transactionID);
                        m_log.WarnFormat("[ASSET TRANSACTIONS]: Timeout on asset upload for transaction {0}", transactionID);
                    }
                }

                foreach (UUID transactionID in expiredUploaders)
                {
                    XferUploaders.Remove(transactionID);
                }
            }

        }

        static Timer cleanupTimer = null;
        public AssetXferUploader RequestXferUploader(UUID transactionID)
        {
            lock (XferUploaders)
            {
                if (!XferUploaders.ContainsKey(transactionID))
                {
                    AssetXferUploader uploader = new AssetXferUploader(this, m_dumpAssetsToFile);
                    if (uploader != null)
                    {
                        XferUploaders.Add(transactionID, uploader);
                        if (cleanupTimer == null)
                            cleanupTimer = new Timer(CleanupCallback, null, 0, 60000); // once per minute
                    }
                    return uploader;
                }
                return null;
            }
        }

        public void HandleXfer(ulong xferID, uint packetID, byte[] data)
        {
            lock (XferUploaders)
            {
                foreach (AssetXferUploader uploader in XferUploaders.Values)
                {
                    if (uploader.XferID == xferID)
                    {
                        if (uploader.HandleXferPacket(xferID, packetID, data))
                        {
                            // returns true when the xfer is complete
                            // signal the item update to check for completion
                            Monitor.PulseAll(XferUploaders);
                        }
                        break;
                    }
                }
            }
        }

        public void RequestCreateInventoryItem(IClientAPI remoteClient, UUID transactionID, UUID folderID,
                                               uint callbackID, string description, string name, sbyte invType,
                                               sbyte type, byte wearableType, uint nextOwnerMask)
        {
            AssetXferUploader uploader = null;

            lock (XferUploaders)
            {
                XferUploaders.TryGetValue(transactionID, out uploader);
            }

            if (uploader != null)
            {
                uploader.RequestCreateInventoryItem(remoteClient, transactionID, folderID,
                                                                        callbackID, description, name, invType, type,
                                                                        wearableType, nextOwnerMask);
            }
        }

        private AssetXferUploader GetTransactionUploader(UUID transactionID)
        {
            lock (XferUploaders)
            {
                AssetXferUploader value = null;
                if (XferUploaders.TryGetValue(transactionID, out value))
                    return value;
            }
            return null;
        }

        public bool RequestUpdateTaskInventoryItem(
           IClientAPI remoteClient, SceneObjectPart part, UUID transactionID, TaskInventoryItem item)
        {
            AssetXferUploader uploader = GetTransactionUploader(transactionID);
            if (uploader == null)
            {
                m_log.WarnFormat("[ASSET TRANSACTIONS]: Transaction {0} NOT FOUND (duplicate removed?) for inventory item update {1}", transactionID, item.Name);
                return false;
            }

            // This may complete now if upload complete, or later when the upload completes.
            uploader.TriggerWhenUploadComplete(delegate(AssetBase asset)
            {
                // This upload transaction is complete.
                XferUploaders.Remove(transactionID);

                if (asset == null)
                    return;         // UpdateItem() not called

                m_log.DebugFormat(
                    "[ASSET TRANSACTIONS]: Updating task item {0} in {1} with asset in transaction {2}",
                    item.Name, part.Name, transactionID);

                asset.Name = item.Name;
                asset.Description = item.Description;
                asset.Type = (sbyte)item.Type;

                try
                {
                    Manager.MyScene.CommsManager.AssetCache.AddAsset(asset, AssetRequestInfo.GenericNetRequest());
                    if (part.Inventory.UpdateTaskInventoryItemAsset(part.UUID, item.ItemID, asset.FullID))
                    {
                        part.GetProperties(remoteClient);
                    }
                }
                catch (AssetServerException e)
                {
                    remoteClient.SendAgentAlertMessage("Unable to upload asset. Please try again later.", false);

                    m_log.ErrorFormat("[ASSET TRANSACTIONS] Unable to update task item due to asset server error {0}", e);
                }
            });

            // We at least found an uploader with that transaction ID.
            return true;
        }

        public bool RequestUpdateInventoryItem(IClientAPI remoteClient, UUID transactionID, InventoryItemBase item)
        {
            AssetXferUploader uploader = GetTransactionUploader(transactionID);
            if (uploader == null)
            {
                m_log.WarnFormat("[ASSET TRANSACTIONS]: Transaction {0} NOT FOUND (duplicate removed?) for inventory item update {1}", transactionID, item.Name);
                return false;
            }

            CachedUserInfo userInfo = Manager.MyScene.CommsManager.UserService.GetUserDetails(remoteClient.AgentId);
            if (userInfo == null)
            {
                m_log.WarnFormat("[ASSET TRANSACTIONS]: Could not find user {0} for transaction {1} for inventory update {2}",
                                                                remoteClient.AgentId, transactionID, item.Name);
                return false;
            }

            // This may complete now if upload complete, or later when the upload completes.
            uploader.TriggerWhenUploadComplete(delegate(AssetBase asset)
            {
                // This upload transaction is complete.
                XferUploaders.Remove(transactionID);

                UUID assetID = UUID.Combine(transactionID, remoteClient.SecureSessionId);
                if (asset == null || asset.FullID != assetID)
                {
                    m_log.ErrorFormat("[ASSETS]: RequestUpdateInventoryItem wrong asset ID or not found {0}", asset == null ? "null" : asset.FullID.ToString());
                    return;
                }

                // Assets never get updated, new ones get created
                UUID oldID = asset.FullID;
                asset.FullID = UUID.Random();
                asset.Name = item.Name;
                asset.Description = item.Description;
                asset.Type = (sbyte)item.AssetType;

                try
                {
                    m_log.DebugFormat("[ASSETS]: RequestUpdateInventoryItem for transaction {0}, new asset {1} -> {2}", transactionID, oldID, asset.FullID);
                    Manager.MyScene.CommsManager.AssetCache.AddAsset(asset, AssetRequestInfo.GenericNetRequest());
                }
                catch (AssetServerException e)
                {
                    remoteClient.SendAgentAlertMessage("Unable to upload asset. Please try again later.", false);
                    m_log.ErrorFormat("[ASSET TRANSACTIONS] Creation of asset failed {0}", e);
                    return;
                }

                item.AssetID = asset.FullID;

                //wait for completion of the write to avoid reversion
                ManualResetEventSlim waitEvent = new ManualResetEventSlim();

                remoteClient.HandleWithInventoryWriteThread(() =>
                    {
                        // Update the asset ID
                        userInfo.UpdateItem(item);
                        waitEvent.Set();
                    });

                waitEvent.Wait();
                waitEvent.Dispose();
            });

            return true;    // userInfo item was updated
        }

    }
}
