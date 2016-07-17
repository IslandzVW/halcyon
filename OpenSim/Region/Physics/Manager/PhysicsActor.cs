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

using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.Physics.Manager
{
    public delegate void PositionUpdate();
    public delegate void VelocityUpdate(OpenMetaverse.Vector3 velocity);
    public delegate void OrientationUpdate(Quaternion orientation);

    public enum CollisionEventUpdateType
    {
        /// <summary>
        /// A collision with an object or avatar started
        /// </summary>
        CollisionBegan,
        /// <summary>
        /// A collision with an object or avatar ended
        /// </summary>
        CollisionEnded,
        /// <summary>
        /// A collision with an object or avatar continues
        /// </summary>
        CollisionContinues,


        /// <summary>
        /// A list of object collisions that are continuing
        /// </summary>
        BulkCollisionsContinue,

        /// <summary>
        /// A list of avatar collisions that are continuing
        /// </summary>
        BulkAvatarCollisionsContinue,


        /// <summary>
        /// Object began colliding with land
        /// </summary>
        LandCollisionBegan,
        /// <summary>
        /// Object stopped colliding with land
        /// </summary>
        LandCollisionEnded,
        /// <summary>
        /// The object continues to collide with land
        /// </summary>
        LandCollisionContinues,


        /// <summary>
        /// A collision with a character (avatar) has begun
        /// </summary>
        CharacterCollisionBegan,
        /// <summary>
        /// A collision with a character (avatar) has ended
        /// </summary>
        CharacterCollisionEnded,
        /// <summary>
        /// Object continues colliding with a character (avatar)
        /// </summary>
        CharacterCollisionContinues,

        /// <summary>
        /// A general notification from the physical character to its controller 
        /// about objects it has collided with due to its own movement
        /// </summary>
        CharacterCollisionsChanged
    }

    public class CollisionEventUpdate : EventArgs
    {
        public CollisionEventUpdateType Type;
        public uint OtherColliderLocalId;
        public UUID OtherColliderUUID;

        public IEnumerable<uint> BulkCollisionData;
        public Vector3 CollisionLocation;
    }

    public struct CameraData
    {
        public Quaternion CameraRotation;
        public Vector3 CameraPosition;
        public bool MouseLook;
        public bool Valid;
        public Quaternion HeadRotation;
        public Quaternion BodyRotation;
    }

    public abstract class PhysicsActor
    {
        public delegate void RequestTerseUpdate();
        public delegate void CollisionUpdate(EventArgs e);
        public delegate void OutOfBounds(OpenMetaverse.Vector3 pos);
        public delegate void RequestPersistence();
        public delegate void ComplexityError(string errorMessage);
        public delegate CameraData GetCameraData();

        public delegate void AfterResumeCallback();

        public delegate OpenSim.Framework.Geom.Box RequestOBB();

// disable warning: public events
#pragma warning disable 67
        public event PositionUpdate OnPositionUpdate;
        public event VelocityUpdate OnVelocityUpdate;
        public event OrientationUpdate OnOrientationUpdate;
        public event RequestTerseUpdate OnRequestTerseUpdate;
        public event CollisionUpdate OnCollisionUpdate;
        public event OutOfBounds OnOutOfBounds;
        public event RequestPersistence OnNeedsPersistence;
        public event ComplexityError OnComplexityError;
        public event GetCameraData OnPhysicsRequestingCameraData;
        public event RequestOBB OnPhysicsRequestingOBB;


#pragma warning restore 67

        public CameraData TryGetCameraData()
        {
            GetCameraData handler = OnPhysicsRequestingCameraData;
            if (handler != null)
            {
                return handler();
            }

            return new CameraData { Valid = false };
        }

        public static PhysicsActor Null
        {
            get { return null; }
        }

        public virtual int TotalComplexity { get { return 0; } }

        public abstract bool Stopped { get; }

        public abstract OpenMetaverse.Vector3 Size { get; set; }

        public abstract PrimitiveBaseShape Shape { set; get;  }

        public abstract uint LocalID { get; set; }

        public abstract UUID Uuid { get; set; }

        public abstract bool Grabbed { set; }

        public abstract bool Selected { get; set; }

        public abstract bool Disposed { get; }

        public string SOPName;
        public string SOPDescription;

        public abstract void CrossingFailure();

        public abstract void ForceAboveParcel(float height);

        public abstract void LinkToNewParent(PhysicsActor obj, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot);

        public abstract void DelinkFromParent(Vector3 newWorldPosition, Quaternion newWorldRotation);

        public abstract void LockAngularMotion(OpenMetaverse.Vector3 axis);

        public abstract Vector3 GetLockedAngularMotion();

        public void TriggerComplexityError(string errorMessage)
        {
            ComplexityError handler = OnComplexityError;

            if (handler != null)
            {
                handler(errorMessage);
            }
        }

        public virtual void RequestPhysicsTerseUpdate()
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            RequestTerseUpdate handler = OnRequestTerseUpdate;

            if (handler != null)
            {
                handler();
            }
        }

        public virtual void RequestPhysicsNeedsPersistence()
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            RequestTerseUpdate handler = OnRequestTerseUpdate;

            if (handler != null)
            {
                handler();
            }
        }

        public virtual void RequestPhysicsPositionUpdate()
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            PositionUpdate handler = OnPositionUpdate;

            if (handler != null)
            {
                handler();
            }
        }

        public virtual void RaiseOutOfBounds(OpenMetaverse.Vector3 pos)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            OutOfBounds handler = OnOutOfBounds;

            if (handler != null)
            {
                handler(pos);
            }
        }

        public virtual OpenSim.Framework.Geom.Box DoRequestOBB()
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            RequestOBB handler = OnPhysicsRequestingOBB;

            if (handler != null)
            {
                return handler();
            }

            return null;
        }

        public virtual void SendCollisionUpdate(EventArgs e)
        {
            CollisionUpdate handler = OnCollisionUpdate;

            if (handler != null)
            {
                handler(e);
            }
        }

        public virtual IMaterial GetMaterial()
        {
            return null;
        }

        public virtual void SetMaterial (OpenMetaverse.Material material, bool applyToEntireObject)
        {
            
        }

        public void SetMaterial(IMaterial materialDesc, bool applyToEntireObject)
        {
            SetMaterial(materialDesc, applyToEntireObject, MaterialChanges.All);
        }

        public virtual void SetMaterial(IMaterial materialDesc, bool applyToEntireObject, MaterialChanges changes)
        {

        }

        public abstract OpenMetaverse.Vector3 Position { get; set; }
        public abstract float Mass { get; }
        public abstract OpenMetaverse.Vector3 Force { get; }
        public abstract OpenMetaverse.Vector3 ConstantForce { get; }
        public abstract bool ConstantForceIsLocal { get; }
        public abstract OpenSim.Framework.Geom.Box OBBobject { get; set; }

        public abstract void SetVolumeDetect(bool vd);    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more

        public abstract OpenMetaverse.Vector3 GeometricCenter { get; }
        public abstract OpenMetaverse.Vector3 CenterOfMass { get; }
        public abstract OpenMetaverse.Vector3 Velocity { get; set; }
        public abstract OpenMetaverse.Vector3 Torque { get; set; }
        public abstract float CollisionScore { get; set;}
        public abstract OpenMetaverse.Vector3 Acceleration { get; }
        public abstract Quaternion Rotation { get; set; }
        public abstract ActorType PhysicsActorType { get; }
        public abstract bool IsPhysical { get; }
        public abstract bool Flying { get; set; }
        public abstract bool SetAirBrakes { get; set; }
        public abstract bool SetAlwaysRun { get; set; }
        public abstract bool ThrottleUpdates { get; set; }
        public abstract bool IsColliding { get; set; }
        public abstract bool CollidingGround { get; set; }
        public abstract bool CollidingObj { get; set; }
        public abstract bool FloatOnWater { set; }
        public abstract OpenMetaverse.Vector3 AngularVelocity { get; set; }
        public abstract OpenMetaverse.Vector3 AngularVelocityTarget { get; set; }
        public abstract float Buoyancy { get; set; }

        public virtual Vector4 CollisionPlane { get { return new Vector4(0f, 0f, 0f, 1f); } }


        public abstract void AddForce(OpenMetaverse.Vector3 force, ForceType ftype);
        public abstract void AddAngularForce(OpenMetaverse.Vector3 force, ForceType ftype);
        public abstract void SubscribeCollisionEvents(int ms);
        public abstract void UnSubscribeEvents();
        public abstract bool SubscribedEvents();

        public abstract void SyncWithPhysics(float timeStep, uint ticksSinceLastSimulate, uint frameNum);

        public abstract void AddForceSync(Vector3 Force, Vector3 forceOffset, ForceType type);

        public abstract void UpdateOffsetPosition(Vector3 newOffset, Quaternion rotOffset);

        public abstract IPhysicsProperties Properties { get; }

        public abstract void GatherTerseUpdate(out OpenMetaverse.Vector3 position, out OpenMetaverse.Quaternion rotation,
            out OpenMetaverse.Vector3 velocity, out OpenMetaverse.Vector3 acceleration, out OpenMetaverse.Vector3 angularVelocity);

        public virtual void DoCollisionRepeats(float timeStep, uint ticksSinceLastSimulate, uint frameNum)
        {

        }

        public virtual void SetGrabSpinVelocity(OpenMetaverse.Vector3 target)
        {
        }

        public virtual void SetGrabTarget(OpenMetaverse.Vector3 target, float tau)
        {
        }

        public virtual void SetMoveToTarget(OpenMetaverse.Vector3 target, float tau)
        {
        }

        public virtual void SetRotLookAtTarget(Quaternion target, float strength, float damping)
        {
        }

        public virtual void StopRotLookAt()
        {

        }

        public virtual void SetAngularVelocity(Vector3 force, bool local)
        {
        }

        public virtual Manager.PIDHoverFlag GetHoverType()
        {
            return Manager.PIDHoverFlag.None;
        }

        public virtual void SetHover(Manager.PIDHoverFlag hoverType, float height, float tau, float damping)
        {

        }

        public virtual void ClearHover()
        {

        }

        public virtual byte[] GetSerializedPhysicsProperties()
        {
            return null;
        }

        public abstract void Suspend();

        public abstract void Resume(bool interpolate, AfterResumeCallback callback);

        public virtual void SetPhantom(bool phantom)
        {

        }

        public virtual void SetVelocity(Vector3 force, bool local)
        {
        }

        public virtual byte[] GetSerializedPhysicsShapes()
        {
            throw new NotImplementedException();
        }

        public virtual void SetVehicleType(Vehicle.VehicleType type)
        {
        }

        public virtual void SetVehicleFlags(Vehicle.VehicleFlags flags)
        {
        }

        public virtual void RemoveVehicleFlags(Vehicle.VehicleFlags flags)
        {
        }

        public virtual void SetVehicleFloatParam(Vehicle.FloatParams param, float value)
        {
        }

        public virtual void SetVehicleRotationParam(Vehicle.RotationParams param, OpenMetaverse.Quaternion value)
        {
        }

        public virtual void SetVehicleVectorParam(Vehicle.VectorParams param, OpenMetaverse.Vector3 value)
        {
        }
    }
}
