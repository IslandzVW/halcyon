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
        /// <summary>
        /// Maximum amount of time that is allowed to be passed to the velocity
        /// and other computations. If more time than this has passed, the 
        /// movements of the avatar will be dilated to prevent explosions
        /// </summary>
        private const float MAX_TIMESTEP = 0.5f;
        /// <summary>
        /// The minimum tick count on windows
        /// </summary>
        private const float MIN_TIMESTEP = 0.0156f;
        private const float TERMINAL_VELOCITY_GRAVITY = 55.0f;
        private const float MIN_FORCE_MAG_BEFORE_ZEROING_SQUARED = 0.00625f;
        private const float ACCELERATION_COMPARISON_TOLERANCE = 0.006f;
        private const int VELOCITY_RAMPUP_TIME = 600;
        private const float GRAVITY_PUSHBACK_DIFF_TOLERANCE = 0.001f;
        private const float POSITION_COMPARISON_TOLERANCE = 0.1f;
        private const float STEP_OFFSET = 0.45f;

        /// <summary>
        /// The center of the collision capsule, and consequently the root of the avatar.
        /// </summary>
        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Vector3 _velocity;
        private OpenMetaverse.Vector3 _acceleration;
        private OpenMetaverse.Vector3 _size;
        private OpenMetaverse.Quaternion _rotation;
        private OpenMetaverse.Vector3 m_rotationalVelocity = OpenMetaverse.Vector3.Zero;
        private float _mass;

        private ulong _lastVelocityNonZero = 0;

        /// <summary>
        /// The current velocity due to gravity clamped at terminal.
        /// </summary>
        private OpenMetaverse.Vector3 _vGravity;

        /// <summary>
        /// Current self-decaying forces acting on an avatar.
        /// </summary>
        private OpenMetaverse.Vector3 _vForces;

        /// <summary>
        /// Target velocity set by the user. (walking, flying, etc)
        /// </summary>
        private OpenMetaverse.Vector3 _vTarget;

        /// <summary>
        /// Whether the current constant forces acting on an avatar are local to the avatar.
        /// </summary>
        // TODO - these two variables have to be serialized.
        // [...]
        private bool _cForcesAreLocal = false;
        /// <summary>
        /// Current constant forces acting on an avatar.
        /// </summary>
        private OpenMetaverse.Vector3 _cForces;

        /// <summary>
        /// Whether the user has requested the brakes be set (AGENT_CONTROL_STOP) and the agent isn't sitting on a prim.
        /// </summary>
        private bool _brakes;
        private bool _running;
        private bool _flying;
        private bool _colliding;
        private volatile bool _collidingGround = false;

        /// <summary>
        ///  The height is the distance between the two sphere centers at the end of the capsule.
        /// </summary>
        private float _height;
        /// <summary>
        /// The radius of the capsule.
        /// </summary>
        private float _radius;

        /// <summary>
        /// Whether or not this character is frozen in place with its state intact.
        /// Used when moving a character between regions.
        /// </summary>
        private bool _suspended = false;

        uint _lastSync;

        private bool _disposed = false;

        private readonly BasicScene _scene;

        public BasicActor(BasicScene scene, float height, float radius,
            OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation,
            bool flying, OpenMetaverse.Vector3 initialVelocity)
        {
            _scene = scene;

            _radius = Math.Max(radius, 0.2f);

            /*
             * The capsule is defined as a position, a vertical height, and a radius. The height is the distance between the 
             * two sphere centers at the end of the capsule. In other words:
             *
             *   p = pos (returned by controller)
             *   h = height
             *   r = radius
             *
             *   p = center of capsule
             *   top sphere center = p.z + h*0.5
             *   bottom sphere center = p.z - h*0.5
             *   top capsule point = p.z + h*0.5 + r
             *   bottom capsule point = p.z - h*0.5 - r
             */
            _height = height;

            _flying = flying;

            float volume = (float)(Math.PI * Math.Pow(_radius, 2) * this.CapsuleHeight);
            _mass = CHARACTER_DENSITY * volume;

            _position = position;
            _rotation = rotation;

            DoZDepenetration();

            _lastSync = (uint)Environment.TickCount;

            _vTarget = initialVelocity;
            _velocity = initialVelocity;
            if (_vTarget != OpenMetaverse.Vector3.Zero)
            {
                //hack to continue at velocity until the controller picks up
                _lastVelocityNonZero = OpenSim.Framework.Util.GetLongTickCount() - VELOCITY_RAMPUP_TIME;
            }
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

        public override bool SetAirBrakes
        {
            get
            {
                return _brakes;
            }
            set
            {
                _brakes = value;
            }
        }

        public override bool SetAlwaysRun
        {
            get
            {
                return _running;
            }
            set
            {
                _running = value;
            }
        }

        public override uint LocalID
        {
            get;
            set;
        }

        public override bool Grabbed
        {
            set { }
        }

        public override bool Selected
        {
            get;
            set;
        }

        public override float Buoyancy
        {
            get { return 0f; }
            set { }
        }

        public override bool FloatOnWater
        {
            set { }
        }

        public override bool IsPhysical
        {
            get { return false; }
        }

        public override bool ThrottleUpdates
        {
            get { return false; }
            set { }
        }

        public override bool Flying
        {
            get { return _flying; }
            set { _flying = value; }
        }

        public override bool IsColliding
        {
            get { return _colliding; }
            set { _colliding = value; }
        }

        public override bool CollidingGround
        {
            get
            {
                return _collidingGround;
            }
            set
            {

            }
        }

        public override bool CollidingObj
        {
            get
            {
                return _colliding && !_collidingGround;
            }
            set
            {

            }
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
            get
            {
                return _velocity;
            }
            set
            {
                if (_vTarget == OpenMetaverse.Vector3.Zero && value != OpenMetaverse.Vector3.Zero)
                {
                    _lastVelocityNonZero = OpenSim.Framework.Util.GetLongTickCount() - 30; //dont begin stopped
                }

                _vTarget = value;
            }
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
            AddForceSync(force, OpenMetaverse.Vector3.Zero, ftype);
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
                _lastSync = (uint)Environment.TickCount;
                return;
            }

            float secondsSinceLastSync = Math.Min(((uint)Environment.TickCount - _lastSync) * 0.001f, MAX_TIMESTEP);
            //m_log.DebugFormat("[CHAR]: secondsSinceLastSync: {0}", secondsSinceLastSync);

            //sometimes a single quantum doesnt show up here, and the calculation returns a zero
            if (secondsSinceLastSync < MIN_TIMESTEP * 2)
            {
                secondsSinceLastSync = MIN_TIMESTEP * 2;
            }

            AccumulateGravity(secondsSinceLastSync);
            DecayForces(secondsSinceLastSync);

            OpenMetaverse.Vector3 cforces = _cForcesAreLocal ? _cForces * _rotation : _cForces;
            cforces.Z = 0;

            OpenMetaverse.Vector3 vCombined = ApplyAirBrakes(_vGravity + _vForces + cforces + this.VTargetWithRunAndRamp) * secondsSinceLastSync;
            //m_log.DebugFormat("[CHAR]: vGrav: {0}, vForces: {1}, vTarget {2}", _vGravity, _vForces, this.VTargetWithRun);

            if (vCombined == OpenMetaverse.Vector3.Zero)
            {
                SetVelocityAndRequestTerseUpdate(secondsSinceLastSync, OpenMetaverse.Vector3.Zero);
                //ReportCollisionsFromLastFrame(frameNum);
                return;
            }

            OpenMetaverse.Vector3 lastPosition = _position;
            //PhysX.ControllerFlag flags = _controller.Move(PhysUtil.OmvVectorToPhysx(vCombined), TimeSpan.FromSeconds(secondsSinceLastSync), 0.001f, FILTERS);
            //_position = PhysUtil.PhysxVectorToOmv(_controller.Position);
            bool collidingDown = CalcPhysics(vCombined, secondsSinceLastSync, 0.001f);
            _lastSync = (uint)Environment.TickCount;

            //take into account any movement not accounted for by the other calculations
            //this is due to collision
            OpenMetaverse.Vector3 vColl = (_position - lastPosition) - vCombined;
            //m_log.InfoFormat("vColl {0} {1} PosDiff: {2} Expected: {3}", vColl, flags, _position - lastPosition, vCombined);
            //m_log.DebugFormat("[CHAR]: vColl: {0}", vColl);

            //bool collidingDown = (flags & PhysX.ControllerFlag.Down) != 0;
            //if (!collidingDown) _rideOnBehavior.AvatarNotStandingOnPrim();

            //negative z in vcoll while colliding down is due to gravity/ground collision, dont report it
            float gravityPushback = Math.Abs(_vGravity.Z) * secondsSinceLastSync;
            if (collidingDown && vColl.Z > 0 && Math.Abs(vColl.Z - gravityPushback) < GRAVITY_PUSHBACK_DIFF_TOLERANCE) vColl.Z = 0;
            //m_log.DebugFormat("[CHAR]: vColl: {0} gravityPushback {1} collidingDown:{2}", vColl, gravityPushback, collidingDown);

            if (/*flags != 0*/ collidingDown)
            {
                _colliding = true;
                if (collidingDown)
                {
                    _collidingGround = true;
                    _flying = false;

                    _vGravity = OpenMetaverse.Vector3.Zero;
                    _vForces.Z = 0.0f;
                    _vTarget.Z = 0.0f;
                }
                else
                {
                    _collidingGround = false;
                    //if we're colliding with anything but the ground, zero out other forces
                    _vForces = OpenMetaverse.Vector3.Zero;
                }
            }
            else
            {
                _colliding = false;
                _collidingGround = false;
            }

            /* Redundant here, and I suspect never even did anything unless the agent tunneled through the terrain mesh in the PhysX implmentation...
            if (frameNum % 3 == 0)
            {
                CheckAvatarNotBelowGround();
            }
            */

            SetVelocityAndRequestTerseUpdate(secondsSinceLastSync, vColl);
            //ReportCollisionsFromLastFrame(frameNum);

            if (!_position.ApproxEquals(lastPosition, POSITION_COMPARISON_TOLERANCE))
            {
                RequestPhysicsPositionUpdate();
            }
        }

        public override void AddForceSync(Vector3 Force, Vector3 forceOffset, ForceType type)
        {
            Force /= _mass;

            switch (type)
            {
                case ForceType.ConstantLocalLinearForce:
                    _cForcesAreLocal = true;
                    _cForces = Force;
                    break;

                case ForceType.ConstantGlobalLinearForce:
                    _cForcesAreLocal = false;
                    _cForces = Force;
                    break;

                case ForceType.GlobalLinearImpulse:
                    _vForces += Force;
                    break;

                case ForceType.LocalLinearImpulse:
                    _vForces += Force * _rotation;
                    break;
            }
        }

        public override void UpdateOffsetPosition(Vector3 newOffset, Quaternion rotOffset)
        {
            throw new System.NotImplementedException();
        }

        public override void GatherTerseUpdate(out Vector3 position, out Quaternion rotation, out Vector3 velocity, out Vector3 acceleration, out Vector3 angularVelocity)
        {
            position = _position;
            rotation = _rotation;
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
            get
            {
                return _rotation;
            }
            set
            {
                _rotation = value;
                //m_log.DebugFormat("[PhysxCharacter] new rot={0}", value);
            }
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

        private OpenMetaverse.Vector3 VTargetWithRunAndRamp
        {
            get
            {
                if (_vTarget == OpenMetaverse.Vector3.Zero)
                    return OpenMetaverse.Vector3.Zero;

                OpenMetaverse.Vector3 baseTarget = _vTarget;

                if (_running && !_flying)
                {
                    baseTarget *= 2.0f;
                }

                return ComputeVelocityRamp(baseTarget);
            }
        }

        private OpenMetaverse.Vector3 ComputeVelocityRamp(OpenMetaverse.Vector3 baseTarget)
        {
            ulong accelerationTime = OpenSim.Framework.Util.GetLongTickCount() - _lastVelocityNonZero;
            if (accelerationTime >= VELOCITY_RAMPUP_TIME) return baseTarget; //fully accelerated

            //linear ramp
            OpenMetaverse.Vector3 result = baseTarget * ((float)accelerationTime / VELOCITY_RAMPUP_TIME);

            //m_log.DebugFormat("[CHAR]: {0}", result);

            return result;
        }

        private void DoZDepenetration()
        {
            CheckAvatarNotBelowGround();
        }

        private void SetVelocityAndRequestTerseUpdate(float secondsSinceLastSync, OpenMetaverse.Vector3 vColl)
        {
            OpenMetaverse.Vector3 cforces = _cForcesAreLocal ? _cForces * _rotation : _cForces;
            cforces.Z = 0;

            OpenMetaverse.Vector3 oldVelocity = _velocity;
            _velocity = ApplyAirBrakes(_vGravity + _vForces + cforces + this.VTargetWithRunAndRamp + vColl);

            if (_velocity == OpenMetaverse.Vector3.Zero && oldVelocity != OpenMetaverse.Vector3.Zero)
            {
                _acceleration = OpenMetaverse.Vector3.Zero;
                RequestPhysicsTerseUpdate();
            }
            else
            {
                OpenMetaverse.Vector3 velDiff = _velocity - oldVelocity;
                OpenMetaverse.Vector3 accel = velDiff / secondsSinceLastSync;

                if (!accel.ApproxEquals(_acceleration, ACCELERATION_COMPARISON_TOLERANCE))
                {
                    _acceleration = accel;
                    RequestPhysicsTerseUpdate();
                    //m_log.DebugFormat("Avatar Terse Vel: {0} Accel: {1} Sync: {2}", _velocity, _acceleration, secondsSinceLastSync);
                    //m_log.DebugFormat("Vel Breakdown: vGravity {0} vForces {1} vTarget {2} vColl {3}", _vGravity, _vForces, this.VTargetWithRun, vColl);
                }
            }
        }

        private void DecayForces(float secondsSinceLastSync)
        {
            if (_vForces != OpenMetaverse.Vector3.Zero) // Do I have any forces to decay?
            {
                if (_vTarget != OpenMetaverse.Vector3.Zero) // Has user input?
                {

                    if (!_flying)
                    {
                        //user movement instantly cancels any x or y axis movement, but
                        //it does not cancel z axis movement while jumping. This allows the user to have
                        //a nice jump while walking
                        _vForces.X = 0f;
                        _vForces.Y = 0f;
                    }
                    else
                    {
                        _vForces = OpenMetaverse.Vector3.Zero;
                    }
                }
                else // No user input found.
                {
                    //decay velocity in relation to velocity to badly mimic drag
                    OpenMetaverse.Vector3 decayForce;
                    if (_collidingGround)
                    {
                        decayForce = OpenMetaverse.Vector3.Multiply(_vForces, 2.0f * secondsSinceLastSync);
                    }
                    else
                    {
                        decayForce = OpenMetaverse.Vector3.Multiply(_vForces, 1.0f * secondsSinceLastSync);
                    }

                    _vForces -= decayForce;

                    if (_vForces.LengthSquared() < MIN_FORCE_MAG_BEFORE_ZEROING_SQUARED)
                    {
                        _vForces = OpenMetaverse.Vector3.Zero;
                    }
                }
            }
        }

        private void AccumulateGravity(float secondsSinceLastSync)
        {
            if (_flying)
            {
                _vGravity.Z = 0.0f;
            }
            else
            {
                OpenMetaverse.Vector3 cforces = _cForcesAreLocal ? _cForces * _rotation : _cForces;

                //if we have an upward force, we need to start removing the energy from that before
                //adding negative force to vGravity
                if (_vForces.Z > 0.0f)
                {
                    _vForces.Z += (BasicScene.GRAVITY + cforces.Z) * secondsSinceLastSync;
                    if (_vForces.Z < 0.0f) _vForces.Z = 0.0f;
                }
                else if (Math.Abs(_vGravity.Z) < TERMINAL_VELOCITY_GRAVITY)
                {
                    _vGravity.Z += (BasicScene.GRAVITY + cforces.Z) * secondsSinceLastSync;
                }
            }
        }

        private void CheckAvatarNotBelowGround()
        {
            float groundHeightAdjustedForAgent = _scene.TerrainChannel.CalculateHeightAt(_position.X, _position.Y) + _height * 0.5f + _radius;
            if (_position.Z < groundHeightAdjustedForAgent)
            {
                _vForces = OpenMetaverse.Vector3.Zero;
                _vGravity = OpenMetaverse.Vector3.Zero;
                _vTarget = OpenMetaverse.Vector3.Zero;

                //place the avatar above the ground
                _position.Z = groundHeightAdjustedForAgent;
                //_controller.Position = PhysUtil.OmvVectorToPhysx(_position);
            }
        }

        private OpenMetaverse.Vector3 ApplyAirBrakes(OpenMetaverse.Vector3 velocity)
        {
            if (_brakes)
            {
                // The user has said to stop.

                if (_flying || !_colliding || _vTarget == OpenMetaverse.Vector3.Zero) // We are either flying or falling straight down. (Or standing still...)
                {
                    // Possible BUG: Could not be handling the case of falling down a steeply inclined surface - whether ground or prim. Cannot test because in IW you cannot fall down a steeply inclined plane!
                    // Dead stop. HACK: In SL a little bit of gravity sneaks in anyway. The constant comes from measuring that value.
                    _vForces = OpenMetaverse.Vector3.Zero;
                    _vGravity = OpenMetaverse.Vector3.Zero;
                    velocity = new OpenMetaverse.Vector3(0.0f, 0.0f, -0.217762f);
                }
                else // We are walking or running.
                {
                    // Slow down.
                    velocity *= 0.25f; // SL seems to just about quarter walk/run speeds according to tests run on 20151217.
                }
            }

            return velocity;
        }

        private bool CalcPhysics(OpenMetaverse.Vector3 displacement, float elapsedTime, float minDist)
        {
            bool groundCollision = false;

            Vector3 newPos = _position + displacement;
            newPos.X = Util.Clip(newPos.X, 0.1f, Constants.RegionSize - 0.1f);
            newPos.Y = Util.Clip(newPos.Y, 0.1f, Constants.RegionSize - 0.1f);

            float groundHeightAdjustedForAgent = _scene.TerrainChannel.CalculateHeightAt(newPos.X, newPos.Y) + _height * 0.5f + _radius;
            if (newPos.Z < groundHeightAdjustedForAgent)
            {
                groundCollision = true;

                newPos.Z = groundHeightAdjustedForAgent;
            }
            _position = newPos;

            return groundCollision;
        }
    }
    
}
