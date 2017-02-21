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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    /// <summary>
    /// A snapshot in time of the current state of a SceneObjectPart that is 
    /// also protobuf serializable
    /// </summary>
    [ProtoContract]
    public class SceneObjectPartSnapshot
    {
        [ProtoMember(1)]
        public Guid Id;

        [ProtoMember(2)]
        public string Name;

        [ProtoMember(3)]
        public string Description;

        [ProtoMember(4)]
        public int[] PayPrice;

        [ProtoMember(5)]
        public Guid Sound;

        [ProtoMember(6)]
        public byte SoundFlags;

        [ProtoMember(7)]
        public float SoundGain;

        [ProtoMember(8)]
        public float SoundRadius;

        [ProtoMember(9)]
        public byte[] SerializedPhysicsData;

        [ProtoMember(10)]
        public Guid CreatorId;

        [ProtoMember(11)]
        public TaskInventorySnapshot Inventory;

        [ProtoMember(12)]
        public OpenMetaverse.PrimFlags ObjectFlags;

        [ProtoMember(13)]
        public uint LocalId;

        [ProtoMember(14)]
        public OpenMetaverse.Material Material;

        [ProtoMember(15)]
        public bool PassTouches;

        [ProtoMember(16)]
        public ulong RegionHandle;

        [ProtoMember(17)]
        public int ScriptAccessPin;

        [ProtoMember(18)]
        public byte[] TextureAnimation;

        [ProtoMember(19)]
        public OpenMetaverse.Vector3 GroupPosition;

        [ProtoMember(20)]
        public OpenMetaverse.Vector3 OffsetPosition;

        [ProtoMember(21)]
        public OpenMetaverse.Quaternion RotationOffset;

        [ProtoMember(22)]
        public OpenMetaverse.Vector3 Velocity;

        /// <summary>
        /// This maps to the old angular velocity property on the prim which
        /// now is used only to store omegas
        /// </summary>
        [ProtoMember(23)]
        public OpenMetaverse.Vector3 AngularVelocityTarget;

        /// <summary>
        /// This is the new physical angular velocity
        /// </summary>
        [ProtoMember(24)]
        public OpenMetaverse.Vector3 AngularVelocity;


        public System.Drawing.Color TextColor {get; set;}

        [ProtoMember(25, DataFormat=DataFormat.FixedSize)]
        private int SerializedTextColor
        {
            get { return TextColor.ToArgb(); }
            set { TextColor = System.Drawing.Color.FromArgb(value); }
        }


        [ProtoMember(26)]
        public string HoverText;

        [ProtoMember(27)]
        public string SitName;

        [ProtoMember(28)]
        public string TouchName;

        [ProtoMember(29)]
        public int LinkNumber;

        [ProtoMember(30)]
        public byte ClickAction;

        [ProtoMember(31)]
        public PrimShapeSnapshot Shape;

        [ProtoMember(32)]
        public OpenMetaverse.Vector3 Scale;

        [ProtoMember(33)]
        public OpenMetaverse.Quaternion SitTargetOrientation;

        [ProtoMember(34)]
        public OpenMetaverse.Vector3 SitTargetPosition;

        [ProtoMember(35)]
        public uint ParentId;

        [ProtoMember(36)]
        public int CreationDate;

        [ProtoMember(37)]
        public uint Category;

        [ProtoMember(38)]
        public int SalePrice;

        [ProtoMember(39)]
        public byte ObjectSaleType;

        [ProtoMember(40)]
        public int OwnershipCost;

        [ProtoMember(41)]
        public Guid GroupId;

        [ProtoMember(42)]
        public Guid OwnerId;

        [ProtoMember(43)]
        public Guid LastOwnerId;

        [ProtoMember(44)]
        public uint BaseMask;

        [ProtoMember(45)]
        public uint OwnerMask;

        [ProtoMember(46)]
        public uint GroupMask;

        [ProtoMember(47)]
        public uint EveryoneMask;

        [ProtoMember(48)]
        public uint NextOwnerMask;

        [ProtoMember(49)]
        public byte SavedAttachmentPoint;

        [ProtoMember(50)]
        public OpenMetaverse.Vector3 SavedAttachmentPos;

        [ProtoMember(51)]
        public OpenMetaverse.Quaternion SavedAttachmentRot;

        [ProtoMember(52)]
        public OpenMetaverse.PrimFlags Flags;

        [ProtoMember(53)]
        public Guid CollisionSound;

        [ProtoMember(54)]
        public float CollisionSoundVolume;

        /// <summary>
        /// Script states by [Item Id, Data]
        /// </summary>
        [ProtoMember(55)]
        public Dictionary<Guid, byte[]> SerializedScriptStates;

        [ProtoMember(56)]
        public string MediaUrl;

        [ProtoMember(57)]
        public byte[] ParticleSystem;

        /// <summary>
        /// Contains a collection of serialized script bytecode that go with this prim/part
        /// May be null if these are not required
        /// </summary>
        [ProtoMember(58)]
        public Dictionary<Guid, byte[]> SeralizedScriptBytecode;

        /// <summary>
        /// Contains a collection of physx serialized convex meshes that go with this prim/part
        /// May be null if these are not requested
        /// </summary>
        [ProtoMember(59)]
        public byte[] SerializedPhysicsShapes;

        /// <summary>
        /// Stores the item ID that is associated with a worn attachment
        /// </summary>
        [ProtoMember(60)]
        public Guid FromItemId;

        [ProtoMember(61)]
        public float ServerWeight;

        [ProtoMember(62)]
        public float StreamingCost;

        [ProtoMember(63)]
        public KeyframeAnimationSnapshot KeyframeAnimation;

        [ProtoMember(64)]
        public ServerPrimFlags ServerFlags;

        static SceneObjectPartSnapshot()
        {
            ProtoBuf.Serializer.PrepareSerializer<SceneObjectPartSnapshot>();
        }

        static public SceneObjectPartSnapshot FromSceneObjectPart(SceneObjectPart part, SerializationFlags flags)
        {
            bool serializePhysicsShapes = (flags & SerializationFlags.SerializePhysicsShapes) != 0;
            bool serializeScriptBytecode = (flags & SerializationFlags.SerializeScriptBytecode) != 0;

            StopScriptReason stopScriptReason;
            if ((flags & SerializationFlags.StopScripts) == 0)
            {
                stopScriptReason = StopScriptReason.None;
            }
            else
            {
                if ((flags & SerializationFlags.FromCrossing) != 0)
                    stopScriptReason = StopScriptReason.Crossing;
                else
                    stopScriptReason = StopScriptReason.Derez;
            }

            SitTargetInfo sitInfo = part.ParentGroup.SitTargetForPart(part.UUID);
            SceneObjectPartSnapshot partSnap = new SceneObjectPartSnapshot
            {
                AngularVelocity = part.PhysicalAngularVelocity,
                AngularVelocityTarget = part.AngularVelocity,
                BaseMask = part.BaseMask,
                Category = part.Category,
                ClickAction = part.ClickAction,
                CollisionSound = part.CollisionSound.Guid,
                CollisionSoundVolume = part.CollisionSoundVolume,
                CreationDate = part.CreationDate,
                CreatorId = part.CreatorID.Guid,
                Description = part.Description,
                EveryoneMask = part.EveryoneMask,
                Flags = part.Flags,
                GroupId = part.GroupID.Guid,
                GroupMask = part.GroupMask,

                //if this is an attachment, dont fill out the group position. This prevents an issue where
                //a user is crossing to a new region and the vehicle has already been sent. Since the attachment's
                //group position is actually the wearer's position and the wearer's position is the vehicle position,
                //trying to get the attachment grp pos triggers an error and a ton of log spam.
                GroupPosition = part.ParentGroup.IsAttachment ? OpenMetaverse.Vector3.Zero : part.GroupPosition, 

                HoverText = part.Text,
                Id = part.UUID.Guid,
                Inventory = TaskInventorySnapshot.FromTaskInventory(part),
                KeyframeAnimation = KeyframeAnimationSnapshot.FromKeyframeAnimation(part.KeyframeAnimation),
                LastOwnerId = part.LastOwnerID.Guid,
                LinkNumber = part.LinkNum,
                LocalId = part.LocalId,
                Material = (OpenMetaverse.Material)part.Material,
                MediaUrl = part.MediaUrl,
                Name = part.Name,
                NextOwnerMask = part.NextOwnerMask,
                ObjectFlags = (OpenMetaverse.PrimFlags)part.ObjectFlags,
                ObjectSaleType = part.ObjectSaleType,
                OffsetPosition = part.OffsetPosition,
                OwnerId = part.OwnerID.Guid,
                OwnerMask = part.OwnerMask,
                OwnershipCost = part.OwnershipCost,
                ParentId = part.ParentID,
                ParticleSystem = part.ParticleSystem,
                PassTouches = part.PassTouches,
                PayPrice = part.PayPrice,
                RegionHandle = part.RegionHandle,
                RotationOffset = part.RotationOffset,
                SalePrice = part.SalePrice,
                SavedAttachmentPoint = part.SavedAttachmentPoint,
                SavedAttachmentPos = part.SavedAttachmentPos,
                SavedAttachmentRot = part.SavedAttachmentRot,
                Scale = part.Scale,
                ScriptAccessPin = part.ScriptAccessPin,
                SerializedPhysicsData = part.SerializedPhysicsData,
                ServerWeight = part.ServerWeight,
                ServerFlags = (ServerPrimFlags)part.ServerFlags,
                Shape = PrimShapeSnapshot.FromShape(part.Shape),
                SitName = part.SitName,
                SitTargetOrientation = sitInfo.Rotation,
                SitTargetPosition = sitInfo.Offset,
                Sound = part.Sound.Guid,
                SoundFlags = part.SoundOptions,
                SoundGain = part.SoundGain,
                SoundRadius = part.SoundRadius,
                StreamingCost = part.StreamingCost,
                TextColor = part.TextColor,
                TextureAnimation = part.TextureAnimation,
                TouchName = part.TouchName,
                Velocity = part.Velocity,
                FromItemId = part.FromItemID.Guid
            };

            Dictionary<OpenMetaverse.UUID, byte[]> states;
            Dictionary<OpenMetaverse.UUID, byte[]> byteCode;
            if (serializeScriptBytecode)
            {
                Tuple<Dictionary<OpenMetaverse.UUID, byte[]>, Dictionary<OpenMetaverse.UUID, byte[]>>
                    statesAndBytecode = part.Inventory.GetBinaryScriptStatesAndCompiledScripts(stopScriptReason);

                states = statesAndBytecode.Item1;
                byteCode = statesAndBytecode.Item2;
            }
            else
            {
                states = part.Inventory.GetBinaryScriptStates(stopScriptReason);
                byteCode = null;
            }

            
            partSnap.SerializedScriptStates = new Dictionary<Guid, byte[]>(states.Count);
            foreach (var kvp in states)
            {
                //map from UUID to Guid
                partSnap.SerializedScriptStates[kvp.Key.Guid] = kvp.Value;
            }

            if (byteCode != null)
            {
                partSnap.SeralizedScriptBytecode = new Dictionary<Guid, byte[]>();

                foreach (var kvp in byteCode)
                {
                    //map from UUID to Guid
                    partSnap.SeralizedScriptBytecode[kvp.Key.Guid] = kvp.Value;
                }
            }

            if (serializePhysicsShapes)
            {
                partSnap.SerializedPhysicsShapes = part.SerializedPhysicsShapes;
            }

            return partSnap;
        }

        internal SceneObjectPart ToSceneObjectPart()
        {
            SceneObjectPart sop = new SceneObjectPart
            {
                AngularVelocity = this.AngularVelocityTarget,
                PhysicalAngularVelocity = this.AngularVelocity,
                BaseMask = this.BaseMask,
                Category = this.Category,
                ClickAction = this.ClickAction,
                CollisionSound = new OpenMetaverse.UUID(this.CollisionSound),
                CollisionSoundVolume = this.CollisionSoundVolume,
                CreationDate = this.CreationDate,
                CreatorID = new OpenMetaverse.UUID(this.CreatorId),
                Description = this.Description,
                EveryoneMask = this.EveryoneMask,
                Flags = this.Flags,
                GroupID = new OpenMetaverse.UUID(this.GroupId),
                GroupMask = this.GroupMask,
                GroupPosition = this.GroupPosition,
                Text = this.HoverText,
                UUID = new OpenMetaverse.UUID(this.Id),
                TaskInventory = this.Inventory.ToTaskInventory(),
                LastOwnerID = new OpenMetaverse.UUID(this.LastOwnerId),
                LinkNum = this.LinkNumber,
                LocalId = this.LocalId,
                Material = (byte)this.Material,
                MediaUrl = this.MediaUrl,
                Name = this.Name,
                NextOwnerMask = this.NextOwnerMask,
                ObjectFlags = (uint)this.ObjectFlags,
                ObjectSaleType = this.ObjectSaleType,
                OffsetPosition = this.OffsetPosition,
                OwnerID = new OpenMetaverse.UUID(this.OwnerId),
                OwnerMask = this.OwnerMask,
                OwnershipCost = this.OwnershipCost,
                ParentID = this.ParentId,
                ParticleSystem = this.ParticleSystem,
                PassTouches = this.PassTouches,
                PayPrice = this.PayPrice,
                RegionHandle = this.RegionHandle,
                RotationOffset = this.RotationOffset,
                SalePrice = this.SalePrice,
                SavedAttachmentPoint = this.SavedAttachmentPoint,
                SavedAttachmentPos = this.SavedAttachmentPos,
                SavedAttachmentRot = this.SavedAttachmentRot,
                Scale = this.Scale,
                ScriptAccessPin = this.ScriptAccessPin,
                SerializedPhysicsData = this.SerializedPhysicsData,
                ServerFlags = (uint)this.ServerFlags,
                ServerWeight = this.ServerWeight,
                Shape = this.Shape.ToPrimitiveBaseShape(),
                SitName = this.SitName,
                SitTargetOrientation = this.SitTargetOrientation,
                SitTargetPosition = this.SitTargetPosition,
                Sound = new OpenMetaverse.UUID(this.Sound),
                SoundOptions = this.SoundFlags,
                SoundGain = this.SoundGain,
                SoundRadius = this.SoundRadius,
                StreamingCost = this.StreamingCost,
                TextColor = this.TextColor,
                TextureAnimation = this.TextureAnimation,
                TouchName = this.TouchName,
                Velocity = this.Velocity,
                SerializedPhysicsShapes = this.SerializedPhysicsShapes,
                FromItemID = new OpenMetaverse.UUID(this.FromItemId),
                KeyframeAnimation = this.KeyframeAnimation == null ? null : this.KeyframeAnimation.ToKeyframeAnimation()
            };

            // Do legacy to current update for sop.ServerFlags.
            sop.PrepSitTargetFromStorage(sop.SitTargetPosition, sop.SitTargetOrientation);

            if (SerializedScriptStates != null)
            {
                var states = new Dictionary<OpenMetaverse.UUID, byte[]>(SerializedScriptStates.Count);
                foreach (var kvp in SerializedScriptStates)
                {
                    //map from Guid to UUID
                    states.Add(new OpenMetaverse.UUID(kvp.Key), kvp.Value);
                }

                sop.SetSavedScriptStates(states);
            }

            if (SeralizedScriptBytecode != null)
            {
                var byteCode = new Dictionary<OpenMetaverse.UUID, byte[]>(SeralizedScriptBytecode.Count);
                foreach (var kvp in SeralizedScriptBytecode)
                {
                    //map from Guid to UUID
                    byteCode.Add(new OpenMetaverse.UUID(kvp.Key), kvp.Value);
                }

                sop.SerializedScriptByteCode = byteCode;
            }

            return sop;
        }
    }
}
