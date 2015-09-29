/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BasicPhysicsPlugin
{
    /// <summary>
    /// Effectively a physics plugin that simulates no physics at all.
    /// </summary>
    public class BasicPhysicsPlugin : IPhysicsPlugin
    {
        public BasicPhysicsPlugin()
        {
        }

        public bool Init()
        {
            return true;
        }

        public PhysicsScene GetScene(string sceneIdentifier)
        {
            return new BasicScene(sceneIdentifier);
        }

        public string GetName()
        {
            return ("basicphysics");
        }

        public void Dispose()
        {
        }
    }

    public class BasicScene : PhysicsScene
    {
        private List<BasicActor> _actors = new List<BasicActor>();
        private float[] _heightMap;

        private bool _simulating = false;

        //protected internal string sceneIdentifier;

        public BasicScene(string _sceneIdentifier)
        {
            //sceneIdentifier = _sceneIdentifier;
        }

        public override void Initialize(IMesher meshmerizer, IConfigSource config, UUID regionId)
        {
            // Does nothing much right now
            _mesher = meshmerizer;
        }

        public override void Dispose()
        {

        }

        public override PhysicsActor AddAvatar(string avName, OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation, OpenMetaverse.Vector3 size, bool isFlying, OpenMetaverse.Vector3 initialVelocity)
        {
            BasicActor act = new BasicActor();
            act.Position = position;
            act.Flying = isFlying;
            _actors.Add(act);
            return act;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            BasicActor act = (BasicActor)actor;
            if (_actors.Contains(act))
            {
                _actors.Remove(act);
            }
        }

        /*
        public override PhysicsActor AddPrim(OpenMetaverse.Vector3 position, OpenMetaverse.Vector3 size, Quaternion rotation)
        {
            return null;
        }
*/

        public override PhysicsActor AddPrimShape(string primName, AddPrimShapeFlags flags, BulkShapeData shapeData)
        {
            return null;
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep, uint ticksSinceLastSimulate, uint frameNum, bool dilated)
        {
            if (!_simulating)
                return 0;
            
            float fps = 0;
            for (int i = 0; i < _actors.Count; ++i)
            {
                BasicActor actor = _actors[i];

                Vector3 temp;

                temp.X = actor.Position.X + actor.Velocity.X * timeStep;
                temp.Y = actor.Position.Y + actor.Velocity.Y * timeStep;
                temp.Z = actor.Position.Z;

                if (temp.Y < 0)
                {
                    temp.Y = 0.1F;
                }
                else if (temp.Y >= Constants.RegionSize)
                {
                    temp.Y = 255.9F;
                }

                if (temp.X < 0)
                {
                    temp.X = 0.1F;
                }
                else if (actor.Position.X >= Constants.RegionSize)
                {
                    temp.X = 255.9F;
                }

                float height = _heightMap[(int)temp.Y * Constants.RegionSize + (int)temp.X] + actor.Size.Z;
                if (actor.Flying)
                {
                    if (temp.Z + (temp.Z * timeStep) <
                        _heightMap[(int)temp.Y * Constants.RegionSize + (int)temp.X] + 2)
                    {
                        temp.Z = height;
                        temp.Z = 0;
                        actor.IsColliding = true;
                    }
                    else
                    {
                        temp.Z += actor.Velocity.Z * timeStep;
                        actor.IsColliding = false;
                    }
                }
                else
                {
                    temp.Z = height;
                    temp.Z = 0;
                    actor.IsColliding = true;
                }

                actor.Position = temp;
            }
            return fps;
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            get
            {
                return (false); // for now we won't be multithreaded
            }
        }

        public override void SetTerrain(float[] heightMap, int revision)
        {
            _heightMap = heightMap;
        }

        public override void SetWaterLevel(float baseheight)
        {
        }

        public override void DeleteTerrain()
        {
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            Dictionary<uint, float> returncolliders = new Dictionary<uint, float>();
            return returncolliders;
        }

        #region implemented abstract members of PhysicsScene

        public override void BulkAddPrimShapes(ICollection<BulkShapeData> shapeData, AddPrimShapeFlags flags)
        {
            // TODO maybe
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim, TaintType taint)
        {
        }

        public override IMaterial FindMaterialImpl(OpenMetaverse.Material materialEnum)
        {
            return new Material(OpenMetaverse.Material.Wood);
            ; // Plywood is everything! Everything is plywood!
        }

        public override void DumpCollisionInfo()
        {
        }

        public override void SendPhysicsWindData(Vector2[] sea, Vector2[] gnd, Vector2[] air, float[] ranges, float[] maxheights)
        {
        }

        public override void RayCastWorld(Vector3 start, Vector3 direction, float distance, int hitAmounts, System.Action<List<ContactResult>> result)
        {
        }

        public override List<ContactResult> RayCastWorld(Vector3 start, Vector3 direction, float distance, int hitAmounts)
        {
            return null;
        }

        public override float SimulationFPS
        {
            get
            {
                return 999;
            }
        }

        public override int SimulationFrameTimeAvg
        {
            get
            {
                return 999;
            }
        }

        public override bool Simulating
        {
            get
            {
                return _simulating;
            }
            set
            {
                _simulating = value;
            }
        }

        IMesher _mesher;

        public override IMesher Mesher
        {
            get
            {
                return _mesher;
            }
        }

        public override OpenSim.Region.Interfaces.ITerrainChannel TerrainChannel { get; set; }

        public override RegionSettings RegionSettings { get; set; }

        #endregion
    }

    public class BasicActor : PhysicsActor
    {
        private OpenMetaverse.Vector3 _position;
        private OpenMetaverse.Vector3 _velocity;
        private OpenMetaverse.Vector3 _acceleration;
        private OpenMetaverse.Vector3 _size;
        private OpenMetaverse.Vector3 m_rotationalVelocity = OpenMetaverse.Vector3.Zero;
        private bool flying;
        private bool iscolliding;

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
            _size = new OpenMetaverse.Vector3();
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
            //TODO
        }

        public override void Resume(bool interpolate, AfterResumeCallback callback)
        {
            //TODO
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

    public class Material : IMaterial
    {
        private OpenMetaverse.Material _material;

        public Material(OpenMetaverse.Material mat)
        {
            _material = mat;
        }

        #region IMaterial implementation

        public int MaterialPreset
        {
            get
            {
                return (int)_material;
            }
        }

        public float Density
        {
            get
            {
                return 1000.0f;
            }
        }

        public float StaticFriction
        {
            get
            {
                return 0.6f;
            }
        }

        public float DynamicFriction
        {
            get
            {
                return 0.55f;
            }
        }

        public float Restitution
        {
            get
            {
                return 0.5f;
            }
        }

        public float GravityMultiplier
        {
            get
            {
                return 1.0f;
            }
        }

        #endregion


    }
}
