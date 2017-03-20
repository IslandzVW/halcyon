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
using OpenMetaverse;

namespace OpenSim.Framework.Communications.Messages
{
    /// <summary>
    /// Message that corresponds to a PUT on /agent2/. This indicates a user is crossing over
    /// or teleporting from another region and carries all the data we may need to authenticate
    /// and service them
    /// </summary>
    [ProtoContract]
    public class AgentPutMessage
    {
        [ProtoMember(1)]
        public Guid AgentId;

        [ProtoMember(2)]
        public ulong RegionHandle;

        [ProtoMember(3)]
        public uint CircuitCode;

        [ProtoMember(4)]
        public Guid SessionId;

        [ProtoMember(5)]
        public Vector3 Position;

        [ProtoMember(6)]
        public Vector3 Velocity;

        [ProtoMember(7)]
        public Vector3 Center;

        [ProtoMember(8)]
        public Vector3 Size;

        [ProtoMember(9)]
        public Vector3 AtAxis;

        [ProtoMember(10)]
        public Vector3 LeftAxis;

        [ProtoMember(11)]
        public Vector3 UpAxis;


        [ProtoMember(12)]
        public float Far;
        
        [ProtoMember(13)]
        public float Aspect;
        
        [ProtoMember(14)]
        public byte[] Throttles;


        [ProtoMember(15)]
        public uint LocomotionState;

        [ProtoMember(16)]
        public Quaternion HeadRotation;

        [ProtoMember(17)]
        public Quaternion BodyRotation;

        [ProtoMember(18)]
        public uint ControlFlags;

        [ProtoMember(19)]
        public float EnergyLevel;

        [ProtoMember(20)]
        public Byte GodLevel;

        [ProtoMember(21)]
        public bool AlwaysRun;

        [ProtoMember(22)]
        public Guid PreyAgent;

        [ProtoMember(23)]
        public byte AgentAccess;


        [ProtoMember(24)]
        public Guid ActiveGroupID;

        [ProtoMember(25)]
        public PackedGroupMembership[] Groups;


        [ProtoMember(26)]
        public PackedAnimation[] Anims;


        [ProtoMember(27)]
        public string CallbackURI;


        [ProtoMember(28)]
        public Guid SatOnGroup;

        [ProtoMember(29)]
        public Guid SatOnPrim;

        [ProtoMember(30)]
        public Vector3 SatOnPrimOffset;

        [ProtoMember(31)]
        public PackedAppearance Appearance;

        [ProtoMember(32)]
        public List<byte[]> SerializedAttachments;

        [ProtoMember(33)]
        public int LocomotionFlags;

        [ProtoMember(34)]
        public List<RemotePresenceInfo> RemoteAgents;

        [ProtoMember(35)]
        public OpenMetaverse.Vector3 ConstantForces;

        [ProtoMember(36)]
        public bool ConstantForcesAreLocal;

        [ProtoMember(37)]
        public ulong PresenceFlags;

        [ProtoMember(38)]
        public bool AvatarAsAPrim;

        [ProtoMember(39)]
        public PackedAgentPrefs AgentPrefs;

        static AgentPutMessage()
        {
            ProtoBuf.Serializer.PrepareSerializer<AgentPutMessage>();
        }

        internal static AgentPutMessage FromAgentData(AgentData data)
        {
            AgentPutMessage message = new AgentPutMessage
            {
                ActiveGroupID = data.ActiveGroupID.Guid,
                AgentAccess = data.AgentAccess,
                AgentId = data.AgentID.Guid,
                AgentPrefs = PackedAgentPrefs.FromAgentPrefs(data.AgentPrefs),
                AlwaysRun = data.AlwaysRun,
                Anims = PackedAnimation.FromAnimations(data.Anims),
                Appearance = PackedAppearance.FromAppearance(data.Appearance),
                Aspect = data.Aspect,
                AtAxis = data.AtAxis,
                BodyRotation = data.BodyRotation,
                CallbackURI = data.CallbackURI,
                Center = data.Center,
                CircuitCode = data.CircuitCode,
                ControlFlags = data.ControlFlags, 
                EnergyLevel = data.EnergyLevel,
                Far = data.Far,
                GodLevel = data.GodLevel,
                Groups = PackedGroupMembership.FromGroups(data.Groups),
                HeadRotation = data.HeadRotation,
                LeftAxis = data.LeftAxis,
                LocomotionState = data.LocomotionState,
                LocomotionFlags = (int)data.LocomotionFlags,
                Position = data.Position,
                PreyAgent = data.PreyAgent.Guid,
                RegionHandle = data.RegionHandle,
                SatOnGroup = data.SatOnGroup.Guid,
                SatOnPrim = data.SatOnPrim.Guid,
                SatOnPrimOffset = data.SatOnPrimOffset,
                SerializedAttachments = data.SerializedAttachments,
                SessionId = data.SessionID.Guid,
                Size = data.Size,
                Throttles = data.Throttles,
                UpAxis = data.UpAxis,
                Velocity = data.Velocity,
                RemoteAgents = data.RemoteAgents,
                ConstantForces = data.ConstantForces,
                ConstantForcesAreLocal = data.ConstantForcesAreLocal,
                PresenceFlags = data.PresenceFlags,
                AvatarAsAPrim = data.AvatarAsAPrim
            };

            return message;
        }

        public AgentData ToAgentData()
        {
            UUID agentId = new UUID(this.AgentId);

            AgentData agentData = new AgentData
            {
                ActiveGroupID = new UUID(this.ActiveGroupID),
                AgentAccess = this.AgentAccess,
                AgentID = agentId,
                AgentPrefs = (this.AgentPrefs != null) ? this.AgentPrefs.ToAgentPrefs(agentId) : new AgentPreferencesData(),
                AlwaysRun = this.AlwaysRun,
                Anims = PackedAnimation.ToAnimations(this.Anims),
                Appearance = this.Appearance.ToAppearance(agentId),
                Aspect = this.Aspect,
                AtAxis = this.AtAxis,
                BodyRotation = this.BodyRotation,
                CallbackURI = this.CallbackURI,
                Center = this.Center,
                ChangedGrid = false,
                CircuitCode = this.CircuitCode,
                ControlFlags = this.ControlFlags,
                EnergyLevel = this.EnergyLevel,
                Far = this.Far,
                GodLevel = this.GodLevel,
                Groups = PackedGroupMembership.ToGroups(this.Groups),
                HeadRotation = this.HeadRotation,
                LeftAxis = this.LeftAxis,
                LocomotionState = this.LocomotionState,
                LocomotionFlags = (AgentLocomotionFlags)this.LocomotionFlags,
                Position = this.Position,
                PreyAgent = new UUID(this.PreyAgent),
                RegionHandle = this.RegionHandle,
                SatOnGroup = new UUID(this.SatOnGroup),
                SatOnPrim = new UUID(this.SatOnPrim),
                SatOnPrimOffset = this.SatOnPrimOffset,
                SerializedAttachments = this.SerializedAttachments,
                SessionID = new UUID(this.SessionId),
                Size = this.Size,
                Throttles = this.Throttles,
                UpAxis = this.UpAxis,
                Velocity = this.Velocity,
                RemoteAgents = this.RemoteAgents,
                ConstantForces = this.ConstantForces,
                ConstantForcesAreLocal = this.ConstantForcesAreLocal,
                PresenceFlags = this.PresenceFlags,
                AvatarAsAPrim = this.AvatarAsAPrim
            };

            return agentData;
        }
    }
}
