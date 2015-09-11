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

namespace InWorldz.PhysxPhysics.Meshing
{
    /// <summary>
    /// Remeshes an actor and all of its child prims
    /// </summary>
    internal class RemeshActorWorker
    {
        public delegate void CompletedCallback(PhysicsShape rootShape, Dictionary<PhysxPrim, RelatedShapes> childShapes);

        private PhysxScene _scene;
        private PhysxPrim _actor;
        private bool _isPhysical;
        private CompletedCallback _callBack;
        
        private int _totalShapesToMesh;
        private int _currentNumShapesMeshed;

        public PhysicsShape RootShape { get; set; }
        public Dictionary<PhysxPrim, RelatedShapes> ChildShapes { get; set; }

        public RemeshActorWorker(PhysxScene scene, PhysxPrim actor, bool isPhysical, CompletedCallback callBack)
        {
            _scene = scene;
            _actor = actor;
            _isPhysical = isPhysical;
            _callBack = callBack;
        }

        public void Remesh()
        {
            //we need to remesh the prim and all of its child shapes
            _totalShapesToMesh = _actor.ChildShapes.Count + 1;

            _scene.MeshingStageImpl.QueueForMeshing(_actor.SOPName, _actor.Shape, _actor.Size, 
                Meshing.MeshingStage.SCULPT_MESH_LOD, _isPhysical, null, false,
                    delegate(PhysicsShape meshedShape)
                    {
                        RootShape = meshedShape;
                        if (++_currentNumShapesMeshed == _totalShapesToMesh)
                        {
                            _callBack(RootShape, ChildShapes);
                        }
                    }
                );

            if (_actor.ChildShapes.Count > 0) ChildShapes = new Dictionary<PhysxPrim, RelatedShapes>();

            //remesh each child shape to match the parent status
            foreach (KeyValuePair<PhysxPrim, RelatedShapes> kvp in _actor.ChildShapes)
            {
                PhysxPrim thisChild = kvp.Key;

                _scene.MeshingStageImpl.QueueForMeshing(thisChild.SOPName, kvp.Key.Shape,
                    thisChild.Size, Meshing.MeshingStage.SCULPT_MESH_LOD, _isPhysical, null, false,
                        delegate(PhysicsShape meshedShape)
                        {
                            ChildShapes.Add(thisChild, new RelatedShapes { ChildShape = meshedShape });

                            if (++_currentNumShapesMeshed == _totalShapesToMesh)
                            {
                                _callBack(RootShape, ChildShapes);
                            }
                        }
                );
            }
        }
    }
}
