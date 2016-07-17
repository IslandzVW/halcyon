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
// This file contains functions that are part of the basic functionality of a physical 
// primitive object or group.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Physics.Manager.Vehicle;
using OpenSim.Framework;
using System.Threading;
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics
{
    internal partial class PhysxPrim : PhysicsActor, IDisposable
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Tolerance for a change in acceleration before a terse update is generated
        /// </summary>
        private const float ACCEL_COMPARISON_TOLERANCE = 0.03f; //3%

        /// <summary>
        /// Tolerance for a change in angular velocity before a terse update is generated
        /// </summary>
        private const float ANGULAR_VELOCITY_COMPARISON_TOLERANCE = 0.05f; //5%

        private const float POS_ANGULAR_VEL_TOLERANCE = 0.01f;
        
        /// <summary>
        /// The maximum number of convex hulls that is allowed in a dynamic object
        /// </summary>
        public const int MAX_DYNAMIC_COMPLEXITY = 256;

        /// <summary>
        /// The minimum physical size for a dimension in a dynamic object. If a dynamic object
        /// has more than 1 dimension smaller than this number, it will not be allowed to become
        /// dynamic
        /// </summary>
        public const float MIN_SIZE_FOR_DYNAMIC = 0.009f;
        
        /// <summary>
        /// Percentage error by which we allow the center of mass to be off in any direction
        /// before calling the object unbalanced with respect to the root prim
        /// </summary>
        private const float CENTER_OF_MASS_POS_TOLERANCE = 0.01f;


        private const float NOMINAL_FPS = 64f;

        private const ulong MIN_KINEMATIC_UPDATE_FREQ = 1 * 60 * 1000;

        private const uint TOUCH_NOTIFICATION_FLAGS = (uint)(PhysX.PairFlag.NotifyTouchFound | PhysX.PairFlag.NotifyTouchLost);

        private const uint CONTACT_NOTIFICATION_FLAGS = (uint)(PhysX.PairFlag.NotifyTouchFound | PhysX.PairFlag.NotifyTouchLost | PhysX.PairFlag.NotifyTouchPersists | PhysX.PairFlag.NotifyContactPoints);

        /// <summary>
        /// The maximum size of any dimension of an object before the object can not be made physical
        /// </summary>
        private const float MAX_PHSYICAL_DIMENSION = 128.0f;

        /// <summary>
        /// The threshold velocity at which positional velocity is used instead PhsyX velocity.
        /// This is used to dejitter objects pressed against other stationary objects or terrain.
        /// Set to 0.0 to turn this off (once PhysX is fixed).
        /// </summary>
        private const float THRESHOLD_JITTER_DETECT = 0.5f;
 
        
        /// <summary>
        /// Flags returned from the physactor velocity change checks during 
        /// simulation
        /// </summary>
        [Flags]
        private enum VelocityStatus
        {
            NoChange = 0,
            Changed = (1 << 0),
            Zeroed = (1 << 1),
            AngularChanged = (1 << 2),
            AngularPosChanged = (1 << 3)
        }

        [Flags]
        private enum DeleteActorFlags
        {
            None = 0,
            DestroyPrimaryMaterial = (1 << 0),
            UnrefChildShapes = (1 << 1),
            DestroyChildMaterials = (1 << 2),
            DestroyAll = DestroyPrimaryMaterial | UnrefChildShapes | DestroyChildMaterials
        }

        private enum ShapeCommand
        {
            KillNonGroundContacts = 1,
            DisableCCD = 2
        }

        private PhysxScene _scene;
        private PrimitiveBaseShape _pbs;
        private PhysicsShape _myShape;

        private PhysX.RigidActor _actor;
        private PhysX.RigidDynamic _dynActor;

        private uint _localId;
        private OpenMetaverse.UUID _uuid;

        private float _positiondeltaz;
        private OpenMetaverse.Vector3 _lastposition;
        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Quaternion _rotation;
        private OpenMetaverse.Vector3 _velocity;
        private OpenMetaverse.Vector3 _angularVelocity;
        private OpenMetaverse.Vector3 _acceleration;
        private OpenSim.Framework.Geom.Box _OBBobject = new OpenSim.Framework.Geom.Box(OpenMetaverse.Vector3.Zero, OpenMetaverse.Vector3.Zero);

        private float _mass;

        private bool _isPhysical;
        private int _selectCount;

        private ulong _lastKinematicUpdate = 0;

        private ulong _lastAccelVelUpdate = 0;
        bool _wasSleeping = false;


        /// <summary>
        /// The root prim that we're linked to, or null if it is ourselves
        /// </summary>
        private PhysxPrim _parentPrim = null;

        /// <summary>
        /// The physics shapes that are part of our "prim"
        /// </summary>
        private List<PhysX.Shape> _primaryShapes = new List<PhysX.Shape>();

        /// <summary>
        /// Shapes that we've absorbed from our children
        /// </summary>
        private Dictionary<PhysxPrim, RelatedShapes> _childShapes = new Dictionary<PhysxPrim, RelatedShapes>();

        /// <summary>
        /// An index of all PhysX shapes and the parent prim they belong to
        /// </summary>
        private Dictionary<PhysX.Shape, PhysxPrim> _shapeToPrimIndex = new Dictionary<PhysX.Shape, PhysxPrim>();

        /// <summary>
        /// Cached pose for the center of mass 
        /// </summary>
        private Pose _centerOfMassLocalPose;
        private bool _centerOfMassNearlyZero;

        /// <summary>
        /// Cached intertia tensor for this shape
        /// </summary>
        private OpenMetaverse.Vector3 _massSpaceInertiaTensor;

        /// <summary>
        /// Keeps tract of the number of touch points for this prim to other prims
        /// </summary>
        private Lazy<Dictionary<PhysxPrim, int>> _touchCounts = new Lazy<Dictionary<PhysxPrim, int>>();

        /// <summary>
        /// Keeps tract of the number of touch points for this prim to the ground
        /// </summary>
        private int _groundTouchCounts = 0;

        /// <summary>
        /// Keeps tract of the number of touch points for this prim to characters
        /// </summary>
        private Lazy<Dictionary<PhysxCharacter, HashSet<PhysX.Shape>>> _avatarTouchCounts = new Lazy<Dictionary<PhysxCharacter, HashSet<PhysX.Shape>>>();

        /// <summary>
        /// Keeps tract of the number of child prims that want collision notifications
        /// </summary>
        private int _numChildPrimsWantingCollisionNotification;

        /// <summary>
        /// For IDisposable
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Provides consistency to a read or write of terse update properties 
        /// </summary>
        private object _terseConsistencyLock = new object();

        /// <summary>
        /// Whether or not the angular velocity of this body should be used
        /// in the terse updates going to the client
        /// </summary>
        private bool _useAngularVelocity = true;

        /// <summary>
        /// Whether or not this prim is frozen in place with its state in tact.
        /// Used when moving a physactor between regions
        /// </summary>
        private bool _suspended = false;

        /// <summary>
        /// The time physics was suspended
        /// </summary>
        private ulong _suspendedOn = 0;

        /// <summary>
        /// How many more frames we should continue killing contacts
        /// </summary>
        private uint _contactsKilledFor = 0;

        /// <summary>
        /// How much longer CCD is suspended for this prim (0 = not suspended)
        /// </summary>
        private uint _ccdSuspendedFor = 0;

        /// <summary>
        /// Prims we're watching for deletions. Tracked because we need to unbind from their handlers if we're deleted
        /// </summary>
        private Lazy<List<PhysxPrim>> _primsBeingWatchedForDeletes = new Lazy<List<PhysxPrim>>();

        /// <summary>
        /// Vehicle simulation entrypoint
        /// </summary>
        private Vehicle.VehicleDynamics _vehicleDynamics;


        internal Dictionary<PhysxPrim, RelatedShapes> ChildShapes
        {
            get { return _childShapes; }
        }

        /// <summary>
        /// Returns the PhysX dynamic actor for this prim
        /// </summary>
        internal PhysX.RigidDynamic DynActorImpl
        {
            get { return _dynActor; }
        }

        /// <summary>
        /// Returns the primary physics shape for this actor
        /// </summary>
        internal PhysicsShape PhyShape
        {
            get { return _myShape; }
        }

        /// <summary>
        /// Indicates whether or not this prim actually is in charge of an actor.
        /// Prims lose their physical actor shapes when they become the child
        /// in a prim group
        /// </summary>
        internal bool HasActor
        {
            get { return _actor != null; }
        }

        /// <summary>
        /// Returns whether or not this prim is just a surrgate placeholder for
        /// a physactor parent
        /// </summary>
        internal bool IsChild
        {
            get { return _actor == null; }
        }

        /// <summary>
        /// Returns the parent of this prim if it is set. This is set when a prim
        /// has been linked to a parent 
        /// </summary>
        internal PhysxPrim Parent
        {
            get { return _parentPrim; }
            set { _parentPrim = value; }
        }

        private PhysicsProperties _properties = null;
        public override IPhysicsProperties Properties
        {
            get
            {
                return _properties;
            }
        }

        internal PhysicsProperties PhysxProperties
        {
            get
            {
                return _properties;
            }
        }

        public override bool Disposed
        {
            get
            {
                return _disposed;
            }
        }
        
        /// <summary>
        /// Returns whether or not this object wants to have gravity turned on.
        /// An object may not want gravity if it is 1.0 buoyant, or if it has
        /// a move target
        /// </summary>
        private bool WantsGravity
        {
            get
            {
                float DG = -_properties.Buoyancy + _properties.Material.GravityMultiplier;
                if (DG == 0.0 || _properties.MoveTargetTau > 0.0f || _hoverDisableGravity || _grabDisableGravity)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        private CollisionGroupFlag _collisionGroup;
        public CollisionGroupFlag CollisionGrp
        {
            get
            {
                return _collisionGroup;
            }
        }

        /// <summary>
        /// Returns the total physics complexity of this actor
        /// </summary>
        public override int TotalComplexity
        {
            get
            {
                return _shapeToPrimIndex.Count;
            }
        }

        public bool IsVolumeDetect
        {
            get
            {
                if (_shapeToPrimIndex.Count == 0)
                {
                    return false;
                }
                else
                {
                    return (_shapeToPrimIndex.First().Key.Flags & PhysX.ShapeFlag.TriggerShape) != 0;
                }
            }
        }

        public IMaterial PrimMaterial
        {
            get
            {
                lock (_properties)
                {
                    return _properties.Material;
                }
            }
        }

        internal PhysX.Math.Vector3 PhysxExtents
        {
            get
            {
                if (HasActor)
                {
                    return _actor.WorldBounds.Extents;
                }
                else
                {
                    return _parentPrim._actor.WorldBounds.Extents;
                }
            }
        }

        /// <summary>
        /// Called when this prim is deleted
        /// </summary>
        public event Action<PhysxPrim> OnDeleted;


        public PhysxPrim(PhysxScene scene, PrimitiveBaseShape baseShape, OpenMetaverse.Vector3 pos,
            OpenMetaverse.Quaternion rotation, PhysicsShape myShape, PhysX.RigidActor myActor,
            bool isPhysical, IPhysicsProperties properties, CollisionGroupFlag collisionGroup)
            : this(null, scene, baseShape, pos, rotation, myShape, myActor, isPhysical, properties, collisionGroup)
        {
            
        }

        public PhysxPrim(PhysxPrim parent, PhysxScene scene, PrimitiveBaseShape baseShape, OpenMetaverse.Vector3 pos,
            OpenMetaverse.Quaternion rotation, PhysicsShape myShape, PhysX.RigidActor myActor,
            bool isPhysical, IPhysicsProperties properties, CollisionGroupFlag collisionGroup)
        {
            _parentPrim = parent;
            _scene = scene;
            _pbs = baseShape;
            _position = pos;
            _rotation = rotation;
            _isPhysical = isPhysical;
            _properties = (PhysicsProperties)properties;
            _collisionGroup = collisionGroup;

            this.AssignActor(myActor, myShape, _isPhysical, DeleteActorFlags.None);

            if (_properties.VehicleProps != null && _properties.VehicleProps.Type != VehicleType.None)
            {
                //init dynamics
                CheckCreateVehicleDynamics();
            }
        }

        private void AssignActor(PhysX.RigidActor myActor, PhysicsShape newShape, bool isPhysical, DeleteActorFlags flags)
        {
            PhysX.RigidActor oldActor = _actor;
            PhysicsShape oldShape = null;
            
            if (newShape != null)
            {
                oldShape = _myShape;
                _myShape = newShape;
            }

            _actor = myActor;
            
            if (_actor != null && _actor is PhysX.RigidDynamic)
            {
                _dynActor = (PhysX.RigidDynamic)_actor;
                CacheMassData();
            }
            else
            {
                _dynActor = null;
            }

            bool wasPhysical = _isPhysical;
            _isPhysical = isPhysical;

            if (HasActor) _scene.SceneImpl.AddActor(_actor);       //add to physx scene
            this.DeleteActor(oldActor, oldShape, wasPhysical, flags);

            if (_actor != null)
            {
                _actor.UserData = this;
                this.IndexAndConfigurePrimaryShapes();
            }

            ClearForcesAndSendUpdate();
            ChangeGravityIfNeeded();
        }

        private void ClearForces()
        {
            _velocity = OpenMetaverse.Vector3.Zero;
            _angularVelocity = OpenMetaverse.Vector3.Zero;
            _acceleration = OpenMetaverse.Vector3.Zero;
        }

        private void ClearForcesAndSendUpdate()
        {
            ClearForces();
            RequestPhysicsTerseUpdate();
        }

        private void IndexAndConfigurePrimaryShapes()
        {
            foreach (PhysX.Shape shape in _actor.Shapes)
            {
                _shapeToPrimIndex.Add(shape, this);
                _primaryShapes.Add(shape);
            }

            CollisionGroup.SetCollisionGroup(_collisionGroup, _primaryShapes);
        }

        private void ReassignActors(PhysicsShape shape, PhysX.RigidActor actor, Dictionary<PhysxPrim, RelatedShapes> newChildShapes, bool isPhysical)
        {
            OBBobject = null;

            //all bets are off for collision data, our actor has changed. Reset.
            ClearTrackedTouches();

            DeleteActorFlags flags = DeleteActorFlags.None;
            bool wasVd = IsVolumeDetect;

            if (newChildShapes != null)
            {
                //we are reassigning our children, so existing materials are still valid, but
                //the old shapes are not
                flags |= DeleteActorFlags.UnrefChildShapes;
            }
            else
            {
                newChildShapes = _childShapes;
                _childShapes = new Dictionary<PhysxPrim, RelatedShapes>();
            }

            this.AssignActor(actor, shape, isPhysical, flags);     //reassign member vars and remove old actor if exists
            
            if (newChildShapes != null)
            {
                foreach (KeyValuePair<PhysxPrim, RelatedShapes> kvp in newChildShapes)
                {
                    this.CombineChildShape(kvp.Value.ChildShape, kvp.Key, kvp.Key.Position, kvp.Key.Rotation, true);
                }

                UpdateMassAndInertia();
            }

            //make sure this is the last step so that all of our child shapes have been registered
            if (_properties.WantsCollisionNotification) this.EnableCollisionEventsSync();

            this.DynamicsPostcheck();

            if (wasVd)
            {
                SetVolumeDetectSync(wasVd);
            }
        }

        private void ClearTrackedTouches()
        {
            if (_touchCounts.IsValueCreated)
            {
                List<KeyValuePair<PhysxPrim, int>> touches = new List<KeyValuePair<PhysxPrim, int>>(_touchCounts.Value.Count);
                
                //we need to make a copy because ProcessContactChange will modify the touchCounts collection
                //which would break iteration
                foreach (var touchKvp in _touchCounts.Value)
                {
                    touches.Add(touchKvp);
                }

                foreach (var touchKvp in touches)
                {
                    this.ProcessContactChange(-touchKvp.Value, this, touchKvp.Key);
                }

                _touchCounts.Value.Clear();
            }

            if (_avatarTouchCounts.IsValueCreated)
            {
                List<KeyValuePair<PhysxCharacter, HashSet<PhysX.Shape>>> touches = new List<KeyValuePair<PhysxCharacter, HashSet<PhysX.Shape>>>(_avatarTouchCounts.Value.Count);
                
                //we need to make a copy because ProcessContactChange will modify the touchCounts collection
                //which would break iteration
                foreach (var touchKvp in _avatarTouchCounts.Value)
                {
                    touches.Add(touchKvp);
                }

                foreach (var avTouch in touches)
                {
                    this.TerminateAvatarContacts(avTouch.Key);
                }

                _avatarTouchCounts.Value.Clear();
            }

            foreach (var childPrim in _childShapes.Keys)
            {
                childPrim.ClearTrackedTouches();
            }
        }

        private void DeleteActor(PhysX.RigidActor oldActor, PhysicsShape oldShape, bool wasPhysical, DeleteActorFlags flags)
        {
            if (oldActor != null)
            {
                if (_touchCounts.IsValueCreated)
                {
                    SendCollisionEndToContactedObjects();
                    _touchCounts.Value.Clear();
                }

                _scene.ForEachCharacter((PhysxCharacter character) =>
                {
                    character.InvalidateControllerCacheIfContacting(this);
                }
                );

                _scene.SceneImpl.RemoveActor(oldActor);
                oldActor.Dispose();

                if ((flags & DeleteActorFlags.DestroyPrimaryMaterial) != 0)
                {
                    _properties.PhysxMaterial.CheckedDispose();
                }

                if ((flags & DeleteActorFlags.DestroyChildMaterials) != 0)
                {
                    //also dispose child prim materials
                    foreach (PhysxPrim prim in _childShapes.Keys)
                    {
                        prim._properties.PhysxMaterial.CheckedDispose();
                    }
                }

                //unref our old shape
                if (oldShape != null)
                {
                    _scene.MeshingStageImpl.UnrefShape(oldShape, wasPhysical);

                    if ((flags & DeleteActorFlags.UnrefChildShapes) != 0)
                    {
                        //unref child shapes
                        foreach (RelatedShapes childShapes in _childShapes.Values)
                        {
                            _scene.MeshingStageImpl.UnrefShape(childShapes.ChildShape, wasPhysical);
                        }
                    }
                }

                //everyone watching us for delete should be informed
                Action<PhysxPrim> onDel = this.OnDeleted;
                if (onDel != null)
                {
                    onDel(this);
                    foreach (Action<PhysxPrim> act in onDel.GetInvocationList())
                    {
                        onDel -= act;
                    }
                }

                if (_primsBeingWatchedForDeletes.IsValueCreated)
                {
                    foreach (var prim in _primsBeingWatchedForDeletes.Value)
                    {
                        prim.OnDeleted -= new Action<PhysxPrim>(other_OnDeleted);
                    }

                    _primsBeingWatchedForDeletes = new Lazy<List<PhysxPrim>>();
                }

                //no matter if we're unreffing or not, the actual child shape list is now invalid becase
                //these shapes were part of our old physx actor
                _childShapes.Clear();
                _shapeToPrimIndex.Clear();
                _primaryShapes.Clear();
            }
        }

        private void SendCollisionEndToContactedObjects()
        {
            if (_touchCounts.IsValueCreated)
            {
                foreach (PhysxPrim prim in _touchCounts.Value.Keys)
                {
                    prim.ContactedActorDeleted(this);
                }
            }
        }

        private void ContactedActorDeleted(PhysxPrim deletedActor)
        {
            if (_disposed) return;

            if (this.HasActor)
            {
                this.ContactedActorDeleted(deletedActor, this);
            }
            else
            {
                _parentPrim.ContactedActorDeleted(deletedActor, this);
            }
        }

        private void ContactedActorDeleted(PhysxPrim deletedActor, PhysxPrim ourActor)
        {
            if (!_touchCounts.IsValueCreated) return;

            int touchCount;
            if (_touchCounts.Value.TryGetValue(deletedActor, out touchCount))
            {
                ProcessContactChange(-touchCount, ourActor, deletedActor);
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
                return _pbs.Scale;
            }
            set
            {
                throw new NotSupportedException("You can not set the size of a prim physactor directly. Instead, change the size of the parent prim");
            }
        }

        public override OpenSim.Framework.PrimitiveBaseShape Shape
        {
            set 
            {
                _pbs = value;
                _scene.AddPhysicsActorTaint(this, TaintType.ChangedShape);
            }
            get 
            { 
                return _pbs; 
            }
        }

        public override uint LocalID
        {
            set { _localId = value; }
            get { return _localId; }
        }

        public override OpenMetaverse.UUID Uuid
        {
            set { _uuid = value; }
            get { return _uuid; }
        }

        public override bool Grabbed
        {
            set { }
        }

        public override bool Selected
        {
            set 
            {
                if (value)
                {
                    if (Interlocked.Increment(ref _selectCount) == 1 && _isPhysical)
                    {
                        if (this.HasActor) _scene.QueueCommand(new Commands.SetKinematicCmd(this, true));
                    }
                }
                else
                {
                    int resultCount = Interlocked.Decrement(ref _selectCount);
                    if (resultCount == -1)
                    {
                        //this can happen when you delink a child part from a group,
                        //we need to reset to zero
                        resultCount = Interlocked.Increment(ref _selectCount);
                    }

                    if (resultCount == 0 && _isPhysical)
                    {
                        if (this.HasActor) _scene.QueueCommand(new Commands.SetKinematicCmd(this, false));
                    }
                }
            }

            get
            {
                return _selectCount > 0;
            }
        }

        public override void CrossingFailure()
        {
           _scene.QueueCommand(
               new Commands.GenericSyncCmd(this,
                   (PhysxScene scene) => 
                       {
                           OpenMetaverse.Vector3 newPos = _position;
                           //place this object back into the region
                           if (_position.X > Constants.RegionSize - 1)
                           {
                               newPos.X = Constants.RegionSize - 1 - _actor.WorldBounds.Extents.X;
                           }
                           else if (_position.X <= 0f)
                           {
                               newPos.X = _actor.WorldBounds.Extents.X;
                           }

                           if (_position.Y > Constants.RegionSize - 1)
                           {
                               newPos.Y = Constants.RegionSize - 1 - _actor.WorldBounds.Extents.Y;
                           }
                           else if (_position.Y <= 0f)
                           {
                               newPos.Y = _actor.WorldBounds.Extents.Y;
                           }

                           //also make sure Z is above ground
                           float groundHeight = _scene.TerrainChannel.CalculateHeightAt(newPos.X, newPos.Y);
                           if (newPos.Z - _actor.WorldBounds.Extents.Z < groundHeight)
                           {
                               newPos.Z = groundHeight + _actor.WorldBounds.Extents.Z + 0.1f;
                           }

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
                       }
            ));
        }

        public override void ForceAboveParcel(float height)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                    (PhysxScene scene) =>
                    {
                        OpenMetaverse.Vector3 newPos = _position;
                        //place this object back above the parcel
                        newPos.Z = height;

                        if (_dynActor != null)
                        {
                            _dynActor.GlobalPose = PhysUtil.PositionToMatrix(newPos, _rotation);
                        }
                    }
             ));
        }

        public override IMaterial GetMaterial()
        {
            lock (_properties)
            {
                return _properties.Material;
            }
        }
        public override void SetMaterial(OpenMetaverse.Material materialEnum, bool applyToObject)
        {
            Material material = (Material)Material.FindImpl(materialEnum);
            _scene.QueueCommand(new Commands.SetMaterialCmd(this, material, applyToObject, MaterialChanges.All));
        }

        public override void SetMaterial(IMaterial materialDesc, bool applyToObject, MaterialChanges changes)
        {
            _scene.QueueCommand(new Commands.SetMaterialCmd(this, materialDesc, applyToObject, changes));
        }

        //public override void SetMaterial(IMaterial 

        public override void LinkToNewParent(PhysicsActor obj, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot)
        {
            lock (_terseConsistencyLock)
            {
                _position = localPos;
                _rotation = localRot;
            }

            _scene.QueueCommand(new Commands.PrepChildPrimAndLinkCmd((PhysxPrim)obj, this, localPos, localRot));
        }

        public override void DelinkFromParent(OpenMetaverse.Vector3 newWorldPosition, OpenMetaverse.Quaternion newWorldRotation)
        {
            lock (_terseConsistencyLock)
            {
                _position = newWorldPosition;
                _rotation = newWorldRotation;
            }

            _scene.QueueCommand(new Commands.UnlinkFromParentCmd(this, _parentPrim, newWorldPosition, newWorldRotation));
        }

        public override OpenMetaverse.Vector3 GetLockedAngularMotion()
        {
            lock (_properties)
            {
                return _properties.LockedAxes;
            }
        }

        public override void LockAngularMotion(OpenMetaverse.Vector3 axis)
        {
            lock (_properties)
            {
                _properties.LockedAxes = axis;
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

                if (HasActor)
                {
                    _scene.QueueCommand(new Commands.SetPositionCmd(this, value));
                }
                else
                {
                    this.UpdateOffsetPosition(_position, _rotation);
                }
            }
        }

        public override float Mass
        {
            get 
            { 
                PhysX.RigidDynamic dynActor;
                if ((dynActor = _dynActor) != null)
                {
                    return _mass;
                }
                else
                {
                    return float.PositiveInfinity;
                }
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

        public override void SetVolumeDetect(bool volumeDetect)
        {
            lock (_properties)
            {
                _properties.VolumeDetectActive = volumeDetect;
            }

            _scene.QueueCommand(new Commands.ChangeVolumeDetectCmd(this, volumeDetect));
        }

        public override OpenMetaverse.Vector3 GeometricCenter
        {
            get { return new OpenMetaverse.Vector3(); }
        }

        public override OpenMetaverse.Vector3 CenterOfMass
        {
            get 
            {
                return _centerOfMassLocalPose.Position; 
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

        private OpenMetaverse.Vector3 GroupVelocity
        {
            get
            {
                if (HasActor)
                {
                    return _velocity;
                }
                else
                {
                    if (_parentPrim != null)
                    {
                        return _parentPrim._velocity;
                    }
                }

                return OpenMetaverse.Vector3.Zero;
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

                if (HasActor)
                {
                    _scene.QueueCommand(new Commands.SetRotationCmd(this, value));
                }
                else
                {
                    this.UpdateOffsetPosition(_position, _rotation);
                }
            }
        }

        internal OpenMetaverse.Quaternion RotationUnsafe
        {
            get
            {
                return _rotation;
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
                return _isPhysical;
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
                _scene.QueueCommand(new Commands.AddForceCmd(this, value, ForceType.AngularVelocityTarget));
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

                _scene.QueueCommand(new Commands.AddForceCmd(this, value, ForceType.AngularVelocityTarget));
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
                _scene.QueueCommand(new Commands.SetBuoyancyCmd(this, value));
            }
        }

        /// <summary>
        /// Returns whether or not we have a dynamic actor and that actor is not currently kinematic
        /// </summary>
        /// <returns></returns>
        private bool IsFreeDynamic
        {
            get
            {
                return _dynActor != null && (_dynActor.Flags & PhysX.RigidDynamicFlags.Kinematic) == 0;
            }
        }

        public override void AddForce(OpenMetaverse.Vector3 force, ForceType ftype)
        {
            _scene.QueueCommand(new Commands.AddForceCmd(this, force, ftype));
        }

        public override void AddAngularForce(OpenMetaverse.Vector3 force, ForceType ftype)
        {
            _scene.QueueCommand(new Commands.AddForceCmd(this, force, ftype));
        }

        public override void SubscribeCollisionEvents(int ms)
        {
            _scene.QueueCommand(new Commands.EnableCollisionEventsCmd(this, true));
        }

        public override void UnSubscribeEvents()
        {
            _scene.QueueCommand(new Commands.EnableCollisionEventsCmd(this, false));
        }

        public override bool SubscribedEvents()
        {
            return _properties.WantsCollisionNotification;
        }

        public override void DoCollisionRepeats(float timeStep, uint ticksSinceLastSimulate, uint frameNum)
        {
            //repeat collision notifications every 4 frames (7.5 fps nominal)
            if (_properties.WantsCollisionNotification)
            {
                ReportContinuingCollisions();
            }
        }

        bool _hasSimulated = false;
        public override void SyncWithPhysics(float timeStep, uint ticksSinceLastSimulate, uint frameNum)
        {
            _hasSimulated = true;

            if (_dynActor != null && _parentPrim == null)
            {
                
                if (frameNum != 0)
                {
                    if (_contactsKilledFor != 0 && _dynActor != null && (_dynActor.Flags & PhysX.RigidDynamicFlags.Kinematic) == 0)
                    {
                        if (_contactsKilledFor == CONTACTS_STAY_DEAD_FRAMES)
                        {
                            //this is the first kill contact frame, try to depenetrate
                            AddDepenetrationForce();
                        }

                        if (--_contactsKilledFor == 1)
                        {
                            //1 indicates that this is the last frame and we need to restore processing
                            RestoreContactProcessing();
                        }
                    }

                    if (_ccdSuspendedFor != 0 && _dynActor != null && (_dynActor.Flags & PhysX.RigidDynamicFlags.Kinematic) == 0)
                    {
                        if (--_ccdSuspendedFor == 1)
                        {
                            //1 indicates that this is the last frame and we need to restore processing
                            EnableCCD();
                        }
                        
                    }
                }

                bool isSleeping = _dynActor.IsSleeping;
                if ((isSleeping && _wasSleeping) || _suspended)
                {
                    //actor is still asleep, ignore
                    return;
                }

                _wasSleeping = isSleeping;

                VelocityStatus velChanged;
                OpenMetaverse.Vector3 posDiff;
                lock (_terseConsistencyLock)
                {
                    posDiff = CheckSyncPosition();
                    CheckSyncRotation();

                    if (isSleeping)
                    {
                        ClearForces();
                        velChanged = VelocityStatus.Changed;
                    }
                    else
                    {
                        velChanged = CheckSyncVelocity(posDiff);
                    }
                }

                if (velChanged != VelocityStatus.NoChange)
                {
                    //Console.Out.WriteLine("Pos {0}, Rot {1}, Vel {2}", posChanged, rotChanged, velChanged);
                    RequestPhysicsTerseUpdate();
                }

                if (timeStep > 0.0f)
                {
                    UpdateDynamicForVelocityTargets(timeStep);
                    UpdateDynamicForPIDTargets(timeStep, frameNum);   //inside PhysxPrim.pid.cs
                    UpdateDynamicForVehicleDynamics(timeStep, frameNum);
                }

                if (frameNum % (int)NOMINAL_FPS == 0)
                {
                    RequestPhysicsNeedsPersistence();
                }

                if (frameNum % 4 == 0 && posDiff != OpenMetaverse.Vector3.Zero)
                {
                    RequestPhysicsPositionUpdate();
                }
            }
        }

        private void UpdateDynamicForVehicleDynamics(float timeStep, uint frameNum)
        {
            if (_vehicleDynamics != null && IsFreeDynamic)
                _vehicleDynamics.Simulate(timeStep, frameNum);
        }

        private static Random _PrimRand = new Random();

        private void AddDepenetrationForce()
        {
            const float MIN_DEPEN_MULTIPLIER = -10.0f;
            const float MAX_DEPEN_MULTIPLIER = 10.0f;

            if (IsFreeDynamic)
            {
                PhysX.Math.Vector3 bb = _dynActor.WorldBounds.Extents * 2.0f;

                float fx = (float)_PrimRand.NextDouble() * (bb.X * MAX_DEPEN_MULTIPLIER - bb.X * MIN_DEPEN_MULTIPLIER) + bb.X * MIN_DEPEN_MULTIPLIER;
                float fy = (float)_PrimRand.NextDouble() * (bb.Y * MAX_DEPEN_MULTIPLIER - bb.Y * MIN_DEPEN_MULTIPLIER) + bb.Y * MIN_DEPEN_MULTIPLIER;
                float fz = (float)_PrimRand.NextDouble() * (bb.Z * MAX_DEPEN_MULTIPLIER - bb.Z) + bb.Z;

                PhysX.Math.Vector3 f2 = new PhysX.Math.Vector3(fx,fy,fz);

                _dynActor.AddForce(f2, PhysX.ForceMode.VelocityChange, true);
            }
        }

        public override void GatherTerseUpdate(out OpenMetaverse.Vector3 position, out OpenMetaverse.Quaternion rotation,
            out OpenMetaverse.Vector3 velocity, out OpenMetaverse.Vector3 acceleration, out OpenMetaverse.Vector3 angularVelocity)
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

        private void ReportContinuingCollisions()
        {
            if (_touchCounts.IsValueCreated && _touchCounts.Value.Count > 0)
            {
                List<uint> continuingList = new List<uint>(_touchCounts.Value.Count);
                foreach (PhysxPrim prim in _touchCounts.Value.Keys)
                {
                    if (this.GroupVelocity != OpenMetaverse.Vector3.Zero || prim._velocity != OpenMetaverse.Vector3.Zero)
                    {
                        continuingList.Add(prim._localId);
                    }
                }

                if (continuingList.Count > 0)
                {
                    CollisionEventUpdate upd = new CollisionEventUpdate { Type = CollisionEventUpdateType.BulkCollisionsContinue, BulkCollisionData = continuingList };
                    SendCollisionUpdate(upd);
                }
            }

            if (_groundTouchCounts > 0 && this.GroupVelocity != OpenMetaverse.Vector3.Zero)
            {
                OpenMetaverse.Vector3 currentLoc = DecomposeGroupPosition();

                SendCollisionUpdate(new CollisionEventUpdate { Type = CollisionEventUpdateType.LandCollisionContinues,
                    CollisionLocation = currentLoc});
            }

            if (_avatarTouchCounts.IsValueCreated && _avatarTouchCounts.Value.Count > 0)
            {
                List<uint> continuingList = new List<uint>(_avatarTouchCounts.Value.Count);
                foreach (PhysxCharacter avatar in _avatarTouchCounts.Value.Keys)
                {
                    continuingList.Add(avatar.LocalID);
                }

                if (continuingList.Count > 0)
                {
                    CollisionEventUpdate upd = new CollisionEventUpdate { Type = CollisionEventUpdateType.BulkAvatarCollisionsContinue, BulkCollisionData = continuingList };
                    SendCollisionUpdate(upd);
                }
            }
        }

        private OpenMetaverse.Vector3 DecomposeGroupPosition()
        {
            if (HasActor)
            {
                return PhysUtil.DecomposeToPosition(_actor.GlobalPose);
            }
            else
            {
                return PhysUtil.DecomposeToPosition(_parentPrim._actor.GlobalPose);
            }
        }

        private void UpdateDynamicForVelocityTargets(float timeStep)
        {
            if (IsFreeDynamic)
            {
                OpenMetaverse.Vector3 angularVelocityTarget;

                lock (_properties)
                {
                    angularVelocityTarget = _properties.AngularVelocityTarget;
                }

                if (angularVelocityTarget != OpenMetaverse.Vector3.Zero)
                {
                    //physical omegas should be relative to the object
                    angularVelocityTarget *= _rotation;

                    if (_centerOfMassLocalPose == Pose.Identity)
                    {
                        //subtract the target velocity from our current to get the difference that needs to be applied
                        OpenMetaverse.Vector3 currAngVel = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity);

                        _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(angularVelocityTarget - currAngVel), PhysX.ForceMode.VelocityChange, true);
                    }
                    else
                    {
                        OpenMetaverse.Quaternion comRot = _centerOfMassLocalPose.Rotation;
                        OpenMetaverse.Quaternion q = GetRotationFromActor() * comRot;
                        OpenMetaverse.Vector3 tensor = _massSpaceInertiaTensor;

                        OpenMetaverse.Vector3 t = (tensor * (angularVelocityTarget * OpenMetaverse.Quaternion.Inverse(q))) * q;

                        //subtract the target velocity from our current to get the difference that needs to be applied
                        OpenMetaverse.Vector3 currAngVel = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity);

                        _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(t - currAngVel), PhysX.ForceMode.VelocityChange, true);
                    }
                }

                if (_properties.Force != OpenMetaverse.Vector3.Zero)
                {
                    if (_properties.ForceIsLocal)
                    {
                        _dynActor.AddLocalForceAtLocalPosition(PhysUtil.OmvVectorToPhysx(_properties.Force * timeStep),
                            PhysUtil.OmvVectorToPhysx(_centerOfMassLocalPose.Position), PhysX.ForceMode.Impulse, true);
                    }
                    else
                    {
                        _dynActor.AddForce(PhysUtil.OmvVectorToPhysx(_properties.Force * timeStep), PhysX.ForceMode.Impulse, true);
                    }
                }

                if (this.WantsGravity)
                {
                    // Buoyancy is bascially a negative gravity multiplier where B=1.0 exerts -1G. The Delta G is then
                    // the sum of negative bouyancy plus the gravity multiplier.
                    float DG = -_properties.Buoyancy + _properties.Material.GravityMultiplier;

                    // The compensating force to exert (since gravity is enabled) is the delta G minus 1G
                    float force = (DG - 1.0f) * Settings.Instance.Gravity * _mass;

                    // If a compensating force is to be exerted, do so only while the object has very little Z-velocity or 
                    // some positional movement. Using velocity alone fails because with PhysX a high GM object jitters intensely
                    // when stopped by terrain or another static prim, and so its velocity is very high.
                    if (force != 0)
                    {
                        // Compute short term smoothed position delta.
                        _positiondeltaz = _positiondeltaz * 0.8f + (_position.Z - _lastposition.Z) * 0.2f;

                        // A higher velocity may be indicative of PhysX jitter.
                        if (Math.Abs(_dynActor.LinearVelocity.Z) > THRESHOLD_JITTER_DETECT)
                        {
                            // If the positional jitter induced by PhysX is below a threshold, then the
                            // object is beig stopped by stationary object or terrain. Lower the force
                            // to eliminate the jitter altogether, but permits movement should the obstruction
                            // move aside.
                            if (Math.Abs(_positiondeltaz) < THRESHOLD_JITTER_DETECT * timeStep)
                                force = Util.Clamp(Math.Sign(DG - 1.0f) * Settings.Instance.Gravity * _mass * 2.0f, -force, force);
                        }

                        // m_log.DebugFormat("[Buoyancy] f={0} zvel={1} dp={2} at {3} b={4} gm={5}", force, _dynActor.LinearVelocity.Z, _positiondeltaz, _position, _properties.Buoyancy, _properties.Material.GravityMultiplier);
                        _dynActor.AddForce(new PhysX.Math.Vector3(0.0f, 0.0f, force * timeStep), PhysX.ForceMode.Impulse, true);
                        _lastposition = _position;
                    }
                }
            }


        }

        private VelocityStatus CheckSyncVelocity(OpenMetaverse.Vector3 posDiff)
        {
            VelocityStatus status = VelocityStatus.NoChange;

            //check my angular and linear velocity
            OpenMetaverse.Vector3 newVel = PhysUtil.PhysxVectorToOmv(_dynActor.LinearVelocity);

            if (newVel != _velocity)
            {
                if (newVel == OpenMetaverse.Vector3.Zero)
                {
                    // we need to assume there is no acceleration acting on the prim anymore
                    // or our objects will float away instead of coming to rest in their final movement
                    _acceleration = OpenMetaverse.Vector3.Zero;
                    _velocity = OpenMetaverse.Vector3.Zero;
                    _lastAccelVelUpdate = 0;
                    status |= VelocityStatus.Zeroed;
                }
                else
                {
                    //try to get a semi-accurate FPS to send the viewer the correct acceleration
                    ulong tickCountNow = Util.GetLongTickCount();
                    ulong timeDiff = tickCountNow - _lastAccelVelUpdate;

                    float fps;
                    if (_lastAccelVelUpdate == 0 || timeDiff <= 0)
                    {
                        fps = NOMINAL_FPS;
                    }
                    else
                    {
                        fps = 1.0f / (timeDiff * 0.001f);
                    }

                    var lastAccel = _acceleration;
                    var newAccel = (newVel - _velocity) * fps;

                    _velocity = newVel;

                    if (!lastAccel.ApproxEquals(newAccel, ACCEL_COMPARISON_TOLERANCE * _velocity.Length()))
                    {
                        //m_log.DebugFormat("Vel: {0} Accel: {1} Fps: {2}", _velocity, newAccel, fps);
                        _acceleration = newAccel;
                        status |= VelocityStatus.Changed;
                    }
                    
                    
                    _lastAccelVelUpdate = tickCountNow;
                }

                
            }
            else
            {
                if (_velocity != OpenMetaverse.Vector3.Zero)
                {
                    _acceleration = OpenMetaverse.Vector3.Zero;
                    _lastAccelVelUpdate = Util.GetLongTickCount();
                    status |= VelocityStatus.Changed;
                }
            }

            if (status != VelocityStatus.NoChange)
            {
                _angularVelocity = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity);
            }
            else
            {
                OpenMetaverse.Vector3 newAngVel = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity);
                //m_log.DebugFormat("AngVel: {0}, NewAngVel: {1}", _angularVelocity, newAngVel);

                if (newAngVel == OpenMetaverse.Vector3.Zero)
                {
                    if (newAngVel != _angularVelocity)
                    {
                        _angularVelocity = OpenMetaverse.Vector3.Zero;
                        status |= VelocityStatus.AngularChanged;
                    }
                }
                else if (!newAngVel.ApproxEquals(_angularVelocity, ANGULAR_VELOCITY_COMPARISON_TOLERANCE * _angularVelocity.Length()))
                {
                    //Console.Out.WriteLine("Ang: {0}", _angularVelocity);
                    _angularVelocity = newAngVel;
                    status |= VelocityStatus.AngularChanged;
                }
                else
                {
                    //Angular velocity hasnt changed or zeroed. BUT if angular velocity is set and
                    //the center of mass isnt at 0,0,0 and POS has changed we need to send an update
                    if (!_centerOfMassNearlyZero && !posDiff.ApproxEquals(OpenMetaverse.Vector3.Zero, POS_ANGULAR_VEL_TOLERANCE))
                    {
                        _angularVelocity = newAngVel;

                        status |= VelocityStatus.AngularPosChanged;
                        _useAngularVelocity = false;
                    }
                    else
                    {
                        if (_useAngularVelocity == false)
                        {
                            status |= VelocityStatus.AngularPosChanged;
                            _useAngularVelocity = true;
                        }
                    }
                }
            }

            return status;
        }

        private void CheckSyncRotation()
        {
            //check my rotation
            _rotation = GetRotationFromActor();
        }

        private OpenMetaverse.Quaternion GetRotationFromActor()
        {
            return PhysUtil.DecomposeToRotation(_actor.GlobalPose);
        }

        private OpenMetaverse.Vector3 CheckSyncPosition()
        {
            //check my position
            OpenMetaverse.Vector3 oldPos = _position;
            _position = PhysUtil.DecomposeToPosition(_actor.GlobalPose);

            return oldPos - _position;
        }

        /// <summary>
        /// Called in sync by the physics engine to adjust the position of this actor after it has been moved by 
        /// a non- simulation event such as a position adjustment call
        /// </summary>
        /// <param name="newPosition"></param>
        internal void SyncSetPos(OpenMetaverse.Vector3 newPosition)
        {
            if (this.HasActor)
            {
                SyncSetPositionAndRotation();
            }
        }

        private void SyncSetPositionAndRotation()
        {
            if (_dynActor == null)
            {
                //We have no dynamic actor.. we have to delete this actor and re-add it to move it,
                //using physx 3.2 we can set it kinematic and perform all susequent moves on that actor
                PhysX.RigidDynamic kinematic = PhysxActorFactory.CreateRigidDynamic(_scene, _myShape, _position, _rotation, _isPhysical, true, _properties.PhysxMaterial);

                this.ReassignActors(null, kinematic, null, _isPhysical);
                _scene.PrimMadeStaticKinematic(this);
            }
            else
            {
                bool isKinematic = (_dynActor.Flags & PhysX.RigidDynamicFlags.Kinematic) != 0;

                if (isKinematic)
                {
                    //it is only safe to move a dynamic if it is kinematic. This should be set by selection
                    _dynActor.SetKinematicTarget(PhysUtil.PositionToMatrix(_position, _rotation));

                    //prevent spamming of kinematic updates
                    InformSceneOfKinematicUpdate();
                }
            }

            RequestPhysicsTerseUpdate();
        }

        /// <summary>
        /// Tells the scene that this kinematic has been changed and is not a
        /// good candidate to be migrated to static geometry at this time
        /// </summary>
        internal void InformSceneOfKinematicUpdate()
        {
            ulong tickCount = Util.GetLongTickCount();
            if (tickCount - _lastKinematicUpdate >= MIN_KINEMATIC_UPDATE_FREQ)
            {
                _lastKinematicUpdate = tickCount;
                _scene.UpdateKinematic(this);
            }
        }

        /// <summary>
        /// Should only be called from the KinematicManager
        /// </summary>
        internal void MakeStatic()
        {
            PhysX.RigidStatic staticActor = PhysxActorFactory.CreateRigidStatic(_scene, _myShape, _position, _rotation, _properties.PhysxMaterial);

            this.ReassignActors(null, staticActor, null, _isPhysical);
        }

        internal void SyncSetRotation(OpenMetaverse.Quaternion newRotation)
        {
            if (this.HasActor)
            {
                SyncSetPositionAndRotation();
            }
        }

        public override void AddForceSync(OpenMetaverse.Vector3 force, OpenMetaverse.Vector3 forceOffset, ForceType type)
        {
            PhysX.Math.Vector3 pforce  = PhysUtil.OmvVectorToPhysx(force);
            PhysX.Math.Vector3 poffset = PhysUtil.OmvVectorToPhysx(forceOffset);

            //these property setter force types need to be set and assigned regardless
            //of if the object is currently a free dynamic
            switch (type)
            {
                case ForceType.AngularVelocityTarget:
                    lock (_properties)
                    {
                        _properties.AngularVelocityTarget = force;
                    }
                    RequestPhysicsNeedsPersistence();
                    break;

                case ForceType.ConstantGlobalLinearForce:
                    lock (_properties)
                    {
                        _properties.ForceIsLocal = false;
                        _properties.Force = force;
                    }
                    RequestPhysicsNeedsPersistence();
                    break;

                case ForceType.ConstantLocalLinearForce:
                    lock (_properties)
                    {
                        _properties.ForceIsLocal = true;
                        _properties.Force = force;
                    }
                    RequestPhysicsNeedsPersistence();
                    break;
            }

            if (IsFreeDynamic)
            {
                switch (type)
                {
                    case ForceType.LocalAngularImpulse:
                        _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(force * _rotation), PhysX.ForceMode.Impulse, true);
                        break;

                    case ForceType.GlobalAngularImpulse:  // this really means GlobalAngularImpulse
                        _dynActor.AddTorque(pforce, PhysX.ForceMode.Impulse, true);
                        break;

                    case ForceType.LocalLinearImpulse:
                        _dynActor.AddLocalForceAtLocalPosition(PhysUtil.OmvVectorToPhysx(force), PhysUtil.OmvVectorToPhysx(_centerOfMassLocalPose.Position), PhysX.ForceMode.Impulse, true);
                        break;

                    case ForceType.GlobalLinearImpulse:
                        if (poffset == PhysX.Math.Vector3.Zero)
                            _dynActor.AddForce(pforce, PhysX.ForceMode.Impulse, true);
                        else
                            _dynActor.AddForceAtLocalPosition(pforce, poffset, PhysX.ForceMode.Impulse, true);
                        break;

                    case ForceType.ReplaceGlobalAngularVelocity:
                        this.ReplaceAngularVelocity(force, false);
                        break;

                    case ForceType.ReplaceLocalAngularVelocity:
                        this.ReplaceAngularVelocity(force, true);
                        break;

                    case ForceType.ReplaceLocalLinearVelocity:
                        this.ReplaceLinearVelocity(force, true);
                        break;

                    case ForceType.ReplaceGlobalLinearVelocity:
                        this.ReplaceLinearVelocity(force, false);
                        break;
                }
            }

            //in all cases, wake up
            this.WakeUp();
        }

        private void ReplaceLinearVelocity(OpenMetaverse.Vector3 force, bool local)
        {
            OpenMetaverse.Vector3 currLinearVel = PhysUtil.PhysxVectorToOmv(_dynActor.LinearVelocity);

            if (local)
            {
                force = force * _rotation;

                _dynActor.AddForce(-_dynActor.LinearVelocity, PhysX.ForceMode.VelocityChange, true);
                _dynActor.AddForce(PhysUtil.OmvVectorToPhysx(force), PhysX.ForceMode.VelocityChange, true);
            }
            else
            {
                _dynActor.AddForce(PhysUtil.OmvVectorToPhysx(force - currLinearVel), PhysX.ForceMode.VelocityChange, true);
            }
        }

        private void ReplaceAngularVelocity(OpenMetaverse.Vector3 force, bool local)
        {
            OpenMetaverse.Vector3 currentAngVel = PhysUtil.PhysxVectorToOmv(_dynActor.AngularVelocity);

            if (local)
            {
                force = force * _rotation;
                _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(force - currentAngVel), PhysX.ForceMode.VelocityChange, true);
            }
            else
            {
                _dynActor.AddTorque(PhysUtil.OmvVectorToPhysx(force - currentAngVel), PhysX.ForceMode.VelocityChange, true);
            }
        }

        private void ChangeToChild(PhysxPrim parent, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot)
        {
            _parentPrim = parent;

            //this prim no longer has its old shape or its actor
            _scene.PrimBecameChild(this);

            //if we have children, we need to unref their shapes here, as our new parent (ours and our children)
            //may require different shape types and will be rebuilt
            this.DeleteActor(_actor, _myShape, _isPhysical, DeleteActorFlags.UnrefChildShapes);

            _actor = null;
            _dynActor = null;
            _myShape = null;

            _position = localPos;
            _rotation = localRot;
        }

        internal void LinkPrimAsChildSync(PhysicsShape childShape, PhysxPrim newChild, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot, 
            bool delayInertiaRecalc)
        {
            newChild.ChangeToChild(this, localPos, localRot);

            this.CombineChildShape(childShape, newChild, localPos, localRot, delayInertiaRecalc);
        }

        private void CombineChildShape(PhysicsShape childShape, PhysxPrim newChild, OpenMetaverse.Vector3 localPos, OpenMetaverse.Quaternion localRot, 
            bool delayInertiaRecalc)
        {
            //calculate the local rotation for the new shape we'll add to replace the combined child
            PhysX.Math.Matrix localPose = PhysUtil.PositionToMatrix(localPos, localRot);

            if (!_isPhysical || this.CheckComplexityLimitsWithNewChild(childShape))
            {
                try
                {
                    List<PhysX.Shape> actorShapes = childShape.AssignToActor(_actor, newChild.PhysxProperties.PhysxMaterial.PhyMaterial, localPose, _isPhysical);

                    _childShapes.Add(newChild, new RelatedShapes { ChildShape = childShape, PhyShapes = actorShapes });
                    foreach (PhysX.Shape shape in actorShapes)
                    {
                        _shapeToPrimIndex.Add(shape, newChild);
                    }

                    CollisionGroup.SetCollisionGroup(_collisionGroup, actorShapes);

                    if (!delayInertiaRecalc) UpdateMassAndInertia();

                    if (newChild.Properties.WantsCollisionNotification)
                    {
                        this.EnableChildCollisionEventsSync(newChild, true);
                    }

                    newChild._isPhysical = _isPhysical;
                }
                catch (NullReferenceException e) //this catch is in place to try and find an obscure bug where a cast inside the C++/CLI side of the physx sdk throws
                {
                    m_log.ErrorFormat("[InWorldz.PhysX] Unable to assign child shapes to a physactor: {0}, Material: {1}, Pose: {2}", e, newChild.PhysxProperties.PhysxMaterial.PhyMaterial, localPose);
                    _childShapes.Add(newChild, new RelatedShapes { ChildShape = childShape, PhyShapes = new List<PhysX.Shape>(0) });
                    childShape.DecRef();
                }
            }
            else
            {
                //too complex, free the child shape and issue a warning to the owner
                _childShapes.Add(newChild, new RelatedShapes { ChildShape = childShape, PhyShapes = new List<PhysX.Shape>(0) });

                childShape.DecRef();
            }
        }

        /// <summary>
        /// Checks whether or not this new child shape would put us over our complexity limit
        /// </summary>
        /// <param name="childShape">The shape to test</param>
        /// <returns>True if this actor is still under the limit with the shape installed, false if not</returns>
        private bool CheckComplexityLimitsWithNewChild(PhysicsShape childShape)
        {
            int newComplexity = childShape.Complexity + this.TotalComplexity;
            if (newComplexity > MAX_DYNAMIC_COMPLEXITY)
            {
                TriggerComplexityError(String.Format("Object was too complex to be made physical: ({0} of {1} convex hulls)", newComplexity, PhysxPrim.MAX_DYNAMIC_COMPLEXITY));
                return false;
            }

            return true;
        }

        internal void UpdateMassAndInertia()
        {
            if (_dynActor != null && IsPhysical)
            {
                if (_childShapes.Count == 0)
                {
                    _dynActor.UpdateMassAndInertia(_properties.Material.Density);
                }
                else
                {
                    UpdateMassAndInertiaWithChildShapeDensities();
                }

                CacheMassData();
            }
        }

        /// <summary>
        /// Runs through all of our shapes and updates the mass/interia information from
        /// our primary shape and its materials as well as child shapes and materials
        /// </summary>
        private void UpdateMassAndInertiaWithChildShapeDensities()
        {
            List<float> densities = new List<float>();
            //iterate our shapes in order and retrieve the density information for each shape
            foreach (PhysX.Shape shape in _actor.Shapes)
            {
                PhysxPrim prim;
                if (_shapeToPrimIndex.TryGetValue(shape, out prim))
                {
                    densities.Add(prim.Properties.Material.Density);
                }
                else
                {
                    m_log.ErrorFormat("[InWorldz.PhysxPhysics] Unable to find child or primary prim relating to shape, setting density to default");
                    densities.Add(Material.WOOD.Density);
                }
            }

            _dynActor.UpdateMassAndInertia(densities.ToArray(), null);
        }

        private void CacheMassData()
        {
            _mass = _dynActor.Mass;
            _centerOfMassLocalPose = PhysUtil.MatrixToPose(_dynActor.CenterOfMassLocalPose);
            _massSpaceInertiaTensor = PhysUtil.PhysxVectorToOmv(_dynActor.MassSpaceInertiaTensor);

            PhysX.Math.Vector3 bbExtents = _dynActor.WorldBounds.Extents;

            float bbLen = bbExtents.Length() * 2;
            if (_centerOfMassLocalPose.Position.Length() / bbLen > CENTER_OF_MASS_POS_TOLERANCE)    //check if the center of mass is too far off in general
            {
                _centerOfMassNearlyZero = false;
                return;
            }
            else if (Math.Abs(_centerOfMassLocalPose.Position.X / bbExtents.X) > CENTER_OF_MASS_POS_TOLERANCE)  //check if X is too far off
            {
                _centerOfMassNearlyZero = false;
                return;
            }
            else if (Math.Abs(_centerOfMassLocalPose.Position.Y / bbExtents.Y) > CENTER_OF_MASS_POS_TOLERANCE)  //same with Y
            {
                _centerOfMassNearlyZero = false;
                return;
            }
            else if (Math.Abs(_centerOfMassLocalPose.Position.Z / bbExtents.Z) > CENTER_OF_MASS_POS_TOLERANCE)  //and Z
            {
                _centerOfMassNearlyZero = false;
                return;
            }

            _centerOfMassNearlyZero = true;
        }

        public override void UpdateOffsetPosition(OpenMetaverse.Vector3 newOffset, OpenMetaverse.Quaternion rotOffset)
        {
            //Offset updates are only handled for child prims. Upstream a non child move is 
            //handled by setting our position directly
            if (IsChild)
            {
                _position = newOffset;
                _rotation = rotOffset;

                _parentPrim.ChildPrimOffsetChanged(this, newOffset, rotOffset);
            }
        }

        private void ChildPrimOffsetChanged(PhysxPrim child, OpenMetaverse.Vector3 newOffset, OpenMetaverse.Quaternion rotOffset)
        {
            _scene.QueueCommand(new Commands.ChangeChildPrimOffsetCmd(this, child, newOffset, rotOffset));
        }

        internal void ChildPrimOffsetChangedSync(PhysxPrim child, OpenMetaverse.Vector3 newOffset, OpenMetaverse.Quaternion rotOffset)
        {
            RelatedShapes childShapes;
            if (_childShapes.TryGetValue(child, out childShapes))
            {
                PhysX.Math.Matrix pose = PhysUtil.PositionToMatrix(newOffset, rotOffset);
                foreach (PhysX.Shape shape in childShapes.PhyShapes)
                {
                    shape.LocalPose = pose;
                }

                UpdateMassAndInertia();

                //reset to match the physical representation
                child._position = newOffset;
                child._rotation = rotOffset;
            }
        }

        /// <summary>
        /// Rebuilds this actor given a new shape and child shapes
        /// </summary>
        /// <param name="newShape"></param>
        /// <param name="childShapes"></param>
        /// <param name="physical"></param>
        internal void RebuildPhysxActorWithNewShape(PhysicsShape newShape, Dictionary<PhysxPrim, RelatedShapes> newChildShapes, bool physical,
            bool isChildUnlink)
        {
            if (HasActor || isChildUnlink)
            {
                PhysX.RigidDynamic physActor;
                if (physical)
                {
                    physActor = PhysxActorFactory.CreateRigidDynamic(_scene, newShape, _position, _rotation, physical, this.Selected, _properties.PhysxMaterial);
                    if (isChildUnlink) _scene.AddPrimSync(this, physical, false);
                    _scene.PrimMadeDynamic(this);
                }
                else
                {
                    physActor = PhysxActorFactory.CreateRigidDynamic(_scene, newShape, _position, _rotation, physical, true, _properties.PhysxMaterial);
                    if (isChildUnlink) _scene.AddPrimSync(this, physical, true);
                    _scene.PrimMadeStaticKinematic(this);
                }

                this.ReassignActors(newShape, physActor, newChildShapes, physical);

                if (!physical)
                {
                    this.ClearForcesAndSendUpdate();
                }
            }
            else
            {
                //we are a child prim and part of another group, let our parent know that our shape changed
                _parentPrim.ChildShapeChanged(this, newShape);
            }
        }

        /// <summary>
        /// Called by a child prim of this group when its shape has changed
        /// </summary>
        /// <param name="physxPrim"></param>
        /// <param name="newShape"></param>
        private void ChildShapeChanged(PhysxPrim physxPrim, PhysicsShape newShape)
        {
            PhysicsShape oldShape = this.RemoveChildShape(physxPrim);

            if (oldShape != null)
            {
                _scene.MeshingStageImpl.UnrefShape(oldShape, _isPhysical);
            }

            this.CombineChildShape(newShape, physxPrim, physxPrim.Position, physxPrim.Rotation, false);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (!_disposed)
            {
                List<PhysxPrim> childPrims = new List<PhysxPrim>(_childShapes.Keys);
                this.DeleteActor(_actor, _myShape, _isPhysical, DeleteActorFlags.DestroyAll);

                _disposed = true;

                //also marked all child prims as disposed. this prevents
                //late references to children by commands from going through
                foreach (var childPrim in childPrims)
                {
                    childPrim._disposed = true;
                    _scene.ChildPrimDeleted(childPrim);
                }
            }
        }

        #endregion

        private List<PhysxPrim> _childPrimsDelayedByMe = new List<PhysxPrim>();
        private bool _contactDebug;

        /// <summary>
        /// Begins delaying commands for this actor and all of its child prims. This
        /// command should be used whenever the state of this entire object is changing
        /// in a way that may be incompatible with future and pending commands
        /// </summary>
        /// <param name="initiator"></param>
        internal void BeginDelayCommands(Commands.ICommand initiator)
        {
            _scene.BeginDelayCommands(this, initiator);
            foreach (PhysxPrim prim in _childShapes.Keys)
            {
                _childPrimsDelayedByMe.Add(prim);
                _scene.BeginDelayCommands(prim, initiator);
            }
        }

        /// <summary>
        /// Stops delaying commands for this actor and all of its child prims
        /// </summary>
        internal void EndDelayCommands()
        {
            _scene.EndDelayCommands(this);
            foreach (PhysxPrim prim in _childPrimsDelayedByMe)
            {
                _scene.EndDelayCommands(prim);
            }

            _childPrimsDelayedByMe.Clear();
        }

        /// <summary>
        /// Unlinks this prim from its parent
        /// </summary>
        /// <param name="newWorldPosition">The new position for the new physics actor</param>
        /// <param name="newWorldRotation">The new rotation for the new physics actor</param>
        internal void UnlinkFromParent(OpenMetaverse.Vector3 newWorldPosition,
            OpenMetaverse.Quaternion newWorldRotation)
        {
            if (_parentPrim == null) return;

            bool physical = _parentPrim.IsPhysical;

            _position = newWorldPosition;
            _rotation = newWorldRotation;

            PhysicsShape childShape = _parentPrim.UnlinkChild(this);
            _parentPrim = null;

            if (childShape != null)
            {
                this.RebuildPhysxActorWithNewShape(childShape, null, physical, true);
            }
        }

        

        /// <summary>
        /// Unlinks the given child from this prim and returns the child's shape
        /// </summary>
        /// <param name="physxPrim"></param>
        /// <returns></returns>
        private PhysicsShape UnlinkChild(PhysxPrim physxPrim)
        {
            PhysicsShape retShape = RemoveChildShape(physxPrim);
            
            this.UpdateMassAndInertia();
            return retShape;
        }

        /// <summary>
        /// Removes and returns the old child shape associated with the given prim
        /// </summary>
        /// <param name="physxPrim">The child prim</param>
        /// <returns>The old shape</returns>
        private PhysicsShape RemoveChildShape(PhysxPrim physxPrim)
        {
            PhysicsShape retShape = null;
            RelatedShapes childShapes;
            if (_childShapes.TryGetValue(physxPrim, out childShapes))
            {
                physxPrim.ClearTrackedTouches();

                _scene.ForEachCharacter((PhysxCharacter character) =>
                {
                    character.InvalidateControllerCacheIfContacting(this);
                }
                );

                retShape = childShapes.ChildShape;

                foreach (PhysX.Shape shape in childShapes.PhyShapes)
                {
                    shape.Dispose();
                    _shapeToPrimIndex.Remove(shape);
                }

                _childShapes.Remove(physxPrim);

                //collision accounting
                if (physxPrim._properties.WantsCollisionNotification)
                {
                    if (--_numChildPrimsWantingCollisionNotification == 0)
                    {
                        _properties.ChildrenWantCollisionNotification = false;
                    }
                }
            }

            return retShape;
        }

        /// <summary>
        /// Begins a debug of dynamic objects when the physics frame time has gone too high.
        /// All collisions are redirected to the debug manager and it is then up to the debug manager to resolve the situation
        /// </summary>
        internal void DebugCollisionContactsSync()
        {
            if (HasActor)   //We only want to do this directly if we're a parent
            {
                this.SetTouchNotificationOnGivenShapes(CONTACT_NOTIFICATION_FLAGS, _actor.Shapes);
                _contactDebug = true;
            }
        }

        /// <summary>
        /// Begins a debug of dynamic objects when the physics frame time has gone too high.
        /// All collisions are redirected to the debug manager and it is then up to the debug manager to resolve the situation
        /// </summary>
        internal void StopDebuggingCollisionContactsSync()
        {
            if (HasActor)   //We only want to do this directly if we're a parent
            {
                if (_properties.WantsCollisionNotification)
                {
                    this.SetTouchNotificationOnGivenShapes(TOUCH_NOTIFICATION_FLAGS, _actor.Shapes);
                }
                else
                {
                    this.SetTouchNotificationOnGivenShapes(0, _actor.Shapes);
                }

                //If we still have children that want notification, re-enable the shapes
                if (_numChildPrimsWantingCollisionNotification > 0)
                {
                    foreach (KeyValuePair<PhysxPrim, RelatedShapes> childShapes in _childShapes)
                    {
                        if (childShapes.Key.PhysxProperties.WantsCollisionNotification)
                        {
                            SetTouchNotificationOnGivenShapes(TOUCH_NOTIFICATION_FLAGS, childShapes.Value.PhyShapes);
                        }
                    }
                }

                _contactDebug = false;
            }
        }

        /// <summary>
        /// Sets the appropriate flags to enable the physx engine to begin passing 
        /// collision notifications to this object
        /// </summary>
        internal void EnableCollisionEventsSync()
        {
            if (HasActor)   //We only want to do this directly if we're a parent
            {
                this.SetTouchNotificationOnGivenShapes(TOUCH_NOTIFICATION_FLAGS, _actor.Shapes);
                _properties.WantsCollisionNotification = true;
                _scene.PrimWantsCollisionRepeat(this);
                RequestPhysicsNeedsPersistence();
            }
            else    //otherwise, we want to tell our parent to do it for us
            {
                _parentPrim.EnableChildCollisionEventsSync(this, true);
                _properties.WantsCollisionNotification = true;
                _scene.PrimWantsCollisionRepeat(this);
                RequestPhysicsNeedsPersistence();
            }
        }

        private void ResetFilteringOnAllShapes()
        {
            foreach (PhysX.Shape shape in _actor.Shapes)
            {
                shape.ResetFiltering();
            }
        }

        private void SetTouchNotificationOnGivenShapes(uint flags, IEnumerable<PhysX.Shape> shapes)
        {
            try
            {
                foreach (PhysX.Shape shape in shapes)
                {
                    shape.SimulationFilterData = new PhysX.FilterData(flags, shape.SimulationFilterData.Word1,
                        shape.SimulationFilterData.Word2, shape.SimulationFilterData.Word3);

                    if (_hasSimulated)
                        shape.ResetFiltering();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PhysxPrim]: SetTouchNotificationOnGivenShapes exception:\n   {0}", e);
            }
        }

        /// <summary>
        /// Enables collision notification for all shapes belonging to the given child
        /// </summary>
        /// <param name="physxPrim">The child prim</param>
        private void EnableChildCollisionEventsSync(PhysxPrim physxPrim, bool enable)
        {
            RelatedShapes shapes;
            if (_childShapes.TryGetValue(physxPrim, out shapes))
            {
                this.SetTouchNotificationOnGivenShapes(enable ? TOUCH_NOTIFICATION_FLAGS : 0, shapes.PhyShapes);
            }

            _numChildPrimsWantingCollisionNotification++;
            _properties.ChildrenWantCollisionNotification = true;
            RequestPhysicsNeedsPersistence();
        }

        /// <summary>
        /// Unsets the flags used to enable the physx engine to begin passing 
        /// collision notifications to this object
        /// </summary>
        internal void DisableCollisionEventsSync()
        {
            if (HasActor)   
            {
                this.SetTouchNotificationOnGivenShapes(0, _actor.Shapes);
                _properties.WantsCollisionNotification = false;
                _scene.PrimDisabledCollisionRepeat(this);

                //If we still have children that want notification, re-enable the shapes
                if (_numChildPrimsWantingCollisionNotification > 0)
                {
                    foreach (KeyValuePair<PhysxPrim, RelatedShapes> childShapes in _childShapes)
                    {
                        if (childShapes.Key.PhysxProperties.WantsCollisionNotification)
                        {
                            SetTouchNotificationOnGivenShapes(TOUCH_NOTIFICATION_FLAGS, childShapes.Value.PhyShapes);
                        }
                    }
                }
            }
            else    //We need to tell our parent that we no longer need notification
            {
                _parentPrim.EnableChildCollisionEventsSync(this, false);
                _properties.WantsCollisionNotification = false;
                _scene.PrimDisabledCollisionRepeat(this);
            }

            RequestPhysicsNeedsPersistence();
        }

        /// <summary>
        /// Called by the physx scene when a new contact has been made on a prim requesting notification
        /// </summary>
        /// <param name="contactPairHeader"></param>
        /// <param name="pairs"></param>
        internal void OnContactChangeSync(PhysX.ContactPairHeader contactPairHeader, PhysX.ContactPair[] pairs, int actorIndex)
        {
            if (contactPairHeader.Actors[0] == null || contactPairHeader.Actors[0].UserData == null ||
                contactPairHeader.Actors[1] == null || contactPairHeader.Actors[1].UserData == null)
            {
                return;
            }

            if (_contactDebug)
            {
                _scene.ContactDebug.AnalyzeContactChange(contactPairHeader, pairs);
            }
            else
            {
                object[] userData = new object[] { contactPairHeader.Actors[0].UserData, contactPairHeader.Actors[1].UserData };

                if (Util.AllAre<object, PhysxPrim>(userData))
                {
                    HandleContactChangeWithOtherPrim(contactPairHeader, pairs);     //Dealing with contact between this prim and another prim
                }
                else if (Util.AnyAre<object, TerrainManager>(userData))
                {
                    HandleContactChangeWithGround(contactPairHeader, pairs);        //Dealing with contact between this prim and the ground
                }
                else if (Util.AnyAre<object, PhysxCharacter>(userData))
                {
                    HandleContactChangeWithCharacter(contactPairHeader, pairs, actorIndex);     //Dealing with contact between this prim and a character
                }
            }
        }

        private void HandleContactChangeWithGround(PhysX.ContactPairHeader contactPairHeader, PhysX.ContactPair[] pairs)
        {
            foreach (PhysX.ContactPair pair in pairs)
            {
                PhysxPrim prim = null;
                PhysX.Shape ourShape = null;

                if (TryFindOurShape(pair, ref prim, ref ourShape))
                {
                    OpenMetaverse.Vector3 currentLoc = PhysUtil.DecomposeToPosition(_actor.GlobalPose);
                    if (prim._properties.WantsCollisionNotification)
                    {
                        prim.ProcessGroundCollisionLocally(pair, currentLoc);
                    }
                    else if (this._properties.WantsCollisionNotification)
                    {
                        this.ProcessGroundCollisionLocally(pair, currentLoc);
                    }
                }
            }
        }

        private void ProcessGroundCollisionLocally(PhysX.ContactPair pair, OpenMetaverse.Vector3 currentLoc)
        {
            if ((pair.Events & PhysX.PairFlag.NotifyTouchFound) != 0)
            {
                _groundTouchCounts++;
            }
            else if ((pair.Events & PhysX.PairFlag.NotifyTouchLost) != 0)
            {
                if (_groundTouchCounts > 0) _groundTouchCounts--;
            }

            //count a ground collision only if the object is registering its first touch here.
            if (_groundTouchCounts == 1 && (pair.Events & PhysX.PairFlag.NotifyTouchFound) != 0)
            {
                SendCollisionUpdate(new CollisionEventUpdate
                {
                    Type = CollisionEventUpdateType.LandCollisionBegan,
                    CollisionLocation = currentLoc
                });
            }
            else if (_groundTouchCounts == 0)
            {
                SendCollisionUpdate(new CollisionEventUpdate
                {
                    Type = CollisionEventUpdateType.LandCollisionEnded,
                    CollisionLocation = currentLoc
                });
            }
        }

        /// <summary>
        /// This message is sent by the physx sdk when the character's capsule shape touches us
        /// </summary>
        /// <param name="contactPairHeader"></param>
        /// <param name="pairs"></param>
        private void HandleContactChangeWithCharacter(PhysX.ContactPairHeader contactPairHeader, PhysX.ContactPair[] pairs, int ourActorIndex)
        {
            if (_collisionGroup == CollisionGroupFlag.PhysicalPhantom)
            {
                //no collision processing for phantoms
                return;
            }

            PhysxCharacter other;
            other = contactPairHeader.Actors[ourActorIndex == 0 ? 1 : 0].UserData as PhysxCharacter;

            if (other == null)
            {
                m_log.ErrorFormat("[InWorldz.Physx] {0} Contact change with PhysxCharacter, but instance is null", this.SOPName);
                return;
            }

            foreach (PhysX.ContactPair pair in pairs)
            {
                PhysxPrim prim = null;
                PhysX.Shape ourShape = null;

                TryFindOurShape(pair, ref prim, ref ourShape);

                if (prim != null && other != null)
                {
                    prim.ProcessCharacterContactChange((pair.Events & PhysX.PairFlag.NotifyTouchFound) != 0 ? 1 : -1, prim, ourShape, other);
                }
            }
        }

        private bool TryFindOurShape(PhysX.ContactPair pair, ref PhysxPrim prim, ref PhysX.Shape ourShape)
        {
            if (pair.Shapes[0] != null && _shapeToPrimIndex.TryGetValue(pair.Shapes[0], out prim))
            {
                ourShape = pair.Shapes[0];
                return true;
            }
            else if (pair.Shapes[1] != null && _shapeToPrimIndex.TryGetValue(pair.Shapes[1], out prim))
            {
                ourShape = pair.Shapes[1];
                return true;
            }

            return false;
        }

        /// <summary>
        /// This message is sent by the character controller when a character "touches" us by its movement
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="character"></param>
        /// <param name="collisionEventUpdateType"></param>
        internal void OnCharacterContactChangeSync(PhysX.Shape shape, PhysxCharacter character, CollisionEventUpdateType collisionEventUpdateType)
        {
            if (_collisionGroup == CollisionGroupFlag.PhysicalPhantom)
            {
                //no collision processing for phantoms
                return;
            }

            PhysxPrim myPrim;
            if (_shapeToPrimIndex.TryGetValue(shape, out myPrim))
            {
                myPrim.ProcessCharacterContactChange(collisionEventUpdateType == CollisionEventUpdateType.CollisionBegan ? 1 : -1, myPrim, shape, character);
            }
        }

        /// <summary>
        /// Handles a contact change between this prim and another prim
        /// </summary>
        /// <remarks>
        /// Note: This implementation has a small bug. If a prim is rezzed already in contact and then is rotated
        /// so that a new shape that is part of the same prim makes contact with the same surface before the
        /// contact from the first shape is deregistered, we can enter a state where we arent registering
        /// any contacts. This would only persist until another contact change is registered. The only
        /// alternative is to track collisions for each individual shape and so far I dont see that being 
        /// warranted
        /// </remarks>
        /// <param name="contactPairHeader"></param>
        /// <param name="pairs"></param>
        private void HandleContactChangeWithOtherPrim(PhysX.ContactPairHeader contactPairHeader, PhysX.ContactPair[] pairs)
        {
            //if any deletions happen, we should've been informed by other means
            if ((contactPairHeader.Flags & PhysX.ContactPairHeaderFlag.DeletedActor0) != 0 ||
                (contactPairHeader.Flags & PhysX.ContactPairHeaderFlag.DeletedActor1) != 0)
            {
                return;
            }

            //if this prim is a phantom, drop the contact change
            if (_collisionGroup == CollisionGroupFlag.PhysicalPhantom)
            {
                return;
            }

            //tracks the contact changes per-prim since we're getting them per-shape
            Dictionary<Tuple<PhysxPrim, PhysxPrim>, int> contactChanges = new Dictionary<Tuple<PhysxPrim, PhysxPrim>, int>();

            //track the pairs that have changed and collect the changes
            foreach (PhysX.ContactPair pair in pairs)
            {
                //if any deletions happen, we should've been informed by other means
                if ((pair.Flags & PhysX.ContactPairFlag.DeletedShape0) != 0 ||
                    (pair.Flags & PhysX.ContactPairFlag.DeletedShape1) != 0)
                {
                    continue;
                }

                PhysxPrim prim = null;
                PhysxPrim other = null;
                if (_shapeToPrimIndex.TryGetValue(pair.Shapes[0], out prim))
                {
                    other = pair.Shapes[1].Actor.UserData as PhysxPrim;
                }
                else if (_shapeToPrimIndex.TryGetValue(pair.Shapes[1], out prim))
                {
                    other = pair.Shapes[0].Actor.UserData as PhysxPrim;
                }

                if (other != null && other.CollisionGrp == CollisionGroupFlag.PhysicalPhantom)
                {
                    //if the other prim is phantom, drop the contact change
                    return;
                }

                //if both prims are valid, track the contact change
                if (prim != null && other != null)
                {
                    int change = 0;
                    if ((pair.Events & PhysX.PairFlag.NotifyTouchFound) != 0)
                    {
                        change++;
                    }
                    if ((pair.Events & PhysX.PairFlag.NotifyTouchLost) != 0)
                    {
                        change--;
                    }

                    if (change != 0)
                    {
                        Tuple<PhysxPrim, PhysxPrim> key = new Tuple<PhysxPrim, PhysxPrim>(prim, other);

                        if (contactChanges.ContainsKey(key))
                        {
                            contactChanges[key] += change;
                        }
                        else
                        {
                            contactChanges.Add(key, change);
                        }
                    }
                }
            }

            foreach (var kvpChanges in contactChanges)
            {
                kvpChanges.Key.Item1.ProcessContactChange(kvpChanges.Value, kvpChanges.Key.Item1, kvpChanges.Key.Item2);
            }
        }

        /// <summary>
        /// Called when a change has been posted for touches between objects
        /// </summary>
        /// <param name="change"></param>
        /// <param name="other"></param>
        private void ProcessContactChange(int change, PhysxPrim ourPrim, PhysxPrim other)
        {
            //track this collision here if we're interested in getting notifications
            if (_properties.WantsCollisionNotification)
            {
                ProcessContactChangeLocally(change, ourPrim, other);
            }

            //tell the parent about the collision if it wants to know
            if (IsChild && _parentPrim.PhysxProperties.WantsCollisionNotification)
            {
                _parentPrim.ProcessContactChange(change, ourPrim, other);
            }
        }

        private void ProcessContactChangeLocally(int change, PhysxPrim ourPrim, PhysxPrim other)
        {
            int current;
            if (!_touchCounts.Value.TryGetValue(other, out current))
            {
                current = 0;
            }

            int newTotal = Math.Max(current + change, 0);

            if (newTotal == 0 && current == 0)
            {
                //no change
                return;
            }

            //m_log.DebugFormat("Contact Change: This: {0}, Other: {1}, Chg: {2}, Tot: {3}", this.SOPName, other.SOPName, change, newTotal);

            if (newTotal == 0 && change < 0)
            {
                //we've lost all contact with this prim, notify of collision_end
                //NOTE: We supply the other collider's UUID here just in case the prim has been deleted
                ourPrim.SendCollisionUpdate(new CollisionEventUpdate { OtherColliderLocalId = other._localId, OtherColliderUUID = other._uuid, Type = CollisionEventUpdateType.CollisionEnded });
                _touchCounts.Value.Remove(other);
                other.OnDeleted -= new Action<PhysxPrim>(other_OnDeleted);
                _primsBeingWatchedForDeletes.Value.Remove(other);

                return;
            }

            if (newTotal == 1 && change > 0)
            {
                //we have begun colliding with a new object
                ourPrim.SendCollisionUpdate(new CollisionEventUpdate { OtherColliderLocalId = other._localId, Type = CollisionEventUpdateType.CollisionBegan });
                other.OnDeleted += new Action<PhysxPrim>(other_OnDeleted);
                _primsBeingWatchedForDeletes.Value.Add(other);
            }

            _touchCounts.Value[other] = newTotal;

            return;
        }

        /// <summary>
        /// Called by a prim when it has been deleted so that we can clean up our internal state
        /// </summary>
        /// <param name="obj"></param>
        void other_OnDeleted(PhysxPrim other)
        {
            int count;
            if (_touchCounts.Value.TryGetValue(other, out count))
            {
                ProcessContactChangeLocally(-count, this, other);
            }
        }

        /// <summary>
        /// Called when a change has been posted for touches between one of our prims and an avatar. We must track
        /// these touches by shape because the collision detection is sometimes one sided in the case of an avatar
        /// touching a prim
        /// </summary>
        /// <param name="change"></param>
        /// <param name="other"></param>
        private void ProcessCharacterContactChange(int change, PhysxPrim ourPrim, PhysX.Shape primShape, PhysxCharacter other)
        {
            if (IsChild && _properties.WantsCollisionNotification)
            {
                HandleTrackedCharacterContactChange(change, ourPrim, primShape, other);
            }
            else if (IsChild && !_properties.WantsCollisionNotification)
            {
                _parentPrim.ProcessCharacterContactChange(change, ourPrim, primShape, other);
            }
            else if (HasActor && (_properties.WantsCollisionNotification || _properties.ChildrenWantCollisionNotification))
            {
                HandleTrackedCharacterContactChange(change, ourPrim, primShape, other);
            }
        }

        private void HandleTrackedCharacterContactChange(int change, PhysxPrim ourPrim, PhysX.Shape primShape, PhysxCharacter other)
        {
            HashSet<PhysX.Shape> collidingShapes;
            if (!_avatarTouchCounts.Value.TryGetValue(other, out collidingShapes))
            {
                if (change < 0)
                {
                    //we have no record of colliding with this object. therefore removing a 
                    //collision leaves no change to state
                    return;
                }

                collidingShapes = new HashSet<PhysX.Shape>();
                _avatarTouchCounts.Value[other] = collidingShapes;
            }

            if (change > 0 && !collidingShapes.Add(primShape))
            {
                //we're already colliding with this object. no change in state
                return;
            }
            else if (change < 0 && !collidingShapes.Remove(primShape))
            {
                //we weren't colliding with this object. no change in state
                return;
            }

            int newTotal = collidingShapes.Count;

            //m_log.DebugFormat("Char Contact Change: This: {0}, Other: {1}, Chg: {2}, Tot: {3}", this.SOPName, other.SOPName, change, collidingShapes.Count);

            if (newTotal == 0 && change < 0)
            {
                //we've lost all contact with this prim, notify of collision_end
                ourPrim.SendCollisionUpdate(new CollisionEventUpdate { OtherColliderLocalId = other.LocalID, Type = CollisionEventUpdateType.CharacterCollisionEnded });
                _avatarTouchCounts.Value.Remove(other);

                return;
            }

            if (newTotal == 1 && change > 0)
            {
                //we have begun colliding with a new object
                ourPrim.SendCollisionUpdate(new CollisionEventUpdate { OtherColliderLocalId = other.LocalID, Type = CollisionEventUpdateType.CharacterCollisionBegan });
            }

            return;
        }

        internal void ContactedCharacterDeleted(PhysxCharacter physxCharacter)
        {
            TerminateAvatarContacts(physxCharacter);
        }

        private void TerminateAvatarContacts(PhysxCharacter physxCharacter)
        {
            if (_avatarTouchCounts.IsValueCreated)
            {
                HashSet<PhysX.Shape> contactedShapeSet;
                if (_avatarTouchCounts.Value.TryGetValue(physxCharacter, out contactedShapeSet))
                {
                    //we need to make a copy here, because ProcessCharacterContactChange() can 
                    //result in _avatarTouchCounts being modified while we're iterating
                    PhysX.Shape[] contactedShapes = contactedShapeSet.ToArray();

                    foreach (var shape in contactedShapes)
                    {
                        PhysxPrim myPrim;
                        if (_shapeToPrimIndex.TryGetValue(shape, out myPrim))
                        {
                            myPrim.ProcessCharacterContactChange(-1, myPrim, shape, physxCharacter);
                        }
                    }
                }
            }
        }

        internal void WakeUp()
        {
            if (IsFreeDynamic)
            {
                _dynActor.WakeUp();
            }
        }

        /// <summary>
        /// Called when an object becomes kinematic because for some reason if
        /// you have a prim on the ground and then lift it up kinematically, the
        /// TouchLost report is never fired 
        /// </summary>
        internal void InvalidateCollisionData()
        {
            /*_groundTouchCounts = 0;
            if (_touchCounts.IsValueCreated)
            {
                _touchCounts.Value.Clear();
            }*/

            this.ResetFilteringOnAllShapes();
        }

        internal void SetBuoyancy(float buoyancy)
        {
            // Always set the propertybecause the object might be made physical later.
            lock (_properties)
            {
                // Legacy grid hack.
                if ((buoyancy >= 0.977f) && (buoyancy < 1.0f))
                    buoyancy = 1.0f;

                _properties.Buoyancy = buoyancy;
            }

            if (_isPhysical)
            {
                ChangeGravityIfNeeded();
                RequestPhysicsNeedsPersistence();
            }
        }

        private void ChangeGravityIfNeeded()
        {
            if (_dynActor != null)
            {
                PhysX.Actor actor = (PhysX.Actor)_dynActor;
                if (this.WantsGravity && (actor.Flags & PhysX.ActorFlag.DisableGravity) != 0)
                {
                    //make sure gravity is in effect
                    ((PhysX.Actor)_dynActor).Flags &= ~PhysX.ActorFlag.DisableGravity;
                }
                else if (!this.WantsGravity && (actor.Flags & PhysX.ActorFlag.DisableGravity) == 0)
                {
                    //simply cancel gravity
                    ((PhysX.Actor)_dynActor).Flags |= PhysX.ActorFlag.DisableGravity;
                }
            }
        }

        /// <summary>
        /// Sets our root prim or one of our children to the given material
        /// </summary>
        /// <param name="material">The material to set</param>
        /// <param name="applyToObject">Whether the material applies to the entire object or just this prim</param>
        internal void SetMaterialSync(Material material, bool applyToObject)
        {
            if (HasActor)
            {
                SetMaterialSync(material, this, applyToObject);
            }
            else
            {
                _parentPrim.SetMaterialSync(material, this, applyToObject);
            }
        }

        /// <summary>
        /// Sets the given prim in our linkset to the given material
        /// </summary>
        /// <param name="material"></param>
        /// <param name="affectedPrim"></param>
        private void SetMaterialSync(Material material, PhysxPrim affectedPrim, bool applyToObject)
        {
            IEnumerable<PhysX.Shape> shapes;

            if (applyToObject)
            {
                ReplaceMaterialOnAllShapes(material);
                return;
            }

            
            if (affectedPrim == this)
            {
                shapes = _actor.Shapes;
            }
            else
            {
                RelatedShapes childShapes;
                if (_childShapes.TryGetValue(affectedPrim, out childShapes))
                {
                    shapes = childShapes.PhyShapes;
                }
                else
                {
                    m_log.ErrorFormat("[InWorldz.PhysxPhysics] Asked to set material for unknown child shape");
                    return;
                }
            }

            ReplaceMaterialOnShapes(material, affectedPrim, shapes);
        }

        private void ReplaceMaterialOnAllShapes(Material material)
        {
            //place the primary copy of this material on the root prim
            ReplaceMaterialOnShapes(material, this, _primaryShapes);

            //duplicate the material for each child prim shape
            foreach (var childShape in _childShapes)
            {
                ReplaceMaterialOnShapes(material.Duplicate(_scene.SceneImpl.Physics), childShape.Key, childShape.Value.PhyShapes);
            }    
        }

        private void ReplaceMaterialOnShapes(Material material, PhysxPrim affectedPrim, IEnumerable<PhysX.Shape> shapes)
        {
            Material oldMaterial = affectedPrim.PhysxProperties.PhysxMaterial;
            affectedPrim.PhysxProperties.PhysxMaterial = material;

            PhysX.Material[] materialArr = new PhysX.Material[] { material.PhyMaterial };
            foreach (PhysX.Shape shape in shapes)
            {
                shape.SetMaterials(materialArr);
            }

            if (oldMaterial.Density != material.Density) UpdateMassAndInertia();

            oldMaterial.CheckedDispose();
        }

        /// <summary>
        /// Returns a list of the root and child prims that make up this object
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<PhysxPrim> GetComposingPrims()
        {
            yield return this;

            foreach (PhysxPrim child in _childShapes.Keys)
            {
                yield return child;
            }
        }

        public override void SetAngularVelocity(OpenMetaverse.Vector3 force, bool local)
        {
            _scene.QueueCommand(new Commands.AddForceCmd(this, force, local ? ForceType.ReplaceLocalAngularVelocity : ForceType.ReplaceGlobalAngularVelocity));
        }

        public override void SetVelocity(OpenMetaverse.Vector3 force, bool local)
        {
            _scene.QueueCommand(new Commands.AddForceCmd(this, force, local ? ForceType.ReplaceLocalLinearVelocity : ForceType.ReplaceGlobalLinearVelocity));
        }

        public override byte[] GetSerializedPhysicsProperties()
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                lock (_properties)
                {
                    ProtoBuf.Serializer.Serialize<PhysicsProperties>(ms, _properties);
                    return ms.ToArray();
                }
            }
        }

        internal void SetInitialVelocities(OpenMetaverse.Vector3 rootVelocity, OpenMetaverse.Vector3 rootAngularVelocity)
        {
            if (IsFreeDynamic || _suspended)
            {
                _velocity = rootVelocity;
                _angularVelocity = rootAngularVelocity;

                if (IsFreeDynamic)
                {
                    _dynActor.LinearVelocity = PhysUtil.OmvVectorToPhysx(rootVelocity);
                    _dynActor.AngularVelocity = PhysUtil.OmvVectorToPhysx(rootAngularVelocity);
                }
            }
        }

        /// <summary>
        /// Stops this object cold in its tracks by making it kinematic. Used during region crossings
        /// </summary>
        internal void SuspendPhysicsSync()
        {
            this.SuspendPhysicsSync(OpenSim.Framework.Util.GetLongTickCount());
        }

        internal void SuspendPhysicsSync(ulong suspendTime)
        {
            if (!_suspended)
            {
                _suspended = true;

                if (_isPhysical)
                {
                    _dynActor.Flags = _dynActor.Flags | PhysX.RigidDynamicFlags.Kinematic;
                    if (_vehicleDynamics != null)
                    {
                        _vehicleDynamics.OnPhysicsSuspended();
                    }
                }

                lock (_properties)
                {
                    // Adjust the move target if it is set.
                    if (_properties.MoveTargetTau != 0.0)
                    {
                        OpenMetaverse.Vector3 target = _properties.MoveTarget;
                        if (target.X >= Constants.RegionSize) target.X -= Constants.RegionSize;
                        else if (target.X < 0) target.X += Constants.RegionSize;

                        if (target.Y >= Constants.RegionSize) target.Y -= Constants.RegionSize;
                        else if (target.Y < 0) target.Y += Constants.RegionSize;

                        _properties.MoveTarget = target;
                    }

                    // Adjust the grab target if it is set.
                    if (_properties.GrabTargetTau != 0.0)
                    {
                        OpenMetaverse.Vector3 target = _properties.GrabTarget;
                        if (target.X >= Constants.RegionSize) target.X -= Constants.RegionSize;
                        else if (target.X < 0) target.X += Constants.RegionSize;

                        if (target.Y >= Constants.RegionSize) target.Y -= Constants.RegionSize;
                        else if (target.Y < 0) target.Y += Constants.RegionSize;

                        _properties.GrabTarget = target;
                    }
                }

                _suspendedOn = suspendTime;
            }
        }

        /// <summary>
        /// Resumes this object given its current velocity and angular velocity
        /// </summary>
        internal void ResumePhysicsSync(bool interpolate)
        {
            if (_suspended)
            {
                _suspended = false;

                if (_isPhysical)
                {
                    if (interpolate)
                    {
                        DoResumeInterpolation();
                    }

                    _dynActor.Flags = _dynActor.Flags & ~PhysX.RigidDynamicFlags.Kinematic;

                    //kick the prim to resume its velocity
                    _dynActor.AngularVelocity = PhysUtil.OmvVectorToPhysx(_angularVelocity);
                    _dynActor.LinearVelocity = PhysUtil.OmvVectorToPhysx(_velocity);

                    if (_vehicleDynamics != null)
                    {
                        _vehicleDynamics.OnPhysicsResumed();
                    }

                    RequestPhysicsTerseUpdate();
                }
            }
        }

        /// <summary>
        /// The maximum amount of time we'll interpolate a moving object
        /// running longer than this runs the risk of interpenetrating objects
        /// on the sim
        /// </summary>
        const ulong MAX_INTERPOLATION_TIME = 3000;
        private void DoResumeInterpolation()
        {
            //indicates we're resuming without suspending
            if (_suspendedOn == 0) return;

            ulong elapsed = Math.Min(OpenSim.Framework.Util.GetLongTickCount() - _suspendedOn, MAX_INTERPOLATION_TIME);

            //nothing to do
            if (elapsed == 0) return;

            //interpolate
            OpenMetaverse.Vector3 oldPos = _position;
            _position = _position + (_velocity * (elapsed / 1000.0f));
            _dynActor.GlobalPose = PhysUtil.PositionToMatrix(_position, _rotation);

            EnsureObjectAboveGround();
            if (TryRezDepenetrate() != PenetrationStatus.Ok)
            {
                //back out we're slammed into something
                _position = oldPos;
                _dynActor.GlobalPose = PhysUtil.PositionToMatrix(_position, _rotation);
            }
        }

        public override void Suspend()
        {
            _scene.QueueCommand(new Commands.SuspendResumePhysicsCmd { Target = this, Type = Commands.SuspendResumePhysicsCmd.SRType.Suspend });
        }

        public override void Resume(bool interpolate, AfterResumeCallback callback)
        {
            _scene.QueueCommand(new Commands.SuspendResumePhysicsCmd { Target = this, Type = Commands.SuspendResumePhysicsCmd.SRType.Resume, Interpolate = interpolate, Callback = callback });
        }

        public void SetCollisionGroup(CollisionGroupFlag group)
        {
            CollisionGroup.SetCollisionGroup(group, _shapeToPrimIndex.Keys);
        }

        public void UpdateCollisionGroup(CollisionGroupFlag group)
        {
            _collisionGroup = group;
            SetCollisionGroup(group);
        }

        public override void SetPhantom(bool phantom)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this, 
                    (PhysxScene scene) => 
                    {
                        if (phantom)
                        {
                            UpdateCollisionGroup(CollisionGroupFlag.PhysicalPhantom);
                        }
                        else
                        {
                            UpdateCollisionGroup(CollisionGroupFlag.Normal);
                        }
                    }
            ));
        }

        internal void SetVolumeDetectSync(bool vdActive)
        {
            if (vdActive)
            {
                if (IsPhysical)
                {
                    m_log.ErrorFormat("[InWorldz.PhysX] Can not enable volumedetect while an object is still dynamic");
                    throw new PhysxSdkException("Can not enable volumedetect while an object is still dynamic");
                }

                foreach (var shape in _shapeToPrimIndex.Keys)
                {
                    shape.Flags &= ~PhysX.ShapeFlag.SimulationShape;
                    shape.Flags |= PhysX.ShapeFlag.TriggerShape;
                }

                SetCollisionGroup(CollisionGroupFlag.Trigger);
            }
            else
            {
                foreach (var shape in _shapeToPrimIndex.Keys)
                {
                    shape.Flags &= ~PhysX.ShapeFlag.TriggerShape;
                    shape.Flags |= PhysX.ShapeFlag.SimulationShape;
                }

                SetCollisionGroup(CollisionGroupFlag.Normal);
            }
        }

        internal void OnTrigger(PhysX.TriggerPair pair)
        {
            if (pair.OtherShape == null) return;

            PhysxPrim otherPrim = pair.OtherShape.Actor.UserData as PhysxPrim;

            if (otherPrim != null)
            {
                this.ProcessContactChange(pair.Status == PhysX.PairFlag.NotifyTouchFound ? 1 : -1, this, otherPrim);
            }
            else
            {
                if (pair.OtherShape.Actor == null || pair.OtherShape.Actor.UserData == null)
                {
                    m_log.Error("[InWorldz.PhysxPhysics] OnTrigger(): Could not resolve pair.OtherActor");
                    return;
                }

                PhysxCharacter character = pair.OtherShape.Actor.UserData as PhysxCharacter;

                if (character != null)
                {
                    this.ProcessCharacterContactChange(pair.Status == PhysX.PairFlag.NotifyTouchFound ? 1 : -1, this, pair.TriggerShape as PhysX.Shape, character);
                }
            }
        }

        public override void SetVehicleType(VehicleType type)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                       (PhysxScene scene) =>
                       {
                           CheckCreateVehicleProperties();
                           _properties.VehicleProps.Type = type;

                           CheckCreateVehicleDynamics();
                           Vehicle.VehicleDynamics.SetVehicleDefaults(_properties.VehicleProps);
                           _vehicleDynamics.VehicleTypeChanged(type);
                       }
                )
            );
        }

        public override void SetVehicleFlags(VehicleFlags flags)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                       (PhysxScene scene) =>
                       {
                           CheckCreateVehicleProperties();
                           _properties.VehicleProps.Flags = _properties.VehicleProps.Flags | flags;
                           CheckCreateVehicleDynamics();
                           _vehicleDynamics.VehicleFlagsChanged(_properties.VehicleProps.Flags);
                       }
                )
            );
        }

        public override void RemoveVehicleFlags(VehicleFlags flags)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                       (PhysxScene scene) =>
                       {
                           CheckCreateVehicleProperties();
                           _properties.VehicleProps.Flags = _properties.VehicleProps.Flags & ~flags;

                           CheckCreateVehicleDynamics();
                           _vehicleDynamics.VehicleFlagsChanged(_properties.VehicleProps.Flags);
                       }
                )
            );
        }

        public override void SetVehicleFloatParam(FloatParams param, float value)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                       (PhysxScene scene) =>
                       {
                           value = Vehicle.VehicleDynamics.ClampFloatParam(param, value);
                           CheckCreateVehicleProperties();
                           _properties.VehicleProps.ParamsFloat[param] = value;

                           CheckCreateVehicleDynamics();
                           _vehicleDynamics.FloatParamChanged(param, value);
                       }
                )
            );
        }

        public override void SetVehicleRotationParam(RotationParams param, OpenMetaverse.Quaternion value)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                       (PhysxScene scene) =>
                       {
                           value = Vehicle.VehicleDynamics.ClampRotationParam(param, value);
                           CheckCreateVehicleProperties();
                           _properties.VehicleProps.ParamsRot[param] = value;

                           CheckCreateVehicleDynamics();
                           _vehicleDynamics.RotationParamChanged(param, value);
                       }
                )
            );
        }

        public override void SetVehicleVectorParam(VectorParams param, OpenMetaverse.Vector3 value)
        {
            _scene.QueueCommand(
                new Commands.GenericSyncCmd(this,
                       (PhysxScene scene) =>
                       {
                           value = Vehicle.VehicleDynamics.ClampVectorParam(param, value);
                           CheckCreateVehicleProperties();
                           _properties.VehicleProps.ParamsVec[param] = value;

                           CheckCreateVehicleDynamics();
                           _vehicleDynamics.VectorParamChanged(param, value);
                       }
                )
            );
        }

        private void CheckCreateVehicleDynamics()
        {
            if (_vehicleDynamics == null)
            {
                lock (_properties)
                {
                    if (_vehicleDynamics == null)
                        _vehicleDynamics = new Vehicle.VehicleDynamics(this, _properties.VehicleProps, _scene.SceneImpl.Physics, _scene);
                }
            }
        }

        /// <summary>
        /// Checks and confirms that vehicle properties are constructed and initialized properly
        /// THIS MUST BE CALLED FROM WITHIN THE _properties lock
        /// </summary>
        private void CheckCreateVehicleProperties()
        {
            if (_properties.VehicleProps == null)
            {
                _properties.VehicleProps = new Vehicle.VehicleProperties();
            }

            // There is but one simulation dynamics working area.It is modified solely
            // within the vehicle dynamics class and a reference passed through to
            // the vehicle dynamics constructor.
            if (_properties.VehicleProps.Dynamics == null)
            {
                _properties.VehicleProps.Dynamics = new Vehicle.DynamicsSimulationData();
            }
        }

        public override byte[] GetSerializedPhysicsShapes()
        {
            const int SHAPE_WAIT_TIMEOUT = 5000;
            byte[] outShapes = null;

            ManualResetEventSlim signalEvent = new ManualResetEventSlim();

            _scene.QueueCommand(new Commands.GenericSyncCmd(this, (scene) =>
            {
                IEnumerable<PhysX.Shape> shapes = null;

                if (HasActor)
                {
                    shapes = _primaryShapes;
                }
                else
                {
                    RelatedShapes childShapes;
                    if (_parentPrim._childShapes.TryGetValue(this, out childShapes))
                    {
                        shapes = childShapes.PhyShapes;
                    }
                }

                if (shapes == null)
                {
                    signalEvent.Set();
                    return;
                }

                using (PhysX.Collection coll = _scene.SceneImpl.Physics.CreateCollection())
                {
                    CollectShapesForSerialization(shapes, coll);

                    if (coll.GetNumberOfObjects() > 0)
                    {
                        using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
                        {
                            coll.Serialize(memStream);
                            outShapes = memStream.ToArray();
                        }
                    }
                }

                signalEvent.Set();
            }));

            signalEvent.Wait(SHAPE_WAIT_TIMEOUT);

            if (signalEvent.IsSet)
            {
                //this is the normal case, safe to dispose now and not rely on the finalizer
                signalEvent.Dispose();
            }

            return outShapes;
        }

        private static void CollectShapesForSerialization(IEnumerable<PhysX.Shape> shapes, PhysX.Collection coll)
        {
            foreach (PhysX.Shape shape in shapes)
            {
                switch (shape.GeometryType)
                {
                    case PhysX.GeometryType.ConvexMesh:
                        ((PhysX.ConvexMeshGeometry)shape.Geom).ConvexMesh.AsSerializable().CollectForExport(coll);
                        break;

                    case PhysX.GeometryType.TriangleMesh:
                        ((PhysX.TriangleMeshGeometry)shape.Geom).TriangleMesh.AsSerializable().CollectForExport(coll);
                        break;
                }
            }
        }
    }
}
