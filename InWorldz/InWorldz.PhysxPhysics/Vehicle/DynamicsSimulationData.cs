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

namespace InWorldz.PhysxPhysics.Vehicle
{
    /// <summary>
    /// Internal dynamics values used by the vehicle simulator
    /// </summary>
    [ProtoContract]
    internal class DynamicsSimulationData
    {
        /// <summary>
        /// Obsolete field never used in main grid
        /// </summary>
        [Obsolete]
        [ProtoMember(1)]
        public bool Valid_deprecated = false;

        [Obsolete]
        [ProtoMember(2)]
        public OpenMetaverse.Quaternion LastRotation_deprecated = OpenMetaverse.Quaternion.Identity;           // Global coords

        [ProtoMember(3)]
        public OpenMetaverse.Vector3 LocalLinearVelocity;     // Local coords

        [ProtoMember(4)]
        public OpenMetaverse.Vector3 LocalAngularVelocity;    // Local coords

        // Motor dynamics (in local coordinates)
        [ProtoMember(5)]
        public OpenMetaverse.Vector3 LinearTargetVelocity;

        [ProtoMember(6)]
        public float LinearDecayIndex;

        [ProtoMember(7)]
        public OpenMetaverse.Vector3 AngularTargetVelocity;

        [ProtoMember(8)]
        public float AngularDecayIndex;

        [ProtoMember(9)]
        public DateTime LastAccessTOD;

        [ProtoMember(10)]
        public OpenMetaverse.Vector3 AngularDirection;

        [ProtoMember(11)]
        public OpenMetaverse.Vector3 LinearDirection;

        [ProtoMember(12)]
        public OpenMetaverse.Vector3 LastPosition;

        [ProtoMember(13)]
        public OpenMetaverse.Vector3 ShortTermPositionDelta;

        [ProtoMember(14)]
        public float LastVerticalAngle;

        [ProtoMember(15)]
        public float VerticalForceAdjust;

        [ProtoMember(16)]
        public OpenMetaverse.Vector3 TargetAngularDelta;

        [ProtoMember(17)]
        public OpenMetaverse.Vector3 TargetLinearDelta;

        [ProtoMember(18)]
        public float Timestep;

        [ProtoMember(19)]
        public float BankingDirection;

        [ProtoMember(20)]
        public float BankingTargetVelocity;

        [ProtoMember(21)]
        public uint LastVerticalFrameNumber;

        [ProtoMember(22)]
        public OpenMetaverse.Vector3 WindDirection;

        [ProtoMember(23)]
        public OpenMetaverse.Vector3 WaterDirection;
    };
}
