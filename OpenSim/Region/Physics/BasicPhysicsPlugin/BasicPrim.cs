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

using log4net;
using OpenSim.Region.Physics.Manager;
using System;
using System.Reflection;
using OpenSim.Framework;

namespace OpenSim.Region.Physics.BasicPhysicsPlugin
{
    public class BasicPrim : PhysicsActor, IDisposable
    {
        private const float OBJECT_DENSITY = 60.0f;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The root prim that we're linked to, or null if it is ourselves
        /// </summary>
        private BasicPrim _parentPrim = null;

        /// <summary>
        /// Returns whether or not this prim is just a surrgate placeholder for
        /// a physactor parent
        /// </summary>
        internal bool IsChild
        {
            get { return _parentPrim != null; }
        }

        /// <summary>
        /// Provides consistency to a read or write of terse update properties 
        /// </summary>
        private object _terseConsistencyLock = new object();

        /// <summary>
        /// Whether or not the angular velocity of this body should be used
        /// in the terse updates going to the client
        /// </summary>
        private bool _useAngularVelocity = true;

        private BasicScene _scene;
        private PrimitiveBaseShape _pbs;
        private OpenSim.Framework.Geom.Box _OBBobject = new OpenSim.Framework.Geom.Box(OpenMetaverse.Vector3.Zero, OpenMetaverse.Vector3.Zero);

        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Quaternion _rotation;
        private OpenMetaverse.Vector3 _velocity;
        private OpenMetaverse.Vector3 _angularVelocity;
        private OpenMetaverse.Vector3 _acceleration;
        private float _mass;

        private bool _isPhysical;
        private int _selectCount;

        /// <summary>
        /// For IDisposable
        /// </summary>
        private bool _disposed = false;


        public BasicPrim(BasicScene scene, PrimitiveBaseShape baseShape, OpenMetaverse.Vector3 pos,
            OpenMetaverse.Quaternion rotation, /*PhysicsShape myShape, PhysX.RigidActor myActor,*/
            bool isPhysical, IPhysicsProperties properties/*, CollisionGroupFlag collisionGroup*/)
            : this(null, scene, baseShape, pos, rotation, /*myShape, myActor,*/ isPhysical, properties/*, collisionGroup*/)
        {

        }

        public BasicPrim(BasicPrim parent, BasicScene scene, PrimitiveBaseShape baseShape, OpenMetaverse.Vector3 pos,
            OpenMetaverse.Quaternion rotation, /*PhysicsShape myShape, PhysX.RigidActor myActor,*/
            bool isPhysical, IPhysicsProperties properties/*, CollisionGroupFlag collisionGroup*/)
        {
            _parentPrim = parent;
            _scene = scene;
            _pbs = baseShape;
            _position = pos;
            _rotation = rotation;
            _isPhysical = isPhysical;
            _properties = (BasicPhysicsProperties)properties;
            //_collisionGroup = collisionGroup;

            _acceleration = OpenMetaverse.Vector3.Zero;

            _mass = OBJECT_DENSITY * _pbs.Scale.X * _pbs.Scale.Y * _pbs.Scale.Z;

            //this.AssignActor(myActor, myShape, _isPhysical, DeleteActorFlags.None);
        }


        #region IDisposable Members

        public void Dispose()
        {
            if (!_disposed)
            {
                //this.DeleteActor(_actor, _myShape, _isPhysical, DeleteActorFlags.DestroyAll);

                _disposed = true;

                //also marked all child prims as disposed. this prevents
                //late references to children by commands from going through
                // ... removed for now ...
            }
        }

        #endregion

        #region implemented abstract members of PhysicsActor
        public override void CrossingFailure()
        {
            OpenMetaverse.Vector3 newPos = _position;
            //place this object back into the region
            if (_position.X > Constants.RegionSize - 1)
            {
                newPos.X = Constants.RegionSize - 1/* - _actor.WorldBounds.Extents.X*/;
            }
            else if (_position.X <= 0f)
            {
                newPos.X = 0.0f/*_actor.WorldBounds.Extents.X*/;
            }

            if (_position.Y > Constants.RegionSize - 1)
            {
                newPos.Y = Constants.RegionSize - 1/* - _actor.WorldBounds.Extents.Y*/;
            }
            else if (_position.Y <= 0f)
            {
                newPos.Y = 0.0f/*_actor.WorldBounds.Extents.Y*/;
            }

            //also make sure Z is above ground
            float groundHeight = _scene.TerrainChannel.CalculateHeightAt(newPos.X, newPos.Y);
            if (newPos.Z/* - _actor.WorldBounds.Extents.Z*/ < groundHeight)
            {
                newPos.Z = groundHeight/* + _actor.WorldBounds.Extents.Z*/ + 0.1f;
            }
            /*
            if (_dynActor != null)
            {
                bool wasKinematic = (_dynActor.Flags & PhysX.RigidDynamicFlags.Kinematic) != 0;

                _dynActor.GlobalPose = PhysUtil.PositionToMatrix(newPos, _rotation);
                _velocity = OpenMetaverse.Vector3.Zero;
                _angularVelocity = OpenMetaverse.Vector3.Zero;

                if (!wasKinematic)
                {
                    _dynActor.AngularVelocity = PhysX.Math.Vector3.Zero;
                    _dynActor.LinearVelocity = PhysX.Math.Vector3.Zero;
                    _dynActor.PutToSleep();
                }
            }
            */
        }

