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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;

namespace OpenSim.Region.Physics.BasicPhysicsPlugin
{

    public class BasicActor : PhysicsActor
    {
        private const float CHARACTER_DENSITY = 60.0f;

        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Vector3 _velocity;
        private OpenMetaverse.Vector3 _acceleration;
        private OpenMetaverse.Vector3 _size;
        private OpenMetaverse.Vector3 m_rotationalVelocity = OpenMetaverse.Vector3.Zero;
        private float _mass;

        private bool flying;
        private bool iscolliding;

        private const float _height = 1.5f;
        private const float _radius = 0.25f;

        /// <summary>
        /// Whether or not this character is frozen in place with its state intact.
        /// Used when moving a character between regions.
        /// </summary>
        private bool _suspended = false;

        private bool _disposed = false;

        public BasicActor()
        {
            _velocity = new OpenMetaverse.Vector3();
            _position = new OpenMetaverse.Vector3();
            _acceleration = new OpenMetaverse.Vector3();
            _size = new OpenMetaverse.Vector3(_radius * 2.0f, _radius * 2.0f, CapsuleHeight / 2.0f);

            float volume = (float)(Math.PI * Math.Pow(_radius, 2) * this.CapsuleHeight);
            _mass = CHARACTER_DENSITY * volume;
        }

        private float CapsuleHeight
        {
            get
            {
                return _height - (_radius * 2.0f);
            }
        }

        public override ActorType PhysicsActorType
        {
            get
            {
                return ActorType.Agent;
            }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { }
        }

        public override uint LocalID
        {
            get;
            set;
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            get;
            set;
        }

        public override float Buoyancy
        {
            get { return 0f; }
            set { return; }
        }

        public override bool FloatOnWater
        {
            set { return; }
        }

        public override bool IsPhysical
        {
            get { return false; }
        }

        public override bool ThrottleUpdates
        {
            get { return false; }
            set { return; }
        }

        public override bool Flying
        {
            get { return flying; }
            set { flying = value; }
        }

        public override bool IsColliding
        {
            get { return iscolliding; }
            set { iscolliding = value; }
        }

        public override bool CollidingGround
        {
            get { return false; }
            set { return; }
        }

        public override bool CollidingObj
        {
            get { return false; }
            set { return; }
        }

        public override bool Stopped
        {
            get { return false; }
        }

        public override OpenMetaverse.Vector3 Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override OpenMetaverse.Vector3 Size
        {
            get { return _size; }
            set
            {
                _size = value;
                _size.Z = _size.Z / 2.0f;
            }
        }

        public override PrimitiveBaseShape Shape
        {
            set;
            get;
        }

        public override float Mass
        {
            get { return 0f; }
        }

        public override OpenMetaverse.Vector3 Force
        {
            get { return OpenMetaverse.Vector3.Zero; }
        }

        public override OpenMetaverse.Vector3 CenterOfMass
        {
            get { return OpenMetaverse.Vector3.Zero; }
        }

        public override OpenMetaverse.Vector3 GeometricCenter
        {
            get { return OpenMetaverse.Vector3.Zero; }
        }

        public override OpenMetaverse.Vector3 Velocity
        {
            get { return _velocity; }
            set { _velocity = value; }
        }

        public override OpenMetaverse.Vector3 Torque
        {
            get { return OpenMetaverse.Vector3.Zero; }
            set { return; }
        }

        public override float CollisionScore
        {
            get { return 0f; }
            set { }
        }

        public override OpenMetaverse.Vector3 Acceleration
        {
            get { return _acceleration; }
        }

        public override void LockAngularMotion(OpenMetaverse.Vector3 axis)
        {

        }

        public void SetAcceleration(OpenMetaverse.Vector3 accel)
        {
            _acceleration = accel;
        }

        public override void CrossingFailure()
        {
        }

        public override void ForceAboveParcel(float height)
        {

        }

        public override void UnSubscribeEvents()
        {

        }

        public override bool SubscribedEvents()
        {
            return false;
        }

        #region implemented abstract members of PhysicsActor

        public void Dispose()
        {
            if (!_disposed)
            {
                // ...

                _disposed = true;
            }
        }

        public override void LinkToNewParent(PhysicsActor obj, Vector3 localPos, Quaternion localRot)
        {
            
        }

        public override void DelinkFromParent(Vector3 newWorldPosition, Quaternion newWorldRotation)
        {
            
        }

        public override Vector3 GetLockedAngularMotion()
        {
            return OpenMetaverse.Vector3.Zero;
        }

        public override void SetVolumeDetect(bool vd)
        {
        }

        public override void AddForce(Vector3 force, ForceType ftype)
        {
        }

        public override void AddAngularForce(Vector3 force, ForceType ftype)
        {
        }

        public override void SubscribeCollisionEvents(int ms)
        {
        }

        public override void SyncWithPhysics(float timeStep, uint ticksSinceLastSimulate, uint frameNum)
        {
            if (_suspended)
            {
                // character is in the middle of a crossing. we do not simulate
                return;
            }

            // TODO

        }

        public override void AddForceSync(Vector3 Force, Vector3 forceOffset, ForceType type)
        {
            // TODO Maybe
        }

        public override void UpdateOffsetPosition(Vector3 newOffset, Quaternion rotOffset)
        {
            throw new System.NotImplementedException();
        }

        public override void GatherTerseUpdate(out Vector3 position, out Quaternion rotation, out Vector3 velocity, out Vector3 acceleration, out Vector3 angularVelocity)
        {
            position = _position;
            rotation = OpenMetaverse.Quaternion.Identity;
            velocity = _velocity;
            acceleration = _acceleration;
            angularVelocity = OpenMetaverse.Vector3.Zero;
        }

        public override void Suspend()
        {
            this._suspended = true;
        }

        public override void Resume(bool interpolate, AfterResumeCallback callback)
        {
            //TODO
            this._suspended = false;
        }

        public override UUID Uuid
        {
            get
            {
                return OpenMetaverse.UUID.Zero;
            }
            set
            {
            }
        }

        public override bool Disposed
        {
            get
            {
                return _disposed;
            }
        }

        public override Vector3 ConstantForce
        {
            get
            {
                return Vector3.Zero;
            }
        }

        public override bool ConstantForceIsLocal
        {
            get
            {
                return false;
            }
        }

        public override OpenSim.Framework.Geom.Box OBBobject
        {
            get
            {
                return null;
            }
            set
            {
            }
        }

        public override Quaternion Rotation
        {
            get { return Quaternion.Identity; }
            set { }
        }

        public override Vector3 AngularVelocity
        {
            get
            {
                return new OpenMetaverse.Vector3();
            }
            set
            {
            }
        }

        public override Vector3 AngularVelocityTarget
        {
            get
            {
                return new OpenMetaverse.Vector3();
            }
            set
            {
            }
        }

        public override IPhysicsProperties Properties
        {
            get
            {
                return null;
            }
        }

        #endregion
    }
    
}
