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

using OpenSim.Region.Physics.Manager;
using ProtoBuf;
using System;

namespace OpenSim.Region.Physics.BasicPhysicsPlugin
{
    /// <summary>
    /// Physics properties (see IPhysicsProperties)
    /// </summary>
    [ProtoContract]
    public class BasicPhysicsProperties : IPhysicsProperties
    {
        private static readonly log4net.ILog m_log
        = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public BasicPhysicsProperties()
        {
            PassCollisions = false;
            LockedAxes = OpenMetaverse.Vector3.Zero;
        }

        [ProtoMember(1, IsRequired = true)]
        public bool WantsCollisionNotification { get; set; }

        public bool ChildrenWantCollisionNotification { get; set; }

        [ProtoMember(2, IsRequired=true)]
        public bool PassCollisions { get; set; }

        [ProtoMember(19)]
        public OpenMetaverse.Vector3 Force { get; set; }
        [ProtoMember(4)]
        public bool ForceIsLocal { get; set; }

        [ProtoMember(20)]
        public OpenMetaverse.Vector3 Torque { get; set; }
        [ProtoMember(6)]
        public bool TorqueIsLocal { get; set; }

        [ProtoMember(7)]
        public float Buoyancy { get; set; }

        public IMaterial Material 
        {
            get
            {
                return PhysicsMaterial;
            }

            set
            {
                PhysicsMaterial = (Material)value;
            }
        }

        /// <summary>
        /// The actual material implementation actively used for this object
        /// </summary>
        [ProtoMember(8)]
        internal Material PhysicsMaterial { get; set; }

        [ProtoMember(21)]
        public OpenMetaverse.Vector3 MoveTarget { get; set; }
        [ProtoMember(10)]
        public float MoveTargetTau { get; set; }

        [ProtoMember(22)]
        public OpenMetaverse.Quaternion RotLookTarget { get; set; }
        [ProtoMember(12)]
        public float RotLookStrength { get; set; }
        [ProtoMember(13)]
        public float RotLookDamping { get; set; }

        [ProtoMember(14)]
        public PIDHoverFlag HoverType { get; set; }
        [ProtoMember(15)]
        public float HoverHeight { get; set; }
        [ProtoMember(16)]
        public float HoverTau { get; set; }
        [ProtoMember(25)]
        public float HoverDamping { get; set; }

        [ProtoMember(23)]
        public OpenMetaverse.Vector3 AngularVelocityTarget { get; set; }

        [ProtoMember(18)]
        public bool VolumeDetectActive { get; set; }

        /// <summary>
        /// Vehicle specific properties of this prim/group. Will be null if not specified
        /// </summary>
        //[ProtoMember(24)]
        //public Vehicle.VehicleProperties VehicleProps { get; set; }

        [ProtoMember(26)]
        public OpenMetaverse.Vector3 GrabTarget { get; set; }

        [ProtoMember(27)]
        public float GrabTargetTau { get; set; }

        [ProtoMember(28)]
        public bool BlockGrab { get; set; }

        [ProtoMember(30)]
        public OpenMetaverse.Vector3 LockedAxes { get; set; }

        internal static BasicPhysicsProperties DeserializeOrCreateNew(BasicScene scene, IMaterial fallbackMaterial, byte[] serializedProperties)
        {
            BasicPhysicsProperties properties = null;
            if (serializedProperties != null)
            {
                try
                {
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream(serializedProperties))
                    {
                        properties = ProtoBuf.Serializer.Deserialize<BasicPhysicsProperties>(ms);
                    }

                    return properties;
                }
                catch (Exception e)
                {
                    //unable to deserialize physics properties, fallthrough
                    m_log.ErrorFormat("[BasicPhysics] BasicPhysicsProperties.DeserializeOrCreateNew: Deserialization failed, falling back to defaults: {0}", e);
                }
            }

            properties = new BasicPhysicsProperties();
            properties.Material = fallbackMaterial;

            return properties;
        }
    }
}
