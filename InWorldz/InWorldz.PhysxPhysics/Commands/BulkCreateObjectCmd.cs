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
using OpenSim.Region.Framework.Scenes;
using System.Threading;

namespace InWorldz.PhysxPhysics.Commands
{
    /// <summary>
    /// Creates a parent and all child shapes from the given data
    /// </summary>
    internal class BulkCreateObjectCmd : ICommand, IDisposable
    {
        private PhysxScene.AddPrimShapeFlags _flags;
        private ICollection<BulkShapeData> _shapes;
        private PhysicsShape _newPrimaryShape;
        private BulkShapeData _primaryShapeData;

        private Dictionary<BulkShapeData, PhysicsShape> _meshedShapes = new Dictionary<BulkShapeData,PhysicsShape>();

        private int _totalNumShapes;
        private int _meshedSoFar;

        private bool _rootHasVdSet = false;

        public readonly ManualResetEventSlim FinishedEvent = new ManualResetEventSlim(false);


        public BulkCreateObjectCmd(PhysxScene.AddPrimShapeFlags flags, ICollection<BulkShapeData> shapes)
        {
            _flags = flags;
            _shapes = shapes;
            _totalNumShapes = shapes.Count;
            _meshedSoFar = 0;
        }

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

        public void Execute(PhysxScene scene)
        {
            bool isPhysical = (_flags & PhysicsScene.AddPrimShapeFlags.Physical) != 0;

            if (_rootHasVdSet)
            {
                isPhysical = false;
            }

            if (_newPrimaryShape == null)
            {
                bool first = true;
                foreach (BulkShapeData shape in _shapes)
                {
                    BulkShapeData thisShape = shape;
                    SceneObjectPart thisPart = (SceneObjectPart)thisShape.Part;
                    String primName = String.Empty;
                    if (thisPart != null)
                        primName = thisPart.Name;
                    if (first)
                    {
                        _primaryShapeData = thisShape;
                        PhysicsProperties properties = PhysicsProperties.DeserializeOrCreateNew(scene, shape.Material, shape.PhysicsProperties);
                        _rootHasVdSet = properties.VolumeDetectActive;

                        if (_rootHasVdSet)
                        {
                            isPhysical = false;
                        }

                        scene.MeshingStageImpl.QueueForMeshing(primName, shape.Pbs, shape.Size, Meshing.MeshingStage.SCULPT_MESH_LOD, 
                            isPhysical || _rootHasVdSet, shape.SerializedShapes, 
                            (_flags & PhysxScene.AddPrimShapeFlags.FromCrossing) == PhysicsScene.AddPrimShapeFlags.FromCrossing,
                                delegate(PhysicsShape meshedShape)
                                {
                                    _newPrimaryShape = meshedShape;
                                    _meshedShapes.Add(thisShape, meshedShape);
                                    if (++_meshedSoFar == _totalNumShapes)
                                    {
                                        scene.QueueCommand(this);
                                    }
                                }
                            );

                        first = false;
                    }
                    else
                    {
                        scene.MeshingStageImpl.QueueForMeshing(primName, shape.Pbs, shape.Size, Meshing.MeshingStage.SCULPT_MESH_LOD, 
                            isPhysical || _rootHasVdSet, shape.SerializedShapes,
                            (_flags & PhysxScene.AddPrimShapeFlags.FromCrossing) == PhysicsScene.AddPrimShapeFlags.FromCrossing,
                                delegate(PhysicsShape meshedShape)
                                {
                                    _meshedShapes.Add(thisShape, meshedShape);
                                    if (++_meshedSoFar == _totalNumShapes)
                                    {
                                        scene.QueueCommand(this);
                                    }
                                }
                            );
                    }
                }
            }
            else
            {
                OpenMetaverse.Vector3 rootVelocity = OpenMetaverse.Vector3.Zero;
                OpenMetaverse.Vector3 rootAngularVelocity = OpenMetaverse.Vector3.Zero;

                //we have all the shapes for the parent and all children, time to construct the group
                bool first = true;
                PhysxPrim rootPrim = null;

                CollisionGroupFlag collisionGroup = (_flags & PhysicsScene.AddPrimShapeFlags.Phantom) == 0 ? CollisionGroupFlag.Normal : CollisionGroupFlag.PhysicalPhantom;
                foreach (BulkShapeData shape in _shapes)
                {
                    if (first)
                    {
                        bool kinematicStatic;

                        PhysicsProperties properties = PhysicsProperties.DeserializeOrCreateNew(scene, shape.Material, shape.PhysicsProperties);

                        PhysX.RigidActor actor = PhysxActorFactory.CreateProperInitialActor(_newPrimaryShape, scene, shape.Position, shape.Rotation, _flags, out kinematicStatic,
                            properties.PhysxMaterial);

                        rootPrim = new PhysxPrim(scene, shape.Pbs, shape.Position, shape.Rotation, _newPrimaryShape, actor, 
                            isPhysical, properties, collisionGroup);

                        scene.AddPrimSync(rootPrim, isPhysical, kinematicStatic);

                        shape.OutActor = rootPrim;

                        rootVelocity = shape.Velocity;
                        rootAngularVelocity = shape.AngularVelocity;

                        first = false;
                    }
                    else
                    {
                        PhysicsShape phyShape = _meshedShapes[shape];

                        PhysicsProperties properties = PhysicsProperties.DeserializeOrCreateNew(scene, shape.Material, shape.PhysicsProperties);

                        PhysxPrim childPrim = new PhysxPrim(rootPrim, scene, shape.Pbs, shape.Position, shape.Rotation, phyShape, 
                            null, isPhysical, properties, collisionGroup);
                        rootPrim.LinkPrimAsChildSync(phyShape, childPrim, shape.Position, shape.Rotation, true);

                        shape.OutActor = childPrim;
                    }
                }

                rootPrim.UpdateMassAndInertia();

                if (_rootHasVdSet)
                {
                    rootPrim.SetVolumeDetectSync(_rootHasVdSet);
                }

                if ((_flags & PhysicsScene.AddPrimShapeFlags.StartSuspended) != 0)
                {
                    rootPrim.SuspendPhysicsSync(_primaryShapeData.ObjectReceivedOn);
                }

                rootPrim.DynamicsPostcheck();

                rootPrim.SetInitialVelocities(rootVelocity, rootAngularVelocity);

                if ((_flags & PhysicsScene.AddPrimShapeFlags.Interpolate) != 0)
                {
                    rootPrim.SuspendPhysicsSync(_primaryShapeData.ObjectReceivedOn);
                    rootPrim.ResumePhysicsSync(true);
                }

                FinishedEvent.Set();
            }
        }

        public void Dispose()
        {
            FinishedEvent.Dispose();
        }


        public bool IsCullable
        {
            get { return false; }
        }
    }
}
