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
using OpenSim.Region.Physics.Manager;
using ProtoBuf;

namespace InWorldz.PhysxPhysics
{
    /// <summary>
    /// Physics properties (see IPhysicsProperties)
    /// </summary>
    [ProtoContract]
    class PhysicsProperties : IPhysicsProperties
    {
        private static readonly log4net.ILog m_log
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public PhysicsProperties()
        {
            PassCollisions = false;
            LockedAxes = OpenMetaverse.Vector3.Zero;
        }

        #region Deprecated Properties
        /// <summary>
        /// Deprecated. Retained only for backward compatibility
        /// </summary>
        [Obsolete]
        [ProtoMember(3)]
        private string ForcePbuf1Deprecated
        {
            get
            {
                return null;
            }

            set
            {
                if (value != null)
                {
                    Force = OpenMetaverse.Vector3.Parse(value);
                }
            }
        }

        /// <summary>
        /// Deprecated. Retained only for backward compatibility
        /// </summary>
        [Obsolete]
        [ProtoMember(5)]
        private string TorquePbuf1Deprecated
        {
            get
            {
                return null;
            }

            set
            {
                if (value != null)
                {
                    Torque = OpenMetaverse.Vector3.Parse(value);
                }
            }
        }

        /// <summary>
        /// Deprecated. Retained only for backward compatibility
        /// </summary>
        [Obsolete]
        [ProtoMember(9)]
        private string MoveTargetPbuf1Deprecated
        {
            get
            {
                return null;
            }

            set
            {
                if (value != null)
                {
                    MoveTarget = OpenMetaverse.Vector3.Parse(value);
                }
            }
        }

        /// <summary>
        /// Deprecated. Retained only for backward compatibility
        /// </summary>
        [Obsolete]
        [ProtoMember(11)]
        private string RotLookTargetPbuf1Deprecated
        {
            get
            {
                return null;
            }

            set
            {
                if (value != null)
                {
                    RotLookTarget = OpenMetaverse.Quaternion.Parse(value);
                }
            }
        }

        /// <summary>
        /// Deprecated. Retained only for backward compatibility
        /// </summary>
        [Obsolete]
        [ProtoMember(17)]
        private string AngularVelocityTargetPbuf1Deprecated
        {
            get
            {
                return null;
            }

            set
            {
                if (value != null)
                {
                    AngularVelocityTarget = OpenMetaverse.Vector3.Parse(value);
                }
            }
        }

        /// <summary>
        /// Obsolete field never used in main grid
        /// </summary>
        [Obsolete]
        [ProtoMember(29)]
        private OpenMetaverse.Vector3 Member29_deprecated = OpenMetaverse.Vector3.Zero;

        #endregion

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
                return PhysxMaterial;
            }

            set
            {
                PhysxMaterial = (Material)value;
            }
        }

        /// <summary>
        /// The actual material implementation actively used for this object
        /// </summary>
        [ProtoMember(8)]
        internal Material PhysxMaterial { get; set; }

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
        [ProtoMember(24)]
        public Vehicle.VehicleProperties VehicleProps { get; set; }

        [ProtoMember(26)]
        public OpenMetaverse.Vector3 GrabTarget { get; set; }

        [ProtoMember(27)]
        public float GrabTargetTau { get; set; }

        [ProtoMember(28)]
        public bool BlockGrab { get; set; }

        [ProtoMember(30)]
        public OpenMetaverse.Vector3 LockedAxes { get; set; }


        /// <summary>
        /// Fills out the physics material implementation from the description. Used when
        /// deserializing a physics material
        /// </summary>
        internal void FillMaterialFromDesc(PhysX.Physics physics)
        {
            PhysxMaterial = PhysxMaterial.ToLocalMaterial(physics);
        }

        internal static PhysicsProperties DeserializeOrCreateNew(PhysxScene scene, IMaterial fallbackMaterial, byte[] serializedProperties)
        {
            PhysicsProperties properties = null;
            if (serializedProperties != null)
            {
                try
                {
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream(serializedProperties))
                    {
                        properties = ProtoBuf.Serializer.Deserialize<PhysicsProperties>(ms);
                    }

                    properties.FillMaterialFromDesc(scene.SceneImpl.Physics);

                    return properties;
                }
                catch (Exception e)
                {
                    //unable to deserialize physics properties, fallthrough
                    m_log.ErrorFormat("[InWorldz.PhysxX] PhysicsProperties.DeserializeOrCreateNew: Deserialization failed, falling back to defaults: {0}", e);
                }
            }

            properties = new PhysicsProperties();
            properties.Material = fallbackMaterial;

            return properties;
        }
    }
}