        public override byte[] GetSerializedPhysicsShapes()
        {
            byte[] outShapes = null;

            // Ignoring.  No shapes are stored!

            return outShapes;
        }

        public override void ForceAboveParcel(float height)
        {
            OpenMetaverse.Vector3 newPos = _position;
            //place this object back above the parcel
            _position.Z = height;

            _position = newPos;
        }

        public override void LinkToNewParent(PhysicsActor obj, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot)
        {
            lock (_terseConsistencyLock)
            {
                _position = localPos;
                _rotation = localRot;
            }

            _parentPrim = (BasicPrim)obj;
        }

        public override void DelinkFromParent(OpenMetaverse.Vector3 newWorldPosition, OpenMetaverse.Quaternion newWorldRotation)
        {
            lock (_terseConsistencyLock)
            {
                _position = newWorldPosition;
                _rotation = newWorldRotation;
            }

            _parentPrim = null;
        }

        public override void LockAngularMotion(OpenMetaverse.Vector3 axis)
        {
            lock (_properties)
            {
                _properties.LockedAxes = axis;
            }
        }

        public override OpenMetaverse.Vector3 GetLockedAngularMotion()
        {
            lock (_properties)
            {
                return _properties.LockedAxes;
            }
        }

        public override void SetVolumeDetect(bool vd)
        {
            lock (_properties)
            {
                _properties.VolumeDetectActive = vd;
            }
        }

        public override void AddForce(OpenMetaverse.Vector3 force, ForceType ftype)
        {
            AddForceSync(force, OpenMetaverse.Vector3.Zero, ftype);
        }

        public override void AddAngularForce(OpenMetaverse.Vector3 force, ForceType ftype)
        {
            AddForceSync(force, OpenMetaverse.Vector3.Zero, ftype);
        }

        public override void SubscribeCollisionEvents(int ms)
        {
            this.EnableCollisionEventsSync();
        }

        public override void UnSubscribeEvents()
        {
            this.DisableCollisionEventsSync();
        }

        public override bool SubscribedEvents()
        {
            return _properties.WantsCollisionNotification;
        }

        public override void SyncWithPhysics(float timeStep, uint ticksSinceLastSimulate, uint frameNum)
        {
            //throw new NotImplementedException();
            // TODO: if you want to actually simulate prim motion!  Will get very complex very fast.
        }

        public override void AddForceSync(OpenMetaverse.Vector3 Force, OpenMetaverse.Vector3 forceOffset, ForceType type)
        {
            //these property setter force types need to be set and assigned regardless
            //of if the object is currently a free dynamic
            switch (type)
            {
                case ForceType.AngularVelocityTarget:
                    lock (_properties)
                    {
                        _properties.AngularVelocityTarget = Force;
                    }
                    RequestPhysicsNeedsPersistence();
                    break;

                case ForceType.ConstantGlobalLinearForce:
                    lock (_properties)
                    {
                        _properties.ForceIsLocal = false;
                        _properties.Force = Force;
                    }
                    RequestPhysicsNeedsPersistence();
                    break;

                case ForceType.ConstantLocalLinearForce:
                    lock (_properties)
                    {
                        _properties.ForceIsLocal = true;
                        _properties.Force = Force;
                    }
                    RequestPhysicsNeedsPersistence();
                    break;
            }

            //in all cases, wake up
            //this.WakeUp();
        }

        public override void UpdateOffsetPosition(OpenMetaverse.Vector3 newOffset, OpenMetaverse.Quaternion rotOffset)
        {
            //Offset updates are only handled for child prims. Upstream a non child move is 
            //handled by setting our position directly
            if (IsChild)
            {
                _position = newOffset;
                _rotation = rotOffset;
            }
        }

