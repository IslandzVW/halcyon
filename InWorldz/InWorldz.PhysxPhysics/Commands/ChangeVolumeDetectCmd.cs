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

namespace InWorldz.PhysxPhysics.Commands
{
    class ChangeVolumeDetectCmd : IMultiPrimCommand
    {
        private PhysxPrim _actor;
        private bool _vdActive;
        private int _delayCount;

        private PhysicsShape _newPrimaryShape;
        private Dictionary<PhysxPrim, RelatedShapes> _newChildShapes = null;

        public ChangeVolumeDetectCmd(PhysxPrim actor, bool vdActive)
        {
            _actor = actor;
            _vdActive = vdActive;
        }

        public void AddDelay()
        {
            _delayCount++;
        }

        public IEnumerable<PhysxPrim> GetTargetPrims()
        {
            return _actor.GetComposingPrims();
        }

        public bool AffectsMultiplePrims()
        {
            return true;
        }

        public PhysxPrim GetTargetPrim()
        {
            return null;
        }

        public bool RemoveWaitAndCheckReady()
        {
            if (--_delayCount == 0)
            {
                return true;
            }

            return false;
        }

        public void Execute(PhysxScene scene)
        {
            if (_newPrimaryShape == null)
            {
                if (_actor.Disposed) return;

                _actor.BeginDelayCommands(this);

                Meshing.RemeshActorWorker worker = new Meshing.RemeshActorWorker(scene, _actor, _vdActive || _actor.IsPhysical,
                    (PhysicsShape rootShape, Dictionary<PhysxPrim, RelatedShapes> childShapes) =>
                    {
                        _newPrimaryShape = rootShape;
                        _newChildShapes = childShapes;

                        scene.QueueCommand(this);
                    });

                worker.Remesh();
            }
            else
            {
                try
                {
                    if (_actor.IsPhysical)
                    {
                        if (!_actor.DynamicsPrecheck(_newPrimaryShape, _newChildShapes))
                        {
                            return;
                        }
                    }

                    _actor.RebuildPhysxActorWithNewShape(_newPrimaryShape, _newChildShapes, _vdActive ? false : _actor.IsPhysical, false);
                    _actor.SetVolumeDetectSync(_vdActive);
                }
                finally
                {
                    _actor.EndDelayCommands();
                }
            }
        }

        public bool IsCullable
        {
            get { return true; }
        }
    }
}
