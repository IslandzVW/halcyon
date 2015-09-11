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
using OpenSim.Framework;

namespace InWorldz.PhysxPhysics.Commands
{
    internal class PrepChildPrimAndLinkCmd : IMultiPrimCommand
    {
        private PhysxPrim _parent;
        private PhysxPrim _child;
        private OpenMetaverse.Vector3 _localPos;
        private OpenMetaverse.Quaternion _localRot;

        private PhysicsShape _newShape;

        private List<PhysxPrim> _affectedPrims;
        private int _delayCount = 0;

        public PrepChildPrimAndLinkCmd(PhysxPrim parent, PhysxPrim child, OpenMetaverse.Vector3 localPos,
            OpenMetaverse.Quaternion localRot)
        {
            Util.ThrowIfNull(parent, "parent");
            Util.ThrowIfNull(child, "child");

            _parent = parent;
            _child = child;
            _localPos = localPos;
            _localRot = localRot;
            _newShape = null;
            _affectedPrims = new List<PhysxPrim> { parent, child };
            _affectedPrims.AddRange(child.ChildShapes.Keys);
        }

        public void Execute(PhysxScene scene)
        {
            if (_newShape == null)
            {
                if (_parent.Disposed || _child.Disposed) return;

                _child.Parent = _parent;

                //we need to remesh the prim and then free the old shapes
                _child.BeginDelayCommands(this);
                scene.MeshingStageImpl.QueueForMeshing(_child.SOPName, _child.Shape, _child.Size, 
                    Meshing.MeshingStage.SCULPT_MESH_LOD, _parent.IsPhysical, null, false,
                        delegate(PhysicsShape meshedShape)
                        {
                            _newShape = meshedShape;
                            scene.QueueCommand(this);
                        }
                    );
            }
            else
            {
                _parent.LinkPrimAsChildSync(_newShape, _child, _localPos, _localRot, false);
                _child.EndDelayCommands();
            }
        }

        public void AddDelay()
        {
            _delayCount++;
        }

        public IEnumerable<PhysxPrim> GetTargetPrims()
        {
            return _affectedPrims;
        }

        public bool AffectsMultiplePrims()
        {
            return true;
        }

        public PhysxPrim GetTargetPrim()
        {
            throw new NotImplementedException();
        }

        public bool RemoveWaitAndCheckReady()
        {
            if (--_delayCount <= 0) return true;

            return false;
        }


        public bool IsCullable
        {
            get { return false; }
        }
    }
}
