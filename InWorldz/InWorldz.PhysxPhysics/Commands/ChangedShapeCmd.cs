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
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics.Commands
{
    internal class ChangedShapeCmd : IMultiPrimCommand
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private PhysxPrim _actor;
        private PhysicsShape _newShape;

        private int _delayCount;

        public ChangedShapeCmd(PhysxPrim actor)
        {
            _actor = actor;
        }

        #region ICommand Members

        public void Execute(PhysxScene scene)
        {
            if (_newShape == null)
            {
                if (_actor.Disposed) return;
                bool creatingNullShape = _actor.Shape.PreferredPhysicsShape == OpenMetaverse.PhysicsShapeType.None || _actor.Shape.FlexiEntry;

                if (_actor.HasActor && creatingNullShape)
                {
                    //invalid shape for a root prim
                    _actor.Shape.PreferredPhysicsShape = OpenMetaverse.PhysicsShapeType.Prim;
                    _actor.Shape.FlexiEntry = false;
                    creatingNullShape = false;
                }

                if (! creatingNullShape)
                {
                    //we need to remesh the prim and then free the old shapes
                    _actor.BeginDelayCommands(this);

                    scene.MeshingStageImpl.QueueForMeshing(_actor.SOPName, _actor.Shape, _actor.Size, 
                        Meshing.MeshingStage.SCULPT_MESH_LOD, _actor.IsPhysical, null, false,
                            delegate(PhysicsShape meshedShape)
                            {
                                _newShape = meshedShape;
                                scene.QueueCommand(this);
                            }
                        );
                }
                else
                {
                    //child should remove its shape from the parent
                    _actor.RebuildPhysxActorWithNewShape(PhysicsShape.Null, null, _actor.IsPhysical, false);
                }
            }
            else
            {
                try
                {
                    _actor.RebuildPhysxActorWithNewShape(_newShape, null, _actor.IsPhysical, false);
                }
                catch (Exception e)
                {
                    //I have seen some exceptions thrown from physx internals here
                    //everything ranging from physx simply failing to create the shape, to 
                    //null streams on shape creation. Pending finding a real reson for these
                    //issues, we simply report the exception and drop the rebuild in these cases
                    //rather than taking down the region.
                    m_log.ErrorFormat("[InWorldz.PhysX] (ChangedShapeCmd) Rebuilding physx actor failed: {0}", e);

                    //recover by removing this shape and actor
                    try
                    {
                        scene.RemovePrim(_actor);
                    }
                    catch (Exception e2)
                    {
                        m_log.ErrorFormat("[InWorldz.PhysX] (ChangedShapeCmd) Further exception during rebuild recovery: {0}", e2);
                    }
                }

                _actor.EndDelayCommands();
            }
        }

        #endregion

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
            if (--_delayCount <= 0)
            {
                return true;
            }

            return false;
        }

        public bool IsCullable
        {
            get { return false; }
        }

        public void AddDelay()
        {
            _delayCount++;
        }

        public IEnumerable<PhysxPrim> GetTargetPrims()
        {
            return _actor.GetComposingPrims();
        }
    }
}
