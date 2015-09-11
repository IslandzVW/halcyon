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
using System.Threading;
using OpenSim.Region.Physics.Manager;

namespace InWorldz.PhysxPhysics.Commands
{
    /// <summary>
    /// Command to add new physics objects
    /// </summary>
    internal class CreateObjectCmd : ICommand, IDisposable
    {
        private PhysxPrim _parent;
        private string _primName;
        private OpenSim.Framework.PrimitiveBaseShape _pbs;
        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Vector3 _size;
        private OpenMetaverse.Quaternion _rotation;
        private OpenMetaverse.Vector3 _velocity;
        private OpenMetaverse.Vector3 _angularVelocity;
        private float _lod;
        PhysicsScene.AddPrimShapeFlags _flags;
        private Material _material;
        private byte[] _serializedPhysicsProperties;
        private byte[] _serializedPhysicsShapes;
        private ulong _interpolateTime;

        private bool _hasVdSet;

        public PhysicsShape Shape;
        public PhysX.RigidActor Actor;
        public readonly ManualResetEventSlim FinshedEvent = new ManualResetEventSlim(false);



        public PhysxPrim FinalPrim;

        #region ICommand Members

        public CreateObjectCmd(PhysxPrim parent, string primName, OpenSim.Framework.PrimitiveBaseShape pbs, OpenMetaverse.Vector3 position,
            OpenMetaverse.Vector3 size, OpenMetaverse.Quaternion rotation, OpenMetaverse.Vector3 velocity, OpenMetaverse.Vector3 angularVelocity,
            float lod, PhysicsScene.AddPrimShapeFlags flags, Material material, byte[] serializedPhysicsProperties, byte[] serializedPhysicsShapes,
            ulong interpolateTime)
        {
            _parent = parent;
            _primName = primName;
            _pbs = pbs;
            _position = position;
            _size = size;
            _rotation = rotation;
            _velocity = velocity;
            _angularVelocity = angularVelocity;
            _lod = lod;
            _flags = flags;
            _material = material;
            _serializedPhysicsProperties = serializedPhysicsProperties;
            _serializedPhysicsShapes = serializedPhysicsShapes;
            _interpolateTime = interpolateTime;
        }

        public void Execute(PhysxScene scene)
        {
            if (Shape == null)
            {
                PhysicsProperties properties = PhysicsProperties.DeserializeOrCreateNew(scene, _material, _serializedPhysicsProperties);
                _hasVdSet = properties.VolumeDetectActive;

                scene.MeshingStageImpl.QueueForMeshing(_primName, _pbs, _size, _lod, 
                    (_flags & PhysicsScene.AddPrimShapeFlags.Physical) != 0 || _hasVdSet, 
                    _serializedPhysicsShapes,
                    (_flags & PhysxScene.AddPrimShapeFlags.FromCrossing) == PhysicsScene.AddPrimShapeFlags.FromCrossing,
                        delegate(PhysicsShape meshedShape)
                        {
                            Shape = meshedShape;
                            scene.QueueCommand(this);
                        }
                    );
            }
            else
            {
                bool isPhysical = (_flags & PhysicsScene.AddPrimShapeFlags.Physical) != 0;
                if (_hasVdSet)
                {
                    isPhysical = false;
                }

                CollisionGroupFlag collisionGroup = (_flags & PhysicsScene.AddPrimShapeFlags.Phantom) == 0 ? CollisionGroupFlag.Normal : CollisionGroupFlag.PhysicalPhantom;
                if (_parent == null)
                {
                    bool kinematicStatic;

                    PhysicsProperties properties = PhysicsProperties.DeserializeOrCreateNew(scene, _material, _serializedPhysicsProperties);

                    Actor = PhysxActorFactory.CreateProperInitialActor(Shape, scene, _position, _rotation, _flags, out kinematicStatic, properties.PhysxMaterial);

                    FinalPrim = new PhysxPrim(scene, _pbs, _position, _rotation, Shape, Actor, isPhysical, properties, collisionGroup);
                    scene.AddPrimSync(FinalPrim, isPhysical, kinematicStatic);
                }
                else
                {
                    PhysicsProperties properties = PhysicsProperties.DeserializeOrCreateNew(scene, _material, _serializedPhysicsProperties);

                    FinalPrim = new PhysxPrim(_parent, scene, _pbs, _position, _rotation, Shape, null, isPhysical, properties, collisionGroup);
                    _parent.LinkPrimAsChildSync(Shape, FinalPrim, _position, _rotation, false);
                }

                if (_hasVdSet)
                {
                    FinalPrim.SetVolumeDetectSync(true);
                }

                if ((_flags & PhysicsScene.AddPrimShapeFlags.StartSuspended) != 0)
                {
                    FinalPrim.SuspendPhysicsSync(_interpolateTime);
                }

                FinalPrim.DynamicsPostcheck();

                FinalPrim.SetInitialVelocities(_velocity, _angularVelocity);

                if ((_flags & PhysicsScene.AddPrimShapeFlags.Interpolate) != 0)
                {
                    FinalPrim.SuspendPhysicsSync(_interpolateTime);
                    FinalPrim.ResumePhysicsSync(true);
                }

                this.FinshedEvent.Set();
            }
        }

        #endregion

        public bool AffectsMultiplePrims()
        {
            return false;
        }

        public PhysxPrim GetTargetPrim()
        {
            return null;
        }

        public bool RemoveWaitAndCheckReady()
        {
            return true;
        }

        public void Dispose()
        {
            FinshedEvent.Dispose();
        }

        public bool IsCullable
        {
            get { return false; }
        }
    }
}