        public override void GatherTerseUpdate(out OpenMetaverse.Vector3 position, out OpenMetaverse.Quaternion rotation, out OpenMetaverse.Vector3 velocity, out OpenMetaverse.Vector3 acceleration, out OpenMetaverse.Vector3 angularVelocity)
        {
            lock (_terseConsistencyLock)
            {
                position = _position;
                rotation = _rotation;
                velocity = _velocity;

                //if this is a child prim, or it is not physical, report angular velocity 
                //as whatever our target is (targetomega)
                //this is necessary because changing this physical and back again will
                //change the angular velocity and currently TargetOmega is implemented
                //as an overwrite to angular velocity which is wrong.
                if (IsChild || !_isPhysical) angularVelocity = _properties.AngularVelocityTarget;
                else if (_useAngularVelocity) angularVelocity = _angularVelocity;
                else angularVelocity = OpenMetaverse.Vector3.Zero;

                acceleration = _acceleration;
            }
        }

        public override void Suspend()
        {
            // Ignored.
        }

        public override void Resume(bool interpolate, AfterResumeCallback callback)
        {
            // Ignored.
        }

        public override bool Stopped
        {
            get { return false; }
        }

        public override OpenMetaverse.Vector3 Size
        {
            get
            {
                return _pbs.Scale;
            }
            set
            {
                throw new NotSupportedException("You can not set the size of a prim physactor directly. Instead, change the size of the parent prim");
            }
        }

        public override PrimitiveBaseShape Shape
        {
            get 
            { 
                return _pbs; 
            }
            set
            {
                _pbs = value;
                _scene.AddPhysicsActorTaint(this, TaintType.ChangedShape);
            }
        }

        public override uint LocalID
        {
            get;
            set;
        }

        public override OpenMetaverse.UUID Uuid
        {
            get;
            set;
        }

        public override bool Grabbed
        {
            // Analysis disable once ValueParameterNotUsed
            set { }
        }

        public override bool Selected
        {
            get
            {
                return _selectCount > 0;
            }
            set
            {
                if (value)
                {
                    ++_selectCount;
                }
                else
                {
                    --_selectCount;
                }
            }
        }

        public override bool Disposed
        {
            get
            {
                return _disposed;
            }
        }

        public override OpenMetaverse.Vector3 Position
        {
            get
            {
                lock (_terseConsistencyLock)
                {
                    return _position;
                }
            }
            set
            {
                lock (_terseConsistencyLock)
                {
                    _position = value;
                }
            }
        }

        public override float Mass
        {
            get
            {
                return _mass;
            }
        }

        public override OpenMetaverse.Vector3 Force
        {
            get
            {
                OpenMetaverse.Vector3 force = OpenMetaverse.Vector3.Zero;
                bool isLocal = false;

                lock (_properties)
                {
                    force = _properties.Force;
                    isLocal = _properties.ForceIsLocal;
                }

                if (!isLocal)
                {
                    return force;
                }
                else
                {
                    return force * _rotation;
                }
            }
        }

        public override OpenMetaverse.Vector3 ConstantForce
        {
            get
            {
                OpenMetaverse.Vector3 force = OpenMetaverse.Vector3.Zero;

                lock (_properties)
                {
                    force = _properties.Force;
                }

                return force;
            }
        }

        public override bool ConstantForceIsLocal
        {
            get
            {
                bool isLocal = false;

                lock (_properties)
                {
                    isLocal = _properties.ForceIsLocal;
                }

                return isLocal;
            }
        }

        public override OpenSim.Framework.Geom.Box OBBobject
        {
            get
            {
                lock (_terseConsistencyLock)
                {
                    return _OBBobject;
                }
            }
            set
            {
                lock (_terseConsistencyLock)
                {
                    _OBBobject = value;
                }
            }
        }

        public override OpenMetaverse.Vector3 GeometricCenter
        {
            get { return new OpenMetaverse.Vector3(); }
        }

        public override OpenMetaverse.Vector3 CenterOfMass
        {
            get
            {
                // TODO: maybe.
                throw new NotImplementedException();
            }
        }

        public override OpenMetaverse.Vector3 Velocity
        {
            get
            {
                lock (_terseConsistencyLock)
                {
                    return _velocity;
                }
            }
            set
            {
            }
        }

        public override OpenMetaverse.Vector3 Torque
        {
            get
            {
                lock (_properties)
                {
                    return _properties.Torque;
                }
            }
            set
            {
            }
        }

        public override float CollisionScore
        {
            get
            {
                return 0.0f;
            }
            set
            {
            }
        }
        public override OpenMetaverse.Vector3 Acceleration
        {
            get { return _acceleration; }
        }

        public override OpenMetaverse.Quaternion Rotation
        {
            get
            {
                lock (_terseConsistencyLock)
                {
                    return _rotation;
                }
            }
            set
            {
                lock (_terseConsistencyLock)
                {
                    _rotation = value;
                }
            }
        }

