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

//
// This file contains functions that ensure simulation stability and limits
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InWorldz.PhysxPhysics
{
    internal partial class PhysxPrim
    {
        private const uint CONTACTS_STAY_DEAD_FRAMES = 320; //5 seconds
        private const uint CCD_STAY_DEAD_FRAMES = 320; //5 seconds

        /// <summary>
        /// Tests for a violation of limits that can be checked before an actor has been built
        /// </summary>
        /// <param name="newPrimaryShape"></param>
        /// <param name="newChildShapes"></param>
        /// <returns></returns>
        internal bool DynamicsPrecheck(PhysicsShape newPrimaryShape, Dictionary<PhysxPrim, RelatedShapes> newChildShapes)
        {
            int totalComplexity = newPrimaryShape.Complexity;

            if (newChildShapes != null)
            {
                foreach (var shape in newChildShapes)
                {
                    totalComplexity += shape.Value.ChildShape.Complexity;
                }
            }

            //check to make sure it is ok to make this physical
            if (totalComplexity > PhysxPrim.MAX_DYNAMIC_COMPLEXITY)
            {
                //too complex, back out
                newPrimaryShape.DecRef();

                if (newChildShapes != null)
                {
                    foreach (var shape in newChildShapes)
                    {
                        shape.Value.ChildShape.DecRef();
                    }
                }

                this.TriggerComplexityError(String.Format("({0} of {1} convex hulls)", totalComplexity, PhysxPrim.MAX_DYNAMIC_COMPLEXITY));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tests for the violation of limits that can only be determined after the actor has been built
        /// </summary>
        internal void DynamicsPostcheck()
        {
            if (_dynActor != null && _isPhysical)
            {
                EnforceMinimumDynamicDimensions();  //this can turn off the physical flag
                EnforceMaximumDynamicDimensions();  //this can turn off the physical flag

                if (_isPhysical)    //the above can disable the physical flag, so recheck here
                {
                    EnsureObjectAboveGround();

                    PenetrationStatus penStatus = TryRezDepenetrate();

                    switch (penStatus)
                    {
                        case PenetrationStatus.AcceptablePenetration:
                            //temporarily disable CCD to allow natural depenetration
                            TempDisableCCD();
                            break;

                        case PenetrationStatus.CriticalPenetration:
                            //kill all non-ground contacts
                            KillCurrentContacts();
                            break;
                    }
                }
            }
        }

        public float GetTerrainHeightAt(float x, float y)
        {
            // X/Y is Interpolated
            return _scene.TerrainChannel.CalculateHeightAt(x, y);
        }

        private void EnsureObjectAboveGround()
        {
            //check our bounding box. if it is below ground, provide a 
            //z+ adjustment to our position to move it out

            //the height of ALL the terrain below our bounding box must be checked
            //to find the highest peak. quantize to the nearest meter since we don't
            //actually need to have the slope calculations performed
            int yCoordMin = (int)Math.Floor(_actor.WorldBounds.Center.Y - _actor.WorldBounds.Extents.Y);
            int yCoordMax = (int)Math.Ceiling(_actor.WorldBounds.Center.Y + _actor.WorldBounds.Extents.Y);
            int xCoordMin = (int)Math.Floor(_actor.WorldBounds.Center.X - _actor.WorldBounds.Extents.X);
            int xCoordMax = (int)Math.Ceiling(_actor.WorldBounds.Center.X + _actor.WorldBounds.Extents.X);
            float maxHeightFound = -100.0f;

            for (int x = xCoordMin; x <= xCoordMax; x++)
            {
                for (int y = yCoordMin; y <= yCoordMax; y++)
                {
                    maxHeightFound = Math.Max(maxHeightFound, (float)_scene.TerrainChannel[x, y]);
                }
            }

            if (_position.Z - _actor.WorldBounds.Extents.Z <= maxHeightFound)
            {
                _position.Z = maxHeightFound + _actor.WorldBounds.Extents.Z + 0.1f;
                _dynActor.GlobalPose = PhysUtil.PositionToMatrix(_position, _rotation);
            }
        }

        /// <summary>
        /// Temprarily turns of CCD. This is necessary to ensure the stability of the simulation
        /// when there are many prims made physical while interpenetrating
        /// </summary>
        private void TempDisableCCD()
        {
            foreach (var shape in _shapeToPrimIndex.Keys)
            {
                shape.SimulationFilterData = new PhysX.FilterData(shape.SimulationFilterData.Word0,
                    (uint)ShapeCommand.DisableCCD, shape.SimulationFilterData.Word2,
                    shape.SimulationFilterData.Word3);
            }

            _ccdSuspendedFor = CCD_STAY_DEAD_FRAMES;
        }

        /// <summary>
        /// Turns CCD back on after being disabled due to the above disable call
        /// </summary>
        private void EnableCCD()
        {
            foreach (var shape in _shapeToPrimIndex.Keys)
            {
                shape.SimulationFilterData = new PhysX.FilterData(shape.SimulationFilterData.Word0,
                    0, shape.SimulationFilterData.Word2,
                    shape.SimulationFilterData.Word3);
            }

            _ccdSuspendedFor = 0;
        }

        /// <summary>
        /// sets a flag on the shape to kill all current contacts until the next physics step
        /// </summary>
        private void KillCurrentContacts()
        {
            foreach (var shape in _shapeToPrimIndex.Keys)
            {
                shape.SimulationFilterData = new PhysX.FilterData(shape.SimulationFilterData.Word0,
                    (uint)ShapeCommand.KillNonGroundContacts, shape.SimulationFilterData.Word2,
                    shape.SimulationFilterData.Word3);
            }

            _contactsKilledFor = CONTACTS_STAY_DEAD_FRAMES;
        }

        /// <summary>
        /// Unsets a flag to allow contact creation to resume
        /// </summary>
        private void RestoreContactProcessing()
        {
            foreach (var shape in _shapeToPrimIndex.Keys)
            {
                shape.SimulationFilterData = new PhysX.FilterData(shape.SimulationFilterData.Word0,
                    0, shape.SimulationFilterData.Word2,
                    shape.SimulationFilterData.Word3);
            }

            _contactsKilledFor = 0;
        }

        enum PenetrationStatus
        {
            Ok,
            AcceptablePenetration,
            CriticalPenetration
        }

        /// <summary>
        /// Attempts to push an object up out of the ground to prevent
        /// physics explosions
        /// </summary>
        /// <returns></returns>
        private PenetrationStatus TryRezDepenetrate()
        {
            const float PUSH_FACTOR_START = 0.01f;
            float pushFactor = PUSH_FACTOR_START;
            bool foundOverlap = false;
            bool foundCriticalOverlap = false;

            RunDepenetrateCycle(ref pushFactor, ref foundOverlap, ref foundCriticalOverlap);

            if (!foundOverlap && !foundCriticalOverlap)
            {
                return PenetrationStatus.Ok;
            }

            if (foundCriticalOverlap)
            {
                return PenetrationStatus.CriticalPenetration;
            }

            return PenetrationStatus.AcceptablePenetration;
        }

        private void RunDepenetrateCycle(ref float pushFactor, ref bool foundOverlap, ref bool foundCriticalOverlap)
        {
            const int MAX_ITERATIONS = 6;
            const int MAX_OVERLAPS = 32;

            OpenMetaverse.Vector3 offset = OpenMetaverse.Vector3.Zero;

            int iterOverlaps = 0;

            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                iterOverlaps = 0;
                foundOverlap = false;
                foundCriticalOverlap = false;
                foreach (var shape in _shapeToPrimIndex.Keys)
                {
                    OpenMetaverse.Vector3 translatedPose = PhysUtil.DecomposeToPosition(shape.GlobalPose) + offset;
                    PhysX.Shape[] overlapping = _scene.SceneImpl.OverlapMultiple(shape.Geom, PhysUtil.PositionToMatrix(translatedPose, _rotation));

                    if (overlapping == null)
                    {
                        //the overlap is too deep, instant fail
                        foundOverlap = true;
                        foundCriticalOverlap = true;
                        return;
                    }

                    foreach (var oshape in overlapping)
                    {
                        if (oshape != null && oshape.Actor.UserData != this)
                        {
                            iterOverlaps++;
                            foundOverlap = true;
                            if (iterOverlaps > MAX_OVERLAPS)
                            {
                                foundCriticalOverlap = true;
                                break;
                            }
                        }
                    }

                    if (iterOverlaps > MAX_OVERLAPS)
                    {
                        break;
                    }
                }

                if (iterOverlaps > 0 && i + 1 < MAX_ITERATIONS)
                {
                    offset += new OpenMetaverse.Vector3(0f, 0f, pushFactor);
                    pushFactor *= 2.0f;
                }
                else
                {
                    break;
                }
            }

            if (iterOverlaps == 0 && offset != OpenMetaverse.Vector3.Zero)
            {
                _dynActor.GlobalPose = PhysUtil.PositionToMatrix(_position + offset, _rotation);
                _position = _position + offset;
                foundOverlap = false;
            }
            else if (iterOverlaps == 0 && offset == OpenMetaverse.Vector3.Zero)
            {
                foundOverlap = false;
            }
            else if (iterOverlaps < MAX_OVERLAPS)
            {
                foundOverlap = true;
                foundCriticalOverlap = false;
            }
            else
            {
                foundOverlap = true;
                foundCriticalOverlap = true;
            }

        }

        private void EnforceMinimumDynamicDimensions()
        {
            PhysX.Bounds3 bounds = _actor.WorldBounds;

            float xsz = bounds.Extents.X * 2.0f;
            float ysz = bounds.Extents.Y * 2.0f;
            float zsz = bounds.Extents.Z * 2.0f;

            //prevent the creation of esentially 1 dimensional physicals
            int dimsTooSmall = 0;
            if (xsz < MIN_SIZE_FOR_DYNAMIC)
            {
                dimsTooSmall++;
            }

            if (ysz < MIN_SIZE_FOR_DYNAMIC)
            {
                dimsTooSmall++;
            }

            if (zsz < MIN_SIZE_FOR_DYNAMIC)
            {
                dimsTooSmall++;
            }

            if (dimsTooSmall >= 2)
            {
                //object is too small to be dynamic. set kinematic
                _isPhysical = false;
                _dynActor.Flags = _dynActor.Flags | PhysX.RigidDynamicFlags.Kinematic;
            }
        }

        private void EnforceMaximumDynamicDimensions()
        {
            PhysX.Bounds3 bounds = _actor.WorldBounds;

            float xsz = bounds.Extents.X * 2.0f;
            float ysz = bounds.Extents.Y * 2.0f;
            float zsz = bounds.Extents.Z * 2.0f;

            //prevent ridiculous sized dynamics
            if (xsz > MAX_PHSYICAL_DIMENSION * 2.0f ||
                ysz > MAX_PHSYICAL_DIMENSION * 2.0f ||
                zsz > MAX_PHSYICAL_DIMENSION * 2.0f)
            {
                //object is too large to be dynamic. set kinematic
                _isPhysical = false;
                _dynActor.Flags = _dynActor.Flags | PhysX.RigidDynamicFlags.Kinematic;
                return;
            }

            int dimsTooLarge = 0;
            if (xsz > MAX_PHSYICAL_DIMENSION)
            {
                dimsTooLarge++;
            }

            if (ysz > MAX_PHSYICAL_DIMENSION)
            {
                dimsTooLarge++;
            }

            if (zsz > MAX_PHSYICAL_DIMENSION)
            {
                dimsTooLarge++;
            }

            if (dimsTooLarge >= 2)
            {
                //object is too large to be dynamic. set kinematic
                _isPhysical = false;
                _dynActor.Flags = _dynActor.Flags | PhysX.RigidDynamicFlags.Kinematic;
                return;
            }
        }
    }
}
