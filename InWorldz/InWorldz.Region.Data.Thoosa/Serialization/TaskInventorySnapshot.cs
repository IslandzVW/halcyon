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
using System.Linq;
using System.Text;
using ProtoBuf;

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    /// <summary>
    /// A protobuf serializable task inventory
    /// </summary>
    [ProtoContract]
    public class TaskInventorySnapshot
    {
        [ProtoMember(1)]
        public uint Serial;

        [ProtoMember(2)]
        public TaskInventoryItemSnapshot[] Items;


        internal static TaskInventorySnapshot FromTaskInventory(OpenSim.Region.Framework.Scenes.SceneObjectPart part)
        {
            TaskInventorySnapshot snapshot = new TaskInventorySnapshot
            {
                Serial = part.InventorySerial,
                Items = ExtractInventoryItems(part)
            };

            return snapshot; 
        }

        private static TaskInventoryItemSnapshot[] ExtractInventoryItems(OpenSim.Region.Framework.Scenes.SceneObjectPart part)
        {
            var inventory = part.Inventory.CollectInventoryForBackup();

            List<TaskInventoryItemSnapshot> snapItems = new List<TaskInventoryItemSnapshot>();

            foreach (var item in inventory.Value)
            {
                snapItems.Add(new TaskInventoryItemSnapshot
                {
                    AssetId = item.AssetID.Guid,
                    BasePermissions = item.BasePermissions,
                    CreationDate = item.CreationDate,
                    CreatorId = item.CreatorID.Guid,
                    CurrentPermissions = item.CurrentPermissions,
                    Description = item.Description,
                    EveryonePermissions = item.EveryonePermissions,
                    Flags = item.Flags,
                    GroupId = item.GroupID.Guid,
                    GroupPermissions = item.GroupPermissions,
                    InvType = item.InvType,
                    ItemId = item.ItemID.Guid,
                    LastOwnerId = item.LastOwnerID.Guid,
                    Name = item.Name,
                    NextOwnerPermissions = item.NextPermissions,
                    OldItemId = item.OldItemID.Guid,
                    OwnerId = item.OwnerID.Guid,
                    ParentId = item.ParentID.Guid,
                    ParentPartId = item.ParentPartID.Guid,
                    PermsGranter = item.PermsGranter.Guid,
                    PermsMask = item.PermsMask,
                    Type = item.Type
                });
            }

            return snapItems.ToArray();
        }

        internal OpenSim.Framework.TaskInventoryDictionary ToTaskInventory()
        {
            OpenSim.Framework.TaskInventoryDictionary dict = new OpenSim.Framework.TaskInventoryDictionary();

            if (Items != null)
            {
                foreach (var item in Items)
                {
                    dict.Add(new OpenMetaverse.UUID(item.ItemId), item.ToTaskInventoryItem());
                }
            }

            return dict;
        }
    }
}