        public override ActorType PhysicsActorType
        {
            get
            {
                return ActorType.Prim;
            }
        }

        public override bool IsPhysical
        {
            get
            {
                return this._isPhysical;
            }
        }

        public override bool Flying
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool SetAirBrakes
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool SetAlwaysRun
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool ThrottleUpdates
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool IsColliding
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool CollidingGround
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool CollidingObj
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public override bool FloatOnWater
        {
            set {  }
        }

        public override OpenMetaverse.Vector3 AngularVelocity
        {
            get
            {
                return _angularVelocity;
            }
            set
            {
                _angularVelocity = value;
                AddForceSync(value, OpenMetaverse.Vector3.Zero, ForceType.AngularVelocityTarget);
            }
        }

        public override OpenMetaverse.Vector3 AngularVelocityTarget
        {
            get
            {
                lock (_properties)
                {
                    return _properties.AngularVelocityTarget;
                }
            }
            set
            {
                lock (_properties)
                {
                    _properties.AngularVelocityTarget = value;
                }

                AddForceSync(value, OpenMetaverse.Vector3.Zero, ForceType.AngularVelocityTarget);
            }
        }

        public override float Buoyancy
        {
            get
            {
                lock (_properties)
                {
                    return _properties.Buoyancy; 
                }
            }
            set
            {
                lock (_properties)
                {
                    _properties.Buoyancy = value;
                }
            }
        }

        private BasicPhysicsProperties _properties = null;
        public override IPhysicsProperties Properties
        {
            get
            {
                return _properties;
            }
        }
        #endregion

        /// <summary>
        /// Sets the appropriate flags to enable the physx engine to begin passing 
        /// collision notifications to this object
        /// </summary>
        internal void EnableCollisionEventsSync()
        {
            if (!IsChild)   //We only want to do this directly if we're a parent
            {
                //this.SetTouchNotificationOnGivenShapes(TOUCH_NOTIFICATION_FLAGS, _actor.Shapes);
                _properties.WantsCollisionNotification = true;
                //_scene.PrimWantsCollisionRepeat(this);
            }
            else    //otherwise, we want to tell our parent to do it for us
            {
                //_parentPrim.EnableChildCollisionEventsSync(this, true);
                _properties.WantsCollisionNotification = true;
                //_scene.PrimWantsCollisionRepeat(this);
            }

            RequestPhysicsNeedsPersistence();
        }

        internal void DisableCollisionEventsSync()
        {
            if (!IsChild)
            {
                //this.SetTouchNotificationOnGivenShapes(0, _actor.Shapes);
                _properties.WantsCollisionNotification = false;
                //_scene.PrimDisabledCollisionRepeat(this);

                //If we still have children that want notification, re-enable the shapes
                //if (_numChildPrimsWantingCollisionNotification > 0)
                //{
                //    foreach (KeyValuePair<PhysxPrim, RelatedShapes> childShapes in _childShapes)
                //    {
                //        if (childShapes.Key.PhysxProperties.WantsCollisionNotification)
                //        {
                //            SetTouchNotificationOnGivenShapes(TOUCH_NOTIFICATION_FLAGS, childShapes.Value.PhyShapes);
                //        }
                //    }
                //}
            }
            else    //We need to tell our parent that we no longer need notification
            {
                //_parentPrim.EnableChildCollisionEventsSync(this, false);
                _properties.WantsCollisionNotification = false;
                //_scene.PrimDisabledCollisionRepeat(this);
            }

            RequestPhysicsNeedsPersistence();
        }

        private void ChangeToChild(BasicPrim parent, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot)
        {
            _parentPrim = parent;

            //this prim no longer has its old shape or its actor
            //_scene.PrimBecameChild(this);

            //if we have children, we need to unref their shapes here, as our new parent (ours and our children)
            //may require different shape types and will be rebuilt
            //this.DeleteActor(_actor, _myShape, _isPhysical, DeleteActorFlags.UnrefChildShapes);

            //_actor = null;
            //_dynActor = null;
            //_myShape = null;

            _position = localPos;
            _rotation = localRot;
        }

        internal void LinkPrimAsChildSync(/*PhysicsShape childShape, */BasicPrim newChild, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot, 
            bool delayInertiaRecalc)
        {
            newChild.ChangeToChild(this, localPos, localRot);

            this.CombineChildShape(/*childShape, */newChild, localPos, localRot, delayInertiaRecalc);
        }

        private void CombineChildShape(/*PhysicsShape childShape, */BasicPrim newChild, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot, 
            bool delayInertiaRecalc)
        {
            newChild._isPhysical = _isPhysical;
        }
    }
}

