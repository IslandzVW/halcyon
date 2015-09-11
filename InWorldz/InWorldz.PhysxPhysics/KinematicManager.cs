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

namespace InWorldz.PhysxPhysics
{
    /// <summary>
    /// Manages transitions of physactors from kinematic to static after a period of rest
    /// </summary>
    internal class KinematicManager
    {
        /// <summary>
        /// The maximum amount of time before something changes from kinematic to static in ms
        /// if it is not modified
        /// </summary>
        const int KINEMATIC_TRANSITION_TIME = 10 * 60 * 1000;

        /// <summary>
        /// The time window to look for objects that can be transitioned to static upon each check.
        /// We expire this many objects at one time
        /// </summary>
        const int KINEMATIC_TRANSITION_WINDOW_SZ = KINEMATIC_TRANSITION_TIME / 3;

        /// <summary>
        /// The range of age we will look at to expire kinematics
        /// </summary>
        const int KINEMATIC_TRANSITION_WINDOW = KINEMATIC_TRANSITION_TIME - KINEMATIC_TRANSITION_WINDOW_SZ;


        /// <summary>
        /// Storage for the objects
        /// </summary>
        private IndexedPriorityQueue<PhysxPrim, ulong> _trackedObjects = new IndexedPriorityQueue<PhysxPrim, ulong>();

        public KinematicManager()
        {

        }

        /// <summary>
        /// Should be called whenever a kinematic has changed to move it back down the list
        /// of objects to be made static
        /// </summary>
        /// <param name="obj"></param>
        public void KinematicChanged(PhysxPrim obj)
        {
            _trackedObjects[obj] = Util.GetLongTickCount();
        }

        /// <summary>
        /// Should be called when a kinematic is removed or changed to a static or dynamic
        /// </summary>
        /// <param name="obj"></param>
        public void KinematicRemoved(PhysxPrim obj)
        {
            _trackedObjects.Remove(obj);
        }

        /// <summary>
        /// Checks for kinematics that have not been updated in KINEMATIC_TRANSITION_TIME - WINDOW and
        /// changes them to statics if appropriate
        /// </summary>
        public void CheckForExipiredKinematics()
        {
            ulong currentTickCount = Util.GetLongTickCount();

            while (_trackedObjects.Count > 0)
            {
                var lastUpdatedOn = _trackedObjects.FindMinItemAndIndex();
                if (currentTickCount > lastUpdatedOn.Value && currentTickCount - lastUpdatedOn.Value > KINEMATIC_TRANSITION_WINDOW)
                {
                    _trackedObjects.DeleteMin();
                    this.TransitionKinematic(lastUpdatedOn.Key);
                }
                else
                {
                    break;
                }
            }
        }

        private void TransitionKinematic(PhysxPrim physxPrim)
        {
            //if the prim is selected, we want to leave it as a kinematic for a bit longer
            if (physxPrim.Selected)
            {
                this.KinematicChanged(physxPrim);
                return;
            }

            //if the prim is physical, it doesnt belong in here
            if (physxPrim.IsPhysical)
            {
                _trackedObjects.Remove(physxPrim);
                return;
            }

            //otherwise, this prim is a good candidate to be made static
            //note that there is no need to run this through meshing etc
            //as kinematics are "static" in the eyes of the mesh cache
            physxPrim.MakeStatic();
        }
    }
}
