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

namespace InWorldz.PhysxPhysics
{
    internal class PhysxActorFactory
    {
        private const float DEFAULT_ANGULAR_DAMPING = 0.05f;
        private const float DEFAULT_LINEAR_DAMPING = 0.02f;

        /// <summary>
        /// For some reason the default calculation result for sleep threshold is too low, so
        /// we set it constantly here
        /// </summary>
        private const float SLEEP_THRESHOLD = 0.01f;

        /// <summary>
        /// Creates a new PhysX.RigidDynamic using our defaults
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public static PhysX.RigidDynamic CreateRigidDynamic(PhysxScene scene, PhysicsShape shape, OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation,
            bool physical, bool kinematic, Material material)
        {
            PhysX.RigidDynamic physActor = scene.SceneImpl.Physics.CreateRigidDynamic();
            if (kinematic) physActor.Flags |= PhysX.RigidDynamicFlags.Kinematic;

            SetCommonProperties(scene, shape, position, rotation, physActor, physical, material);

            if (physical)
            {
                physActor.UpdateMassAndInertia(material.Density);
                physActor.MaxAngularVelocity *= 4;

                physActor.AngularDamping = DEFAULT_ANGULAR_DAMPING;
                physActor.LinearDamping = DEFAULT_LINEAR_DAMPING;
            }

            physActor.SleepThreshold = SLEEP_THRESHOLD;

            return physActor;
        }

        public static float GetDefaultSleepThreshold()
        {
            return SLEEP_THRESHOLD;
        }

        /// <summary>
        /// Creates a new PhysX.RigidStatic using our defaults
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public static PhysX.RigidStatic CreateRigidStatic(PhysxScene scene, PhysicsShape shape, OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation,
            Material material)
        {
            PhysX.RigidStatic physActor = scene.SceneImpl.Physics.CreateRigidStatic();
            SetCommonProperties(scene, shape, position, rotation, physActor, false, material);

            return physActor;
        }

        private static void SetCommonProperties(PhysxScene scene, PhysicsShape shape, OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation, PhysX.RigidActor physActor,
            bool physical, Material material)
        {
            shape.AssignToActor(physActor, material.PhyMaterial, physical);
            physActor.GlobalPose =
                PhysX.Math.Matrix.RotationQuaternion(new PhysX.Math.Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)) *
                PhysX.Math.Matrix.Translation(position.X, position.Y, position.Z);
        }

        public static PhysX.RigidActor CreateProperInitialActor(PhysicsShape meshedShape, PhysxScene scene, OpenMetaverse.Vector3 pos, 
            OpenMetaverse.Quaternion rotation, PhysicsScene.AddPrimShapeFlags flags, out bool kinematicStatic,
            Material material)
        {
            bool isPhysical = (flags & PhysicsScene.AddPrimShapeFlags.Physical) != 0;
            kinematicStatic = false;

            PhysX.RigidActor actor;
            if (isPhysical)
            {
                actor = PhysxActorFactory.CreateRigidDynamic(scene, meshedShape, pos, rotation, isPhysical, kinematicStatic, material);
            }
            else
            {
                if ((flags & PhysicsScene.AddPrimShapeFlags.FromSceneStartup) != 0)
                {
                    actor = PhysxActorFactory.CreateRigidStatic(scene, meshedShape, pos, rotation, material);
                }
                else
                {
                    kinematicStatic = true;
                    actor = PhysxActorFactory.CreateRigidDynamic(scene, meshedShape, pos, rotation, isPhysical, kinematicStatic, material);
                }
            }

            return actor;
        }
    }
}
