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
    /// Describes a material that prims can be made out of
    /// </summary>
    [ProtoContract]
    public class Material : IMaterial, IDisposable
    {
        public const int NO_PRESET = -1;
        private const float GRAVITY_MULTIPLIER_DEFAULT = 1.0f;

        public static Material GROUND;
        public static Material STONE;
        public static Material METAL;
        public static Material GLASS;
        public static Material WOOD;
        public static Material FLESH;
        public static Material PLASTIC;
        public static Material RUBBER;

        private static Material[] MaterialsByOMVEnum;

        private PhysX.Material _baseMaterial = null;

        private float _serializedStaticFriction;
        private float _serializedDynamicFriction;
        private float _serializedRestitution;

        public bool IsShared { get; set; }

        [ProtoMember(1)]
        public int MaterialPreset { get; set; }

        [ProtoMember(2)]
        public float Density { get; set; }

        [ProtoMember(3)]
        public float StaticFriction
        {
            get
            {
                if (_baseMaterial != null) return _baseMaterial.StaticFriction;
                else return _serializedStaticFriction;
            }

            set
            {
                _serializedStaticFriction = value;
            }
        }

        [ProtoMember(4)]
        public float DynamicFriction
        {
            get
            {
                if (_baseMaterial != null) return _baseMaterial.DynamicFriction;
                else return _serializedDynamicFriction;
            }

            set
            {
                _serializedDynamicFriction = value;
            }
        }

        [ProtoMember(5)]
        public float Restitution
        {
            get
            {
                if (_baseMaterial != null) return _baseMaterial.Restitution;
                else return _serializedRestitution;
            }

            set
            {
                _serializedRestitution = value;
            }
        }

        [ProtoMember(6)]
        public float GravityMultiplier { get; set; }

        public PhysX.Material PhyMaterial
        {
            get { return _baseMaterial; }
        }

        private bool _disposed = false;

        public static void BuiltinMaterialInit(PhysX.Physics physics)
        {
            // PhysX collision forces are very strong, so the ground restitution needs to be lowered significantly.
            GROUND = new Material(physics.CreateMaterial(0.4f, 0.35f, 0.05f), 1200.0f);
            GROUND.IsShared = true;
            // For navigable roads, people need a preset that acts mostly like ground.
            STONE = new Material(physics.CreateMaterial(0.5f, 0.35f, 0.05f), 2400.0f, (int)OpenMetaverse.Material.Stone);
            STONE.IsShared = true;
            METAL = new Material(physics.CreateMaterial(0.3f, 0.25f, 0.4f), 2700.0f, (int)OpenMetaverse.Material.Metal);
            METAL.IsShared = true;
            GLASS = new Material(physics.CreateMaterial(0.2f, 0.15f, 0.7f), 2500.0f, (int)OpenMetaverse.Material.Glass);
            GLASS.IsShared = true;
            WOOD = new Material(physics.CreateMaterial(0.6f, 0.55f, 0.5f), 1000.0f, (int)OpenMetaverse.Material.Wood);
            WOOD.IsShared = true;
            FLESH = new Material(physics.CreateMaterial(0.9f, 0.8f, 0.3f), 1400.0f, (int)OpenMetaverse.Material.Flesh);
            FLESH.IsShared = true;
            PLASTIC = new Material(physics.CreateMaterial(0.4f, 0.35f, 0.7f), 900.0f, (int)OpenMetaverse.Material.Plastic);
            PLASTIC.IsShared = true;
            RUBBER = new Material(physics.CreateMaterial(0.9f, 0.87f, 0.9f), 1100.0f, (int)OpenMetaverse.Material.Rubber);
            RUBBER.IsShared = true;

            MaterialsByOMVEnum = new Material[]
            {
                STONE,  //0...
                METAL,
                GLASS,
                WOOD,
                FLESH,
                PLASTIC,
                RUBBER,
                WOOD    //light.. which makes no sense is remapped to wood
            };
        }

        internal static IMaterial FindImpl(OpenMetaverse.Material materialEnum)
        {
            return MaterialsByOMVEnum[OpenSim.Framework.Util.Clamp<int>((int)materialEnum, 0, MaterialsByOMVEnum.Length - 1)];
        }

        /// <summary>
        /// Used by serialization
        /// </summary>
        public Material()
        {
            //any defaults that may not end up getting set during deserialization of old data must
            //be set here or at their respective property initializer

            //set the GM default
            GravityMultiplier = GRAVITY_MULTIPLIER_DEFAULT;
        }

        public Material(PhysX.Material baseMaterial, float density)
        {
            _baseMaterial = baseMaterial;
            Density = density;
            GravityMultiplier = GRAVITY_MULTIPLIER_DEFAULT;
            MaterialPreset = NO_PRESET;
        }

        public Material(PhysX.Material baseMaterial, float density, float gravityMultiplier)
        {
            _baseMaterial = baseMaterial;
            Density = density;
            GravityMultiplier = gravityMultiplier;
            MaterialPreset = NO_PRESET;
        }

        public Material(PhysX.Material baseMaterial, float density, int materialPreset)
            : this(baseMaterial, density, GRAVITY_MULTIPLIER_DEFAULT)
        {
            MaterialPreset = materialPreset;
        }

        public Material(PhysX.Physics physics, float staticFriction, float dynamicFriction, float restitution, float density, float gravityMultiplier)
        {
            _baseMaterial = physics.CreateMaterial(staticFriction, dynamicFriction, restitution);
            Density = density;
            GravityMultiplier = gravityMultiplier;
        }

        public Material(PhysX.Physics physics, float staticFriction, float dynamicFriction, float restitution, float density)
        {
            _baseMaterial = physics.CreateMaterial(staticFriction, dynamicFriction, restitution);
            Density = density;
            GravityMultiplier = GRAVITY_MULTIPLIER_DEFAULT;
        }

        public Material(PhysX.Physics physics, float staticFriction, float dynamicFriction, float restitution, float density, OpenMetaverse.Material presetNumber)
            : this(physics, staticFriction, dynamicFriction, restitution, density, GRAVITY_MULTIPLIER_DEFAULT)
        {
            MaterialPreset = (int)presetNumber;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _baseMaterial.Dispose();
                _disposed = true;
            }
        }

        public void CheckedDispose()
        {
            if (!IsShared)
            {
                this.Dispose();
            }
        }

        public Material ToLocalMaterial(PhysX.Physics physics)
        {
            if (this.MaterialPreset != NO_PRESET)
            {
                return (Material)FindImpl((OpenMetaverse.Material)this.MaterialPreset);
            }
            else
            {
                _baseMaterial = physics.CreateMaterial(_serializedStaticFriction, _serializedDynamicFriction, _serializedRestitution);
                return this;
            }
        }

        public Material Duplicate(PhysX.Physics physics)
        {
            if (this.MaterialPreset != NO_PRESET)
            {
                return (Material)FindImpl((OpenMetaverse.Material)this.MaterialPreset);
            }
            else
            {
                PhysX.Material baseMaterial = physics.CreateMaterial(StaticFriction, DynamicFriction, Restitution);
                return new Material(baseMaterial, Density, GravityMultiplier);
            }
        }
    }
}
