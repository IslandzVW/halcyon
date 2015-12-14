/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenMetaverse;
using System;
using OpenSim.Region.Interfaces;

namespace OpenSim.Region.Physics.Manager
{
    public delegate void physicsCrash();

    public abstract class PhysicsScene
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Flags]
        public enum AddPrimShapeFlags
        {
            None = 0,
            Physical                    = (1 << 0),
            FromSceneStartup            = (1 << 1),
            Phantom                     = (1 << 2),
            WantsCollisionNotification  = (1 << 3),
            StartSuspended              = (1 << 4),
            Interpolate                 = (1 << 5),
            FromCrossing                = (1 << 6)
        }

        public event physicsCrash OnPhysicsCrash;

        public static PhysicsScene Null
        {
            get { return null; }
        }

        public abstract float SimulationFPS { get; }

        public abstract int SimulationFrameTimeAvg { get; }

        public abstract bool Simulating
        {
            get;
            set;
        }

        public abstract IMesher Mesher { get; }

        public virtual void TriggerPhysicsBasedRestart()
        {
            physicsCrash handler = OnPhysicsCrash;
            if (handler != null)
            {
                OnPhysicsCrash();
            }
        }


        public abstract void Initialize(IMesher meshmerizer, IConfigSource config, UUID regionId);

        public abstract PhysicsActor AddAvatar(string avName, OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation, OpenMetaverse.Vector3 size, bool isFlying, OpenMetaverse.Vector3 initialVelocity);

        public abstract void RemoveAvatar(PhysicsActor actor);

        public abstract void RemovePrim(PhysicsActor prim);

        public abstract PhysicsActor AddPrimShape(string primName, AddPrimShapeFlags flags, BulkShapeData shapeData);

        /// <summary>
        /// Adds all the shapes for a prim and its children
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="primName"></param>
        /// <param name="shapeData"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public abstract void BulkAddPrimShapes(ICollection<BulkShapeData> shapeData, AddPrimShapeFlags flags);

        public virtual bool SupportsNINJAJoints
        {
            get { return false; }
        }

        public virtual PhysicsJoint RequestJointCreation(string objectNameInScene, PhysicsJointType jointType, OpenMetaverse.Vector3 position,
                                            Quaternion rotation, string parms, List<string> bodyNames, string trackedBodyName, Quaternion localRotation)
        { return null; }

        public virtual void RequestJointDeletion(string objectNameInScene)
        { return; }

        public virtual void RemoveAllJointsConnectedToActorThreadLocked(PhysicsActor actor)
        { return; }

        public virtual void DumpJointInfo()
        { return; }

        public event JointMoved OnJointMoved;

        protected virtual void DoJointMoved(PhysicsJoint joint)
        {
            // We need this to allow subclasses (but not other classes) to invoke the event; C# does
            // not allow subclasses to invoke the parent class event.
            if (OnJointMoved != null)
            {
                OnJointMoved(joint);
            }
        }

        public event JointDeactivated OnJointDeactivated;

        protected virtual void DoJointDeactivated(PhysicsJoint joint)
        {
            // We need this to allow subclasses (but not other classes) to invoke the event; C# does
            // not allow subclasses to invoke the parent class event.
            if (OnJointDeactivated != null)
            {
                OnJointDeactivated(joint);
            }
        }

        public event JointErrorMessage OnJointErrorMessage;

        protected virtual void DoJointErrorMessage(PhysicsJoint joint, string message)
        {
            // We need this to allow subclasses (but not other classes) to invoke the event; C# does
            // not allow subclasses to invoke the parent class event.
            if (OnJointErrorMessage != null)
            {
                OnJointErrorMessage(joint, message);
            }
        }

        public virtual OpenMetaverse.Vector3 GetJointAnchor(PhysicsJoint joint)
        { return OpenMetaverse.Vector3.Zero; }

        public virtual OpenMetaverse.Vector3 GetJointAxis(PhysicsJoint joint)
        { return OpenMetaverse.Vector3.Zero; }

        //public abstract ITerrainChannel


        public abstract void AddPhysicsActorTaint(PhysicsActor prim);

        public abstract void AddPhysicsActorTaint(PhysicsActor prim, TaintType taint);

        public abstract float Simulate(float timeStep, uint ticksSinceLastSimulate, uint frameNum, bool dilated);

        public abstract void GetResults();

        public abstract void SetTerrain(float[] heightMap, int revision);

        public virtual void SetStartupTerrain(float[] heightMap, int revision) { }

        public abstract void SetWaterLevel(float baseheight);

        public abstract void Dispose();

        public abstract Dictionary<uint, float> GetTopColliders();

        public abstract bool IsThreaded { get; }

        public abstract IMaterial FindMaterialImpl(Material materialEnum);

        public abstract ITerrainChannel TerrainChannel { get; set; }

        public abstract RegionSettings RegionSettings { get; set; }

        public abstract void DumpCollisionInfo();

        public abstract void SendPhysicsWindData(OpenMetaverse.Vector2[] sea, OpenMetaverse.Vector2[] gnd, OpenMetaverse.Vector2[] air,
                                                 float[] ranges, float[] maxheights);

        public abstract void RayCastWorld(OpenMetaverse.Vector3 start, OpenMetaverse.Vector3 direction,
            float distance, int hitAmounts, Action<List<ContactResult>> result);

        public abstract List<ContactResult> RayCastWorld(OpenMetaverse.Vector3 start, OpenMetaverse.Vector3 direction,
            float distance, int hitAmounts);
    }

    public struct ContactResult
    {
        public PhysicsActor CollisionActor;
        public Vector3 Normal;
        public Vector3 Position;
        public float Distance;
        public int FaceIndex;
    }

    public delegate void JointMoved(PhysicsJoint joint);
    public delegate void JointDeactivated(PhysicsJoint joint);
    public delegate void JointErrorMessage(PhysicsJoint joint, string message); // this refers to an "error message due to a problem", not "amount of joint constraint violation"
}
