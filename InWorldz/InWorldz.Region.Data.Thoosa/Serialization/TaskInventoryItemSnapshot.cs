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
    /// A snapshot in time of an item contained in a prim's inventory
    /// </summary>
    [ProtoContract]
    public class TaskInventoryItemSnapshot
    {
        [ProtoMember(1)]
        public Guid ItemId;

        [ProtoMember(2)]
        public Guid AssetId;

        [ProtoMember(3)]
        public string Name;

        [ProtoMember(4)]
        public uint BasePermissions;

        [ProtoMember(5)]
        public uint GroupPermissions;

        [ProtoMember(6)]
        public uint EveryonePermissions;

        [ProtoMember(7)]
        public uint NextOwnerPermissions;

        [ProtoMember(8)]
        public uint CurrentPermissions;

        [ProtoMember(9)]
        public uint CreationDate;

        [ProtoMember(10)]
        public Guid CreatorId;

        [ProtoMember(11)]
        public string Description;

        [ProtoMember(12)]
        public uint Flags;

        [ProtoMember(13)]
        public Guid GroupId;

        [ProtoMember(14)]
        public int InvType;

        [ProtoMember(15)]
        public Guid OldItemId;

        [ProtoMember(16)]
        public Guid LastOwnerId;

        [ProtoMember(17)]
        public Guid OwnerId;

        [ProtoMember(18)]
        public Guid ParentId;

        [ProtoMember(19)]
        public Guid ParentPartId;

        [ProtoMember(20)]
        public Guid PermsGranter;

        [ProtoMember(21)]
        public int PermsMask;

        [ProtoMember(22)]
        public int Type;

        internal OpenSim.Framework.TaskInventoryItem ToTaskInventoryItem()
        {
            return new OpenSim.Framework.TaskInventoryItem
            {
                AssetID = new OpenMetaverse.UUID(this.AssetId),
                BasePermissions = this.BasePermissions,
                CreationDate = this.CreationDate,
                CreatorID = new OpenMetaverse.UUID(this.CreatorId),
                CurrentPermissions = this.CurrentPermissions,
                Description = this.Description,
                EveryonePermissions = this.EveryonePermissions,
                Flags = this.Flags,
                GroupID = new OpenMetaverse.UUID(this.GroupId),
                GroupPermissions = this.GroupPermissions,
                InvType = this.InvType,
                ItemID = new OpenMetaverse.UUID(this.ItemId),
                LastOwnerID = new OpenMetaverse.UUID(this.LastOwnerId),
                Name = this.Name,
                NextPermissions = this.NextOwnerPermissions,
                OldItemID = new OpenMetaverse.UUID(this.OldItemId),
                OwnerID = new OpenMetaverse.UUID(this.OwnerId),
                ParentID = new OpenMetaverse.UUID(this.ParentId),
                ParentPartID = new OpenMetaverse.UUID(this.ParentPartId),
                PermsGranter = new OpenMetaverse.UUID(this.PermsGranter),
                PermsMask = this.PermsMask,
                Type = this.Type
            };
        }
    }
}
