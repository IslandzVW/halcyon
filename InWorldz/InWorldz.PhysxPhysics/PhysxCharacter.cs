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
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics
{
    class PhysxCharacter : PhysicsActor, IDisposable
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const float MAX_WALKABLE_SLOPE = 65.0f;
        private const float TERMINAL_VELOCITY_GRAVITY = 55.0f;
        private const float CHARACTER_DENSITY = 60.0f;
        private const float CONTACT_OFFSET = 0.015f;
        private const float MIN_FORCE_MAG_BEFORE_ZEROING_SQUARED = 0.00625f;
        private const float STEP_OFFSET = 0.45f;
        private const float ACCELERATION_COMPARISON_TOLERANCE = 0.006f;
        private const float MIN_RIDEON_HALF_EXTENTS = 0.625f;
        private const float POSITION_COMPARISON_TOLERANCE = 0.1f;
        private const float GRAVITY_PUSHBACK_DIFF_TOLERANCE = 0.001f;
        private const int VELOCITY_RAMPUP_TIME = 600;

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

        private PhysxScene _scene;
        private PhysX.CapsuleController _controller;

        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Quaternion _rotation;
        private OpenSim.Framework.Geom.Box _OBBobject;

        private float _height;
        private float _radius;

        private volatile bool _flying;

        private float _mass;

        /// <summary>
        /// The total resultant velocity from all the applied forces
        /// </summary>
        private OpenMetaverse.Vector3 _velocity;

        /// <summary>
        /// Target velocity set by the user (walking, flying, etc)
        /// </summary>
        private OpenMetaverse.Vector3 _vTarget;

        /// <summary>
        /// The current velocity due to gravity clamped at terminal
        /// </summary>
        private OpenMetaverse.Vector3 _vGravity;

        /// <summary>
        /// Current self-decaying forces acting on an avatar
        /// </summary>
        private OpenMetaverse.Vector3 _vForces;

        /// <summary>
        /// Current constant forces acting on an avatar
        /// </summary>
        // TODO - these two variables have to be serialized.
        // [...]
        private bool _cForcesAreLocal = false;
        private OpenMetaverse.Vector3 _cForces;

        /// <summary>
        /// Our current acceleration
        /// </summary>
        private OpenMetaverse.Vector3 _acceleration;


        private volatile bool _colliding = false;
        private volatile bool _collidingGround = false;
//        private volatile bool _collidingGroundMesh = false;

        /// <summary>
        /// Whether or not this character is frozen in place with its state intact.
        /// Used when moving a character between regions.
        /// </summary>
        private bool _suspended = false;

        uint _lastSync;

        private UserControllerHitReportDelegator _hitReportDelegator;

        private HashSet<PhysX.Shape> _collisionsTwoFramesAgo = new HashSet<PhysX.Shape>();
        private HashSet<PhysX.Shape> _collisionsLastFrame = new HashSet<PhysX.Shape>();
        private Dictionary<PhysxPrim, int> _collidingPrims = new Dictionary<PhysxPrim, int>();

        private class ExternalReport
        {
            public bool RemoveAfterReport;
            public bool Reported;
        }

        /// <summary>
        /// This keeps tract of shape collisions reported by PhysX directly that aren't the result
        /// of character controller scans
        /// </summary>
        private Dictionary<PhysX.Shape, ExternalReport> _externalCollisionReports = new Dictionary<PhysX.Shape, ExternalReport>();

        /// <summary>
        /// This keeps tract of the owners of shapes we have collided with in case they are deleted
        /// so that we can remove tracked shapes in the event that the prim gets deleted
        /// </summary>
        private Dictionary<PhysxPrim, HashSet<PhysX.Shape>> _externalCollisionPrims = new Dictionary<PhysxPrim, HashSet<PhysX.Shape>>();


        private uint _localId;

        private bool _brakes;
        private bool _running;

        private bool _disposed = false;

        private CharacterRideOnBehavior _rideOnBehavior = new CharacterRideOnBehavior();

        private OpenMetaverse.Vector4 _collisionPlane = new OpenMetaverse.Vector4(0f, 0f, 0f, 1f);

        private ulong _lastVelocityNonZero = 0;

        private static readonly PhysX.ControllerFilters FILTERS
            = new PhysX.ControllerFilters
            {
                ActiveGroups = (int)(CollisionGroupFlag.Character | CollisionGroupFlag.Ground | CollisionGroupFlag.Normal),
                FilterFlags = PhysX.SceneQueryFilterFlag.Static | PhysX.SceneQueryFilterFlag.Dynamic | PhysX.SceneQueryFilterFlag.Prefilter,
                FilterData = CollisionGroup.GetFilterData((uint)(PhysX.PairFlag.NotifyTouchFound | PhysX.PairFlag.NotifyTouchLost), 0, CollisionGroupFlag.Character)
            };

        public override bool Disposed
        {
            get
            {
                return _disposed;
            }
        }

        

        public PhysxCharacter(PhysxScene scene, float height, float radius,
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
             *   top sphere center = p.y + h*0.5
             *   bottom sphere center = p.y - h*0.5
             *   top capsule point = p.y + h*0.5 + r
             *   bottom capsule point = p.y - h*0.5 - r
             */
            _height = height;
            
            _flying = flying;

            float volume = (float)(Math.PI * Math.Pow(_radius, 2) * this.CapsuleHeight);
            _mass = CHARACTER_DENSITY * volume;

            _position = position;
            _rotation = rotation;
           
            _hitReportDelegator = new UserControllerHitReportDelegator();
            _hitReportDelegator.OnShapeHitCallback += this.OnShapeHit;
            _hitReportDelegator.OnControllerHitCallback += this.OnControllerHit;

            PhysX.CapsuleControllerDesc controllerDesc = new PhysX.CapsuleControllerDesc
            {
                Height = this.CapsuleHeight,
                Radius = _radius,
                StepOffset = STEP_OFFSET,
                UpDirection = new PhysX.Math.Vector3(0.0f, 0.0f, 1.0f),
                Position = PhysUtil.OmvVectorToPhysx(position),
                Material = scene.DEFAULT_MATERIAL,
                InteractionMode = PhysX.CCTInteractionMode.Include,
                SlopeLimit = (float)Math.Cos(OpenMetaverse.Utils.DEG_TO_RAD * MAX_WALKABLE_SLOPE),
                ContactOffset = CONTACT_OFFSET,
                Callback = _hitReportDelegator,
                BehaviorCallback = _rideOnBehavior
            };

            _controller = _scene.ControllerManager.CreateController<PhysX.CapsuleController>(controllerDesc);
            _controller.Actor.UserData = this;

            DoZDepenetration();

            _controller.ShapeFilterData = CollisionGroup.GetFilterData((uint)(PhysX.PairFlag.NotifyTouchFound | PhysX.PairFlag.NotifyTouchLost),
                0, CollisionGroupFlag.Character);
            
            _lastSync = (uint)Environment.TickCount;

            _vTarget = initialVelocity;
            _velocity = initialVelocity;
            if (_vTarget != OpenMetaverse.Vector3.Zero)
            {
                //hack to continue at velocity until the controller picks up
                _lastVelocityNonZero = OpenSim.Framework.Util.GetLongTickCount() - VELOCITY_RAMPUP_TIME;
            }
        }

        private void DoZDepenetration()
        {
            PhysX.CapsuleGeometry capsule = new PhysX.CapsuleGeometry(_radius, this.CapsuleHeight / 2.0f);
            float zdepen = CalculateDepenetrationZOffset(_position, capsule, _controller.Actor.Shapes.First());

            if (zdepen > 0.0f)
            {
                _position.Z += zdepen;
                _controller.Position = PhysUtil.OmvVectorToPhysx(_position);
            }
        }

        private float CalculateDepenetrationZOffset(OpenMetaverse.Vector3 pos, PhysX.Geometry avaGeom, PhysX.Shape avaShape)
        {
            const int MAX_ITERATIONS = 8;
            const float PUSH_MULTIPLIER = 1.5F;
            float pushFactor = 0.1f;

            OpenMetaverse.Vector3 offset = OpenMetaverse.Vector3.Zero;

            bool foundOverlap = false;

            //constant from looking at the rot returned from the live avatar,
            //remember that capsules are always upright, and z rotations don't have an effect
            //on their geometry
            OpenMetaverse.Quaternion capsuleRot = new OpenMetaverse.Quaternion(0f, -0.7071069f, 0f, 0.7071067f);

            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                foundOverlap = false;
                OpenMetaverse.Vector3 translatedPose = pos + offset;
                PhysX.Shape[] overlap = _scene.SceneImpl.OverlapMultiple(avaGeom, PhysUtil.PositionToMatrix(translatedPose, capsuleRot));

                if (overlap == null)
                {
                    foundOverlap = true;
                }
                else
                {
                    foreach (var shape in overlap)
                    {
                        if (shape != avaShape && !ShapeIsVolumeDetect(shape))
                        {
                            foundOverlap = true;
                            break;
                        }
                    }
                }

                if (foundOverlap && i + 1 < MAX_ITERATIONS)
                {
                    offset += new OpenMetaverse.Vector3(0f, 0f, pushFactor);
                    pushFactor *= PUSH_MULTIPLIER;
                }
                else
                {
                    break;
                }
            }

            if (foundOverlap == false && offset != OpenMetaverse.Vector3.Zero)
            {
                return offset.Z;
            }

            return 0.0f;
        }

        private bool ShapeIsVolumeDetect(PhysX.Shape shape)
        {
            return (shape.Flags & PhysX.ShapeFlag.TriggerShape) != 0;
        }

        private float CapsuleHeight
        {
            get
            {
                return _height - (_radius * 2.0f);
            }
        }

        public override bool Stopped
        {
            get { return false; }
        }

        public override OpenMetaverse.Vector3 Size
        {
            get
            {
                return new OpenMetaverse.Vector3(_radius*2.0f, _radius*2.0f, _height);
            }
            set
            {
                _scene.QueueCommand(new Commands.GenericSyncCmd(
                    (PhysxScene scene) =>
                        {
                            if (_height != value.Z)
                            {
                                _height = value.Z;
                                _controller.Height = this.CapsuleHeight;
                                DoZDepenetration();
                            }
                        }
                ));
            }
        }

        public override OpenSim.Framework.PrimitiveBaseShape Shape
        {
            set {  }
            get { return null; }
        }

        public override uint LocalID
        {
            set { _localId = value; }
            get { return _localId; }
        }

        public override OpenMetaverse.UUID Uuid
        {
            get
            {
                return OpenMetaverse.UUID.Zero;
            }
            set
            {
                
            }
        }

        public override bool Grabbed
        {
            set {  }
        }

        public override bool Selected
        {
            set {  }
            get { return false; }
        }

        public override void CrossingFailure()
        {
            
        }

        public override void ForceAboveParcel(float height)
        {

        }

        public override void DelinkFromParent(OpenMetaverse.Vector3 newWorldPos, OpenMetaverse.Quaternion newWorldRot)
        {
            
        }

        public override OpenMetaverse.Vector3 GetLockedAngularMotion()
        {
            return OpenMetaverse.Vector3.Zero;
        }

        public override void LockAngularMotion(OpenMetaverse.Vector3 axis)
        {
            
        }

        public override OpenMetaverse.Vector3 Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;

                _scene.QueueCommand(
                    new Commands.GenericSyncCmd((PhysxScene scene) =>
                        {
                            _position = value;
                            _controller.Position = PhysUtil.OmvVectorToPhysx(value);
                            DoZDepenetration();
                            RequestPhysicsTerseUpdate();
                        }
                ));
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
                return _velocity * _mass;
            }
        }

        public override OpenMetaverse.Vector3 ConstantForce
        {
            get
            {
                return _cForces;
            }
        }

        public override bool ConstantForceIsLocal
        {
            get
            {
                return _cForcesAreLocal;
            }
        }

        public override OpenSim.Framework.Geom.Box OBBobject
        {
            get
            {
                return _OBBobject;
            }
            set
            {
                _OBBobject = value;
            }
        }

        public override void SetVolumeDetect(bool param)
        {
            
        }

        public override OpenMetaverse.Vector3 GeometricCenter
        {
            get { return new OpenMetaverse.Vector3(); }
        }

        public override OpenMetaverse.Vector3 CenterOfMass
        {
            get { return new OpenMetaverse.Vector3(); }
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
            get
            {
                return new OpenMetaverse.Vector3();
            }
            set
            {
                
            }
        }

        public override float CollisionScore
        {
            get
            {
                return 0;
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
                return _rotation;
            }
            set
            {
                _rotation = value;
                //m_log.DebugFormat("[PhysxCharacter] new rot={0}", value);
            }
        }

        public override ActorType PhysicsActorType
        {
            get
            {
                return ActorType.Agent;
            }
        }

        public override bool IsPhysical
        {
            get
            {
                return true;
            }
        }

        public override bool Flying
        {
            get
            {
                return _flying;
            }
            set
            {
                _flying = value;
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
                return _colliding;
            }
            set
            {
                
            }
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

        public override bool FloatOnWater
        {
            set {  }
        }

        public override OpenMetaverse.Vector3 AngularVelocity
        {
            get
            {
                return new OpenMetaverse.Vector3();
            }
            set
            {

            }
        }

        public override OpenMetaverse.Vector3 AngularVelocityTarget
        {
            get
            {
                return new OpenMetaverse.Vector3();
            }
            set
            {

            }
        }

        public override float Buoyancy
        {
            get
            {
                return 0;
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

            /*
            float x = (float)Math.Pow(2.0, Math.Log(Math.Abs(baseTarget.X) * (((double)accelerationTime / VELOCITY_RAMPUP_TIME)))) * Math.Sign(baseTarget.X);
            float y = (float)Math.Pow(2.0, Math.Log(Math.Abs(baseTarget.Y) * (((double)accelerationTime / VELOCITY_RAMPUP_TIME)))) * Math.Sign(baseTarget.Y);
            float z = (float)Math.Pow(2.0, Math.Log(Math.Abs(baseTarget.Z) * (((double)accelerationTime / VELOCITY_RAMPUP_TIME)))) * Math.Sign(baseTarget.Z);
            */

            //linear ramp
            OpenMetaverse.Vector3 result = baseTarget * ((float)accelerationTime / VELOCITY_RAMPUP_TIME);

            //m_log.DebugFormat("[CHAR]: {0}", result);

            return result;
        }

        public static readonly OpenMetaverse.Vector4 NoCollPlane = new OpenMetaverse.Vector4(0f, 0f, 0f, 1f);
        public override OpenMetaverse.Vector4 CollisionPlane
        {
            get
            {
                if (CollidingGround)
                {
                    return _collisionPlane;
                }
                else
                {
                    return NoCollPlane;
                }
            }
        }

        public override void AddForce(OpenMetaverse.Vector3 force, ForceType ftype)
        {
            _scene.QueueCommand(new Commands.AddForceCmd(this, force, OpenMetaverse.Vector3.Zero, ftype));
        }

        public override void AddAngularForce(OpenMetaverse.Vector3 force, ForceType ftype)
        {
            
        }

        public override void SubscribeCollisionEvents(int ms)
        {
            
        }

        public override void UnSubscribeEvents()
        {
            
        }

        public override bool SubscribedEvents()
        {
            return false;
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
            //m_log.DebugFormat("[CHAR]: vGrav: {0}, vForces: {1}, cForces: {2}, vTarget {3}", _vGravity, _vForces, cforces, this.VTargetWithRunAndRamp);

            if (vCombined == OpenMetaverse.Vector3.Zero) 
            {
                SetVelocityAndRequestTerseUpdate(secondsSinceLastSync, OpenMetaverse.Vector3.Zero);
                ReportCollisionsFromLastFrame(frameNum);
                return;
            }

            OpenMetaverse.Vector3 lastPosition = _position;
            PhysX.ControllerFlag flags = _controller.Move(PhysUtil.OmvVectorToPhysx(vCombined), TimeSpan.FromSeconds(secondsSinceLastSync), 0.001f, FILTERS);
            _position = PhysUtil.PhysxVectorToOmv(_controller.Position);
            _lastSync = (uint)Environment.TickCount;

            //take into account any movement not accounted for by the other calculations
            //this is due to collision
            OpenMetaverse.Vector3 vColl = (_position - lastPosition) - vCombined;
            //m_log.InfoFormat("vColl {0} {1} PosDiff: {2} Expected: {3}", vColl, flags, _position - lastPosition, vCombined);
            //m_log.DebugFormat("[CHAR]: vColl: {0}", vColl);

            bool collidingDown = (flags & PhysX.ControllerFlag.Down) != 0;
            if (!collidingDown) _rideOnBehavior.AvatarNotStandingOnPrim();

            //negative z in vcoll while colliding down is due to gravity/ground collision, dont report it
            float gravityPushback = Math.Abs(_vGravity.Z) * secondsSinceLastSync;
            if (collidingDown && vColl.Z > 0 && Math.Abs(vColl.Z - gravityPushback) < GRAVITY_PUSHBACK_DIFF_TOLERANCE) vColl.Z = 0;
            //m_log.DebugFormat("[CHAR]: vColl: {0} gravityPushback {1} collidingDown:{2}", vColl, gravityPushback, collidingDown);

            if (flags != 0)
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

            if (frameNum % 3 == 0)
            {
                CheckAvatarNotBelowGround();
            }

            SetVelocityAndRequestTerseUpdate(secondsSinceLastSync, vColl);
            ReportCollisionsFromLastFrame(frameNum);

            if (!_position.ApproxEquals(lastPosition, POSITION_COMPARISON_TOLERANCE))
            {
                RequestPhysicsPositionUpdate();
            }
        }

        private void CheckAvatarNotBelowGround()
        {
            float groundHeight = _scene.TerrainChannel.CalculateHeightAt(_position.X, _position.Y);
            if (_position.Z < groundHeight)
            {
                _vForces = OpenMetaverse.Vector3.Zero;
                _vGravity = OpenMetaverse.Vector3.Zero;
                _vTarget = OpenMetaverse.Vector3.Zero;

                //place the avatar a decimeter above the ground
                _position.Z = groundHeight + (_height / 2.0f) + 0.1f;
                _controller.Position = PhysUtil.OmvVectorToPhysx(_position);
            }
        }

        private void ReportCollisionsFromLastFrame(uint frameNum)
        {
            IList<PhysX.Shape> shapesThatNeedDelete = DumpContinuingExternalCollisionsToLastFrame();

            //try to optimize the common case where the collision set hasnt changed
            if (_collisionsLastFrame.Count == _collisionsTwoFramesAgo.Count && 
                _collisionsLastFrame.SetEquals(_collisionsTwoFramesAgo) &&
               ( shapesThatNeedDelete == null || shapesThatNeedDelete.Count == 0))
            {
                ReportContinuingCollisionList(frameNum, _collisionsLastFrame);
                _collisionsLastFrame.Clear();
                return;
            }

            IEnumerable<PhysX.Shape> continuingCollisions = _collisionsLastFrame.Intersect(_collisionsTwoFramesAgo);
            IEnumerable<PhysX.Shape> newCollisions = _collisionsLastFrame.Except(_collisionsTwoFramesAgo);
            IEnumerable<PhysX.Shape> endedCollisions = _collisionsTwoFramesAgo.Except(_collisionsLastFrame);

            ReportNewCollisions(newCollisions);
            ReportNewCollisions(shapesThatNeedDelete);

            ReportEndedCollisions(endedCollisions);
            ReportEndedCollisions(shapesThatNeedDelete);

            if (shapesThatNeedDelete != null)
            {
                foreach (PhysX.Shape shape in shapesThatNeedDelete)
                {
                    RemoveExternalCollidingPrimShape(shape, (PhysxPrim)shape.Actor.UserData);
                }
            }

            ReportContinuingCollisionList(frameNum, continuingCollisions);

            SendCollisionUpdate(new CollisionEventUpdate { Type = CollisionEventUpdateType.CharacterCollisionsChanged });

            HashSet<PhysX.Shape> oldTwoFramesAgo = _collisionsTwoFramesAgo;
            oldTwoFramesAgo.Clear();

            _collisionsTwoFramesAgo = _collisionsLastFrame;
            _collisionsLastFrame = oldTwoFramesAgo;
        }

        private void ReportEndedCollisions(IEnumerable<PhysX.Shape> endedCollisions)
        {
            if (endedCollisions == null) return;

            foreach (PhysX.Shape shape in endedCollisions)
            {
                PhysxPrim primActor = shape.Actor.UserData as PhysxPrim;
                if (primActor != null)
                {
                    if (primActor.Properties.WantsCollisionNotification || primActor.Properties.ChildrenWantCollisionNotification)
                    {
                        primActor.OnCharacterContactChangeSync(shape, this, CollisionEventUpdateType.CollisionEnded);
                    }

                    //send the collision update to the scene presence
                    SendCollisionUpdate(new CollisionEventUpdate
                    {
                        Type = CollisionEventUpdateType.CollisionEnded,
                        OtherColliderLocalId = primActor.LocalID,
                        OtherColliderUUID = primActor.Uuid
                    });

                    int count;
                    if (_collidingPrims.TryGetValue(primActor, out count))
                    {
                        --count;
                        if (count == 0)
                        {
                            _collidingPrims.Remove(primActor);
                        }
                        else
                        {
                            _collidingPrims[primActor] = count;
                        }
                    }
                }
                else
                {
                    //terrain?
                    if (shape.Actor.UserData is TerrainManager)
                    {
                        //send the collision update to the scene presence
                        SendCollisionUpdate(new CollisionEventUpdate
                        {
                            Type = CollisionEventUpdateType.LandCollisionEnded,
                            CollisionLocation = _position
                        });
                    }
                    else
                    {
                        //character?
                        PhysxCharacter otherChar = shape.Actor.UserData as PhysxCharacter;
                        if (otherChar != null)
                        {
                            //send the collision update to the scene presence
                            SendCollisionUpdate(new CollisionEventUpdate
                            {
                                Type = CollisionEventUpdateType.CharacterCollisionEnded,
                                OtherColliderLocalId = otherChar.LocalID
                            });
                        }
                    }
                }
            }
        }

        private void ReportNewCollisions(IEnumerable<PhysX.Shape> newCollisions)
        {
            if (newCollisions == null) return;

            foreach (PhysX.Shape shape in newCollisions)
            {
                PhysxPrim primActor = shape.Actor.UserData as PhysxPrim;
                if (primActor != null)
                {
                    if (primActor.Properties.WantsCollisionNotification || primActor.Properties.ChildrenWantCollisionNotification)
                    {
                        primActor.OnCharacterContactChangeSync(shape, this, CollisionEventUpdateType.CollisionBegan);
                    }

                    //send the collision update to the scene presence
                    SendCollisionUpdate(new CollisionEventUpdate
                    {
                        Type = CollisionEventUpdateType.CollisionBegan,
                        OtherColliderLocalId = primActor.LocalID,
                        OtherColliderUUID = primActor.Uuid
                    });

                    int count;
                    if (_collidingPrims.TryGetValue(primActor, out count))
                    {
                        _collidingPrims[primActor] = ++count;
                    }
                    else
                    {
                        _collidingPrims.Add(primActor, 1);
                    }
                }
                else
                {
                    if (shape.Actor.UserData is TerrainManager)
                    {
                        //send the collision update to the scene presence
                        SendCollisionUpdate(new CollisionEventUpdate
                        {
                            Type = CollisionEventUpdateType.LandCollisionBegan,
                            CollisionLocation = _position
                        });
                    }
                    else
                    {
                        //character?
                        PhysxCharacter otherChar = shape.Actor.UserData as PhysxCharacter;
                        if (otherChar != null)
                        {
                            //send the collision update to the scene presence
                            SendCollisionUpdate(new CollisionEventUpdate
                            {
                                Type = CollisionEventUpdateType.CharacterCollisionBegan,
                                OtherColliderLocalId = otherChar.LocalID
                            });
                        }
                    }

                }
            }
        }

        private void ReportContinuingCollisionList(uint frameNum, IEnumerable<PhysX.Shape> continuingCollisions)
        {
            if (frameNum % 3 == 0 || frameNum % 4 == 0)
            {
                foreach (PhysX.Shape shape in continuingCollisions)
                {
                    PhysxPrim primActor = shape.Actor.UserData as PhysxPrim;
                    if (primActor != null)
                    {
                        //send the collision update to the scene presence
                        SendCollisionUpdate(new CollisionEventUpdate
                        {
                            Type = CollisionEventUpdateType.CollisionContinues,
                            OtherColliderLocalId = primActor.LocalID,
                            OtherColliderUUID = primActor.Uuid
                        });
                    }
                    else
                    {
                        TerrainManager terrainMgr = shape.Actor.UserData as TerrainManager;
                        if (terrainMgr != null)
                        {
                            //send the ground collision update to the scene presence
                            SendCollisionUpdate(new CollisionEventUpdate
                            {
                                Type = CollisionEventUpdateType.LandCollisionContinues,
                                CollisionLocation = _position
                            });
                        }
                        else
                        {
                            PhysxCharacter otherChar = shape.Actor.UserData as PhysxCharacter;
                            if (otherChar != null)
                            {
                                //send the ground collision update to the scene presence
                                SendCollisionUpdate(new CollisionEventUpdate
                                {
                                    Type = CollisionEventUpdateType.CharacterCollisionContinues,
                                    OtherColliderLocalId = otherChar.LocalID
                                });
                            }
                        }
                    }

                }
            }
        }

        private IList<PhysX.Shape> DumpContinuingExternalCollisionsToLastFrame()
        {
            Lazy<List<PhysX.Shape>> needsDeleteShapes = new Lazy<List<PhysX.Shape>>();
            foreach (var shapeReportKVP in _externalCollisionReports)
            {
                shapeReportKVP.Value.Reported = true;

                if (shapeReportKVP.Value.RemoveAfterReport)
                {
                    needsDeleteShapes.Value.Add(shapeReportKVP.Key);
                }
                else
                {
                    _collisionsLastFrame.Add(shapeReportKVP.Key);
                }
            }
            
            if (needsDeleteShapes.IsValueCreated)
            {
                return needsDeleteShapes.Value;
            }
            else
            {
                return null;
            }
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
            if (_vForces != OpenMetaverse.Vector3.Zero)
            {
                if (_vTarget != OpenMetaverse.Vector3.Zero)
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
                else
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
                    _vForces.Z += (Settings.Instance.Gravity + cforces.Z) * secondsSinceLastSync;
                    if (_vForces.Z < 0.0f) _vForces.Z = 0.0f;
                }
                else if (Math.Abs(_vGravity.Z) < TERMINAL_VELOCITY_GRAVITY)
                {
                    _vGravity.Z += (Settings.Instance.Gravity + cforces.Z) * secondsSinceLastSync;
                }
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

        public override void AddForceSync(OpenMetaverse.Vector3 Force, OpenMetaverse.Vector3 forceOffset, ForceType type)
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

        public override void LinkToNewParent(PhysicsActor obj, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot)
        {
            throw new NotImplementedException();
        }

        public override void UpdateOffsetPosition(OpenMetaverse.Vector3 newOffset, OpenMetaverse.Quaternion rotOffset)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                InformCollidersOfRemoval();
                _controller.InvalidateCache();
                _scene.RemoveCharacterSync(this);
                _controller.Dispose();
                _hitReportDelegator.Dispose();

                _disposed = true;
            }
        }

        private void InformCollidersOfRemoval()
        {
            foreach (var colliderKvp in _collidingPrims)
            {
                colliderKvp.Key.ContactedCharacterDeleted(this);
            }
        }

        private void OnShapeHit(PhysX.ControllerShapeHit hit)
        {
            PhysicsActor otherActor = hit.Shape.Actor.UserData as PhysicsActor;

            UpdateCollisionPlane(hit);

            if (otherActor != null)
            {
                _collisionsLastFrame.Add(hit.Shape);

                if (otherActor.IsPhysical && 
                    otherActor.PhysicsActorType == ActorType.Prim && 
                    ((PhysxPrim)otherActor).CollisionGrp != CollisionGroupFlag.PhysicalPhantom)
                {
                    KickOrRideOnPrim(hit, otherActor);
                }
                else if (hit.Direction.Z != 0)
                {
                    _rideOnBehavior.AvatarNotStandingOnPrim();
                }
            }
            else if (hit.Shape.Actor.UserData is TerrainManager)
            {
                //ground
                _collisionsLastFrame.Add(hit.Shape);
                _rideOnBehavior.AvatarNotStandingOnPrim();
            }
        }


        static readonly TimeSpan MIN_TIMESTEP_TS = TimeSpan.FromSeconds(MIN_TIMESTEP);
        /// <summary>
        /// Called by physx when this controller runs into another one
        /// </summary>
        /// <param name="hit"></param>
        private void OnControllerHit(PhysX.ControllersHit hit)
        {
            hit.Other.Move(hit.Direction * hit.Length, MIN_TIMESTEP_TS);
            _collisionsLastFrame.Add(hit.Other.Actor.Shapes.First());

            //let the other controller know we collided with them
            ((PhysxCharacter)hit.Other.Actor.UserData).HitByOtherCharacter(this, _controller.Actor.Shapes.First());
        }

        /// <summary>
        /// Called by another PhysxCharacter when it collides with this one
        /// </summary>
        /// <param name="physxCharacter"></param>
        /// <param name="shape"></param>
        private void HitByOtherCharacter(PhysxCharacter physxCharacter, PhysX.Shape shape)
        {
            _collisionsLastFrame.Add(shape);
        }


        private void KickOrRideOnPrim(PhysX.ControllerShapeHit hit, PhysicsActor otherActor)
        {
            PhysxPrim otherPrimActor = (PhysxPrim)otherActor;
            //m_log.InfoFormat("dir: {0} {1} {2}", hit.Direction, hit.WorldNormal, hit.WorldPosition);

            float coeff;
            if (hit.Direction.Z != 0)
            {
                if (otherPrimActor.PhysxExtents.X > MIN_RIDEON_HALF_EXTENTS || otherPrimActor.PhysxExtents.Y > MIN_RIDEON_HALF_EXTENTS ||
                    otherPrimActor.PhysxExtents.Z > MIN_RIDEON_HALF_EXTENTS)
                {
                    _rideOnBehavior.AvatarStandingOn(otherPrimActor);
                }
                else
                {
                    _rideOnBehavior.AvatarNotStandingOnPrim();
                }

                coeff = _mass * Math.Abs(Settings.Instance.Gravity) * MIN_TIMESTEP;
            }
            else
            {
                if (_rideOnBehavior.IsRideOnPrim(otherPrimActor))
                {
                    //dont kick the thing we're riding on
                    coeff = 0;
                }
                else
                {
                    //push the object out of the way at the appropriate rate
                    const float OTHER_PUSH_MULT = 100.0f;
                    coeff = _mass * hit.Length * OTHER_PUSH_MULT;
                }
            }

            //OpenMetaverse.Vector3.Negate(PhysUtil.PhysxVectorToOmv(hit.WorldNormal))

            if (coeff != 0.0f)
            {
                OpenMetaverse.Vector3 hitVector = PhysUtil.PhysxVectorToOmv(hit.WorldPosition);
                if (hit.Direction.Z == 0)
                {
                    //put the hit down the legs
                    hitVector.Z -= _height / 3.0f;
                }

                otherActor.AddForceSync(PhysUtil.PhysxVectorToOmv(hit.Direction) * coeff, hitVector, ForceType.GlobalLinearImpulse);
            }
        }

        private void UpdateCollisionPlane(PhysX.ControllerShapeHit hit)
        {
            if (hit.Direction == -PhysX.Math.Vector3.UnitZ)
            {
                var omvNorm = PhysUtil.PhysxVectorToOmv(hit.WorldNormal);

                //we're colliding down, the collision normal should never have a negative Z
                if (omvNorm.Z < 0)
                {
                    omvNorm = OpenMetaverse.Vector3.UnitZ;
                }

                OpenMetaverse.Vector4 collPlane = new OpenMetaverse.Vector4(omvNorm,
                    OpenMetaverse.Vector3.Dot(PhysUtil.PhysxVectorToOmv(hit.WorldPosition), omvNorm)
                );

                //m_log.InfoFormat("ColPlane: WorldNormal: {0}, WorldPosition: {1}", hit.WorldNormal, hit.WorldPosition);

                _collisionPlane = collPlane;
            }
        }

        public override void GatherTerseUpdate(out OpenMetaverse.Vector3 position, out OpenMetaverse.Quaternion rotation, out OpenMetaverse.Vector3 velocity,
            out OpenMetaverse.Vector3 acceleration, out OpenMetaverse.Vector3 angularVelocity)
        {
            position = _position;
            rotation = _rotation;
            velocity = _velocity;
            acceleration = _acceleration;
            angularVelocity = OpenMetaverse.Vector3.Zero;
        }

        /// <summary>
        /// Called when a prim actor shape changes or an actor is deleted.
        /// This invalidates the controller cache to prevent PhysX crashes
        /// due to bugs
        /// </summary>
        /// <param name="prim"></param>
        internal void InvalidateControllerCacheIfContacting(PhysxPrim prim)
        {
            if (_collidingPrims.ContainsKey(prim))
            {
                _controller.InvalidateCache();
            }
        }

        /// <summary>
        /// Called by physx collision handling when this character has changed contact with a prim and this change was
        /// not triggered by the character controller (which keeps objects physically separated when reporting contacts)
        /// 
        /// This means that when this method is called it is the result of a prim hitting a character, not the other way around
        /// </summary>
        /// <param name="contactPairHeader">PhysX pair header</param>
        /// <param name="pairs">PhysX contact pairs</param>
        /// <param name="pairNumber">The index number where theis character appears in the pair</param>
        internal void OnContactChangeSync(PhysX.ContactPairHeader contactPairHeader, PhysX.ContactPair[] pairs, int ourActorIndex)
        {
            if ((contactPairHeader.Flags & PhysX.ContactPairHeaderFlag.DeletedActor0) != 0 ||
                (contactPairHeader.Flags & PhysX.ContactPairHeaderFlag.DeletedActor1) != 0)
            {
                return;
            }

            foreach (var pair in pairs)
            {
                PhysX.Shape shape0 = pair.Shapes[0];
                PhysX.Shape shape1 = pair.Shapes[1];
                if ((shape0 == null) || (shape1 == null))
                    continue;

                PhysX.Shape otherShape;
                if (shape0.Actor != _controller.Actor)
                {
                    otherShape = shape0;
                }
                else
                {
                    otherShape = shape1;
                }

                //m_log.DebugFormat("[CHAR]: Collision: {0}", pair.Events);

                PhysxPrim colPrim = otherShape.Actor.UserData as PhysxPrim;
                if (colPrim != null)
                {
                    if ((pair.Events & PhysX.PairFlag.NotifyTouchFound) != 0)
                    {
                        AddExternalCollidingPrimShape(otherShape, colPrim);
                    }
                    else if ((pair.Events & PhysX.PairFlag.NotifyTouchLost) != 0)
                    {
                        SetToRemoveAfterReport(otherShape, colPrim);
                    }
                }
            }
        }

        private void SetToRemoveAfterReport(PhysX.Shape otherShape, PhysxPrim colPrim)
        {
            ExternalReport report;
            if (_externalCollisionReports.TryGetValue(otherShape, out report))
            {
                if (report.Reported)
                {
                    //this collision was reported already. remove it
                    RemoveExternalCollidingPrimShape(otherShape, colPrim);
                }
                else
                {
                    //this collision hasn't been reported yet. make sure the 
                    //collision processor knows to remove it after it is reported
                    report.RemoveAfterReport = true;
                }
            }
        }

        private void RemoveExternalCollidingPrimShape(PhysX.Shape otherShape, PhysxPrim colPrim)
        {
            _externalCollisionReports.Remove(otherShape);

            HashSet<PhysX.Shape> primShapes;
            if (_externalCollisionPrims.TryGetValue(colPrim, out primShapes))
            {
                primShapes.Remove(otherShape);
                if (primShapes.Count == 0)
                {
                    _externalCollisionPrims.Remove(colPrim);
                    colPrim.OnDeleted -= colPrim_OnDeleted;
                }
            }
        }
        
        private void AddExternalCollidingPrimShape(PhysX.Shape otherShape, PhysxPrim colPrim)
        {
            _externalCollisionReports[otherShape] = new ExternalReport { RemoveAfterReport = false, Reported = false };

            HashSet<PhysX.Shape> primShapes;
            if (!_externalCollisionPrims.TryGetValue(colPrim, out primShapes))
            {
                primShapes = new HashSet<PhysX.Shape>();
                _externalCollisionPrims.Add(colPrim, primShapes);
                colPrim.OnDeleted += colPrim_OnDeleted;
            }

            primShapes.Add(otherShape);
        }

        /// <summary>
        /// Called when one of the prims we're doing external tracking on is deleted or makes
        /// a shape/state change and our collision data becomes invalid
        /// </summary>
        /// <param name="obj"></param>
        void colPrim_OnDeleted(PhysxPrim obj)
        {
            HashSet<PhysX.Shape> shapes;
            if (_externalCollisionPrims.TryGetValue(obj, out shapes))
            {
                foreach (var shape in shapes)
                {
                    _externalCollisionReports.Remove(shape);
                }

                _externalCollisionPrims.Remove(obj);
            }
        }

        public override void Suspend()
        {
            _scene.QueueCommand(new Commands.GenericSyncCmd(
                (PhysxScene scene) =>
                {
                    _suspended = true;
                }
            ));
        }

        public override void Resume(bool interpolate, AfterResumeCallback callback)
        {
            _scene.QueueCommand(new Commands.GenericSyncCmd(
                (PhysxScene scene) =>
                {
                    _suspended = false;
                    if (callback != null) callback();
                }
            ));
        }
    }
}
