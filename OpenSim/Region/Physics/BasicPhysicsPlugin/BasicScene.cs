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

    public class BasicScene : PhysicsScene
    {
        public const float GRAVITY = -9.81f;
        public const int TIMESTEP = 15;
        public const float TIMESTEP_IN_SECONDS = TIMESTEP / 1000.0f;
        public const float DILATED_TIMESTEP_IN_SECONDS = TIMESTEP_IN_SECONDS * 2.0f;
        public const int SIMULATE_DELAY_TO_BEGIN_DILATION = (int)(TIMESTEP * 1.9f);

        private const int UPDATE_WATCHDOG_FRAMES = 200;
        private const int CHECK_EXPIRED_KINEMATIC_FRAMES = (int) ((1.0f / TIMESTEP_IN_SECONDS) * 60.0f);
        private const int UPDATE_FPS_FRAMES = 30;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private C5.HashSet<BasicActor> _actors = new C5.HashSet<BasicActor>();
        private float[] _heightMap;

        private Thread HeartbeatThread;
        private Thread TimingThread;
        private ManualResetEventSlim _timingSignal = new ManualResetEventSlim(true);
        private uint _lastSimulate;
        private int _frameNum;
        private MovingIntegerAverage _frameTimeAvg = new MovingIntegerAverage(10);

        private volatile bool _stop = false;
        private bool _simulating = false;

        private uint _lastFpsCalc = (uint)Environment.TickCount;
        private short _framesSinceLastFpsCalc = 0;
        private volatile float _currFps = 60.0f;

        public BasicScene()
        {
        }

        #region implemented abstract members of PhysicsScene

        public override void Initialize(IMesher meshmerizer, IConfigSource config, UUID regionId)
        {
            // Does nothing much right now
            _mesher = meshmerizer;

            //fire up our work loop
            HeartbeatThread = Watchdog.StartThread(new ThreadStart(Heartbeat), "Physics Heartbeat",
                ThreadPriority.Normal, false);

            TimingThread = Watchdog.StartThread(new ThreadStart(DoTiming), string.Format("Physics Timing"),
                ThreadPriority.Highest, false);
        }

        public override void Dispose()
        {
            _stop = true;
            TimingThread.Join();

            _timingSignal.Set();
            HeartbeatThread.Join();
        }

        #endregion

        void DoTiming()
        {
            // Note: Thread priorities are not really implementable under UNIX-style systems with user privs.

            while (!_stop)
            {
                _timingSignal.Set();

                if (_frameNum % 100 == 0)
                {
                    Watchdog.UpdateThread();
                }

                Thread.Sleep(TIMESTEP);
                Interlocked.Increment(ref _frameNum);
            }
        }

        private void Heartbeat()
        {
            uint lastSimulateFrame = 0;
            while (!_stop)
            {
                uint startingFrame = (uint)Environment.TickCount;

                uint uframe = (uint)_frameNum;

                if (_simulating && (uframe > lastSimulateFrame))
                {
                    uint tickCount = (uint)Environment.TickCount;
                    uint ticksSinceLastSimulate = Math.Max(tickCount - _lastSimulate, TIMESTEP);

                    _lastSimulate = (uint)Environment.TickCount;
                    lastSimulateFrame = uframe;

                    if (ticksSinceLastSimulate >= SIMULATE_DELAY_TO_BEGIN_DILATION)
                    {
                        Simulate(DILATED_TIMESTEP_IN_SECONDS, ticksSinceLastSimulate, uframe, true);
                        //m_log.DebugFormat("[PHYSICS]: Dilated simulate {0}", ticksSinceLastSimulate);
                    }
                    else
                    {
                        Simulate(ticksSinceLastSimulate * 0.001f, ticksSinceLastSimulate, uframe, false);
                    }

                    ++_framesSinceLastFpsCalc;
                    if (uframe % UPDATE_WATCHDOG_FRAMES == 0)
                    {
                        Watchdog.UpdateThread();
                    }
                    if (uframe % UPDATE_FPS_FRAMES == 0)
                    {
                        this.UpdateFpsCalc();
                    }
                }

                _frameTimeAvg.AddValue((uint)Environment.TickCount - startingFrame);

                //if (_currentCommandQueue.Count == 0)
                //{
                //    _timingSignal.Wait();
                //}

                _timingSignal.Reset();
            }
        }

        private void UpdateFpsCalc()
        {
            uint msSinceLastCalc = (uint)Environment.TickCount - _lastFpsCalc;
            _currFps = _framesSinceLastFpsCalc / (msSinceLastCalc * 0.001f);

            _framesSinceLastFpsCalc = 0;
            _lastFpsCalc = (uint)Environment.TickCount;
            //Console.WriteLine("FPS: {0}", _currFps);

            //const float LOW_FPS_THRESHOLD = 54.0f;
            const float LOW_FPS_THRESHOLD = 45.0f;
            if (_currFps < LOW_FPS_THRESHOLD)
            {
                m_log.WarnFormat("[BasicPhysics] Low physics FPS {0}", _currFps);
            }
        }

        #region implemented abstract members of PhysicsScene

        public override PhysicsActor AddAvatar(string avName, OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation, OpenMetaverse.Vector3 size, bool isFlying, OpenMetaverse.Vector3 initialVelocity)
        {
            BasicActor act = new BasicActor(this, size.Z, size.X, position, rotation, isFlying, initialVelocity);
            _actors.Add(act);
            return act;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            BasicActor act = (BasicActor)actor;
            if (_actors.Contains(act))
            {
                _actors.Remove(act);
            }
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
            //throw new NotSupportedException("AddPhysicsActorTaint must be called with a taint type");
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim, TaintType taint)
        {
            m_log.DebugFormat("Called AddPhysicsActorTaint() for prim name {0}", prim.SOPName);
        }

        public override PhysicsActor AddPrimShape(string primName, AddPrimShapeFlags flags, BulkShapeData shapeData)
        {
            byte[] serializedPhysicsProperties = shapeData.PhysicsProperties;
            BasicPhysicsProperties properties = BasicPhysicsProperties.DeserializeOrCreateNew(this, shapeData.Material, serializedPhysicsProperties);

            bool isPhysical = (flags & PhysicsScene.AddPrimShapeFlags.Physical) != 0;
            return new BasicPrim(this, shapeData.Pbs, shapeData.Position, shapeData.Rotation, /*Shape, Actor,*/ isPhysical, properties/*, collisionGroup*/);
        }

        public override void BulkAddPrimShapes(ICollection<BulkShapeData> shapeData, AddPrimShapeFlags flags)
        {
            // Called on restore of objects when region boots up.  Other times as well, but that's the critical.
            AddPrimShapeFlags _flags = flags;
            ICollection<BulkShapeData> _shapes = shapeData;
            int _totalNumShapes = _shapes.Count;

            bool _rootHasVdSet = false;
            bool isPhysical = (_flags & PhysicsScene.AddPrimShapeFlags.Physical) != 0;

            //we have all the shapes for the parent and all children, time to construct the group
            bool first = true;
            BasicPrim rootPrim = null;

            //CollisionGroupFlag collisionGroup = (_flags & PhysicsScene.AddPrimShapeFlags.Phantom) == 0 ? CollisionGroupFlag.Normal : CollisionGroupFlag.PhysicalPhantom;
            foreach (BulkShapeData shape in _shapes)
            {
                if (first)
                {
                    BasicPhysicsProperties properties = BasicPhysicsProperties.DeserializeOrCreateNew(this, shape.Material, shape.PhysicsProperties);
                    _rootHasVdSet = properties.VolumeDetectActive;

                    if (_rootHasVdSet)
                    {
                        isPhysical = false;
                    }

                    rootPrim = new BasicPrim(this, shape.Pbs, shape.Position, shape.Rotation, /*_newPrimaryShape, actor, */
                        isPhysical, properties/*, collisionGroup*/);

                    shape.OutActor = rootPrim;

                    first = false;
                }
                else
                {
                    BasicPhysicsProperties properties = BasicPhysicsProperties.DeserializeOrCreateNew(this, shape.Material, shape.PhysicsProperties);

                    BasicPrim childPrim = new BasicPrim(rootPrim, this, shape.Pbs, shape.Position, shape.Rotation, /*phyShape, 
                        null,*/ isPhysical, properties/*, collisionGroup*/);
                    rootPrim.LinkPrimAsChildSync(/*phyShape, */childPrim, shape.Position, shape.Rotation, true);

                    shape.OutActor = childPrim;
                }
            }
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            // TODO maybe
            m_log.DebugFormat("Called RemovePrim() for prim name {0}", prim.SOPName);
        }

        public override float Simulate(float timeStep, uint ticksSinceLastSimulate, uint frameNum, bool dilated)
        {
            //ProcessDynamicPrimChanges(timeStep, ticksSinceLastSimulate, frameNum);
            //ProcessCollisionRepeats(timeStep, ticksSinceLastSimulate, frameNum);

            //run avatar dynamics at 1/2 simulation speed (30fps nominal)
            if (frameNum % 2 == 0)
            {
                foreach (BasicActor character in _actors)
                {
                    character.SyncWithPhysics(timeStep, ticksSinceLastSimulate, frameNum);
                }
            }

            return 0.0f;
        }

        public override bool IsThreaded
        {
            // Ignored.  If this returns true then GetResults is called.
            get { return false; }
        }

        public override void GetResults()
        {
            // Ignored.
        }

        public override void SetTerrain(float[] heightMap, int revision)
        {
            _heightMap = heightMap;
        }

        public override void SetWaterLevel(float baseheight)
        {
            // Ignored.
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            return new Dictionary<uint, float>(); // Ignored.
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

        public override List<ContactResult> RayCastWorld(Vector3 start, Vector3 direction, float distance, int hitAmounts)
        {
            List<ContactResult> contactResults = new List<ContactResult>();
            AutoResetEvent ev = new AutoResetEvent(false);
            RayCastWorld(start, direction, distance, hitAmounts, (r) =>
                {
                    contactResults = r;
                    ev.Set();
                });
            ev.WaitOne(1000);
            return contactResults;
        }

        public override void RayCastWorld(Vector3 start, Vector3 direction, float distance, int hitAmounts, System.Action<List<ContactResult>> result)
        {
            // Used by Phlox and BotManager
            // Only consumer of void RayCastWorld()
        }

        public override float SimulationFPS
        {
            get
            {
                return _currFps;
            }
        }

        public override int SimulationFrameTimeAvg
        {
            get
            {
                return _frameTimeAvg.CalculateAverage();
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
    
}
