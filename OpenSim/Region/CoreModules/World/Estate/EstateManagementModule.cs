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
using System.IO;
using System.Reflection;
using System.Security;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Estate
{
    public class EstateManagementModule : IEstateModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private delegate void LookupUUIDS(List<UUID> uuidLst);

        private Scene m_scene;

        private EstateTerrainXferHandler TerrainUploader = null;

        #region Packet Data Responders

        private void sendDetailedEstateData(IClientAPI remote_client, UUID invoice)
        {
            uint sun = 0;

            if (!m_scene.RegionInfo.EstateSettings.UseGlobalTime)
                sun=(uint)(m_scene.RegionInfo.EstateSettings.SunPosition*1024.0) + 0x1800;
            UUID estateOwner;
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                estateOwner = m_scene.RegionInfo.EstateSettings.EstateOwner;
            else
                estateOwner = m_scene.RegionInfo.MasterAvatarAssignedUUID;

            if (m_scene.Permissions.IsGod(remote_client.AgentId))
                estateOwner = remote_client.AgentId;

            remote_client.SendDetailedEstateData(invoice,
                    m_scene.RegionInfo.EstateSettings.EstateName,
                    m_scene.RegionInfo.EstateSettings.EstateID,
                    m_scene.RegionInfo.EstateSettings.ParentEstateID,
                    GetEstateFlags(),
                    sun,
                    m_scene.RegionInfo.RegionSettings.Covenant,
                    m_scene.RegionInfo.RegionSettings.CovenantLastUpdated,
                    m_scene.RegionInfo.EstateSettings.AbuseEmail,
                    estateOwner);

            remote_client.SendEstateUUIDList(invoice,
                    (int)Constants.EstateAccessDeltaResponse.EstateManagers,
                    m_scene.RegionInfo.EstateSettings.EstateManagers,
                    m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendEstateUUIDList(invoice,
                    (int)Constants.EstateAccessDeltaResponse.AllowedUsers,
                    m_scene.RegionInfo.EstateSettings.EstateAccess,
                    m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendEstateUUIDList(invoice,
                    (int)Constants.EstateAccessDeltaResponse.AllowedGroups,
                    m_scene.RegionInfo.EstateSettings.EstateGroups,
                    m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendBannedUserList(invoice,
                    m_scene.RegionInfo.EstateSettings.EstateBans,
                    m_scene.RegionInfo.EstateSettings.EstateID);
        }

        private void estateSetRegionInfoHandler(bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor,
                                                int matureLevel, bool restrictPushObject, bool allowParcelChanges)
        {
            if (blockTerraform)
                m_scene.RegionInfo.RegionSettings.BlockTerraform = true;
            else
                m_scene.RegionInfo.RegionSettings.BlockTerraform = false;

            if (noFly)
                m_scene.RegionInfo.RegionSettings.BlockFly = true;
            else
                m_scene.RegionInfo.RegionSettings.BlockFly = false;

            if (allowDamage)
                m_scene.RegionInfo.RegionSettings.AllowDamage = true;
            else
                m_scene.RegionInfo.RegionSettings.AllowDamage = false;

            if (blockLandResell)
                m_scene.RegionInfo.RegionSettings.AllowLandResell = false;
            else
                m_scene.RegionInfo.RegionSettings.AllowLandResell = true;

            m_scene.RegionInfo.RegionSettings.AgentLimit = (byte) maxAgents;

            m_scene.RegionInfo.RegionSettings.ObjectBonus = objectBonusFactor;

            if (matureLevel <= 13)
                m_scene.RegionInfo.RegionSettings.Maturity = 0;
            else if (matureLevel <= 21)
                m_scene.RegionInfo.RegionSettings.Maturity = 1;
            else
                m_scene.RegionInfo.RegionSettings.Maturity = 2;

            if (restrictPushObject)
                m_scene.RegionInfo.RegionSettings.RestrictPushing = true;
            else
                m_scene.RegionInfo.RegionSettings.RestrictPushing = false;

            if (allowParcelChanges)
                m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide = true;
            else
                m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide = false;

            m_scene.RegionInfo.RegionSettings.Save();

            sendRegionInfoPacketToAll();
        }

        public void setEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, UUID texture)
        {
            if (texture == UUID.Zero)
                return;

            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture1 = texture;
                    break;
                case 1:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture2 = texture;
                    break;
                case 2:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture3 = texture;
                    break;
                case 3:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture4 = texture;
                    break;
            }
            m_scene.RegionInfo.RegionSettings.Save();
            m_scene.MarkMapTileTainted(WorldMapTaintReason.TerrainTextureChange);
        }

        public void setEstateTerrainTextureHeights(IClientAPI client, int corner, float lowValue, float highValue)
        {
            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.RegionSettings.Elevation1SW = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2SW = highValue;
                    break;
                case 1:
                    m_scene.RegionInfo.RegionSettings.Elevation1NW = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2NW = highValue;
                    break;
                case 2:
                    m_scene.RegionInfo.RegionSettings.Elevation1SE = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2SE = highValue;
                    break;
                case 3:
                    m_scene.RegionInfo.RegionSettings.Elevation1NE = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2NE = highValue;
                    break;
            }
            m_scene.RegionInfo.RegionSettings.Save();
            m_scene.MarkMapTileTainted(WorldMapTaintReason.TerrainTextureChange);
        }

        private void handleCommitEstateTerrainTextureRequest(IClientAPI remoteClient)
        {
            sendRegionHandshakeToAll();
        }

        public void setRegionTerrainSettings(float WaterHeight,
                float TerrainRaiseLimit, float TerrainLowerLimit,
                bool UseEstateSun, bool UseFixedSun, float SunHour,
                bool UseGlobal, bool EstateFixedSun, float EstateSunHour)
        {
            // Water Height
            m_scene.RegionInfo.RegionSettings.WaterHeight = WaterHeight;

            // Terraforming limits
            m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit = TerrainRaiseLimit;
            m_scene.RegionInfo.RegionSettings.TerrainLowerLimit = TerrainLowerLimit;

            // Time of day / fixed sun
            m_scene.RegionInfo.RegionSettings.UseEstateSun = UseEstateSun;
            m_scene.RegionInfo.RegionSettings.FixedSun = UseFixedSun;
            m_scene.RegionInfo.RegionSettings.SunPosition = SunHour;

            TriggerEstateToolsSunUpdate();

            //m_log.Debug("[ESTATE]: UFS: " + UseFixedSun.ToString());
            //m_log.Debug("[ESTATE]: SunHour: " + SunHour.ToString());

            sendRegionInfoPacketToAll();
            m_scene.RegionInfo.RegionSettings.Save();
        }

        private void handleEstateRestartSimRequest(IClientAPI remoteClient, int timeInSeconds)
        {
            m_scene.Restart(timeInSeconds);
        }

        private void handleChangeEstateCovenantRequest(IClientAPI remoteClient, UUID estateCovenantID, uint lastUpdated)
        {
            m_scene.RegionInfo.RegionSettings.Covenant = estateCovenantID;
            m_scene.RegionInfo.RegionSettings.CovenantLastUpdated = lastUpdated;
            m_scene.RegionInfo.RegionSettings.Save();
        }

        private void SendEstateUserAccessChanged(IClientAPI remote_client, UUID invoice, bool bannedChanged)
        {
            // Any time the viewer sends a change to the banned user list or the user access list, it clears both lists.
            // So for any change request for either, we will send both back. Factored here.

            // use 'bannedChanged' to send whichever one changed last

            if (bannedChanged)
                remote_client.SendEstateUUIDList(invoice, (int)Constants.EstateAccessDeltaResponse.AllowedUsers,
                                m_scene.RegionInfo.EstateSettings.EstateAccess, m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendBannedUserList(invoice, m_scene.RegionInfo.EstateSettings.EstateBans, m_scene.RegionInfo.EstateSettings.EstateID);

            if (!bannedChanged)
                remote_client.SendEstateUUIDList(invoice, (int)Constants.EstateAccessDeltaResponse.AllowedUsers,
                                m_scene.RegionInfo.EstateSettings.EstateAccess, m_scene.RegionInfo.EstateSettings.EstateID);
        }

        public EstateResult EstateBanUser(UUID AgentId, bool isBan)
        {
            if (AgentId == UUID.Zero)
                return EstateResult.InvalidReq; // not found
            if (isBan)
            {
                if (m_scene.IsEstateManager(AgentId))
                    return EstateResult.InvalidReq; // never process EO
                if (m_scene.IsEstateOwnerPartner(AgentId))
                    return EstateResult.InvalidReq; // never process EO
                if (AgentId == m_scene.RegionInfo.MasterAvatarAssignedUUID)
                    return EstateResult.InvalidReq; // never process owner
            }

            EstateBan[] banlistcheck = m_scene.RegionInfo.EstateSettings.EstateBans;

            bool alreadyInList = false;

            for (int i = 0; i < banlistcheck.Length; i++)
            {
                if (AgentId == banlistcheck[i].BannedUserID)
                {
                    alreadyInList = true;
                    break;
                }
            }

            if (!isBan)
            {   // This is an unban.
                if (!alreadyInList)
                    return EstateResult.AlreadySet;
                m_scene.RegionInfo.EstateSettings.RemoveBan(AgentId);
                SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveBans);
                return EstateResult.Success;
            }

            // This is a ban.
            
            // A user cannot be in both the banned and access lists.
            // if they are being banned, remove them from access.
            EstateAllowUser(AgentId, false);    
            if (alreadyInList)
                return EstateResult.AlreadySet;

            EstateBan item = new EstateBan();
            item.BannedUserID = AgentId;
            item.EstateID = m_scene.RegionInfo.EstateSettings.EstateID;
            item.BannedHostAddress = "0.0.0.0";
            item.BannedHostIPMask = "0.0.0.0";
            m_scene.RegionInfo.EstateSettings.AddBan(item);
            SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveBans);

            // Banned, now shoo them away.
            ScenePresence sp = m_scene.GetScenePresence(AgentId);
            if (sp != null)
            {
                if (!sp.IsChildAgent)
                {
                    UserProfileData UserProfile = m_scene.CommsManager.UserService.GetUserProfile(AgentId);
                    if ((UserProfile != null) && (UserProfile.HomeRegionID == m_scene.RegionInfo.RegionID))
                    {
                        // Can't send them to their home region, already there and now banned.
                        sp.ControllingClient.Kick("You have been banned from your Home location. You must login directly to a different region.");
                        System.Threading.Thread.Sleep(1000);
                        sp.Scene.IncomingCloseAgent(AgentId);
                    }
                    else
                    {
                        sp.ControllingClient.SendTeleportLocationStart();
                        m_scene.TeleportClientHome(AgentId, sp.ControllingClient);
                    }
                }
            }
            return EstateResult.Success;
        }

        public EstateResult EstateQueryBannedUser(UUID AgentId)
        {
            if (AgentId == UUID.Zero)
                return EstateResult.InvalidReq; // not found

            EstateBan[] banlistcheck = m_scene.RegionInfo.EstateSettings.EstateBans;

            bool alreadyInList = false;

            for (int i = 0; i < banlistcheck.Length; i++)
            {
                if (AgentId == banlistcheck[i].BannedUserID)
                {
                    alreadyInList = true;
                    break;
                }
            }

            return (alreadyInList) ? EstateResult.Success : EstateResult.InvalidReq;
        }

        public EstateResult EstateAllowUser(UUID AgentId, bool isAllowed)
        {
            if (AgentId == UUID.Zero)
                return EstateResult.InvalidReq; // not found

            UUID[] accessList = m_scene.RegionInfo.EstateSettings.EstateAccess;

            bool alreadyInList = false;
            for (int i = 0; i < accessList.Length; i++)
            {
                if (AgentId == accessList[i])
                {
                    alreadyInList = true;
                    break;
                }
            }

            if (isAllowed)
            {
                // A user cannot be in both the banned and access lists.
                // if they are being allowed, remove them from ban list.
                EstateBanUser(AgentId, false);

                if (alreadyInList)
                    return EstateResult.AlreadySet;
                m_scene.RegionInfo.EstateSettings.AddAccess(AgentId);
            }
            else
            {
                if (!alreadyInList)
                    return EstateResult.AlreadySet;
                m_scene.RegionInfo.EstateSettings.RemoveAccess(AgentId);
            }
            SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveUsers);
            return EstateResult.Success;
        }

        public EstateResult EstateQueryAllowedUser(UUID AgentId)
        {
            if (AgentId == UUID.Zero)
                return EstateResult.InvalidReq; // not found

            UUID[] accessList = m_scene.RegionInfo.EstateSettings.EstateAccess;

            bool alreadyInList = false;
            for (int i = 0; i < accessList.Length; i++)
            {
                if (AgentId == accessList[i])
                {
                    alreadyInList = true;
                    break;
                }
            }

            return (alreadyInList) ? EstateResult.Success : EstateResult.InvalidReq;
        }

        public EstateResult EstateAllowGroup(UUID GroupId, bool isAllowed)
        {
            if (GroupId == UUID.Zero)
                return EstateResult.InvalidReq; // not found

            UUID[] accessList = m_scene.RegionInfo.EstateSettings.EstateGroups;

            bool alreadyInList = false;
            for (int i = 0; i < accessList.Length; i++)
            {
                if (GroupId == accessList[i])
                {
                    alreadyInList = true;
                    break;
                }
            }

            if (isAllowed)
            {
                if (alreadyInList)
                    return EstateResult.AlreadySet;
                m_scene.RegionInfo.EstateSettings.AddGroup(GroupId);
            }
            else
            {
                if (!alreadyInList)
                    return EstateResult.AlreadySet;
                m_scene.RegionInfo.EstateSettings.RemoveGroup(GroupId);
            }
            SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveGroups);
            return EstateResult.Success;
        }

        public EstateResult EstateQueryAllowedGroup(UUID GroupId)
        {
            if (GroupId == UUID.Zero)
                return EstateResult.InvalidReq; // not found

            UUID[] accessList = m_scene.RegionInfo.EstateSettings.EstateGroups;

            bool alreadyInList = false;
            for (int i = 0; i < accessList.Length; i++)
            {
                if (GroupId == accessList[i])
                {
                    alreadyInList = true;
                    break;
                }
            }

            return (alreadyInList) ? EstateResult.Success : EstateResult.InvalidReq;
        }

        private void handleEstateAccessDeltaRequest(IClientAPI remote_client, UUID invoice, int estateAccessType, UUID user)
        {
            // EstateAccessDelta handles Estate Managers, Sim Access, Sim Banlist, allowed Groups..  etc.

            // OMV documents AllEstates variants as either bit 0 or bit 1 set.
            // Testing finds it's always bit 0, however to be safe, let's treat either as on.
            bool noReply = ((int)estateAccessType & (int)Constants.EstateAccessDeltaCommands.NoReply) != 0;
            bool allEstates = ((int)estateAccessType & 3) != 0; // mask off lower 2 bits
            int operation = (int)estateAccessType & ~((int)Constants.EstateAccessDeltaCommands.NoReply|3); // mask off lower 2 bits + NoReply

            if ((operation & (int)Constants.EstateAccessDeltaCommands.BanUser) != 0) 
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateResult result = EstateBanUser(user, true);
                    if (result == EstateResult.Success)
                    {
                        if (!noReply)
                            SendEstateUserAccessChanged(remote_client, invoice, true);
                    }
                    else
                    if (result == EstateResult.AlreadySet)
                    {
                        remote_client.SendAlertMessage("User is already on the region ban list");
                    }
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate ban list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.UnbanUser) != 0) 
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateResult result = EstateBanUser(user, false);
                    if (result == EstateResult.Success)
                    {
                        if (!noReply)
                            SendEstateUserAccessChanged(remote_client, invoice, true);
                    }
                    else
                    if (result == EstateResult.AlreadySet)
                    {
                        remote_client.SendAlertMessage("User is not on the region ban list");
                    }
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate ban list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.AddUserAsAllowed) != 0) 
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateResult result = EstateAllowUser(user, true);
                    if (result == EstateResult.Success)
                    {
                        if (!noReply)
                            SendEstateUserAccessChanged(remote_client, invoice, false);
                    }
                    else
                    if (result == EstateResult.AlreadySet)
                    {
                        remote_client.SendAlertMessage("User is already on the region access list.");
                    }
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate access list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.RemoveUserAsAllowed) != 0) 
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateResult result = EstateAllowUser(user, false);
                    if (result == EstateResult.Success)
                    {
                        if (!noReply)
                            SendEstateUserAccessChanged(remote_client, invoice, false);
                    }
                    else
                    if (result == EstateResult.AlreadySet)
                    {
                        remote_client.SendAlertMessage("User is not on the region access list");
                    }
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate access list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.AddGroupAsAllowed) != 0) 
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateResult result = EstateAllowGroup(user, true);
                    if (result == EstateResult.Success)
                    {
                        if (!noReply)
                            remote_client.SendEstateUUIDList(invoice, (int)Constants.EstateAccessDeltaResponse.AllowedGroups,
                                        m_scene.RegionInfo.EstateSettings.EstateGroups, m_scene.RegionInfo.EstateSettings.EstateID);
                    }
                    else
                    if (result == EstateResult.AlreadySet)
                    {
                        remote_client.SendAlertMessage("Group is already in the region group access list.");
                    }
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate group access list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.RemoveGroupAsAllowed) != 0) 
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateResult result = EstateAllowGroup(user, false);
                    if (result == EstateResult.Success)
                    {
                        if (!noReply)
                            remote_client.SendEstateUUIDList(invoice, (int)Constants.EstateAccessDeltaResponse.AllowedGroups,
                                        m_scene.RegionInfo.EstateSettings.EstateGroups, m_scene.RegionInfo.EstateSettings.EstateID);
                    }
                    else
                    if (result == EstateResult.AlreadySet)
                    {
                        remote_client.SendAlertMessage("Group is not on the estate group access list");
                    }
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate group access list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.AddManager) != 0)
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, true) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.AddEstateManager(user);
                    SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveManagers);
                    if (!noReply)
                        remote_client.SendEstateUUIDList(invoice, (int)Constants.EstateAccessDeltaResponse.EstateManagers,
                                m_scene.RegionInfo.EstateSettings.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate manager list.");
                }
            }

            if ((operation & (int)Constants.EstateAccessDeltaCommands.RemoveManager) != 0)
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, true) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.RemoveEstateManager(user);
                    SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveManagers);

                    if (!noReply)
                        remote_client.SendEstateUUIDList(invoice, (int)Constants.EstateAccessDeltaResponse.EstateManagers,
                                m_scene.RegionInfo.EstateSettings.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("You are not permitted to change the estate manager list.");
                }
            }

            if (allEstates)
                remote_client.SendAlertMessage("The 'All Estates' option is not implemented yet. Change applied to This Estate only.");
        }

        private void SendSimulatorBlueBoxMessage(
            IClientAPI remote_client, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message)
        {
            IDialogModule dm = m_scene.RequestModuleInterface<IDialogModule>();
            
            if (dm != null)
                dm.SendNotificationToUsersInRegion(senderID, senderName, message);
        }

        private void SendEstateBlueBoxMessage(
            IClientAPI remote_client, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message)
        {
            IDialogModule dm = m_scene.RequestModuleInterface<IDialogModule>();
            
            if (dm != null)
                dm.SendNotificationToUsersInEstate(senderID, senderName, message);
        }

        private void handleEstateDebugRegionRequest(IClientAPI remote_client, UUID invoice, UUID senderID, bool scripted, bool collisionEvents, bool physics)
        {
            if (physics)
                m_scene.RegionInfo.RegionSettings.DisablePhysics = true;
            else
                m_scene.RegionInfo.RegionSettings.DisablePhysics = false;

            if (scripted)
                m_scene.RegionInfo.RegionSettings.DisableScripts = true;
            else
                m_scene.RegionInfo.RegionSettings.DisableScripts = false;

            if (collisionEvents)
                m_scene.RegionInfo.RegionSettings.DisableCollisions = true;
            else
                m_scene.RegionInfo.RegionSettings.DisableCollisions = false;


            m_scene.RegionInfo.RegionSettings.Save();

            m_scene.SetSceneCoreDebug(scripted, collisionEvents, physics);
        }

        private void handleEstateTeleportOneUserHomeRequest(IClientAPI remover_client, UUID invoice, UUID senderID, UUID prey)
        {
            if (prey != UUID.Zero)
            {
                ScenePresence s = m_scene.GetScenePresence(prey);
                if (s != null)
                {
                    s.ControllingClient.SendTeleportLocationStart(); 
                    m_scene.TeleportClientHome(prey, s.ControllingClient);
                }
            }
        }

        private void handleEstateTeleportAllUsersHomeRequest(IClientAPI remover_client, UUID invoice, UUID senderID)
        {
            // Get a fresh list that will not change as people get teleported away
            List<ScenePresence> prescences = m_scene.GetScenePresences(); 
            foreach (ScenePresence p in prescences)
            {
                if (p.UUID != senderID)
                {
                    // make sure they are still there, we could be working down a long list
                    ScenePresence s = m_scene.GetScenePresence(p.UUID);
                    if (s != null)
                    {
                        // Also make sure they are actually in the region
                        if (!(s.IsChildAgent || s.IsDeleted || s.IsInTransit))
                        {
                            s.ControllingClient.SendTeleportLocationStart();
                            m_scene.TeleportClientHome(s.UUID, s.ControllingClient);
                        }
                    }
                }
            }
        }
        private void AbortTerrainXferHandler(IClientAPI remoteClient, ulong XferID)
        {
            if (TerrainUploader != null)
            {
                lock (TerrainUploader)
                {
                    if (XferID == TerrainUploader.XferID)
                    {
                        remoteClient.OnXferReceive -= TerrainUploader.XferReceive;
                        remoteClient.OnAbortXfer -= AbortTerrainXferHandler;
                        TerrainUploader.TerrainUploadDone -= HandleTerrainApplication;

                        TerrainUploader = null;
                        remoteClient.SendAlertMessage("Terrain Upload aborted by the client");
                    }
                }
            }

        }
        private void HandleTerrainApplication(string filename, byte[] terrainData, IClientAPI remoteClient)
        {
            lock (TerrainUploader)
            {
                remoteClient.OnXferReceive -= TerrainUploader.XferReceive;
                remoteClient.OnAbortXfer -= AbortTerrainXferHandler;
                TerrainUploader.TerrainUploadDone -= HandleTerrainApplication;

                TerrainUploader = null;
            }
            remoteClient.SendAlertMessage("Terrain Upload Complete. Loading....");
            ITerrainModule terr = m_scene.RequestModuleInterface<ITerrainModule>();

            if (terr != null)
            {
                m_log.Warn("[CLIENT]: Got Request to Send Terrain in region " + m_scene.RegionInfo.RegionName);
                if (File.Exists(Util.dataDir() + "/terrain.raw"))
                {
                    File.Delete(Util.dataDir() + "/terrain.raw");
                }
                try
                {
                    FileStream input = new FileStream(Util.dataDir() + "/terrain.raw", FileMode.CreateNew);
                    input.Write(terrainData, 0, terrainData.Length);
                    input.Close();
                }
                catch (IOException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was an IO Exception loading your terrain.  Please check free space");

                    return;
                }
                catch (SecurityException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a security Exception loading your terrain.  Please check the security on the simulator drive");

                    return;
                }
                catch (UnauthorizedAccessException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a security Exception loading your terrain.  Please check the security on the simulator drive");

                    return;
                }




                try
                {
                    terr.LoadFromFile(Util.dataDir() + "/terrain.raw");
                    remoteClient.SendAlertMessage("Your terrain was loaded. Give it a minute or two to apply");
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error loading a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a general error loading your terrain.  Please fix the terrain file and try again");
                }

            }
            else
            {
                remoteClient.SendAlertMessage("Unable to apply terrain.  Cannot get an instance of the terrain module");
            }



        }

        private void handleUploadTerrain(IClientAPI remote_client, string clientFileName)
        {

            if (TerrainUploader == null)
            {

                TerrainUploader = new EstateTerrainXferHandler(remote_client, clientFileName);
                lock (TerrainUploader)
                {
                    remote_client.OnXferReceive += TerrainUploader.XferReceive;
                    remote_client.OnAbortXfer += AbortTerrainXferHandler;
                    TerrainUploader.TerrainUploadDone += HandleTerrainApplication;
                }
                TerrainUploader.RequestStartXfer(remote_client);

            }
            else
            {
                remote_client.SendAlertMessage("Another Terrain Upload is in progress.  Please wait your turn!");
            }

        }
        private void handleTerrainRequest(IClientAPI remote_client, string clientFileName)
        {
            // Save terrain here
            ITerrainModule terr = m_scene.RequestModuleInterface<ITerrainModule>();
            
            if (terr != null)
            {
                m_log.Warn("[CLIENT]: Got Request to Send Terrain in region " + m_scene.RegionInfo.RegionName);
                if (File.Exists(Util.dataDir() + "/terrain.raw"))
                {
                    File.Delete(Util.dataDir() + "/terrain.raw");
                }
                terr.SaveToFile(Util.dataDir() + "/terrain.raw");

                FileStream input = new FileStream(Util.dataDir() + "/terrain.raw", FileMode.Open);
                byte[] bdata = new byte[input.Length];
                input.Read(bdata, 0, (int)input.Length);
                remote_client.SendAlertMessage("Terrain file written, starting download...");
                m_scene.XferManager.AddNewFile("terrain.raw", bdata);
                // Tell client about it
                m_log.Warn("[CLIENT]: Sending Terrain to " + remote_client.Name);
                remote_client.SendInitiateDownload("terrain.raw", clientFileName);
            }
        }

        private void HandleRegionInfoRequest(IClientAPI remote_client)
        {
           RegionInfoForEstateMenuArgs args = new RegionInfoForEstateMenuArgs();
           args.billableFactor = m_scene.RegionInfo.EstateSettings.BillableFactor;
           args.estateID = m_scene.RegionInfo.EstateSettings.EstateID;
           args.maxAgents = (byte)m_scene.RegionInfo.RegionSettings.AgentLimit;
           args.objectBonusFactor = (float)m_scene.RegionInfo.RegionSettings.ObjectBonus;
           args.parentEstateID = m_scene.RegionInfo.EstateSettings.ParentEstateID;
           args.pricePerMeter = m_scene.RegionInfo.EstateSettings.PricePerMeter;
           args.redirectGridX = m_scene.RegionInfo.EstateSettings.RedirectGridX;
           args.redirectGridY = m_scene.RegionInfo.EstateSettings.RedirectGridY;
           args.regionFlags = GetRegionFlags();
           args.simAccess = m_scene.RegionInfo.AccessLevel;
           args.sunHour = (float)m_scene.RegionInfo.RegionSettings.SunPosition;
           args.terrainLowerLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
           args.terrainRaiseLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;
           args.useEstateSun = m_scene.RegionInfo.RegionSettings.UseEstateSun;
           args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
           args.simName = m_scene.RegionInfo.RegionName;
           args.product = m_scene.RegionInfo.Product;
           args.productAccess = m_scene.RegionInfo.ProductAccess;

           remote_client.SendRegionInfoToEstateMenu(args);
        }

        private void HandleEstateCovenantRequest(IClientAPI remote_client)
        {
            remote_client.SendEstateCovenantInformation(m_scene.RegionInfo.RegionSettings.Covenant,
                                                        m_scene.RegionInfo.RegionSettings.CovenantLastUpdated);
        }

        private void HandleLandStatRequest(int parcelID, uint reportType, uint requestFlags, string filter, IClientAPI remoteClient)
        {
            Dictionary<uint, float> SceneData = new Dictionary<uint,float>();
            List<UUID> uuidNameLookupList = new List<UUID>();

            if (reportType == 1)
            {
                SceneData = m_scene.PhysicsScene.GetTopColliders();
            }
            else if (reportType == 0)
            {
                SceneData = m_scene.SceneGraph.GetTopScripts();
            }

            List<LandStatReportItem> SceneReport = new List<LandStatReportItem>();
            lock (SceneData)
            {
                foreach (uint obj in SceneData.Keys)
                {
                    SceneObjectPart prt = m_scene.GetSceneObjectPart(obj);
                    if (prt != null)
                    {
                        if (prt.ParentGroup != null)
                        {
                            SceneObjectGroup sog = prt.ParentGroup;
                            if (sog != null)
                            {
                                LandStatReportItem lsri = new LandStatReportItem();
                                lsri.LocationX = sog.AbsolutePosition.X;
                                lsri.LocationY = sog.AbsolutePosition.Y;
                                lsri.LocationZ = sog.AbsolutePosition.Z;
                                lsri.Score = SceneData[obj];
                                lsri.TaskID = sog.UUID;
                                lsri.TaskLocalID = sog.LocalId;
                                lsri.TaskName = sog.GetPartName(obj);
                                if (m_scene.CommsManager.UUIDNameCachedTest(sog.OwnerID))
                                {
                                    lsri.OwnerName = m_scene.CommsManager.UUIDNameRequestString(sog.OwnerID);
                                }
                                else
                                {
                                    lsri.OwnerName = "waiting";
                                    lock (uuidNameLookupList)
                                        uuidNameLookupList.Add(sog.OwnerID);
                                }

                                if (!String.IsNullOrEmpty(filter))
                                {
                                    if ((lsri.OwnerName.Contains(filter) || lsri.TaskName.Contains(filter)))
                                    {
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                SceneReport.Add(lsri);
                            }
                        }
                    }

                }
            }
            remoteClient.SendLandStatReply(reportType, requestFlags, (uint)SceneReport.Count,SceneReport);

            if (uuidNameLookupList.Count > 0)
                LookupUUID(uuidNameLookupList);
        }

        private void LookupUUIDSCompleted(IAsyncResult iar)
        {
            LookupUUIDS icon = (LookupUUIDS)iar.AsyncState;
            icon.EndInvoke(iar);
        }
        private void LookupUUID(List<UUID> uuidLst)
        {
            LookupUUIDS d = LookupUUIDsAsync;

            d.BeginInvoke(uuidLst,
                          LookupUUIDSCompleted,
                          d);
        }
        private void LookupUUIDsAsync(List<UUID> uuidLst)
        {
            UUID[] uuidarr = new UUID[0];

            lock (uuidLst)
            {
                uuidarr = uuidLst.ToArray();
            }

            for (int i = 0; i < uuidarr.Length; i++)
            {
                // string lookupname = m_scene.CommsManager.UUIDNameRequestString(uuidarr[i]);
                m_scene.CommsManager.UUIDNameRequestString(uuidarr[i]);
                // we drop it.  It gets cached though...  so we're ready for the next request.
            }
        }
        #endregion

        #region Outgoing Packets

        public void sendRegionInfoPacketToAll()
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                if ((!avatar.IsDeleted) && (!avatar.IsInTransit))
                    HandleRegionInfoRequest(avatar.ControllingClient); ;
            }
        }

        public void sendRegionHandshake(IClientAPI remoteClient)
        {
            RegionHandshakeArgs args = new RegionHandshakeArgs();

            args.isEstateManager = m_scene.RegionInfo.EstateSettings.IsEstateManager(remoteClient.AgentId);
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero && m_scene.RegionInfo.EstateSettings.EstateOwner == remoteClient.AgentId)
                args.isEstateManager = true;

            args.billableFactor = m_scene.RegionInfo.EstateSettings.BillableFactor;
            args.terrainStartHeight0 = (float)m_scene.RegionInfo.RegionSettings.Elevation1SW;
            args.terrainHeightRange0 = (float)m_scene.RegionInfo.RegionSettings.Elevation2SW;
            args.terrainStartHeight1 = (float)m_scene.RegionInfo.RegionSettings.Elevation1NW;
            args.terrainHeightRange1 = (float)m_scene.RegionInfo.RegionSettings.Elevation2NW;
            args.terrainStartHeight2 = (float)m_scene.RegionInfo.RegionSettings.Elevation1SE;
            args.terrainHeightRange2 = (float)m_scene.RegionInfo.RegionSettings.Elevation2SE;
            args.terrainStartHeight3 = (float)m_scene.RegionInfo.RegionSettings.Elevation1NE;
            args.terrainHeightRange3 = (float)m_scene.RegionInfo.RegionSettings.Elevation2NE;
            args.simAccess = m_scene.RegionInfo.AccessLevel;
            args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
            args.regionFlags = GetRegionFlags();
            args.regionName = m_scene.RegionInfo.RegionName;
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                args.SimOwner = m_scene.RegionInfo.EstateSettings.EstateOwner;
            else
                args.SimOwner = m_scene.RegionInfo.MasterAvatarAssignedUUID;

            // Fudge estate owner
            //if (m_scene.Permissions.IsGod(remoteClient.AgentId))
            //    args.SimOwner = remoteClient.AgentId;

            args.terrainBase0 = UUID.Zero;
            args.terrainBase1 = UUID.Zero;
            args.terrainBase2 = UUID.Zero;
            args.terrainBase3 = UUID.Zero;
            args.terrainDetail0 = m_scene.RegionInfo.RegionSettings.TerrainTexture1;
            args.terrainDetail1 = m_scene.RegionInfo.RegionSettings.TerrainTexture2;
            args.terrainDetail2 = m_scene.RegionInfo.RegionSettings.TerrainTexture3;
            args.terrainDetail3 = m_scene.RegionInfo.RegionSettings.TerrainTexture4;

            remoteClient.SendRegionHandshake(m_scene.RegionInfo,args);
        }

        public void sendRegionHandshakeToAll()
        {
            m_scene.Broadcast(sendRegionHandshake);
        }

        public void handleEstateChangeInfo(IClientAPI remoteClient, UUID invoice, UUID senderID, UInt32 parms1, UInt32 parms2)
        {
            if (parms2 == 0)
            {
                m_scene.RegionInfo.EstateSettings.UseGlobalTime = true;
                m_scene.RegionInfo.EstateSettings.SunPosition = 0.0;
            }
            else
            {
                m_scene.RegionInfo.EstateSettings.UseGlobalTime = false;
                m_scene.RegionInfo.EstateSettings.SunPosition = (double)(parms2 - 0x1800)/1024.0;
            }

            if ((parms1 & 0x00000010) != 0)
                m_scene.RegionInfo.EstateSettings.FixedSun = true;
            else
                m_scene.RegionInfo.EstateSettings.FixedSun = false;

            if ((parms1 & 0x00008000) != 0)
                m_scene.RegionInfo.EstateSettings.PublicAccess = true;
            else
                m_scene.RegionInfo.EstateSettings.PublicAccess = false;

            if ((parms1 & 0x10000000) != 0)
                m_scene.RegionInfo.EstateSettings.AllowVoice = true;
            else
                m_scene.RegionInfo.EstateSettings.AllowVoice = false;

            if ((parms1 & 0x00100000) != 0)
                m_scene.RegionInfo.EstateSettings.AllowDirectTeleport = true;
            else
                m_scene.RegionInfo.EstateSettings.AllowDirectTeleport = false;

            if ((parms1 & 0x00800000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyAnonymous = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyAnonymous = false;

            if ((parms1 & 0x01000000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyIdentified = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyIdentified = false;

            if ((parms1 & 0x02000000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyTransacted = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyTransacted = false;

            if ((parms1 & 0x40000000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyMinors = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyMinors = false;

            SaveEstateDataAndUpdateRegions((uint)EstateSettings.EstateSaveOptions.EstateSaveSettingsOnly);

            TriggerEstateToolsSunUpdate();

            sendDetailedEstateData(remoteClient, invoice);
        }

        private enum SimWideDeletesFlags
        {
            ReturnObjectsOtherEstate = 1,
            ReturnObjects = 2,
            OthersLandNotUserOnly = 3,
            ScriptedPrimsOnly = 4
        }

        public void SimWideDeletes(IClientAPI client, int flags, UUID targetID)
        {
            if (m_scene.Permissions.CanIssueEstateCommand(client.AgentId, false))
            {
                m_log.InfoFormat("[ESTATE]: Executing simwide deletes requested by {0}, flags: {1}, target: {2}", client.Name, flags, targetID);

                List<SceneObjectGroup> prims = new List<SceneObjectGroup>();
                bool containsScript = (flags & (int)SimWideDeletesFlags.ScriptedPrimsOnly) == (int)SimWideDeletesFlags.ScriptedPrimsOnly;

                foreach (ILandObject selectedParcel in m_scene.LandChannel.AllParcels())
                {
                    if ((flags & (int)SimWideDeletesFlags.OthersLandNotUserOnly) ==
                        (int)SimWideDeletesFlags.OthersLandNotUserOnly)
                    {
                        if (selectedParcel.landData.OwnerID != targetID) //Check to make sure it isn't their land
                            prims.AddRange(selectedParcel.GetPrimsOverByOwner(targetID, containsScript));
                    }
                    //Other estates flag doesn't seem to get sent by the viewer, so don't touch it
                    //else if ((flags & (int)SimWideDeletesFlags.ReturnObjectsOtherEstate) == (int)SimWideDeletesFlags.ReturnObjectsOtherEstate)
                    //    prims.AddRange (selectedParcel.GetPrimsOverByOwner (targetID, containsScript));
                    else
                        // if ((flags & (int)SimWideDeletesFlags.ReturnObjects) == (int)SimWideDeletesFlags.ReturnObjects)//Return them all
                        prims.AddRange(selectedParcel.GetPrimsOverByOwner(targetID, containsScript));
                }
                m_scene.returnObjects(prims.ToArray());
            }
        }

        #endregion

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IEstateModule>(this);
            m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            m_scene.EventManager.OnRequestChangeWaterHeight += changeWaterHeight;
        }


        public void PostInitialize()
        {
            // Sets up the sun module based no the saved Estate and Region Settings
            // DO NOT REMOVE or the sun will stop working
            TriggerEstateToolsSunUpdate();
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EstateManagementModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region Other Functions

        private void TriggerEstateToolsSunUpdate()
        {
            float sun;
            if (m_scene.RegionInfo.RegionSettings.UseEstateSun)
            {
                sun = (float)m_scene.RegionInfo.EstateSettings.SunPosition;
                if (m_scene.RegionInfo.EstateSettings.UseGlobalTime)
                {
                    sun = m_scene.EventManager.GetCurrentTimeAsSunLindenHour() - 6.0f;
                }

                // 
                m_scene.EventManager.TriggerEstateToolsSunUpdate(
                        m_scene.RegionInfo.RegionHandle,
                        m_scene.RegionInfo.EstateSettings.FixedSun,
                        m_scene.RegionInfo.RegionSettings.UseEstateSun, 
                        sun);
            }
            else
            {
                // Use the Sun Position from the Region Settings
                sun = (float)m_scene.RegionInfo.RegionSettings.SunPosition - 6.0f;

                m_scene.EventManager.TriggerEstateToolsSunUpdate(
                        m_scene.RegionInfo.RegionHandle,
                        m_scene.RegionInfo.RegionSettings.FixedSun,
                        m_scene.RegionInfo.RegionSettings.UseEstateSun, 
                        sun);
            }


        }


        public void changeWaterHeight(float height)
        {
            setRegionTerrainSettings(height,
                    (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit,
                    (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit,
                    m_scene.RegionInfo.RegionSettings.UseEstateSun,
                    m_scene.RegionInfo.RegionSettings.FixedSun,
                    (float)m_scene.RegionInfo.RegionSettings.SunPosition,
                    m_scene.RegionInfo.EstateSettings.UseGlobalTime,
                    m_scene.RegionInfo.EstateSettings.FixedSun,
                    (float)m_scene.RegionInfo.EstateSettings.SunPosition);

            sendRegionInfoPacketToAll();
        }

        #endregion

        private void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnDetailedEstateDataRequest += sendDetailedEstateData;
            client.OnSetEstateFlagsRequest += estateSetRegionInfoHandler;
//            client.OnSetEstateTerrainBaseTexture += setEstateTerrainBaseTexture;
            client.OnSetEstateTerrainDetailTexture += setEstateTerrainBaseTexture;
            client.OnSetEstateTerrainTextureHeights += setEstateTerrainTextureHeights;
            client.OnCommitEstateTerrainTextureRequest += handleCommitEstateTerrainTextureRequest;
            client.OnSetRegionTerrainSettings += setRegionTerrainSettings;
            client.OnEstateRestartSimRequest += handleEstateRestartSimRequest;
            client.OnEstateChangeCovenantRequest += handleChangeEstateCovenantRequest;
            client.OnEstateChangeInfo += handleEstateChangeInfo;
            client.OnUpdateEstateAccessDeltaRequest += handleEstateAccessDeltaRequest;
            client.OnSimulatorBlueBoxMessageRequest += SendSimulatorBlueBoxMessage;
            client.OnEstateBlueBoxMessageRequest += SendEstateBlueBoxMessage;
            client.OnEstateDebugRegionRequest += handleEstateDebugRegionRequest;
            client.OnEstateTeleportOneUserHomeRequest += handleEstateTeleportOneUserHomeRequest;
            client.OnEstateTeleportAllUsersHomeRequest += handleEstateTeleportAllUsersHomeRequest;
            client.OnRequestTerrain += handleTerrainRequest;
            client.OnUploadTerrain += handleUploadTerrain;
            client.OnSimWideDeletes += SimWideDeletes;

            client.OnRegionInfoRequest += HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += HandleEstateCovenantRequest;
            client.OnLandStatRequest += HandleLandStatRequest;
            client.OnGodlikeMessage += GodlikeMessage;
            client.OnEstateTelehubRequest += GodlikeMessage;
            sendRegionHandshake(client);
        }

        #region Telehub Settings

        public void GodlikeMessage(IClientAPI client, UUID requester, string Method, List<string> Parameters)
        {
            ScenePresence Sp = m_scene.GetScenePresence(client.AgentId);
            if (!m_scene.Permissions.CanIssueEstateCommand(client.AgentId, false))
                return;

            string parameter1 = Parameters[0];
            if (Method == "telehub")
            {
                if (parameter1 == "spawnpoint remove")
                {
                    Telehub telehub = m_scene.StorageManager.EstateDataStore.FindTelehub(client.Scene.RegionInfo.RegionID);
                    if (telehub == null)
                        return;
                    //Remove the one we sent at X
                    telehub.SpawnPos.RemoveAt(int.Parse(Parameters[1]));
                    m_scene.StorageManager.EstateDataStore.AddTelehub(telehub);
                    SendTelehubInfo(client);
                }
                if (parameter1 == "spawnpoint add")
                {
                    SceneObjectPart part = Sp.Scene.GetSceneObjectPart(uint.Parse(Parameters[1]));
                    if (part == null)
                        return;
                    Telehub telehub = m_scene.StorageManager.EstateDataStore.FindTelehub(client.Scene.RegionInfo.RegionID);
                    if (telehub == null)
                        return;
                    telehub.RegionID = client.Scene.RegionInfo.RegionID;
                    if (telehub.TelehubLoc.X == 0 && telehub.TelehubLoc.Y == 0)
                        return; //No spawns without a telehub
                    telehub.SpawnPos.Add(part.AbsolutePosition - telehub.TelehubLoc); //Spawns are offsets
                    m_scene.StorageManager.EstateDataStore.AddTelehub(telehub);
                    SendTelehubInfo(client);
                }
                if (parameter1 == "delete")
                {
                    m_scene.StorageManager.EstateDataStore.RemoveTelehub(client.Scene.RegionInfo.RegionID);
                    SendTelehubInfo(client);
                }
                if (parameter1 == "connect")
                {
                    SceneObjectPart part = Sp.Scene.GetSceneObjectPart(uint.Parse(Parameters[1]));
                    if (part == null)
                        return;
                    Telehub telehub = m_scene.StorageManager.EstateDataStore.FindTelehub(client.Scene.RegionInfo.RegionID);
                    if (telehub == null)
                        telehub = new Telehub();
                    telehub.RegionID = client.Scene.RegionInfo.RegionID;
                    telehub.TelehubLoc = part.AbsolutePosition;
                    telehub.TelehubRot = part.ParentGroup.Rotation;
                    telehub.ObjectUUID = part.UUID;
                    telehub.Name = part.Name;
                    m_scene.StorageManager.EstateDataStore.AddTelehub(telehub);
                    SendTelehubInfo(client);
                }

                if (parameter1 == "info ui")
                    SendTelehubInfo(client);
            }
        }

        private void SendTelehubInfo(IClientAPI client)
        {
            Telehub telehub = m_scene.StorageManager.EstateDataStore.FindTelehub(client.Scene.RegionInfo.RegionID);
            if (telehub == null)
            {
                client.SendTelehubInfo(Vector3.Zero, Quaternion.Identity, new List<Vector3>(), UUID.Zero, String.Empty);
            }
            else
            {
                client.SendTelehubInfo(telehub.TelehubLoc, telehub.TelehubRot, telehub.SpawnPos, telehub.ObjectUUID, telehub.Name);
            }
        }

        #endregion

        public uint GetRegionFlags()
        {
            RegionFlags flags = RegionFlags.None;

            // Fully implemented
            //
            if (m_scene.RegionInfo.RegionSettings.AllowDamage)
                flags |= RegionFlags.AllowDamage;
            if (m_scene.RegionInfo.RegionSettings.BlockTerraform)
                flags |= RegionFlags.BlockTerraform;
            if (!m_scene.RegionInfo.RegionSettings.AllowLandResell)
                flags |= RegionFlags.BlockLandResell;
            if (m_scene.RegionInfo.RegionSettings.DisableCollisions)
                flags |= RegionFlags.SkipCollisions;
            if (m_scene.RegionInfo.RegionSettings.DisableScripts)
                flags |= RegionFlags.SkipScripts;
            if (m_scene.RegionInfo.RegionSettings.DisablePhysics)
                flags |= RegionFlags.SkipPhysics;
            if (m_scene.RegionInfo.RegionSettings.BlockFly)
                flags |= RegionFlags.NoFly;
            if (m_scene.RegionInfo.RegionSettings.RestrictPushing)
                flags |= RegionFlags.RestrictPushObject;
            if (m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide)
                flags |= RegionFlags.AllowParcelChanges;
            if (m_scene.RegionInfo.RegionSettings.BlockShowInSearch)
                flags |= RegionFlags.BlockParcelSearch;

            if (m_scene.RegionInfo.RegionSettings.FixedSun)
                flags |= RegionFlags.SunFixed;
            if (m_scene.RegionInfo.RegionSettings.Sandbox)
                flags |= RegionFlags.Sandbox;
            if (m_scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;
            if (m_scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= RegionFlags.BlockDwell;
            if (m_scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= RegionFlags.ResetHomeOnTeleport;

            // Fudge these to always on, so the menu options activate
            //
            flags |= RegionFlags.AllowLandmark;
            flags |= RegionFlags.AllowSetHome;

            // TODO: SkipUpdateInterestList

            // Omitted
            //
            // Omitted: NullLayer (what is that?)
            // Omitted: SkipAgentAction (what does it do?)

            return (uint)flags;
        }

        public uint GetEstateFlags()
        {
            RegionFlags flags = RegionFlags.None;

            if (m_scene.RegionInfo.EstateSettings.FixedSun)
                flags |= RegionFlags.SunFixed;
            if (m_scene.RegionInfo.EstateSettings.PublicAccess)
                flags |= (RegionFlags.PublicAllowed |
                          RegionFlags.ExternallyVisible);
            if (m_scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;
            if (m_scene.RegionInfo.EstateSettings.AllowDirectTeleport)
                flags |= RegionFlags.AllowDirectTeleport;
            if (m_scene.RegionInfo.EstateSettings.DenyAnonymous)
                flags |= RegionFlags.DenyAnonymous;
            if (m_scene.RegionInfo.EstateSettings.DenyIdentified)
                flags |= RegionFlags.DenyIdentified;
            if (m_scene.RegionInfo.EstateSettings.DenyTransacted)
                flags |= RegionFlags.DenyTransacted;
            if (m_scene.RegionInfo.EstateSettings.AbuseEmailToEstateOwner)
                flags |= RegionFlags.AbuseEmailToEstateOwner;
            if (m_scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= RegionFlags.BlockDwell;
            if (m_scene.RegionInfo.EstateSettings.EstateSkipScripts)
                flags |= RegionFlags.EstateSkipScripts;
            if (m_scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= RegionFlags.ResetHomeOnTeleport;
            if (m_scene.RegionInfo.EstateSettings.TaxFree)
                flags |= RegionFlags.TaxFree;
            if (m_scene.RegionInfo.EstateSettings.DenyMinors)
                flags |= (RegionFlags.DenyAgeUnverified);

            return (uint)flags;
        }

        public bool IsManager(UUID avatarID)
        {
            if (avatarID == m_scene.RegionInfo.MasterAvatarAssignedUUID)
                return true;
            if (avatarID == m_scene.RegionInfo.EstateSettings.EstateOwner)
                return true;

            List<UUID> ems = new List<UUID>(m_scene.RegionInfo.EstateSettings.EstateManagers);
            if (ems.Contains(avatarID))
                return true;

            return false;
        }

        public void SaveEstateDataAndUpdateRegions(uint listsToSave)
        {
            m_scene.RegionInfo.EstateSettings.Save(listsToSave);
            //Now tell all other regions in the estate to update themselves
            List<UUID> regions = m_scene.StorageManager.EstateDataStore.GetEstateRegions(m_scene.RegionInfo.EstateSettings.EstateID);
            foreach (UUID region in regions)
            {
                if (region == m_scene.RegionInfo.RegionID)
                    continue;

                m_scene.SceneGridService.SendUpdateEstateInfo(region);
            }
        }
    }
}
