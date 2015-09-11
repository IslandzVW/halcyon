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
using OpenMetaverse;

namespace InWorldz.PhysxPhysics.Commands
{
    internal struct SetMaterialCmd : ICommand
    {
        private PhysxPrim _target;
        private IMaterial _material;
        private bool _applyToObject;
        private MaterialChanges _changes;

        public SetMaterialCmd(PhysxPrim target, IMaterial material, bool applyToObject, MaterialChanges changes)
        {
            _target = target;
            _material = material;
            _applyToObject = applyToObject;
            _changes = changes;
        }

        public bool AffectsMultiplePrims()
        {
            return false;
        }

        public PhysxPrim GetTargetPrim()
        {
            return _target;
        }

        public bool RemoveWaitAndCheckReady()
        {
            return true;
        }

        public void Execute(PhysxScene scene)
        {
            if (_target.Disposed) return;

            if (_material is Material)
            {
                //this is already our native material
                _target.SetMaterialSync((Material)_material, _applyToObject);
            }
            else
            {
                IMaterial currMat = _target.PrimMaterial;
                
                //we need to build our native material
                Material material = new Material
                {
                    Density = ((_changes & MaterialChanges.Density) != 0) ? Utils.Clamp(_material.Density, 1.0f, 22587.0f) : currMat.Density,
                    StaticFriction = ((_changes & MaterialChanges.Friction) != 0) ? Utils.Clamp(_material.StaticFriction, 0.0f, 255.0f) : currMat.StaticFriction,
                    DynamicFriction = ((_changes & MaterialChanges.Friction) != 0) ? Utils.Clamp(_material.DynamicFriction, 0.0f, 255.0f) : currMat.DynamicFriction,
                    Restitution = ((_changes & MaterialChanges.Restitution) != 0) ? Utils.Clamp(_material.Restitution, 0.0f, 1.0f) : currMat.Restitution,
                    GravityMultiplier = ((_changes & MaterialChanges.GravityMultiplier) != 0) ? Utils.Clamp(_material.GravityMultiplier, -1.0f, 28.0f) : currMat.GravityMultiplier,
                    MaterialPreset = _material.MaterialPreset
                };

                _target.SetMaterialSync(material.ToLocalMaterial(scene.SceneImpl.Physics), _applyToObject);
            }
        }


        public bool IsCullable
        {
            get { return true; }
        }
    }
}
