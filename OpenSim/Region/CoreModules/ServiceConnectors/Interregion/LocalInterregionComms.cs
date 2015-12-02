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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.ServiceConnectors.Interregion
{
    public class LocalInterregionComms : IRegionModule, IInterregionCommsOut, IInterregionCommsIn
    {
        private bool m_enabled = false;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Scene> m_sceneList = new List<Scene>();

        #region Events
        public event ChildAgentUpdateReceived OnChildAgentUpdate;

        #endregion /* Events */

        #region IRegionModule

        public void Initialize(Scene scene, IConfigSource config)
        {
            if (m_sceneList.Count == 0)
            {
                IConfig startupConfig = config.Configs["Communications"];

                if ((startupConfig != null) && (startupConfig.GetString("InterregionComms", "RESTComms") == "LocalComms"))
                {
                    m_log.Debug("[LOCAL COMMS]: Enabling InterregionComms LocalComms module");
                    m_enabled = true;
                }
            }

            if (!m_enabled)
                return;

            Init(scene);
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LocalInterregionCommsModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
        /// <summary>
        /// Can be called from other modules.
        /// </summary>
        /// <param name="scene"></param>
        public void Init(Scene scene)
        {
            if (!m_sceneList.Contains(scene))
            {
                lock (m_sceneList)
                {
                    m_sceneList.Add(scene);
                    if (m_enabled)
                        scene.RegisterModuleInterface<IInterregionCommsOut>(this);
                    scene.RegisterModuleInterface<IInterregionCommsIn>(this);
                }

            }
        }

        #endregion /* IRegionModule */

        #region IInterregionComms

        /**
         * Agent-related communications 
         */

        public bool SendCreateChildAgent(ulong regionHandle, AgentCircuitData aCircuit, bool authorize, out string reason)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    // If this is intended as a root agent entry into the region, check whether it's authorized (e.g. not banned).
                    if (authorize && !s.AuthorizeUserInRegion(aCircuit.AgentID, aCircuit.FirstName, aCircuit.LastName, aCircuit.ClientVersion, out reason))
                        return false;

                    //                    m_log.DebugFormat("[LOCAL COMMS]: Found region {0} to send SendCreateChildAgent", regionHandle);
                    return s.NewUserConnection(aCircuit, out reason);
                }
            }

            //            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for SendCreateChildAgent", regionHandle);
            reason = "Did not find region.";
            return false;
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentData cAgentData)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.DebugFormat(
                    //    "[LOCAL COMMS]: Found region {0} {1} to send ChildAgentUpdate", 
                    //    s.RegionInfo.RegionName, regionHandle);
                    
                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
            }
            
//            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for ChildAgentUpdate", regionHandle);
            return false;
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentPosition cAgentData)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool SendRetrieveRootAgent(ulong regionHandle, UUID id, out IAgentData agent)
        {
            agent = null;
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                    return s.IncomingRetrieveRootAgent(id, out agent);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool SendReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            //uint x, y;
            //Utils.LongToUInts(regionHandle, out x, out y);
            //x = x / Constants.RegionSize;
            //y = y / Constants.RegionSize;
            //m_log.Debug("\n >>> Local SendReleaseAgent " + x + "-" + y);
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendReleaseAgent");
                    return s.IncomingReleaseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendReleaseAgent");
            return false;
        }

        public bool SendCloseAgent(ulong regionHandle, UUID id)
        {
            //uint x, y;
            //Utils.LongToUInts(regionHandle, out x, out y);
            //x = x / Constants.RegionSize;
            //y = y / Constants.RegionSize;
            //m_log.Debug("\n >>> Local SendCloseAgent " + x + "-" + y);
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCloseAgent");
                    return s.IncomingCloseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendCloseAgent");
            return false;
        }

        /**
         * Object-related communications 
         */

        public bool SendCreateObject(ulong regionHandle, SceneObjectGroup sog, List<UUID> avatars, bool isLocalCall, Vector3 posInOtherRegion,
            bool isAttachment)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCreateObject");
                    if (isLocalCall)
                    {
                        if (!isAttachment)
                        {
                            sog.OffsetForNewRegion(posInOtherRegion);
                        }

                        // We need to make a local copy of the object
                        ISceneObject sogClone = sog.CloneForNewScene();
                        sogClone.SetState(sog.GetStateSnapshot(true), s.RegionInfo.RegionID);
                        return s.IncomingCreateObject(sogClone, avatars);
                    }
                    else
                    {
                        // Use the object as it came through the wire
                        return s.IncomingCreateObject(sog, avatars);
                    }
                }
            }
            return false;
        }

        public bool SendCreateObject(ulong regionHandle, UUID userID, UUID itemID)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    return s.IncomingCreateObject(userID, itemID);
                }
            }
            return false;
        }

        public bool SendDeleteObject(ulong regionHandle, UUID objectID, long nonceID)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    SceneObjectGroup sog = (part == null) ? null : part.ParentGroup;
                    if (sog != null)
                    {
                        m_log.InfoFormat("[LOCAL COMMS]: Crossing abort - deleting object {0} named '{1}'.", sog.UUID, sog.Name);
                        s.DeleteSceneObject(sog, false, true, false);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool SendUpdateEstateInfo(UUID regionID)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionID == regionID)
                {
                    // m_log.DebugFormat("[LOCAL COMMS]: Found region {0} to send SendUpdateEstateInfo", regionHandle);
                    EstateSettings es = s.StorageManager.EstateDataStore.LoadEstateSettings(s.RegionInfo.RegionID);
                    if (es == null)
                        return false;

                    // Don't replace the existing estate settings unless we were able to successfully fully load them.
                    s.RegionInfo.EstateSettings = es;
                    return true;
                }
            }
            return false;
        }

        #endregion /* IInterregionComms */

        #region Misc

        public UUID GetRegionID(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return s.RegionInfo.RegionID;
            }
            // ? weird. should not happen
            return m_sceneList[0].RegionInfo.RegionID;
        }

        public bool IsLocalRegion(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return true;
            return false;
        }

        public SimpleRegionInfo GetRegion(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return s.RegionInfo;

            return null;
        }

        #endregion

        public ChildAgentUpdate2Response SendChildAgentUpdate2(SimpleRegionInfo regionInfo, AgentData data)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionInfo.RegionHandle)
                {
                    return s.IncomingChildAgentDataUpdate2(data);
                }
            }

            return ChildAgentUpdate2Response.Error;
        }


        public System.Threading.Tasks.Task<Tuple<bool, string>> SendCreateRemoteChildAgentAsync(SimpleRegionInfo regionInfo, AgentCircuitData aCircuit)
        {
            throw new NotImplementedException();
        }

        internal bool SendWaitScenePresence(ulong regionHandle, UUID agentId, int maxSpWait)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    if (s.IncomingWaitScenePresence(agentId, maxSpWait))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public System.Threading.Tasks.Task<bool> SendCloseAgentAsync(SimpleRegionInfo regionInfo, UUID id)
        {
            throw new NotImplementedException();
        }
    }
}
