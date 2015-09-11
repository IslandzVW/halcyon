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

        //protected internal string sceneIdentifier;

        public BasicScene(string _sceneIdentifier)
        {
            //sceneIdentifier = _sceneIdentifier;
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
            // Does nothing right now
        }

        public override void Dispose()
        {

        }
        public override PhysicsActor AddAvatar(string avName, OpenMetaverse.Vector3 position, OpenMetaverse.Vector3 size, bool isFlying)
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
            BasicActor act = (BasicActor) actor;
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

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, OpenMetaverse.Vector3 position,
                                                  OpenMetaverse.Vector3 size, Quaternion rotation)
        {
            return AddPrimShape(primName, pbs, position, size, rotation, false);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, OpenMetaverse.Vector3 position,
                                                  OpenMetaverse.Vector3 size, Quaternion rotation, bool isPhysical)
        {
            return null;
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep)
        {
            float fps = 0;
            for (int i = 0; i < _actors.Count; ++i)
            {
                BasicActor actor = _actors[i];

                actor.Position.X += actor.Velocity.X*timeStep;
                actor.Position.Y += actor.Velocity.Y*timeStep;

                if (actor.Position.Y < 0)
                {
                    actor.Position.Y = 0.1F;
                }
                else if (actor.Position.Y >= Constants.RegionSize)
                {
                    actor.Position.Y = 255.9F;
                }

                if (actor.Position.X < 0)
                {
                    actor.Position.X = 0.1F;
                }
                else if (actor.Position.X >= Constants.RegionSize)
                {
                    actor.Position.X = 255.9F;
                }

                float height = _heightMap[(int)actor.Position.Y * Constants.RegionSize + (int)actor.Position.X] + actor.Size.Z;
                if (actor.Flying)
                {
                    if (actor.Position.Z + (actor.Velocity.Z*timeStep) <
                        _heightMap[(int)actor.Position.Y * Constants.RegionSize + (int)actor.Position.X] + 2)
                    {
                        actor.Position.Z = height;
                        actor.Velocity.Z = 0;
                        actor.IsColliding = true;
                    }
                    else
                    {
                        actor.Position.Z += actor.Velocity.Z*timeStep;
                        actor.IsColliding = false;
                    }
                }
                else
                {
                    actor.Position.Z = height;
                    actor.Velocity.Z = 0;
                    actor.IsColliding = true;
                }
            }
            return fps;
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            get { return (false); // for now we won't be multithreaded
            }
        }

        public override void SetTerrain(float[] heightMap)
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
        public BasicActor()
        {
            _velocity = new OpenMetaverse.Vector3();
            _position = new OpenMetaverse.Vector3();
            _acceleration = new OpenMetaverse.Vector3();
            _size = new OpenMetaverse.Vector3();
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Agent; }
            set { return; }
        }

        public override OpenMetaverse.Vector3 RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }

        public override uint LocalID
        {
            set { return; }
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            set { return; }
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
            set { return; }
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
            set {
                  _size = value;
                  _size.Z = _size.Z / 2.0f;
                }
        }

        public override PrimitiveBaseShape Shape
        {
            set { return; }
        }

        public override float Mass
        {
            get { return 0f; }
        }

        public override OpenMetaverse.Vector3 Force
        {
            get { return OpenMetaverse.Vector3.Zero; }
            set { return; }
        }

        public override int VehicleType
        {
            get { return 0; }
            set { return; }
        }

        public override void VehicleFloatParam(int param, float value)
        {

        }

        public override void VehicleVectorParam(int param, OpenMetaverse.Vector3 value)
        {

        }

        public override void VehicleRotationParam(int param, Quaternion rotation)
        {

        }

        public override void SetVolumeDetect(int param)
        {

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

        public override Quaternion Orientation
        {
            get { return Quaternion.Identity; }
            set { }
        }

        public override OpenMetaverse.Vector3 Acceleration
        {
            get { return _acceleration; }
        }

        public override bool Kinematic
        {
            get { return true; }
            set { }
        }

        public override void link(PhysicsActor obj)
        {
        }

        public override void delink()
        {
        }

        public override void LockAngularMotion(OpenMetaverse.Vector3 axis)
        {

        }

        public void SetAcceleration(OpenMetaverse.Vector3 accel)
        {
            _acceleration = accel;
        }

        public override void AddForce(OpenMetaverse.Vector3 force, bool pushforce)
        {
        }

        public override void AddAngularForce(OpenMetaverse.Vector3 force, bool pushforce)
        {
        }

        public override void SetMomentum(OpenMetaverse.Vector3 momentum)
        {
        }

        public override void CrossingFailure()
        {
        }

        public override OpenMetaverse.Vector3 PIDTarget { set { return; } }
        public override bool PIDActive { set { return; } }
        public override float PIDTau { set { return; } }

        public override float PIDHoverHeight { set { return; } }
        public override bool PIDHoverActive { set { return; } }
        public override PIDHoverType PIDHoverType { set { return; } }
        public override float PIDHoverTau { set { return; } }


        public override void SubscribeEvents(int ms)
        {

        }
        public override void UnSubscribeEvents()
        {

        }
        public override bool SubscribedEvents()
        {
            return false;
        }
    }
}
