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
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Box = OpenSim.Framework.Geom.Box;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.CoreModules.Agent.SceneView
{
    /// <summary>
    /// A Culling Module for ScenePresences.
    /// </summary>
    /// <remarks>
    /// Cull updates for ScenePresences.  This does simple Draw Distance culling but could be expanded to work
    /// off of other scene parameters as well;
    /// </remarks>

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class SceneViewModule : ISceneViewModule, INonSharedRegionModule
    {
        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public bool UseCulling { get; set; }

        #endregion

        #region ISharedRegionModule Members

        public SceneViewModule()
        {
            UseCulling = false;
        }

        public string Name 
        {
            get { return "SceneViewModule"; }
        }
        
        public Type ReplaceableInterface 
        {
            get { return null; }
        }
        
        public void Initialize(IConfigSource source)
        {
            IConfig config = source.Configs["SceneView"];
            if (config != null)
            {
                UseCulling = config.GetBoolean("UseCulling", true);
            }
            else
            {
                UseCulling = true;
            }

            m_log.DebugFormat("[SCENE VIEW]: INITIALIZED MODULE, CULLING is {0}", (UseCulling ? "ENABLED" : "DISABLED"));
        }
        
        public void Close()
        {
        }
        
        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<ISceneViewModule>(this);
        }
        
        public void RemoveRegion(Scene scene)
        {
        }        
        
        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        #region ICullerModule Members

        /// <summary>
        /// Create a scene view for the given presence
        /// </summary>
        /// <param name="presence"></param>
        /// <returns></returns>
        public ISceneView CreateSceneView(ScenePresence presence)
        {
            return new SceneView(presence, UseCulling);
        }

        #endregion
    }
    
    public class SceneView : ISceneView
    {
        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private const int _MINIMUM_DRAW_DISTANCE = 32;
        private ScenePresence m_presence;
        private int m_perfMonMS;
        private int m_timeBeforeChildAgentUpdate;
        private OpenSim.Region.Framework.Scenes.Types.UpdateQueue m_partsUpdateQueue =
            new OpenSim.Region.Framework.Scenes.Types.UpdateQueue();
        private Dictionary<uint, ScenePartUpdate> m_updateTimes = new Dictionary<uint, ScenePartUpdate>();
        private List<UUID> m_presencesInView = new List<UUID>();
        private bool[,] m_TerrainCulling = new bool[16, 16];

        public bool UseCulling { get; set; }
        public float DistanceBeforeCullingRequired { get; set; }
        public bool NeedsFullSceneUpdate { get; set; }
        public bool HasFinishedInitialUpdate { get; set; }

        #endregion

        #region Constructor
        public SceneView(ScenePresence presence, bool useCulling)
        {
            UseCulling = useCulling;
            NeedsFullSceneUpdate = true;
            HasFinishedInitialUpdate = false;
            m_presence = presence;

            //Update every 1/4th a draw distance
            DistanceBeforeCullingRequired = _MINIMUM_DRAW_DISTANCE / 8;
        }
        #endregion

        #region Culler Methods

        /// <summary>
        /// Checks to see whether any prims, avatars, or terrain have come into view since the last culling check
        /// </summary>
        public void CheckForDistantEntitiesToShow()
        {
            if (UseCulling == false)
                return;

            //Bots don't get to check for updates
            if (m_presence.IsBot)
                return;

            //With 25K objects, this method takes around 10ms if the client has seen none of the objects in the sim
            if (m_presence.DrawDistance <= 0)
                return;

            if (m_presence.IsInTransit)
                return; // disable prim updates during a crossing, but leave them queued for after transition

            if (m_presence.Closed)
            {
                //Don't check if we are closed, and clear the update queues
                ClearAllTracking();
                return;
            }

            Util.FireAndForget((o) =>
            {
                CheckForDistantTerrainToShow();
                CheckForDistantPrimsToShow();
                CheckForDistantAvatarsToShow();
                HasFinishedInitialUpdate = true;
            });
        }

        #endregion

        #region Avatar Culling

        /// <summary>
        /// Checks to see whether any new avatars have come into view since the last time culling checks were done
        /// </summary>
        private void CheckForDistantAvatarsToShow()
        {
            List<ScenePresence> SPs;
            lock (m_presence.Scene.SyncRoot)
            {
                if (!m_presence.IsChildAgent || m_presence.Scene.m_seeIntoRegionFromNeighbor)
                    SPs = m_presence.Scene.GetScenePresences();
                else
                    return;
            }

            Vector3 clientAbsPosition = m_presence.AbsolutePosition;

            foreach (ScenePresence presence in SPs)
            {
                if (m_presence.Closed)
                {
                    ClearAllTracking();
                    return;
                }

                if (presence.UUID != m_presence.UUID)
                {
                    if (ShowEntityToClient(clientAbsPosition, presence) ||
                            ShowEntityToClient(m_presence.CameraPosition, presence))
                    {
                        if (!m_presencesInView.Contains(presence.UUID))
                        {
                            //Send the update both ways
                            SendPresenceToUs(presence);
                            presence.SceneView.SendPresenceToUs(m_presence);
                        }
                        else
                        {
                            //Check to see whether any attachments have changed for both users
                            CheckWhetherAttachmentsHaveChanged(presence);
                            if (!m_presence.IsChildAgent)
                                presence.SceneView.CheckWhetherAttachmentsHaveChanged(m_presence);
                        }
                    }
                }
            }
        }

        public void CheckWhetherAttachmentsHaveChanged(ScenePresence presence)
        {
            foreach (SceneObjectGroup grp in presence.GetAttachments())
            {
                if (CheckWhetherAttachmentShouldBeSent(grp))
                    SendGroupUpdate(grp, PrimUpdateFlags.ForcedFullUpdate);
            }
        }

        public void ClearFromScene(ScenePresence presence)
        {
            m_presencesInView.Remove(presence.UUID);
        }

        public void ClearScene()
        {
            m_presencesInView.Clear();
        }

        /// <summary>
        /// Tests to see whether the given avatar can be seen by the client at the given position
        /// </summary>
        /// <param name="clientAbsPosition">Position to check from</param>
        /// <param name="presence">The avatar to check</param>
        /// <returns></returns>
        public bool ShowEntityToClient(Vector3 clientAbsPosition, ScenePresence presence)
        {
            if (UseCulling == false)
                return true;
            Vector3 avpos;
            if (!presence.HasSafePosition(out avpos))
                return false;

            float drawDistance = m_presence.DrawDistance;
            if (m_presence.DrawDistance < _MINIMUM_DRAW_DISTANCE)
                drawDistance = _MINIMUM_DRAW_DISTANCE; //Smallest distance we will check

            return Vector3.DistanceSquared(clientAbsPosition, avpos) <= drawDistance * drawDistance;
        }

        #region Avatar Update Sending methods

        public void SendAvatarTerseUpdate(ScenePresence scenePresence)
        {
            //if (!ShowEntityToClient(m_presence.AbsolutePosition, scenePresence))
            //    return;//When they move around, they will trigger more updates, so when they get into the other avatar's DD, they will update
            Vector3 vPos = Vector3.Zero;
            Quaternion vRot = Quaternion.Identity;
            uint vParentID = 0;
            m_perfMonMS = Environment.TickCount;
            scenePresence.RecalcVisualPosition(out vPos, out vRot, out vParentID);    // vParentID is not used in terse updates.  o.O
            PhysicsActor physActor = scenePresence.PhysicsActor;
            Vector3 accel = (physActor != null) ? physActor.Acceleration : Vector3.Zero;

            // m_log.InfoFormat("[SCENE PRESENCE]: SendTerseUpdateToClient sit at {0} vel {1} rot {2} ", pos.ToString(),vel.ToString(), rot.ToString()); 
            m_presence.ControllingClient.SendAvatarTerseUpdate(scenePresence.Scene.RegionInfo.RegionHandle, (ushort)(scenePresence.Scene.TimeDilation * ushort.MaxValue), scenePresence.LocalId, vPos,
                scenePresence.Velocity, accel, vRot, scenePresence.UUID, physActor != null ? physActor.CollisionPlane : Vector4.UnitW);
            m_presence.Scene.StatsReporter.AddAgentTime(Environment.TickCount - m_perfMonMS);
            m_presence.Scene.StatsReporter.AddAgentUpdates(1);
        }

        /// <summary>
        /// Tell other client about this avatar (The client previously didn't know or had outdated details about this avatar)
        /// </summary>
        /// <param name="remoteAvatar"></param>
        public void SendFullUpdateToOtherClient(ScenePresence otherClient)
        {
            if (m_presence.IsChildAgent)
                return;//Never send data from a child client
            // 2 stage check is needed.
            if (otherClient == null)
                return;
            if (otherClient.IsBot)
                return;
            if (otherClient.ControllingClient == null)
                return;
            if (m_presence.Appearance.Texture == null)
                return;

            //if (ShowEntityToClient(otherClient.AbsolutePosition, m_presence))
                m_presence.SendAvatarData(otherClient.ControllingClient, false);
        }

        /// <summary>
        /// Tell *ALL* agents about this agent
        /// </summary>
        public void SendInitialFullUpdateToAllClients()
        {
            m_perfMonMS = Environment.TickCount;

            List<ScenePresence> avatars = m_presence.Scene.GetScenePresences();
            Vector3 clientAbsPosition = m_presence.AbsolutePosition;
            foreach (ScenePresence avatar in avatars)
            {
                // don't notify self of self (confuses the viewer).
                if (avatar.UUID == m_presence.UUID)
                    continue;
                // okay, send the other avatar to our client
//              if (ShowEntityToClient(clientAbsPosition, avatar)) 
                SendPresenceToUs(avatar);

                // also show our avatar to the others, including those in other child regions
                if (avatar.IsDeleted || avatar.IsInTransit)
                    continue;
//              if (ShowEntityToClient(avatar.AbsolutePosition, m_presence))
                avatar.SceneView.SendPresenceToUs(m_presence);
            }

            m_presence.Scene.StatsReporter.AddAgentUpdates(avatars.Count);
            m_presence.Scene.StatsReporter.AddAgentTime(Environment.TickCount - m_perfMonMS);
        }

        /// <summary>
        /// Tell our client about other client's avatar (includes appearance and full object update)
        /// </summary>
        /// <param name="m_presence"></param>
        public void SendPresenceToUs(ScenePresence avatar)
        {
            if (m_presence.IsBot)
                return;
            if (avatar.IsChildAgent)
                return;
            // uint inRegion = (uint)avatar.AgentInRegion;
            if (!avatar.IsFullyInRegion)
            {
                // m_log.WarnFormat("[SendPresenceToUs]: AgentInRegion={0:x2}",inRegion);
                return; // don't send to them yet, they aren't ready
            }
            if (avatar.IsDeleted)
                return; // don't send them, they're on their way outta here

            // Witnessed an exception internal to the .Add method with the list resizing. The avatar.UUID was
            // already in the list inside the .Add call, so it should be safe to ignore here (already added).
            try
            {
                if (!m_presencesInView.Contains(avatar.UUID))
                    m_presencesInView.Add(avatar.UUID);
            }
            catch(Exception)
            {
                m_log.InfoFormat("[SCENEVIEW]: Exception adding presence, probably race with it already added. Ignoring.");
            }

            avatar.SceneView.SendFullUpdateToOtherClient(m_presence);
            avatar.SendAppearanceToOtherAgent(m_presence);
            avatar.SendAnimPackToClient(m_presence.ControllingClient);

            avatar.SendFullUpdateForAttachments(m_presence);
        }

        /// <summary>
        /// Sends a full update for our client to all clients in the scene
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            m_perfMonMS = Environment.TickCount;

            // only send update from root agents to other clients; children are only "listening posts"
            List<ScenePresence> avatars = m_presence.Scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                if (avatar.IsDeleted)
                    continue;
                if (avatar.IsInTransit)
                    continue;
                SendFullUpdateToOtherClient(avatar);
            }

            m_presence.Scene.StatsReporter.AddAgentUpdates(avatars.Count);
            m_presence.Scene.StatsReporter.AddAgentTime(Environment.TickCount - m_perfMonMS);

            //            Thread.Sleep(100);

            m_presence.SendAnimPack();
        }

        #endregion

        #endregion

        #region Prim Culling

        /// <summary>
        /// Checks to see whether any new prims have come into view since the last time culling checks were done
        /// </summary>
        private void CheckForDistantPrimsToShow()
        {
            List<EntityBase> SOGs;
            lock (m_presence.Scene.SyncRoot)
            {
                if (!m_presence.IsChildAgent || m_presence.Scene.m_seeIntoRegionFromNeighbor)
                    SOGs = m_presence.Scene.Entities.GetAllByType<SceneObjectGroup>();
                else
                    return;
            }

            Vector3 clientAbsPosition = m_presence.AbsolutePosition;
            List<KeyValuePair<double, SceneObjectGroup>> grps = new List<KeyValuePair<double, SceneObjectGroup>>();
            foreach (EntityBase sog in SOGs)
            {
                SceneObjectGroup e = (SceneObjectGroup)sog;

                if (m_presence.Closed)
                {
                    ClearAllTracking();
                    return;
                }

                if ((e).IsAttachment)
                {
                    if(CheckWhetherAttachmentShouldBeSent(e))
                        grps.Add(new KeyValuePair<double, SceneObjectGroup>(0, e));
                    continue;
                }

                if ((e).IsAttachedHUD && (e).OwnerID != m_presence.UUID)
                    continue;//Don't ever send HUD attachments to non-owners

                IReadOnlyCollection<SceneObjectPart> sogParts = null;
                bool needsParts = false;
                lock (m_updateTimes)
                {
                    if (m_updateTimes.ContainsKey(e.LocalId))
                    {
                        needsParts = true;
                    }
                }

                if (needsParts) sogParts = e.GetParts();

                lock (m_updateTimes)
                {
                    if (m_updateTimes.ContainsKey(e.LocalId))
                    {
                        bool hasBeenUpdated = false;
                        if (sogParts == null) sogParts = e.GetParts();

                        foreach (SceneObjectPart part in sogParts)
                        {
                            if (!m_updateTimes.ContainsKey(part.LocalId))
                            {
                                //Threading issue? Shouldn't happen unless this method is called 
                                //  while a group is being sent, but hasn't sent all prims yet
                                //  so... let's ignore the prim that is missing for now, and if
                                //  any other parts change, it'll re-send it all
                                hasBeenUpdated = true;
                                break;
                            }
                            ScenePartUpdate update = m_updateTimes[part.LocalId];
                            if (update.LastFullUpdateTimeRequested == update.LastFullUpdateTime &&
                                update.LastTerseUpdateTime == update.LastTerseUpdateTimeRequested)
                                continue;//Only if we haven't sent them this prim before and it hasn't changed
                            //It's changed, check again
                            hasBeenUpdated = true;
                            break;
                        }
                        if (!hasBeenUpdated)
                            continue;//Only if we haven't sent them this prim before and it hasn't changed
                    }
                }
                
                double distance;
                if (!ShowEntityToClient(clientAbsPosition, e, out distance) &&
                    !ShowEntityToClient(m_presence.CameraPosition, e, out distance))
                    continue;

                grps.Add(new KeyValuePair<double, SceneObjectGroup>(distance, e));
            }

            KeyValuePair<double, SceneObjectGroup>[] arry = grps.ToArray();
            C5.Sorting.IntroSort<KeyValuePair<double, SceneObjectGroup>>(arry, 0, arry.Length, new DoubleComparer());

            //Sort by distance here, so that we send the closest updates first
            foreach (KeyValuePair<double, SceneObjectGroup> kvp in arry)
            {
                if (m_presence.Closed)
                {
                    ClearAllTracking();
                    return;
                }

                SendGroupUpdate(kvp.Value, PrimUpdateFlags.FindBest);
            }
        }

        private bool CheckWhetherAttachmentShouldBeSent(SceneObjectGroup e)
        {
            bool hasBeenUpdated = false;
            var attParts = e.GetParts();

            lock (m_updateTimes)
            {
                foreach (SceneObjectPart part in attParts)
                {
                    if (!m_updateTimes.ContainsKey(part.LocalId))
                    {
                        //Threading issue? Shouldn't happen unless this method is called 
                        //  while a group is being sent, but hasn't sent all prims yet
                        //  so... let's ignore the prim that is missing for now, and if
                        //  any other parts change, it'll re-send it all
                        hasBeenUpdated = true;
                        continue;
                    }
                    ScenePartUpdate update = m_updateTimes[part.LocalId];
                    if (update.LastFullUpdateTimeRequested == update.LastFullUpdateTime &&
                        update.LastTerseUpdateTime == update.LastTerseUpdateTimeRequested)
                        continue;//Only if we haven't sent them this prim before and it hasn't changed
                    //It's changed, check again
                    hasBeenUpdated = true;
                    break;
                }
            }
            return hasBeenUpdated;
        }

        private class DoubleComparer : IComparer<KeyValuePair<double, SceneObjectGroup>>
        {
            public int Compare(KeyValuePair<double, SceneObjectGroup> x, KeyValuePair<double, SceneObjectGroup> y)
            {
                //ensure objects with avatars on them are shown first followed by everything else
                if (x.Value.HasSittingAvatars && !y.Value.HasSittingAvatars)
                {
                    return -1;
                }

                return x.Key.CompareTo(y.Key);
            }
        }

        /// <summary>
        /// Tests to see whether the given group can be seen by the client at the given position
        /// </summary>
        /// <param name="clientAbsPosition">Position to check from</param>
        /// <param name="group">Object to check</param>
        /// <param name="distance">The distance to the group</param>
        /// <returns></returns>
        public bool ShowEntityToClient(Vector3 clientAbsPosition, SceneObjectGroup group, out double distance)
        {
            distance = 0;
            if (UseCulling == false)
                return true;

            if (m_presence.DrawDistance <= 0)
                return (true);

            if (group.IsAttachedHUD)
            {
                if (group.OwnerID != m_presence.UUID)
                    return false; // Never show attached HUDs
                return true;
            }
            if (group.IsAttachment)
                return true;
            if (group.HasSittingAvatars)
                return true;//Send objects that avatars are sitting on

            var box = group.BoundingBox();

            float drawDistance = m_presence.DrawDistance;
            if (m_presence.DrawDistance < _MINIMUM_DRAW_DISTANCE)
                drawDistance = _MINIMUM_DRAW_DISTANCE; //Smallest distance we will check

            if ((distance = Vector3.DistanceSquared(clientAbsPosition, group.AbsolutePosition)) <= drawDistance * drawDistance)
                return (true);

            //If the basic check fails, we then want to check whether the object is large enough that we would 
            //  want to start doing more agressive checks

            if (box.Size.X >= DistanceBeforeCullingRequired ||
                box.Size.Y >= DistanceBeforeCullingRequired ||
                box.Size.Z >= DistanceBeforeCullingRequired)
            {
                //Any side of the box is larger than the distance that 
                //  we check for, run tests on the bounding box to see 
                //  whether it is actually needed to be sent

                box = group.BoundingBox();

                float closestX;
                float closestY;
                float closestZ;

                float boxLeft = box.Center.X - box.Extent.X;
                float boxRight = box.Center.X + box.Extent.X;

                float boxFront = box.Center.Y - box.Extent.Y;
                float boxRear = box.Center.Y + box.Extent.Y;

                float boxBottom = box.Center.Z - box.Extent.Z;
                float boxTop = box.Center.Z + box.Extent.Z;

                if (clientAbsPosition.X < boxLeft)
                {
                    closestX = boxLeft;
                }
                else if (clientAbsPosition.X > boxRight)
                {
                    closestX = boxRight;
                }
                else
                {
                    closestX = clientAbsPosition.X;
                }

                if (clientAbsPosition.Y < boxFront)
                {
                    closestY = boxFront;
                }
                else if (clientAbsPosition.Y > boxRear)
                {
                    closestY = boxRear;
                }
                else
                {
                    closestY = clientAbsPosition.Y;
                }

                if (clientAbsPosition.Z < boxBottom)
                {
                    closestZ = boxBottom;
                }
                else if (clientAbsPosition.Z > boxTop)
                {
                    closestZ = boxTop;
                }
                else
                {
                    closestZ = clientAbsPosition.Z;
                }

                if (Vector3.DistanceSquared(clientAbsPosition, new Vector3(closestX, closestY, closestZ)) <= drawDistance * drawDistance)
                    return (true);
            }

            return false;
        }

        #endregion

        #region Prim Update Sending


        /// <summary>
        /// Send updates to the client about prims which have been placed on the update queue.  We don't
        /// necessarily send updates for all the parts on the queue, e.g. if an updates with a more recent
        /// timestamp has already been sent.
        /// 
        /// SHOULD ONLY BE CALLED WITHIN THE SCENE LOOP
        /// </summary>
        public void SendPrimUpdates()
        {
            if (m_presence.Closed)
            {
                ClearAllTracking();
                return;
            }

            //Bots don't get to check for updates
            if (m_presence.IsBot)
                return;

            if (m_presence.IsInTransit)
                return; // disable prim updates during a crossing, but leave them queued for after transition
            // uint inRegion = (uint)m_presence.AgentInRegion;
            if (!m_presence.IsChildAgent && !m_presence.IsFullyInRegion) 
            {
                // m_log.WarnFormat("[SendPrimUpdates]: AgentInRegion={0:x2}",inRegion);
                return; // don't send to them yet, they aren't ready
            }

            m_perfMonMS = Environment.TickCount;

            if (((UseCulling == false) || (m_presence.DrawDistance != 0)) && NeedsFullSceneUpdate)
            {
                NeedsFullSceneUpdate = false;
                if (UseCulling == false)//Send the entire heightmap
                    m_presence.ControllingClient.SendLayerData(m_presence.Scene.Heightmap.GetFloatsSerialized());

                if (UseCulling == true && !m_presence.IsChildAgent)
                    m_timeBeforeChildAgentUpdate = Environment.TickCount;

                CheckForDistantEntitiesToShow();
            }

            if (m_timeBeforeChildAgentUpdate != 0 && (Environment.TickCount - m_timeBeforeChildAgentUpdate) >= 5000)
            {
                m_log.Debug("[SCENE VIEW]: Sending child agent update to update all child regions about fully established user");
                //Send out an initial child agent update so that the child region can add their objects accordingly now that we are all set up
                m_presence.SendChildAgentUpdate();
                //Don't ever send another child update from here again
                m_timeBeforeChildAgentUpdate = 0;
            }

            // Before pulling the AbsPosition, since this isn't locked, make sure it's a good position.
            Vector3 clientAbsPosition = Vector3.Zero;
            if (!m_presence.HasSafePosition(out clientAbsPosition))
                return; // this one has gone into late transit or something, or the prim it's on has.

            int queueCount = m_partsUpdateQueue.Count;
            int time = Environment.TickCount;

            SceneObjectGroup lastSog = null;
            bool lastParentObjectWasCulled = false;
            IReadOnlyCollection<SceneObjectPart> lastParentGroupParts = null;

            while (m_partsUpdateQueue.Count > 0 && HasFinishedInitialUpdate)
            {
                if (m_presence.Closed)
                {
                    ClearAllTracking();
                    return;
                }

                KeyValuePair<SceneObjectPart, PrimUpdateFlags>? kvp = m_partsUpdateQueue.Dequeue();

                if (!kvp.HasValue || kvp.Value.Key.ParentGroup == null || kvp.Value.Key.ParentGroup.IsDeleted)
                    continue;

                SceneObjectPart part = kvp.Value.Key;

                double distance;

                // Cull part updates based on the position of the SOP.
                if ((lastSog == part.ParentGroup && lastParentObjectWasCulled) ||
                    (lastSog != part.ParentGroup &&
                                    UseCulling && !ShowEntityToClient(clientAbsPosition, part.ParentGroup, out distance) &&
                                    !ShowEntityToClient(m_presence.CameraPosition, part.ParentGroup, out distance)))
                {
                    bool sendKill = false;
                    lastParentObjectWasCulled = true;

                    lock (m_updateTimes)
                    {
                        if (m_updateTimes.ContainsKey(part.LocalId))
                        {
                            sendKill = true;
                            // If we are going to send a kill, it is for the complete object.
                            // We are telling the viewer to nuke everything it knows about ALL of 
                            // the prims, not just the child prim. So we need to remove ALL of the 
                            // prims from m_updateTimes before continuing.
                            IReadOnlyCollection<SceneObjectPart> sogPrims = part.ParentGroup.GetParts();
                            foreach (SceneObjectPart prim in sogPrims)
                            {
                                m_updateTimes.Remove(prim.LocalId);
                            }
                        }
                    }

                    //Only send the kill object packet if we have seen this object
                    //Note: I'm not sure we should be sending a kill at all in this case. -Jim
                    //      The viewer has already hidden the object if outside DD, and the
                    //      KillObject causes the viewer to discard its cache of the objects.
                    if (sendKill)
                        m_presence.ControllingClient.SendNonPermanentKillObject(m_presence.Scene.RegionInfo.RegionHandle,
                            part.ParentGroup.RootPart.LocalId);

                    lastSog = part.ParentGroup;
                    continue;
                }
                else
                {
                    lastParentObjectWasCulled = false;
                }

                IReadOnlyCollection<SceneObjectPart> parentGroupParts = null;
                bool needsParentGroupParts = false;
                lock (m_updateTimes)
                {
                    if (m_updateTimes.ContainsKey(part.ParentGroup.LocalId))
                    {
                        needsParentGroupParts = true;
                    }
                }

                if (!needsParentGroupParts)
                {
                    lastParentGroupParts = null;
                }
                else if (needsParentGroupParts && lastSog == part.ParentGroup && lastParentGroupParts != null)
                {
                    parentGroupParts = lastParentGroupParts;
                }
                else
                {
                    parentGroupParts = part.ParentGroup.GetParts();
                    lastParentGroupParts = parentGroupParts;
                }

                lock (m_updateTimes)
                {
                    bool hasBeenUpdated = false;
                    if (m_updateTimes.ContainsKey(part.ParentGroup.LocalId))
                    {
                        if (parentGroupParts == null)
                        {
                            if (lastSog == part.ParentGroup && lastParentGroupParts != null)
                            {
                                parentGroupParts = lastParentGroupParts;
                            }
                            else
                            {
                                parentGroupParts = part.ParentGroup.GetParts();
                                lastParentGroupParts = parentGroupParts;
                            }
                        }

                        foreach (SceneObjectPart p in parentGroupParts)
                        {
                            ScenePartUpdate update;
                            if (!m_updateTimes.TryGetValue(p.LocalId, out update))
                            {
                                //Threading issue? Shouldn't happen unless this method is called 
                                //  while a group is being sent, but hasn't sent all prims yet
                                //  so... let's ignore the prim that is missing for now, and if
                                //  any other parts change, it'll re-send it all
                                hasBeenUpdated = true;
                                break;
                            }

                            if (update.LastFullUpdateTimeRequested == update.LastFullUpdateTime &&
                                update.LastTerseUpdateTime == update.LastTerseUpdateTimeRequested)
                            {
                                continue;//Only if we haven't sent them this prim before and it hasn't changed
                            }

                            //It's changed, check again
                            hasBeenUpdated = true;
                            break;
                        }
                    }
                    else
                    {
                        hasBeenUpdated = true;
                    }

                    if (hasBeenUpdated)
                    {
                        SendGroupUpdate(part.ParentGroup, kvp.Value.Value);
                        lastSog = part.ParentGroup;
                        continue;
                    }
                }

                lastSog = part.ParentGroup;
                SendPartUpdate(part, kvp.Value.Value);
            }

            /*if (queueCount > 0)
            {
                m_log.DebugFormat("Update queue flush of {0} objects took {1}", queueCount, Environment.TickCount - time);
            }*/

            //ControllingClient.FlushPrimUpdates();

            m_presence.Scene.StatsReporter.AddAgentTime(Environment.TickCount - m_perfMonMS);
        }

        public void SendGroupUpdate(SceneObjectGroup sceneObjectGroup, PrimUpdateFlags updateFlags)
        {
            //Bots don't get to send updates
            if (m_presence.IsBot)
                return;

            SendPartUpdate(sceneObjectGroup.RootPart, updateFlags);
            foreach (SceneObjectPart part in sceneObjectGroup.GetParts())
            {
                if (!part.IsRootPart())
                    SendPartUpdate(part, updateFlags);
            }
        }

        public void SendPartUpdate(SceneObjectPart part, PrimUpdateFlags updateFlags)
        {
            ScenePartUpdate update = null;

            int partFullUpdateCounter = part.FullUpdateCounter;
            int partTerseUpdateCounter = part.TerseUpdateCounter;

            bool sendFullUpdate = false, sendFullInitialUpdate = false, sendTerseUpdate = false;
            lock(m_updateTimes)
            {
                if (m_updateTimes.TryGetValue(part.LocalId, out update))
                {
                    if ((update.LastFullUpdateTime != partFullUpdateCounter) ||
                            part.ParentGroup.IsAttachment)
                    {
                        //                            m_log.DebugFormat(
                        //                                "[SCENE PRESENCE]: Fully   updating prim {0}, {1} - part timestamp {2}",
                        //                                part.Name, part.UUID, part.TimeStampFull);

                        update.LastFullUpdateTime = partFullUpdateCounter;
                        update.LastFullUpdateTimeRequested = partFullUpdateCounter;
                        //also cancel any pending terses since the full covers it
                        update.LastTerseUpdateTime = partTerseUpdateCounter;
                        update.LastTerseUpdateTimeRequested = partTerseUpdateCounter;

                        sendFullUpdate = true;
                    }
                    else if (update.LastTerseUpdateTime != partTerseUpdateCounter)
                    {
                        //                            m_log.DebugFormat(
                        //                                "[SCENE PRESENCE]: Tersely updating prim {0}, {1} - part timestamp {2}",
                        //                                part.Name, part.UUID, part.TimeStampTerse);

                        update.LastTerseUpdateTime = partTerseUpdateCounter;
                        update.LastTerseUpdateTimeRequested = partTerseUpdateCounter;

                        sendTerseUpdate = true;
                    }
                }
                else
                {
                    //never been sent to client before so do full update
                    ScenePartUpdate newUpdate = new ScenePartUpdate();
                    newUpdate.FullID = part.UUID;
                    newUpdate.LastFullUpdateTime = partFullUpdateCounter;
                    newUpdate.LastFullUpdateTimeRequested = partFullUpdateCounter;

                    m_updateTimes.Add(part.LocalId, newUpdate);
                    sendFullInitialUpdate = true;
                }
            }
            if (sendFullUpdate)
            {
                part.SendFullUpdate(m_presence.ControllingClient, m_presence.GenerateClientFlags(part.UUID), updateFlags);
            }
            else if (sendTerseUpdate)
            {
                part.SendTerseUpdateToClient(m_presence.ControllingClient);
            }
            else if (sendFullInitialUpdate)
            {
                // Attachment handling
                //
                if (part.ParentGroup.IsAttachment)
                {
                    if (part != part.ParentGroup.RootPart)
                        return;

                    part.ParentGroup.SendFullUpdateToClient(m_presence.ControllingClient, PrimUpdateFlags.FullUpdate);
                    return;
                }

                part.SendFullUpdate(m_presence.ControllingClient, m_presence.GenerateClientFlags(part.UUID), PrimUpdateFlags.FullUpdate);
            }
        }

        /// <summary>
        /// Add the part to the queue of parts for which we need to send an update to the client
        /// 
        /// THIS METHOD SHOULD ONLY BE CALLED FROM WITHIN THE SCENE LOOP!!
        /// </summary>
        /// <param name="part"></param>
        public void QueuePartForUpdate(SceneObjectPart part, PrimUpdateFlags updateFlags)
        {
            if (m_presence.IsBot) return;

            // m_log.WarnFormat("[ScenePresence]: {0} Queuing update for {1} {2} {3}", part.ParentGroup.Scene.RegionInfo.RegionName, part.UUID.ToString(), part.LocalId.ToString(), part.ParentGroup.Name);
            m_partsUpdateQueue.Enqueue(part, updateFlags);
        }

        /// <summary>
        /// Clears all data we have about the region
        /// 
        /// SHOULD ONLY BE CALLED FROM CHILDREN OF THE SCENE LOOP!!
        /// </summary>
        public void ClearAllTracking()
        {
            if (m_partsUpdateQueue.Count > 0)
                m_partsUpdateQueue.Clear();
            lock (m_updateTimes)
            {
                if (m_updateTimes.Count > 0)
                    m_updateTimes.Clear();
            }
            if (m_presencesInView.Count > 0)
                m_presencesInView.Clear();
            m_TerrainCulling = new bool[16, 16];
        }

        /// <summary>
        /// Sends kill packets if the given object is within the draw distance of the avatar
        /// </summary>
        /// <param name="grp"></param>
        /// <param name="localIds"></param>
        public void SendKillObjects(SceneObjectGroup grp, List<uint> localIds)
        {
            //Bots don't get to check for updates
            if (m_presence.IsBot)
                return;

            //Only send the kill object packet if we have seen this object
            lock (m_updateTimes)
            {
                if (m_updateTimes.ContainsKey(grp.LocalId))
                    m_presence.ControllingClient.SendKillObjects(m_presence.Scene.RegionInfo.RegionHandle, localIds.ToArray());
            }
        }

        #endregion

        #region Terrain Patch Sending

        /// <summary>
        /// Informs the SceneView that the given patch has been modified and must be resent
        /// </summary>
        /// <param name="serialized"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void TerrainPatchUpdated(float[] serialized, int x, int y)
        {
            //Bots don't get to check for updates
            if (m_presence.IsBot)
                return;

            //Check to make sure that we only send it if we can see it or culling is disabled
            if ((UseCulling == false) || ShowTerrainPatchToClient(x, y))
                m_presence.ControllingClient.SendLayerData(x, y, serialized);
            else if (UseCulling == true)
                m_TerrainCulling[x, y] = false;//Resend it next time it comes back into draw distance
        }

        /// <summary>
        /// Checks to see whether any new terrain has come into view since the last time culling checks were done
        /// </summary>
        private void CheckForDistantTerrainToShow()
        {
            const int FUDGE_FACTOR = 2; //Use a fudge factor, as we can see slightly farther than the draw distance
            const int MAX_PATCH = 16;

            int startX = Math.Min((((int)(m_presence.AbsolutePosition.X - m_presence.DrawDistance)) / Constants.TerrainPatchSize) - FUDGE_FACTOR,
                (((int)(m_presence.CameraPosition.X - m_presence.DrawDistance)) / Constants.TerrainPatchSize) - FUDGE_FACTOR);
            int startY = Math.Min((((int)(m_presence.AbsolutePosition.Y - m_presence.DrawDistance)) / Constants.TerrainPatchSize) - FUDGE_FACTOR,
                (((int)(m_presence.CameraPosition.Y - m_presence.DrawDistance)) / Constants.TerrainPatchSize) - FUDGE_FACTOR);
            int endX = Math.Max((((int)(m_presence.AbsolutePosition.X + m_presence.DrawDistance)) / Constants.TerrainPatchSize) + FUDGE_FACTOR,
                (((int)(m_presence.CameraPosition.X + m_presence.DrawDistance)) / Constants.TerrainPatchSize) + FUDGE_FACTOR);
            int endY = Math.Max((((int)(m_presence.AbsolutePosition.Y + m_presence.DrawDistance)) / Constants.TerrainPatchSize) + FUDGE_FACTOR,
                (((int)(m_presence.CameraPosition.Y + m_presence.DrawDistance)) / Constants.TerrainPatchSize) + FUDGE_FACTOR);
            float[] serializedMap = m_presence.Scene.Heightmap.GetFloatsSerialized();

            if (startX < 0) startX = 0;
            if (startY < 0) startY = 0;
            if (endX > MAX_PATCH) endX = MAX_PATCH;
            if (endY > MAX_PATCH) endY = MAX_PATCH;

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    //Need to make sure we don't send the same ones over and over
                    if (!m_TerrainCulling[x, y])
                    {
                        if (ShowTerrainPatchToClient(x, y))
                        {
                            //They can see it, send it to them
                            m_TerrainCulling[x, y] = true;
                            m_presence.ControllingClient.SendLayerData(x, y, serializedMap);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check to see whether a specific terrain patch is in view
        /// </summary>
        /// <param name="x">Terrain patch X</param>
        /// <param name="y">Terrain patch Y</param>
        /// <returns></returns>
        private bool ShowTerrainPatchToClient(int x, int y)
        {
            Vector3 clientAbsPosition = m_presence.AbsolutePosition;

            clientAbsPosition.Z = 0;//Force to the ground, we only want the 2D distance

            bool success = Util.DistanceLessThan(
                clientAbsPosition,
                new Vector3(x * Constants.TerrainPatchSize, y * Constants.TerrainPatchSize, 0),
                m_presence.DrawDistance + (16*2));

            if (!success)
            {
                Vector3 clientCamPos = m_presence.CameraPosition;

                clientCamPos.Z = 0;//Force to the ground, we only want the 2D distance
                success = Util.DistanceLessThan(
                    clientCamPos,
                    new Vector3(x * Constants.TerrainPatchSize, y * Constants.TerrainPatchSize, 0),
                    m_presence.DrawDistance + (16 * 2));
            }

            return success;
        }

        #endregion

        #region Private classes

        public class ScenePartUpdate
        {
            public UUID FullID;
            public int LastFullUpdateTime;
            public int LastFullUpdateTimeRequested;
            public int LastTerseUpdateTime;
            public int LastTerseUpdateTimeRequested;

            public ScenePartUpdate()
            {
                FullID = UUID.Zero;
                LastFullUpdateTime = 0;
                LastFullUpdateTimeRequested = 0;
                LastTerseUpdateTime = 0;
                LastTerseUpdateTimeRequested = 0;
            }
        }
        
        #endregion
    }
}
