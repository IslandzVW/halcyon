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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.CoreModules.World.Land
{
    /// <summary>
    /// Keeps track of a specific piece of land's information
    /// </summary>
    public class LandObject : ILandObject
    {
        #region Member Variables

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool[,] m_landBitmap = new bool[64,64];

        protected LandData m_landData = new LandData();
        protected Scene m_scene;
        protected List<SceneObjectGroup> primsOverMe = new List<SceneObjectGroup>();
        private bool landInfoNeedsUpdate = true;

        #endregion

        #region ILandObject Members

        public LandData landData
        {
            get { return m_landData; }

            set 
            { 
                m_landData = value;
                landInfoNeedsUpdate = true;
            }
        }

        public UUID regionUUID
        {
            get { return m_scene.RegionInfo.RegionID; }
        }

        public string regionName
        {
            get { return m_scene.RegionInfo.RegionName; }
        }

        #region Constructors

        public LandObject(UUID owner_id, bool is_group_owned, Scene scene)
        {
            m_scene = scene;
            landData.OwnerID = owner_id;
            landData.IsGroupOwned = is_group_owned;
        }

        #endregion

        #region Member Functions

        #region General Functions

        /// <summary>
        /// Checks to see if this land object contains a point
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>Returns true if the piece of land contains the specified point</returns>
        public bool containsPoint(int x, int y)
        {
            if (x >= 0 && y >= 0 && x <= Constants.RegionSize && x <= Constants.RegionSize)
            {
                return (m_landBitmap[x / 4, y / 4] == true);
            }
            else
            {
                return false;
            }
        }

        public ILandObject Copy()
        {
            LandObject newLand = new LandObject(landData.OwnerID, landData.IsGroupOwned, m_scene);

            //Place all new variables here!
            newLand.m_landBitmap = (bool[,]) (m_landBitmap.Clone());
            newLand.landData = landData.Copy();

            return newLand;
        }

        static overrideParcelMaxPrimCountDelegate overrideParcelMaxPrimCount;
        static overrideSimulatorMaxPrimCountDelegate overrideSimulatorMaxPrimCount;

        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            overrideParcelMaxPrimCount = overrideDel;
        }
        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            overrideSimulatorMaxPrimCount = overrideDel;
        }

        public int getMaxPrimCount(int areaSize, bool includeBonusFactor)
        {
            //Normal Calculations
            double bonus = 1.0;
            if (includeBonusFactor)
                bonus = m_scene.RegionInfo.RegionSettings.ObjectBonus;
            int prims = Convert.ToInt32(
                        Math.Round((Convert.ToDouble(areaSize) / 65536.0)
                                  * Convert.ToDouble(m_scene.RegionInfo.PrimLimit) 
                                  * bonus
                                  ));
            if (prims > m_scene.RegionInfo.PrimLimit)
                prims = m_scene.RegionInfo.PrimLimit;
            return prims;
        }

        public int getParcelMaxPrimCount(ILandObject thisObject, bool includeBonusFactor)
        {
            if (overrideParcelMaxPrimCount != null)
            {
                return overrideParcelMaxPrimCount(thisObject);
            }
            else
            {
                //Normal Calculations
                m_scene.LandChannel.UpdateLandPrimCounts();
                return getMaxPrimCount(landData.Area, includeBonusFactor);
            }
        }
        public int getSimulatorMaxPrimCount(ILandObject thisObject)
        {
            if (overrideSimulatorMaxPrimCount != null)
            {
                return overrideSimulatorMaxPrimCount(thisObject);
            }
            else
            {
                //Normal Calculations
                m_scene.LandChannel.UpdateLandPrimCounts();
                int area = landData.SimwideArea;
                if (area == 0)
                    area = landData.Area;
                if (area == 0)
                    return m_scene.RegionInfo.PrimLimit;
                else
                    return getMaxPrimCount(area, true);
            }
        }
        #endregion

        #region Packet Request Handling

        public void sendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client)
        {
            IEstateModule estateModule = m_scene.RequestModuleInterface<IEstateModule>();
            uint regionFlags = (uint)(RegionFlags.PublicAllowed | RegionFlags.AllowDirectTeleport | RegionFlags.AllowParcelChanges | RegionFlags.AllowVoice);
            if (estateModule != null)
                regionFlags = estateModule.GetRegionFlags();

            LandData parcel = landData.Copy();
            if (m_scene.RegionInfo.Product == ProductRulesUse.ScenicUse)
            {
                // the current AgentId should already be cached, with presence, etc.
                UserProfileData profile = m_scene.CommsManager.UserService.GetUserProfile(remote_client.AgentId);
                if (profile != null)
                {
                    // If it's the parnter of the owner of the scenic region...
                    if (profile.Partner == landData.OwnerID)
                    {
                        // enable Create at the viewer end, checked at the server end
                        parcel.Flags |= (uint)ParcelFlags.CreateObjects;
                    }
                }
            }

            remote_client.SendLandProperties(sequence_id,
                    snap_selection, request_result, parcel,
                    (float)m_scene.RegionInfo.RegionSettings.ObjectBonus,
                    getParcelMaxPrimCount(this, false),
                    getSimulatorMaxPrimCount(this), regionFlags);
        }

        public void updateLandProperties(LandUpdateArgs args, IClientAPI remote_client)
        {
            //Needs later group support
            LandData newData = landData.Copy();

            uint allowedDelta = 0;
            bool snap_selection = false;
            bool needsInspect = false;

            // These two are always blocked as no client can set them anyway
            // ParcelFlags.ForSaleObjects
            // ParcelFlags.LindenHome

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandOptions))
            {
                allowedDelta |= (uint)(
                        ParcelFlags.AllowFly |
                        ParcelFlags.AllowLandmark |
                        ParcelFlags.AllowTerraform |
                        ParcelFlags.AllowDamage |
                        ParcelFlags.CreateObjects |
                        ParcelFlags.RestrictPushObject |
                        ParcelFlags.AllowOtherScripts |
                        ParcelFlags.AllowGroupScripts |
                        ParcelFlags.CreateGroupObjects |
                        ParcelFlags.AllowAPrimitiveEntry |
                        ParcelFlags.AllowGroupObjectEntry);
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandSetSale))
            {
                if (m_scene.RegionInfo.AllowOwners != ProductRulesWho.OnlyEO)
                {
                    bool ownerTransfer = false;
                    if (args.AuthBuyerID != newData.AuthBuyerID || args.SalePrice != newData.SalePrice)
                    {
                        ownerTransfer = true;
                        snap_selection = true;
                    }

                    if (m_scene.RegionInfo.AllowSales)
                    {
                        newData.AuthBuyerID = args.AuthBuyerID;
                        newData.SalePrice = args.SalePrice;
                        allowedDelta |= (uint)ParcelFlags.ForSale;
                    }
                    else
                    if (ownerTransfer)
                    {
                        newData.ClearSaleInfo();    // SalePrice, AuthBuyerID and sale-related Flags
                        remote_client.SendAgentAlertMessage("This parcel cannot be set for sale.", false);
                    }
                }

                if (m_scene.RegionInfo.AllowGroupTags && !landData.IsGroupOwned)
                {
                    if (newData.GroupID != args.GroupID)
                    {
                        // Group tag change
                        if (m_scene.RegionInfo.AllowGroupTags || (args.GroupID == UUID.Zero))
                        {
                            newData.GroupID = args.GroupID;
                            needsInspect = true;
                        }
                        else
                            remote_client.SendAgentAlertMessage("This parcel cannot be tagged with a group.", false);
                    }

                    if (newData.GroupID != UUID.Zero)
                    {
                        allowedDelta |= (uint) ParcelFlags.SellParcelObjects;
                        if (m_scene.RegionInfo.AllowDeeding && !landData.IsGroupOwned)
                            allowedDelta |= (uint)(ParcelFlags.AllowDeedToGroup | ParcelFlags.ContributeWithDeed);
                    }
                }
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.FindPlaces))
            {
                newData.Category = args.Category;

                allowedDelta |= (uint)(ParcelFlags.ShowDirectory |
                        ParcelFlags.AllowPublish |
                        ParcelFlags.MaturePublish);
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandChangeIdentity))
            {
                newData.Description = args.Desc;
                newData.Name = args.Name;
                newData.SnapshotID = args.SnapshotID;
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.SetLandingPoint))
            {
                newData.LandingType = args.LandingType;
                newData.UserLocation = args.UserLocation;
                newData.UserLookAt = args.UserLookAt;
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.ChangeMedia))
            {
                newData.MediaAutoScale = args.MediaAutoScale;
                newData.MediaID = args.MediaID;
                newData.MediaURL = args.MediaURL.Trim();
                newData.MusicURL = args.MusicURL.Trim();
                newData.MediaType = args.MediaType;
                newData.MediaDescription = args.MediaDescription;
                newData.MediaWidth = args.MediaWidth;
                newData.MediaHeight = args.MediaHeight;
                newData.MediaLoop = args.MediaLoop;
                newData.ObscureMusic = args.ObscureMusic;
                newData.ObscureMedia = args.ObscureMedia;

                allowedDelta |= (uint)(ParcelFlags.SoundLocal |
                        ParcelFlags.UrlWebPage |
                        ParcelFlags.UrlRawHtml |
                        ParcelFlags.AllowVoiceChat |
                        ParcelFlags.UseEstateVoiceChan);
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandManagePasses))
            {
                newData.PassHours = args.PassHours;
                newData.PassPrice = args.PassPrice;

                allowedDelta |= (uint)ParcelFlags.UsePassList;
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandManageAllowed))
            {
                allowedDelta |= (uint)(ParcelFlags.UseAccessGroup |
                        ParcelFlags.UseAccessList);
            }

            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandManageBanned))
            {
                allowedDelta |= (uint)(ParcelFlags.UseBanList |
                        ParcelFlags.DenyAnonymous |
                        ParcelFlags.DenyAgeUnverified);
            }

            uint preserve = landData.Flags & ~allowedDelta;
            newData.Flags = preserve | (args.ParcelFlags & allowedDelta);

            // Override: Parcels in Plus regions are always [x] Public access parcels
            if (m_scene.RegionInfo.Product == ProductRulesUse.PlusUse)
                newData.Flags &= ~(uint)ParcelFlags.UseAccessList;

            m_log.InfoFormat("[LAND]: updateLandProperties for land parcel {0} [{1}] flags {2} -> {3} by {4}",
                newData.LocalID, newData.GlobalID, landData.Flags.ToString("X8"), newData.Flags.ToString("X8"), remote_client.Name);
            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);

            sendLandUpdateToAvatarsOverParcel(snap_selection);
            SendSelectedLandUpdate(remote_client);
            if (needsInspect)
                InspectParcelForAutoReturn();
        }

        public void updateLandSold(UUID avatarID, UUID groupID, bool groupOwned)
        {
            LandData newData = landData.Copy();
            newData.OwnerID = avatarID;
            newData.GroupID = groupID;
            newData.IsGroupOwned = groupOwned;
            newData.AuctionID = 0;
            newData.ClaimDate = Util.UnixTimeSinceEpoch();
            newData.ClaimPrice = landData.SalePrice;

            newData.ClearSaleInfo();    // SalePrice, AuthBuyerID and sale-related Flags

            m_log.InfoFormat("[LAND]: updateLandSold for land parcel {0} [{1}] flags {2} -> {3}",
                newData.LocalID, newData.GlobalID, landData.Flags.ToString("X8"), newData.Flags.ToString("X8"));
            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            sendLandUpdateToAvatarsOverParcel(true);
            InspectParcelForAutoReturn();
        }

        public void deedToGroup(UUID groupID)
        {
            LandData newData = landData.Copy();
            newData.OwnerID = groupID;
            newData.GroupID = groupID;
            newData.IsGroupOwned = true;
            newData.ClearSaleInfo();

            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);
            m_scene.EventManager.TriggerParcelPrimCountUpdate();
            sendLandUpdateToAvatarsOverParcel(true);
            InspectParcelForAutoReturn();
        }

        public bool AllowAccessEstatewide(UUID avatar)
        {
            if (m_scene.Permissions.BypassPermissions())
                return true;
            ScenePresence sp = m_scene.GetScenePresence(avatar);
            if ((sp != null) && (sp.GodLevel > 0.0))
                return true; // never deny grid gods in godmode
            if (avatar == m_scene.RegionInfo.MasterAvatarAssignedUUID)
                return true; // never deny the master avatar
            if (avatar == m_scene.RegionInfo.EstateSettings.EstateOwner)
                return true; // never deny the estate owner
            if (m_scene.IsEstateManager(avatar))    // includes Estate Owner
                return true;
            if (m_scene.IsEstateOwnerPartner(avatar))
                return true;
            return false;
        }

        // Returns false and reason == ParcelPropertiesStatus.ParcelSelected if access is allowed, otherwise reason enum.
        public bool DenyParcelAccess(UUID avatar, out ParcelPropertiesStatus reason)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatar);

            if (isBannedFromLand(avatar) || (sp != null && sp.IsBot && isBannedFromLand(sp.OwnerID)))
            {
                reason = ParcelPropertiesStatus.CollisionBanned;
                return true;
            }

            if (isRestrictedFromLand(avatar) || (sp != null && sp.IsBot && isRestrictedFromLand(sp.OwnerID)))
            {
                reason = ParcelPropertiesStatus.CollisionNotOnAccessList;
                return true;
            }

            reason = ParcelPropertiesStatus.ParcelSelected;   // we can treat this as the no error case.
            return false;
        }


        // Returns false and reason == ParcelPropertiesStatus.ParcelSelected if access is allowed, otherwise reason enum.
        public bool DenyParcelAccess(SceneObjectGroup group, bool checkSitters, out ParcelPropertiesStatus reason)
        {
            if (checkSitters)
            {
                // some voodo with reason variables to avoid compiler problems.
                ParcelPropertiesStatus reason2;
                ParcelPropertiesStatus sitterReason = 0;
                bool result = true;
                group.ForEachSittingAvatar((ScenePresence sp) =>
                {
                    if (sp.UUID != group.OwnerID)   // checked separately below
                    {
                        if (DenyParcelAccess(sp.UUID, out reason2))
                        {
                            sitterReason = reason2;
                            result = false;
                        }
                    }
                });
                if (!result)
                {
                    reason = sitterReason;
                    return false;
                }
            }

            return DenyParcelAccess(group.OwnerID, out reason);
        }

        public bool isBannedFromLand(UUID avatar)
        {
            if (m_scene.Permissions.BypassPermissions())
                return false;

            if (landData.OwnerID == avatar)
                return false;

            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = avatar;
            entry.Flags = AccessList.Ban;
            entry.Time = new DateTime();
            if (landData.ParcelAccessList.Contains(entry))
            {
                // Banned, but ignore if EO etc?  Delay this call to here because
                // this call is potentially slightly expensive due to partner check profile lookup.
                if (!AllowAccessEstatewide(avatar))
                    return true;
            }

            return false;
        }

        public bool isRestrictedFromLand(UUID avatar)
        {
            if (m_scene.Permissions.BypassPermissions())
                return false;

            // Is everyone allowed in? Most common case, just permit entry.
            if ((landData.Flags & (uint)ParcelFlags.UseAccessList) == 0)
                return false;

            // Land owner always has access. Just permit entry.
            if (landData.OwnerID == avatar)
                return false;

            // Not public access, not the owner. Check other overrides.

            // Check if this user is listed with access.
            if (landData.ParcelAccessList.Count != 0)
            {
                ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                entry.AgentID = avatar;
                entry.Flags = AccessList.Access;
                entry.Time = new DateTime();
                if (landData.ParcelAccessList.Contains(entry))
                    return false;   // explicitly permitted to enter
            }

            // Note: AllowAccessEstatewide is potentially slightly expensive due to partner check profile lookup.
            if (AllowAccessEstatewide(avatar))
                return false;

            if (landData.GroupID != UUID.Zero)  // It has a group set.
            {
                // If it's group-owned land or group-based access enabled, allow in group members.
                if (landData.IsGroupOwned || ((landData.Flags & (uint)ParcelFlags.UseAccessGroup) != 0))
                {
                    // FastConfirmGroupMember is light if the avatar has a root or child connection to this region.
                    if (m_scene.FastConfirmGroupMember(avatar, landData.GroupID))
                        return false;
                }
            }

            // Parcel not public access and no qualifications to allow in.
            return true;
        }

        public void RemoveAvatarFromParcel(UUID userID)
        {
            ScenePresence sp = m_scene.GetScenePresence(userID);
            EntityBase.PositionInfo posInfo = sp.GetPosInfo();

            if (posInfo.Parent != null)
            {
                // can't find the prim seated on, stand up
                sp.StandUp(false, true);

                // fall through to unseated avatar code.
            }

            // If they are moving, stop them.  This updates the physics object as well.
            sp.Velocity = Vector3.Zero;

            Vector3 pos = sp.AbsolutePosition;  // may have changed from posInfo by StandUp above.

            ParcelPropertiesStatus reason2;
            if (!sp.lastKnownAllowedPosition.Equals(Vector3.Zero))
            {
                pos = sp.lastKnownAllowedPosition;
            }
            else
            {
                // Still a forbidden parcel, they must have been above the limit or entering region for the first time.
                // Let's put them up higher, over the restricted parcel.
                // We could add 50m to avatar.Scene.Heightmap[x,y] but then we need subscript checks, etc.
                // For now, this is simple and safer than TPing them home.
                pos.Z += 50.0f;
            }

            ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
            float minZ;
            if ((parcel != null) && m_scene.TestBelowHeightLimit(sp.UUID, pos, parcel, out minZ, out reason2))
            {
                if (pos.Z < minZ)
                    pos.Z = minZ + Constants.AVATAR_BOUNCE;
            }

            // Now force the non-sitting avatar to a position above the parcel
            sp.Teleport(pos);   // this is really just a move
        }

        public void sendLandUpdateToClient(IClientAPI remote_client, int sequence_id, bool snap_selection)
        {
            sendLandProperties(sequence_id, snap_selection, 0, remote_client);
        }

        public void sendLandUpdateToClient(IClientAPI remote_client, bool snap_selection)
        {
            sendLandProperties(0, snap_selection, 0, remote_client);
        }

        public void sendLandUpdateToClient(IClientAPI remote_client)
        {
            sendLandProperties(0, false, 0, remote_client);
        }

        public const int SELECTED_PARCEL_SEQ_ID = -10000;
        public void SendSelectedLandUpdate(IClientAPI client)
        {
            sendLandUpdateToClient(client, SELECTED_PARCEL_SEQ_ID, true);
        }

        public void sendLandUpdateToAllAvatars(bool snap_selection)
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                Vector3 avpos;
                if (avatar.HasSafePosition(out avpos))
                {
                    sendLandUpdateToClient(avatar.ControllingClient, snap_selection);
                }
            }
        }
        public void sendLandUpdateToAllAvatars()
        {
            sendLandUpdateToAllAvatars(false);
        }

        public void sendLandUpdateToAvatarsOverParcel(bool snap_selection)
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                Vector3 avpos;
                if (avatar.HasSafePosition(out avpos))
                {
                    ILandObject over = null;
                    try
                    {
                        over =
                            m_scene.LandChannel.GetLandObject(Util.Clamp<int>((int)Math.Round(avpos.X), 0, 255),
                                                              Util.Clamp<int>((int)Math.Round(avpos.Y), 0, 255));
                    }
                    catch (Exception)
                    {
                        m_log.Warn("[LAND]: Unable to get land at " + Math.Round(avpos.X) + "," + Math.Round(avpos.Y));
                    }

                    if (over != null)
                    {
                        if (over.landData.LocalID == landData.LocalID)
                        {
                            if (((over.landData.Flags & (uint)ParcelFlags.AllowDamage) != 0) && m_scene.RegionInfo.RegionSettings.AllowDamage)
                                avatar.Invulnerable = false;
                            else
                                avatar.Invulnerable = true;

                            sendLandUpdateToClient(avatar.ControllingClient, snap_selection);
                        }
                    }
                }
            }
        }

        public void sendLandUpdateToAvatarsOverParcel()
        {
            sendLandUpdateToAvatarsOverParcel(false);
        }

        #endregion

        #region AccessList Functions

        public List<UUID>  createAccessListArrayByFlag(AccessList flag)
        {
            List<UUID> list = new List<UUID>();
            foreach (ParcelManager.ParcelAccessEntry entry in landData.ParcelAccessList)
            {
                if (entry.Flags == flag)
                {
                   list.Add(entry.AgentID);
                }
            }
            if (list.Count == 0)
            {
                list.Add(UUID.Zero);
            }

            return list;
        }

        public void sendAccessList(uint flags, IClientAPI remote_client)
        {

            if ((flags & (uint) AccessList.Access) == (uint)AccessList.Access)
            {
                List<UUID> avatars = createAccessListArrayByFlag(AccessList.Access);
                remote_client.SendLandAccessListData(avatars,(uint) AccessList.Access,landData.LocalID);
            }

            if ((flags & (uint)AccessList.Ban) == (uint)AccessList.Ban)
            {
                List<UUID> avatars = createAccessListArrayByFlag(AccessList.Ban);
                remote_client.SendLandAccessListData(avatars, (uint)AccessList.Ban, landData.LocalID);
            }
        }

        public void updateAccessList(uint flags, List<ParcelManager.ParcelAccessEntry> entries, IClientAPI remote_client)
        {
            LandData newData = landData.Copy();

            if (entries.Count == 1 && entries[0].AgentID == UUID.Zero)
            {
                entries.Clear();
            }

            List<ParcelManager.ParcelAccessEntry> toRemove = new List<ParcelManager.ParcelAccessEntry>();
            foreach (ParcelManager.ParcelAccessEntry entry in newData.ParcelAccessList)
            {
                if (entry.Flags == (AccessList)flags)
                {
                    toRemove.Add(entry);
                }
            }

            foreach (ParcelManager.ParcelAccessEntry entry in toRemove)
            {
                newData.ParcelAccessList.Remove(entry);
            }
            foreach (ParcelManager.ParcelAccessEntry entry in entries)
            {
                ParcelManager.ParcelAccessEntry temp = new ParcelManager.ParcelAccessEntry();
                temp.AgentID = entry.AgentID;
                temp.Time = new DateTime(); //Pointless? Yes.
                temp.Flags = (AccessList)flags;

                if (!newData.ParcelAccessList.Contains(temp))
                {
                    newData.ParcelAccessList.Add(temp);
                }
            }

            m_log.InfoFormat("[LAND]: updateAccessList for land parcel {0} [{1}] flags {2} -> {3} by {4}",
                newData.LocalID, newData.GlobalID, landData.Flags.ToString("X8"), newData.Flags.ToString("X8"), remote_client.Name);
            m_scene.LandChannel.UpdateLandObject(landData.LocalID, newData);
        }

        #endregion

        #region Update Functions

        private void updateLandBitmapByteArray()
        {
            landData.Bitmap = convertLandBitmapToBytes();
        }

        /// <summary>
        /// Update all settings in land such as area, bitmap byte array, etc
        /// </summary>
        public void forceUpdateLandInfo()
        {
            updateAABBAndAreaValues();
            updateLandBitmapByteArray();
            landInfoNeedsUpdate = false;
        }

        public void updateLandInfoIfNeeded()
        {
            if (landInfoNeedsUpdate)
            {
                forceUpdateLandInfo();
            }
        }

        public void setLandBitmapFromByteArray()
        {
            m_landBitmap = convertBytesToLandBitmap();
            landInfoNeedsUpdate = true;
        }

        /// <summary>
        /// Updates the AABBMin and AABBMax values after area/shape modification of the land object
        /// </summary>
        private void updateAABBAndAreaValues()
        {
            int min_x = 64;
            int min_y = 64;
            int max_x = 0;
            int max_y = 0;
            int tempArea = 0;
            int x, y;
            for (x = 0; x < 64; x++)
            {
                for (y = 0; y < 64; y++)
                {
                    if (m_landBitmap[x, y] == true)
                    {
                        if (min_x > x) min_x = x;
                        if (min_y > y) min_y = y;
                        if (max_x < x) max_x = x;
                        if (max_y < y) max_y = y;
                        tempArea += 16; //16sqm peice of land
                    }
                }
            }

            int tx = min_x * 4;
            if (tx > ((int)Constants.RegionSize - 1))
                tx = ((int)Constants.RegionSize - 1);
            int ty = min_y * 4;
            if (ty > ((int)Constants.RegionSize - 1))
                ty = ((int)Constants.RegionSize - 1);
            landData.AABBMin =
                new Vector3((float) (min_x * 4), (float) (min_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);

            tx = max_x * 4;
            if (tx > ((int)Constants.RegionSize - 1))
                tx = ((int)Constants.RegionSize - 1);
            ty = max_y * 4;
            if (ty > ((int)Constants.RegionSize - 1))
                ty = ((int)Constants.RegionSize - 1);
            landData.AABBMax =
                new Vector3((float) (max_x * 4), (float) (max_y * 4),
                              (float) m_scene.Heightmap[tx, ty]);
            landData.Area = tempArea;
        }

        #endregion

        #region Land Bitmap Functions

        /// <summary>
        /// Sets the land's bitmap manually
        /// </summary>
        /// <param name="bitmap">64x64 block representing where this land is on a map</param>
        public void setLandBitmap(bool[,] bitmap)
        {
            if (bitmap.GetLength(0) != 64 || bitmap.GetLength(1) != 64 || bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                //throw new Exception("Error: Invalid Parcel Bitmap");
            }
            else
            {
                //Valid: Lets set it
                m_landBitmap = bitmap;
                landInfoNeedsUpdate = true;
                forceUpdateLandInfo();
            }
        }

        /// <summary>
        /// Gets the land's bitmap manually
        /// </summary>
        /// <returns></returns>
        public bool[,] getLandBitmap()
        {
            return m_landBitmap;
        }

        /// <summary>
        /// Used to modify the bitmap between the x and y points. Points use 64 scale
        /// </summary>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <returns></returns>
        public bool[,] getSquareLandBitmap(int start_x, int start_y, int end_x, int end_y)
        {
            bool[,] tempBitmap = new bool[64,64];
            tempBitmap.Initialize();

            tempBitmap = modifyLandBitmapSquare(tempBitmap, start_x, start_y, end_x, end_y, true);
            return tempBitmap;
        }

        /// <summary>
        /// Change a land bitmap at within a square and set those points to a specific value
        /// </summary>
        /// <param name="land_bitmap"></param>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <param name="set_value"></param>
        /// <returns></returns>
        public bool[,] modifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y,
                                              bool set_value)
        {
            if (land_bitmap.GetLength(0) != 64 || land_bitmap.GetLength(1) != 64 || land_bitmap.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                //throw new Exception("Error: Invalid Parcel Bitmap in modifyLandBitmapSquare()");
            }

            int x, y;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    if (x >= start_x / 4 && x < end_x / 4
                        && y >= start_y / 4 && y < end_y / 4)
                    {
                        land_bitmap[x, y] = set_value;
                    }
                }
            }
            return land_bitmap;
        }

        /// <summary>
        /// Join the true values of 2 bitmaps together
        /// </summary>
        /// <param name="bitmap_base"></param>
        /// <param name="bitmap_add"></param>
        /// <returns></returns>
        public bool[,] mergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add)
        {
            if (bitmap_base.GetLength(0) != 64 || bitmap_base.GetLength(1) != 64 || bitmap_base.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap - Bitmap_base in mergeLandBitmaps");
            }
            if (bitmap_add.GetLength(0) != 64 || bitmap_add.GetLength(1) != 64 || bitmap_add.Rank != 2)
            {
                //Throw an exception - The bitmap is not 64x64
                throw new Exception("Error: Invalid Parcel Bitmap - Bitmap_add in mergeLandBitmaps");
            }

            int x, y;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    if (bitmap_add[x, y])
                    {
                        bitmap_base[x, y] = true;
                    }
                }
            }
            return bitmap_base;
        }

        /// <summary>
        /// Converts the land bitmap to a packet friendly byte array
        /// </summary>
        /// <returns></returns>
        private byte[] convertLandBitmapToBytes()
        {
            byte[] tempConvertArr = new byte[512];
            byte tempByte = 0;
            int x, y, i, byteNum = 0;
            i = 0;
            for (y = 0; y < 64; y++)
            {
                for (x = 0; x < 64; x++)
                {
                    tempByte = Convert.ToByte(tempByte | Convert.ToByte(m_landBitmap[x, y]) << (i++ % 8));
                    if (i % 8 == 0)
                    {
                        tempConvertArr[byteNum] = tempByte;
                        tempByte = (byte) 0;
                        i = 0;
                        byteNum++;
                    }
                }
            }
            return tempConvertArr;
        }

        private bool[,] convertBytesToLandBitmap()
        {
            bool[,] tempConvertMap = new bool[64,64];
            tempConvertMap.Initialize();
            byte tempByte = 0;
            int x = 0, y = 0, i = 0, bitNum = 0;
            for (i = 0; i < 512; i++)
            {
                tempByte = landData.Bitmap[i];
                for (bitNum = 0; bitNum < 8; bitNum++)
                {
                    bool bit = Convert.ToBoolean(Convert.ToByte(tempByte >> bitNum) & (byte) 1);
                    tempConvertMap[x, y] = bit;
                    x++;
                    if (x > 63)
                    {
                        x = 0;
                        y++;
                    }
                }
            }
            return tempConvertMap;
        }

        #endregion

        #region Object Select and Object Owner Listing

        public void sendForceObjectSelect(int local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandOptions))
            {
                List<uint> resultLocalIDs = new List<uint>();
                try
                {
                    lock (primsOverMe)
                    {
                        foreach (SceneObjectGroup obj in primsOverMe)
                        {
                            if (obj.LocalId > 0)
                            {
                                if (request_type == LandChannel.LAND_SELECT_OBJECTS_OWNER && obj.OwnerID == landData.OwnerID)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_GROUP && obj.GroupID == landData.GroupID && landData.GroupID != UUID.Zero)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == LandChannel.LAND_SELECT_OBJECTS_OTHER &&
                                         obj.OwnerID != remote_client.AgentId)
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                                else if (request_type == (int)ObjectReturnType.List && returnIDs.Contains(obj.OwnerID))
                                {
                                    resultLocalIDs.Add(obj.LocalId);
                                }
                            }
                        }
                    }
                } catch (InvalidOperationException)
                {
                    m_log.Error("[LAND]: Unable to force select the parcel objects. Arr.");
                }

                remote_client.SendForceClientSelectObjects(resultLocalIDs);
            }
        }

        /// <summary>
        /// Notify the parcel owner each avatar that owns prims situated on their land.  This notification includes
        /// aggreagete details such as the number of prims.
        ///
        /// </summary>
        /// <param name="remote_client">
        /// A <see cref="IClientAPI"/>
        /// </param>
        public void sendLandObjectOwners(IClientAPI remote_client)
        {
            if (m_scene.Permissions.CanEditParcel(remote_client.AgentId, this, GroupPowers.LandOptions))
            {
                Dictionary<UUID, int> primCount = new Dictionary<UUID, int>();
                List<UUID> groups = new List<UUID>();

                lock (primsOverMe)
                {
                    try
                    {

                        foreach (SceneObjectGroup obj in primsOverMe)
                        {
                            try
                            {
                                if (!primCount.ContainsKey(obj.OwnerID))
                                {
                                    primCount.Add(obj.OwnerID, 0);
                                }
                            }
                            catch (NullReferenceException)
                            {
                                m_log.Info("[LAND]: " + "Got Null Reference when searching land owners from the parcel panel");
                            }
                            try
                            {
                                primCount[obj.OwnerID] += obj.LandImpact;
                            }
                            catch (KeyNotFoundException)
                            {
                                m_log.Error("[LAND]: Unable to match a prim with it's owner.");
                            }
                            if (obj.OwnerID == obj.GroupID && (!groups.Contains(obj.OwnerID)))
                                groups.Add(obj.OwnerID);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        m_log.Error("[LAND]: Unable to Enumerate Land object arr.");
                    }
                }

                remote_client.SendLandObjectOwners(landData, groups, primCount);
            }
        }

        public Dictionary<UUID, int> getLandObjectOwners()
        {
            Dictionary<UUID, int> ownersAndCount = new Dictionary<UUID, int>();
            lock (primsOverMe)
            {
                try
                {

                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (!ownersAndCount.ContainsKey(obj.OwnerID))
                        {
                            ownersAndCount.Add(obj.OwnerID, 0);
                        }
                        ownersAndCount[obj.OwnerID] += obj.LandImpact;
                    }
                }
                catch (InvalidOperationException)
                {
                    m_log.Error("[LAND]: Unable to enumerate land owners. arr.");
                }

            }
            return ownersAndCount;
        }

        #endregion

        #region Object Returning

        public void returnObject(SceneObjectGroup obj)
        {
            SceneObjectGroup[] objs = new SceneObjectGroup[1];
            objs[0] = obj;
            m_scene.returnObjects(objs);
        }

        public List<SceneObjectGroup> GetPrimsOverByOwner(UUID targetID, bool scriptedOnly)
        {
            List<SceneObjectGroup> prims = new List<SceneObjectGroup>();
            List<SceneObjectGroup> myPrims;

            lock (primsOverMe)
            {
                myPrims = new List<SceneObjectGroup>(primsOverMe);
            }
            foreach (SceneObjectGroup obj in myPrims)
            {
                if (obj.OwnerID == targetID)
                {
                    if (scriptedOnly)
                    {
                        bool containsScripts = false;
                        foreach (SceneObjectPart part in obj.GetParts())
                        {
                            if (part.Inventory.ContainsScripts())
                            {
                                containsScripts = true;
                                break;
                            }
                        }
                        if (!containsScripts)
                            continue;
                    }
                    prims.Add(obj);
                }
            }
            return prims;
        }

        public void returnLandObjects(uint type, UUID[] owners, UUID[] tasks, IClientAPI remote_client)
        {
            m_log.InfoFormat("[LAND]: ReturnLandObjects requested by {0} with: type {1}, {2} owners, {3} tasks", remote_client.Name, type, owners.Length, tasks.Length);

            Dictionary<UUID,List<SceneObjectGroup>> returns =
                    new Dictionary<UUID,List<SceneObjectGroup>>();

            lock (primsOverMe)
            {
                if (type == (uint)ObjectReturnType.Owner)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.OwnerID == m_landData.OwnerID)
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Group && m_landData.GroupID != UUID.Zero)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.GroupID == m_landData.GroupID)
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.Other)
                {
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (obj.OwnerID != m_landData.OwnerID &&
                            (obj.GroupID != m_landData.GroupID ||
                            m_landData.GroupID == UUID.Zero))
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == (uint)ObjectReturnType.List)
                {
                    List<UUID> ownerlist = new List<UUID>(owners);

                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (ownerlist.Contains(obj.OwnerID))
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                        new List<SceneObjectGroup>();
                            returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
                else if (type == 1)//Return by sim owner by object UUID
                {
                    List<UUID> Tasks = new List<UUID>(tasks);
                    foreach (SceneObjectGroup obj in primsOverMe)
                    {
                        if (Tasks.Contains(obj.UUID))
                        {
                            if (!returns.ContainsKey(obj.OwnerID))
                                returns[obj.OwnerID] =
                                    new List<SceneObjectGroup>();
                            if (!returns[obj.OwnerID].Contains(obj))
                                returns[obj.OwnerID].Add(obj);
                        }
                    }
                }
            }

            foreach (List<SceneObjectGroup> ol in returns.Values)
            {
                if (m_scene.Permissions.CanUseObjectReturn(this, type, remote_client, remote_client.AgentId, ol))
                    m_scene.returnObjects(ol.ToArray());
            }
        }

        // This is a fast check for the case where there's an agent present and we've loaded group info.
        public bool IsAgentGroupOwner(IClientAPI remoteClient, UUID groupID)
        {
            if (groupID == UUID.Zero)
                return false;

            // Use the known in-memory group membership data if available before going to db.
            if (remoteClient == null)
                return false; // we don't know who to check

            // This isn't quite the same as being in the Owners role, but close enough in 
            // order to avoid multiple complex queries in order to check Role membership.
            return remoteClient.GetGroupPowers(groupID) == (ulong)Constants.OWNER_GROUP_POWERS;
        }

        public bool IsAgentGroupOwner(UUID agentID, UUID groupID)
        {
            if (groupID == UUID.Zero)
                return false;

            ScenePresence sp = m_scene.GetScenePresence(agentID);
            if (sp != null) {
                IClientAPI remoteClient = sp.ControllingClient;
                if (remoteClient != null)
                    return IsAgentGroupOwner(remoteClient, groupID);
            }

            // Otherwise, do it the hard way.
            IGroupsModule groupsModule = m_scene.RequestModuleInterface<IGroupsModule>();

            GroupRecord groupRec = groupsModule.GetGroupRecord(groupID);
            if (groupRec == null) return false;

            List<GroupRolesData> agentRoles = groupsModule.GroupRoleDataRequest(null, groupID);
            foreach (GroupRolesData role in agentRoles)
            {
                if (role.RoleID == groupRec.OwnerRoleID)
                    return true;
            }
            return false;
        }

        // Anyone can grant PERMISSION_RETURN_OBJECTS but it can be only used under strict rules.
        // Returns 0 on success, or LSL error code on error.
        int canUseReturnPermission(ILandObject targetParcel, TaskInventoryItem scriptItem)
        {
            // First check the runtime perms to see if we're allowed to do returns at all.
            if (scriptItem.PermsGranter == UUID.Zero)
                return Constants.ERR_RUNTIME_PERMISSIONS;

            // For both the user-owned and group-deeded script cases, there is a script check and a parcel check.
            // The non-group PermsGranter is simpler, so check that first.

            // Script Check: If script owned by an agent, PERMISSION_RETURN_OBJECTS granted by owner of the script.
            if (scriptItem.OwnerID == scriptItem.PermsGranter)
            {
                // a user owns the scripted object, not a group.

                // Parcel Check: owner of the parcel with the target prim == perms granter / script owner?
                if (targetParcel.landData.OwnerID == scriptItem.OwnerID)
                    return 0; // passed both tests

                return Constants.ERR_PARCEL_PERMISSIONS; // not parcel owner
            }

            // else, granter is not the owner of the script, see if it's group-owned and granter a group Owner

            // Script Check: If script is group-deeded, permission granted by an agent belonging to group's "Owners" role.
            if ((scriptItem.GroupID == UUID.Zero) || (scriptItem.GroupID != scriptItem.OwnerID))
                return Constants.ERR_RUNTIME_PERMISSIONS; // not group-owned object

            // Parcel Check: first check if parcel is group-owned, the only remaining valid usage case.
            if ((targetParcel.landData.GroupID == UUID.Zero) || (targetParcel.landData.GroupID != targetParcel.landData.OwnerID))
                return Constants.ERR_PARCEL_PERMISSIONS; // not group-deeded (so not parcel owner)

            // Parcel Check: group-owned, so check if granter is an Owner in group
            if (IsAgentGroupOwner(scriptItem.PermsGranter, targetParcel.landData.GroupID))
                return 0; // passed both tests

            return Constants.ERR_PARCEL_PERMISSIONS;
        }

        public int scriptedReturnLandObjectsByOwner(TaskInventoryItem scriptItem, UUID targetOwnerID)
        {
            // Check if we're allowed to be using return permission at all, and if so, allowed in this parcel.
            int rc = canUseReturnPermission(this, scriptItem);
            if (rc != 0) return rc;

            // EO, EM and parcel owner's objects cannot be returned by this method.
            if (targetOwnerID == landData.OwnerID)
                return 0;
            if (m_scene.IsEstateManager(targetOwnerID))
                return 0;

            // This function will only work properly if one of the following is true:
            // - the land is owned by the scripted owner and this permission has been granted by the land owner, or
            // - the land is group owned and this permission has been granted by a group member filling the group "Owners" role.

            List<SceneObjectGroup> returns = new List<SceneObjectGroup>();

            lock (primsOverMe)
            {
                foreach (SceneObjectGroup grp in primsOverMe)
                {
                    // ignore anything not owned by the target
                    if (grp.OwnerID != targetOwnerID)
                        continue;
                    // Objects owned (deeded), to the group that the land is set to, will not be returned (ByOwner only)
                    if (grp.IsGroupDeeded && (grp.GroupID == landData.GroupID))
                        continue;

                    returns.Add(grp);
                }
            }

            if (returns.Count > 0)
                return m_scene.returnObjects(returns.ToArray(), "scripted parcel owner return");

            return 0;   // nothing to return
        }

        public int scriptedReturnLandObjectsByIDs(SceneObjectPart callingPart, TaskInventoryItem scriptItem, List<UUID> IDs)
        {
            int count = 0;

            // Check if we're allowed to be using return permission at all, and if so, allowed in this parcel.
            int rc = canUseReturnPermission(this, scriptItem);
            if (rc != 0) return rc;

            // This function will only work properly if one of the following is true:
            // - the land is owned by the scripted owner and this permission has been granted by the land owner, or
            // - the land is group owned and this permission has been granted by a group member filling the group "Owners" role.
            List<SceneObjectGroup> returns = new List<SceneObjectGroup>();

            lock (primsOverMe)
            {
                foreach (UUID id in IDs)
                {
                    SceneObjectPart part = m_scene.GetSceneObjectPart(id);
                    SceneObjectGroup grp = part == null ? null : part.ParentGroup;

                    if ((grp != null) && (primsOverMe.Contains(grp)))
                    {
                        // The rules inside this IF only apply if the calling prim is not specifying itself.
                        if (grp.UUID != callingPart.ParentGroup.UUID)
                        {
                            // EO, EM and parcel owner's objects cannot be returned by this method.
                            if (grp.OwnerID == landData.OwnerID)
                                continue;
                            if (m_scene.IsEstateManager(grp.OwnerID))
                                continue;
                            if (!m_scene.IsEstateManager(scriptItem.OwnerID))
                            {
                                // EO and EM can return from any parcel, otherwise allowed only on 
                                // objects located in any parcel owned by the script owner in the region.
                                if (landData.OwnerID != scriptItem.OwnerID)
                                    continue;   // parcel has different owner
                            }
                        }

                        returns.Add(grp);
                    }
                }
            }

            foreach (var grp in returns)
            {
                // the returns list by ID could be a variety of owners and group settings, need to check each one separately.
                List<SceneObjectGroup> singleList = new List<SceneObjectGroup>();
                singleList.Add(grp);
                if (singleList.Count > 0)
                    count += m_scene.returnObjects(singleList.ToArray(), "scripted parcel owner return by ID");
            }

            return count;
        }

        public void InspectParcelForAutoReturn()
        {
            lock (primsOverMe)
            {
                foreach (SceneObjectGroup obj in primsOverMe)
                {
                    m_scene.InspectForAutoReturn(obj, this.landData);
                }
            }
        }

        #endregion

        #region Object Adding/Removing from Parcel

        public void resetLandPrimCounts()
        {
            landData.GroupPrims = 0;
            landData.OwnerPrims = 0;
            landData.OtherPrims = 0;
            landData.SelectedPrims = 0;


            lock (primsOverMe)
                primsOverMe.Clear();
        }

        public void addPrimToCount(SceneObjectGroup obj)
        {

            UUID prim_owner = obj.OwnerID;
            int prim_count = obj.LandImpact;

            if (obj.IsSelected)
            {
                landData.SelectedPrims += prim_count;
            }
            else
            {
                if (prim_owner == landData.OwnerID)
                {
                    landData.OwnerPrims += prim_count;
                }
                else if ((obj.GroupID == landData.GroupID ||
                          prim_owner  == landData.GroupID) &&
                          landData.GroupID != UUID.Zero)
                {
                    landData.GroupPrims += prim_count;
                }
                else
                {
                    landData.OtherPrims += prim_count;
                }
            }

            lock (primsOverMe)
                primsOverMe.Add(obj);
        }

        public void removePrimFromCount(SceneObjectGroup obj)
        {
            lock (primsOverMe)
            {
                if (primsOverMe.Contains(obj))
                {
                    UUID prim_owner = obj.OwnerID;
                    int prim_count = obj.LandImpact;

                    if (prim_owner == landData.OwnerID)
                    {
                        landData.OwnerPrims -= prim_count;
                    }
                    else if (obj.GroupID == landData.GroupID ||
                             prim_owner  == landData.GroupID)
                    {
                        landData.GroupPrims -= prim_count;
                    }
                    else
                    {
                        landData.OtherPrims -= prim_count;
                    }

                    primsOverMe.Remove(obj);
                }
            }
        }

        #endregion

        #endregion

        #endregion
        
        /// <summary>
        /// Set the media url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMediaUrl(string url)
        {
            landData.MediaURL = url.Trim();
            sendLandUpdateToAvatarsOverParcel(false);            
        }
        
        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public void SetMusicUrl(string url)
        {
            landData.MusicURL = url.Trim();
            sendLandUpdateToAvatarsOverParcel(false);            
        }

        /// <summary>
        /// Get the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        public string GetMusicUrl()
        {
            return landData.MusicURL;
        }
    }
}
