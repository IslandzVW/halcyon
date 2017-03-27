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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using Caps=OpenSim.Framework.Communications.Capabilities.Caps;
using OpenSim.Data.SimpleDB;

namespace OpenSim.Region.CoreModules.World.Land
{
    // used for caching
    internal class ExtendedLandData {
        public LandData landData;
        public ulong regionHandle;
        public uint x, y;
    }

    public class LandManagementModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string remoteParcelRequestPath = "0009/";

        private LandChannel landChannel;
        private Scene m_scene;

        private readonly int[,] m_landIDList = new int[64, 64];
        private readonly Dictionary<int, ILandObject> m_landList = new Dictionary<int, ILandObject>();

        private bool m_landPrimCountTainted;
        private int m_lastLandLocalID = LandChannel.START_LAND_LOCAL_ID - 1;

        private bool m_allowedForcefulBans = true;

        // caches ExtendedLandData
        private Cache parcelInfoCache;

        #region IRegionModule Members

        private ConnectionFactory _connFactory;

        // NOTE: RDB-related code is all grouped here and is copied in AvatarSearchModule (for now).
        // This needs to be factored. I'd like to see MySQLSimpleDB be extended to 
        // a MySQLSimpleRegionDB class that wraps the underlying single-db calls into combined calls
        // and redirects queries to the correct database.
        private string _rdbConnectionTemplate;
        private string _rdbConnectionTemplateDebug;
        private readonly TimeSpan RDB_CACHE_TIMEOUT = TimeSpan.FromHours(4);
        private DateTime _rdbCacheTime;
        private List<string> _rdbHostCache = new List<string>();

        public void Initialize(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_landIDList.Initialize();
            landChannel = new LandChannel(scene, this);

            parcelInfoCache = new Cache();
            parcelInfoCache.Size = 30; // the number of different parcel requests in this region to cache
            parcelInfoCache.DefaultTTL = new TimeSpan(0, 5, 0);

            m_scene.EventManager.OnParcelPrimCountAdd += AddPrimToLandPrimCounts;
            m_scene.EventManager.OnParcelPrimCountUpdate += UpdateLandPrimCounts;
            m_scene.EventManager.OnAvatarEnteringNewParcel += new EventManager.AvatarEnteringNewParcel(handleAvatarChangingParcel);
            m_scene.EventManager.OnValidateLandBuy += handleLandValidationRequest;
            m_scene.EventManager.OnLandBuy += handleLandBuyRequest;
            m_scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
            m_scene.EventManager.OnSignificantClientMovement += handleSignificantClientMovement;
            m_scene.EventManager.OnObjectBeingRemovedFromScene += RemovePrimFromLandPrimCounts;

            m_scene.EventManager.OnNoticeNoLandDataFromStorage += this.NoLandDataFromStorage;
            m_scene.EventManager.OnIncomingLandDataFromStorage += this.IncomingLandObjectsFromStorage;
            m_scene.EventManager.OnSetAllowForcefulBan += this.SetAllowedForcefulBans;
            m_scene.EventManager.OnRequestParcelPrimCountUpdate += this.PerformParcelPrimCountUpdate;
            m_scene.EventManager.OnParcelPrimCountTainted += this.SetPrimsTainted;
            m_scene.EventManager.OnRegisterCaps += this.OnRegisterCaps;

            IConfig myConfig = config.Configs["Startup"];
            string connstr = myConfig.GetString("core_connection_string", String.Empty);
            _rdbConnectionTemplate = myConfig.GetString("rdb_connection_template", String.Empty);
            if (!String.IsNullOrWhiteSpace(_rdbConnectionTemplate))
            {
                if (!_rdbConnectionTemplate.ToLower().Contains("data source"))
                {
                    _rdbConnectionTemplate = "Data Source={0};" + _rdbConnectionTemplate;
                }
            }
            _rdbConnectionTemplateDebug = myConfig.GetString("rdb_connection_template_debug", String.Empty);
            if (!String.IsNullOrWhiteSpace(_rdbConnectionTemplateDebug))
            {
                if (!_rdbConnectionTemplateDebug.ToLower().Contains("data source"))
                {
                    _rdbConnectionTemplateDebug = "Data Source={0};" + _rdbConnectionTemplateDebug;
                }
            }
            _connFactory = new ConnectionFactory("MySQL", connstr);

            CacheRdbHosts();

            lock (m_scene)
            {
                m_scene.LandChannel = (ILandChannel)landChannel;
            }
        }

        private void CacheRdbHosts()
        {
            using (ISimpleDB db = _connFactory.GetConnection())
            {
                CacheRdbHosts(db);
            }
        }

        private void CacheRdbHosts(ISimpleDB db)
        {
            List<Dictionary<string, string>> hostNames = db.QueryWithResults("SELECT host_name FROM RdbHosts");

            _rdbHostCache.Clear();
            foreach (var hostNameDict in hostNames)
            {
                _rdbHostCache.Add(hostNameDict["host_name"]);
            }

            _rdbCacheTime = DateTime.Now;
        }

        // WARNING: This function only supports queries that result in a TOTAL number of results of 100 or less.
        // If too many results come back, the rest can be discarded/lost. Intended for searching for a specific parcel ID (1 result).
        // See AvatarSearchModule.cs for the alternative more complex implementation that avoids this restriction.
        private List<Dictionary<string, string>> DoLandQueryAndCombine(ISimpleDB coreDb, string query, Dictionary<string, object> parms)
        {
            string[] rdbHosts = this.CheckAndRetrieveRdbHostList(coreDb);

            if (rdbHosts.Length == 0)
            {
                // RDB not configured.  Fall back to core db.
                return coreDb.QueryWithResults(query, parms);
            }

            List<Dictionary<string, string>> finalList = new List<Dictionary<string, string>>();
            int whichDB = 0;
            foreach (string host in rdbHosts)
            {
                ConnectionFactory rdbFactory;
                if ((++whichDB == 1) || String.IsNullOrWhiteSpace(_rdbConnectionTemplateDebug))
                    rdbFactory = new ConnectionFactory("MySQL", String.Format(_rdbConnectionTemplate, host));
                else  // Special debugging support for multiple RDBs on one machine ("inworldz_rdb2", etc)
                    rdbFactory = new ConnectionFactory("MySQL", String.Format(_rdbConnectionTemplateDebug, host, whichDB));
                using (ISimpleDB rdb = rdbFactory.GetConnection())
                {
                    finalList.AddRange(rdb.QueryWithResults(query, parms));
                }
            }

            return finalList;
        }

        private string[] CheckAndRetrieveRdbHostList(ISimpleDB coreDb)
        {
            lock (_rdbHostCache)
            {
                if (DateTime.Now - _rdbCacheTime >= RDB_CACHE_TIMEOUT)
                {
                    //recache
                    CacheRdbHosts(coreDb);
                }

                return _rdbHostCache.ToArray();
            }
        }
        // NOTE: END OF RDB-related code copied in AvatarSearchModule (for now).

        void EventManager_OnNewClient(IClientAPI client)
        {
            //Register some client events
            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest += new ParcelPropertiesUpdateRequest(handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest += new ParcelObjectOwnerRequest(handleParcelObjectOwnersRequest);
            client.OnParcelAccessListRequest += new ParcelAccessListRequest(handleParcelAccessRequest);
            client.OnParcelAccessListUpdateRequest += new ParcelAccessListUpdateRequest(handleParcelAccessUpdateRequest);
            client.OnParcelAbandonRequest += new ParcelAbandonRequest(handleParcelAbandonRequest);
            client.OnParcelGodForceOwner += new ParcelGodForceOwner(handleParcelGodForceOwner);
            client.OnParcelReclaim += new ParcelReclaim(handleParcelReclaim);
            client.OnParcelInfoRequest += new ParcelInfoRequest(handleParcelInfo);
            client.OnParcelDwellRequest += new ParcelDwellRequest(handleParcelDwell);
            client.OnParcelFreezeUser += new FreezeUserUpdate(OnParcelFreezeUser);
            client.OnParcelEjectUser += new EjectUserUpdate(OnParcelEjectUser);

            client.OnParcelDeedToGroup += new ParcelDeedToGroup(handleParcelDeedToGroup);

            //client.OnRegionHandShakeReply += RegionHandShakeReply;

            if (m_scene.Entities.ContainsKey(client.AgentId))
            {
                ScenePresence avatar = m_scene.GetScenePresence(client.AgentId);
                ILandObject parcel = GetAvatarParcel(avatar);
                if ((avatar != null) && (parcel != null))
                    SendAvatarLandUpdate(avatar, parcel, true);
                SendParcelOverlay(client);
            }
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LandManagementModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region Parcel Add/Remove/Get/Create

        public void SetAllowedForcefulBans(bool forceful)
        {
            AllowedForcefulBans = forceful;
        }

        public bool AllowedForcefulBans
        {
            get { return m_allowedForcefulBans; }
            set { m_allowedForcefulBans = value; }
        }

        public float BanHeight
        {
            get { return LandChannel.BAN_LINE_SAFETY_HEIGHT; }
        }

        public float NonPublicHeight
        {
            get { return LandChannel.NON_PUBLIC_SAFETY_HEIGHT; }
        }

        public void UpdateLandObject(int local_id, LandData data)
        {
            LandData newData = data.Copy();
            newData.LocalID = local_id;

            ILandObject dirtyObject = null;
            lock (m_landList)
            {
                m_log.InfoFormat("[LAND]: Updating parcel {0} price={1} and flags={2:X8}", local_id, newData.SalePrice, newData.Flags);
                if (m_landList.ContainsKey(local_id))
                {
                    m_landList[local_id].landData = newData;
                    dirtyObject = m_landList[local_id];
                }
                else
                    m_log.ErrorFormat("[LAND]: UpdateLandObject: Parcel [{0}] not found.", local_id);
            }

            if (dirtyObject != null)
            {
                m_scene.EventManager.TriggerLandObjectUpdated((uint)local_id, dirtyObject);
            }
        }

        public void SendSelectedParcelUpdate(ILandObject parcel, Scene scene, UUID agentID)
        {
            ScenePresence SP = scene.GetScenePresence(agentID);
            IClientAPI client = (SP == null) ? null : SP.ControllingClient;
            if (client != null)
                parcel.SendSelectedLandUpdate(client);
        }

        /// <summary>
        /// Resets the sim to the default land object (full sim piece of land owned by the default user)
        /// </summary>
        public void ResetSimLandObjects()
        {
            //Remove all the land objects in the sim and add a blank, full sim land object set to public
            lock (m_landList)
            {
                m_landList.Clear();
                m_lastLandLocalID = LandChannel.START_LAND_LOCAL_ID - 1;
                m_landIDList.Initialize();
            }

            ILandObject fullSimParcel = new LandObject(UUID.Zero, false, m_scene);

            fullSimParcel.setLandBitmap(fullSimParcel.getSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize));
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                fullSimParcel.landData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
            else
                fullSimParcel.landData.OwnerID = m_scene.RegionInfo.MasterAvatarAssignedUUID;
            fullSimParcel.landData.ClaimDate = Util.UnixTimeSinceEpoch();
            AddLandObject(fullSimParcel);
        }

        public List<ILandObject> AllParcels()
        {
            lock (m_landList)
            {
                return new List<ILandObject>(m_landList.Values);
            }
        }

        public List<ILandObject> ParcelsNearPoint(Vector3 position)
        {
            List<ILandObject> parcelsNear = new List<ILandObject>();
            for (int x = -4; x <= 4; x += 4)
            {
                for (int y = -4; y <= 4; y += 4)
                {
                    ILandObject check = GetLandObject(position.X + x, position.Y + y);
                    if (check != null)
                    {
                        if (!parcelsNear.Contains(check))
                        {
                            parcelsNear.Add(check);
                        }
                    }
                }
            }

            return parcelsNear;
        }

        public void SendNoEntryNotice(ScenePresence avatar, ParcelPropertiesStatus reason)
        {
            if (!AllowedForcefulBans)
            {
                avatar.ControllingClient.SendAlertMessage(
                    "You are not allowed on this parcel, however the grid administrator has disabled ban lines globally. Please respect the land owner's privacy, or you could be banned from the full region.");
                return;
            }

            if (reason == ParcelPropertiesStatus.CollisionBanned)
                avatar.ControllingClient.SendAlertMessage(
                    "You are not permitted to enter this parcel because you are banned.");
            else
                avatar.ControllingClient.SendAlertMessage(
                    "You are not permitted to enter this parcel due to parcel restrictions.");
        }

        // Bounce constants are how far above a no-entry parcel we'll place an object or avatar.
        public void RemoveAvatarFromParcel(ScenePresence avatar)
        {
            EntityBase.PositionInfo posInfo = avatar.GetPosInfo();

            if (posInfo.Parent != null)
            {
                // can't find the prim seated on, stand up
                avatar.StandUp(false, true);

                // fall through to unseated avatar code.
            }

            // If they are moving, stop them.  This updates the physics object as well.
            // The avatar needs to be stopped before entering the parcel otherwise there
            // are timing windows where the avatar can just pound away at the parcel border
            // and get across it due to physics placing them there.
            avatar.Velocity = Vector3.Zero;

            Vector3 pos = avatar.AbsolutePosition;  // may have changed from posInfo by StandUp above.

            ParcelPropertiesStatus reason2;
            if (!avatar.lastKnownAllowedPosition.Equals(Vector3.Zero))
            {
                pos = avatar.lastKnownAllowedPosition;
            }

            ILandObject parcel = landChannel.GetLandObject(pos.X, pos.Y);
            float minZ;
            if ((parcel != null) && m_scene.TestBelowHeightLimit(avatar.UUID, pos, parcel, out minZ, out reason2))
            {
                float groundZ = (float)m_scene.Heightmap.CalculateHeightAt(pos.X, pos.Y);
                minZ += groundZ;

                // make them bounce above the banned parcel if being removed
                if (pos.Z < minZ)
                    pos.Z = minZ + Constants.AVATAR_BOUNCE;
            }

            // Now force the non-sitting avatar to a position above the parcel
            avatar.Teleport(pos);   // this is really just a move
        }
        public void RemoveAvatarFromParcel(UUID userID)
        {
            ScenePresence sp = m_scene.GetScenePresence(userID);
            RemoveAvatarFromParcel(sp);
        }

        public void handleAvatarChangingParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            if (m_scene.RegionInfo.RegionID == regionID)
            {
                ILandObject parcelAvatarIsEntering;
                lock (m_landList)
                {
                    parcelAvatarIsEntering = m_landList[localLandID];
                }

                if (parcelAvatarIsEntering != null)
                {
                    Vector3 pos = avatar.AbsolutePosition;
                    if (pos.Z < LandChannel.BAN_LINE_SAFETY_HEIGHT)
                    {
                        ParcelPropertiesStatus reason;
                        if (parcelAvatarIsEntering.DenyParcelAccess(avatar.UUID, out reason))
                        {
                            EntityBase.PositionInfo avatarPos = avatar.GetPosInfo();
                            RemoveAvatarFromParcel(avatar);
                            SendNoEntryNotice(avatar, reason);
                            return;
                        }
                    }
                    avatar.lastKnownAllowedPosition = pos;
                }
            }
        }

        public void SendOutNearestBanLine(IClientAPI avatar)
        {
            ParcelPropertiesStatus reason;
            ScenePresence presence = m_scene.GetScenePresence(avatar.AgentId);
            if (presence == null)
                return;

            List<ILandObject> ParcelList = ParcelsNearPoint(presence.AbsolutePosition);
            foreach (ILandObject parcel in ParcelList)
            {
                if (parcel.DenyParcelAccess(avatar.AgentId, out reason))
                {
                    parcel.sendLandProperties((int)reason, false, (int)ParcelResult.Single, avatar);
                    return; // send one only
                }
            }
        }

        public ILandObject NearestAllowedParcel(UUID avatar, Vector3 where)
        {
            ParcelPropertiesStatus reason;
            List<ILandObject> parcelList = ParcelsNearPoint(where);
            foreach (ILandObject parcel in parcelList)
                if (!parcel.DenyParcelAccess(avatar, out reason))
                    return parcel;
            return null;
        }

        public ILandObject NearestAllowedParcel(IClientAPI avatar)
        {
            ScenePresence presence = m_scene.GetScenePresence(avatar.AgentId);
            return this.NearestAllowedParcel(avatar.AgentId,presence.AbsolutePosition);
        }

        // Avatar is present in this parcel, possibly after entering it.
        private void SendAvatarLandUpdate(ScenePresence avatar, ILandObject parcel, bool force)
        {
            if (avatar.IsChildAgent) 
                return;

            bool newParcel = (avatar.currentParcelUUID != parcel.landData.GlobalID);
            if (newParcel || force)
            {
                parcel.sendLandUpdateToClient(avatar.ControllingClient);

                avatar.currentParcelUUID = parcel.landData.GlobalID;

                if ((parcel.landData.Flags & (uint)ParcelFlags.AllowDamage) != 0 && m_scene.RegionInfo.RegionSettings.AllowDamage)
                {
                    avatar.Invulnerable = false;
                }
                else
                {
                    avatar.Invulnerable = true;
                }
            }
        }

        public ILandObject GetNearestParcel(Vector3 pos)
        {
            pos.X = Util.Clamp<int>((int)pos.X, 0, 255);
            pos.Y = Util.Clamp<int>((int)pos.Y, 0, 255);
            return GetLandObject(pos.X, pos.Y);
        }

        public ILandObject GetAvatarParcel(ScenePresence avatar)
        {
            return GetNearestParcel(avatar.AbsolutePosition);
        }

        public void RefreshParcelInfo(IClientAPI remote_client, bool force)
        {
            // remote_client can be null on botRemoveBot (or if the agent disconnects)
            // because CompleteMovement calls RefreshParcelInfo from an async thread.
            if (remote_client == null) return;

            ScenePresence avatar = m_scene.GetScenePresence(remote_client.AgentId);
            if (avatar != null)
            {
                Vector3 pos = avatar.AbsolutePosition;
                ILandObject parcel = GetLandObject(pos.X, pos.Y);   // use the real values, ignore parcel not found
                if (parcel != null)
                {
                    SendOutNearestBanLine(remote_client);

                    // Possibly entering the restricted zone of the parcel.
                    ParcelPropertiesStatus reason;
                    float minZ;
                    if (!m_scene.TestBelowHeightLimit(avatar.UUID, pos, parcel, out minZ, out reason))
                    {
                        avatar.lastKnownAllowedPosition = pos;
                    }

                    bool newParcel = (avatar.currentParcelUUID != parcel.landData.GlobalID);
                    if (newParcel)
                    {
                        SendAvatarLandUpdate(avatar, parcel, force);
                        m_scene.EventManager.TriggerAvatarEnteringNewParcel(avatar, parcel.landData.LocalID, m_scene.RegionInfo.RegionID);
                    }
                }
            }
        }

        public void handleSignificantClientMovement(IClientAPI remote_client)
        {
            RefreshParcelInfo(remote_client, false);
        }

        public void handleParcelAccessRequest(uint flags, int landLocalID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(landLocalID, out land);
            }

            if (land != null)
            {
                m_landList[landLocalID].sendAccessList(flags, remote_client);
            }
        }

        public void handleParcelAccessUpdateRequest(uint flags, List<ParcelManager.ParcelAccessEntry> entries,
                                                    int landLocalID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(landLocalID, out land);
            }

            if (land != null)
            {
                if (!land.landData.IsGroupOwned)
                {
                    if (remote_client.AgentId == land.landData.OwnerID)
                        land.updateAccessList(flags, entries, remote_client);
                    else
                        remote_client.SendAlertMessage("You must be the parcel owner to manage the parcel access list.");
                    return;
                }

                if ((flags & (uint)(AccessList.Access)) != 0)
                {
                    if (!m_scene.Permissions.CanEditParcel(remote_client.AgentId, land, GroupPowers.LandManageAllowed))
                    {
                        remote_client.SendAlertMessage("You do not have group ability to manage the Allowed list for this group-deeded parcel.");
                        return;
                    }
                }

                if ((flags & (uint)(AccessList.Ban)) != 0)
                {
                    if (!m_scene.Permissions.CanEditParcel(remote_client.AgentId, land, GroupPowers.LandManageBanned))
                    {
                        remote_client.SendAlertMessage("You do not have group ability to manage the Banned list for this group-deeded parcel.");
                        return;
                    }
                }

                // Okay go ahead and make the change now.
                land.updateAccessList(flags, entries, remote_client);
            }
            else
            {
                m_log.WarnFormat("[LAND]: Invalid local land ID {0}", landLocalID);
                remote_client.SendAlertMessage("Parcel update failed: Could not find the specified land parcel.");
            }
        }

        /// <summary>
        /// Creates a basic Parcel object without an owner (a zeroed key)
        /// </summary>
        /// <returns></returns>
        public ILandObject CreateBaseLand()
        {
            return new LandObject(UUID.Zero, false, m_scene);
        }

        /// <summary>
        /// Adds a land object to the stored list and adds them to the landIDList to what they own
        /// </summary>
        /// <param name="new_land">The land object being added</param>
        public ILandObject AddLandObject(ILandObject land)
        {
            ILandObject new_land = land.Copy();

            lock (m_landList)
            {
                int newLandLocalID = ++m_lastLandLocalID;
                new_land.landData.LocalID = newLandLocalID;

                bool[,] landBitmap = new_land.getLandBitmap();
                for (int x = 0; x < 64; x++)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        if (landBitmap[x, y])
                        {
                            m_landIDList[x, y] = newLandLocalID;
                        }
                    }
                }

                m_landList.Add(newLandLocalID, new_land);
            }

            new_land.forceUpdateLandInfo();
            m_scene.EventManager.TriggerLandObjectAdded(new_land);
            return new_land;
        }

        /// <summary>
        /// Removes a land object from the list. Will not remove if local_id is still owning an area in landIDList
        /// </summary>
        /// <param name="local_id">Land.localID of the peice of land to remove.</param>
        public void removeLandObject(int local_id)
        {
            lock (m_landList)
            {
                for (int x = 0; x < 64; x++)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        if (m_landIDList[x, y] == local_id)
                        {
                            m_log.WarnFormat("[LAND]: Not removing land object {0}; still being used at {1}, {2}",
                                             local_id, x, y);
                            return;
                            //throw new Exception("Could not remove land object. Still being used at " + x + ", " + y);
                        }
                    }
                }

                m_scene.EventManager.TriggerLandObjectRemoved(m_landList[local_id].landData.GlobalID);
                m_landList.Remove(local_id);
            }
        }

        private void performFinalLandJoin(ILandObject master, ILandObject slave)
        {
            bool[,] landBitmapSlave = slave.getLandBitmap();
            lock (m_landList)
            {
                for (int x = 0; x < 64; x++)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        if (landBitmapSlave[x, y])
                        {
                            m_landIDList[x, y] = master.landData.LocalID;
                        }
                    }
                }
            }

            m_log.WarnFormat("[LAND]: Joining land parcel {0} [{1}] to {2} [{3}]", 
                slave.landData.LocalID, slave.landData.GlobalID, master.landData.LocalID, master.landData.GlobalID);
            removeLandObject(slave.landData.LocalID);
            UpdateLandObject(master.landData.LocalID, master.landData);
        }

        public ILandObject GetLandObject(int parcelLocalID)
        {
            lock (m_landList)
            {
                if (m_landList.ContainsKey(parcelLocalID))
                {
                    return m_landList[parcelLocalID];
                }
            }
            return null;
        }

        /// <summary>
        /// Get the land object at the specified point
        /// </summary>
        /// <param name="x_float">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y_float">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Land object at the point supplied</returns>
        public ILandObject GetLandObject(float x_float, float y_float)
        {
            int x;
            int y;

            try
            {
                x = Convert.ToInt32(Math.Floor(Convert.ToDouble(x_float) / 4.0));
                y = Convert.ToInt32(Math.Floor(Convert.ToDouble(y_float) / 4.0));
            }
            catch (OverflowException)
            {
                return null;
            }

            if (x >= 64 || y >= 64 || x < 0 || y < 0)
            {
                return null;
            }
            lock (m_landList)
            {
                // Corner case. If an autoreturn happens during sim startup
                // we will come here with the list uninitialized
                //
                if (m_landList.ContainsKey(m_landIDList[x, y]))
                    return m_landList[m_landIDList[x, y]];
                return null;
            }
        }

        public ILandObject GetLandObject(int x, int y)
        {
            if (x >= Convert.ToInt32(Constants.RegionSize) || y >= Convert.ToInt32(Constants.RegionSize) || x < 0 || y < 0)
            {
                // These exceptions here will cause a lot of complaints from the users specifically because
                // they happen every time at border crossings
                m_log.Warn("[LAND]: Error: Parcel not found at point " + x + ", " + y);
                throw new Exception("Error: Parcel not found at point " + x + ", " + y);
            }
            lock (m_landIDList)
            {
                return m_landList[m_landIDList[x / 4, y / 4]];
            }
        }

        /// <summary>
        /// Get the land object nearest to the (possibly) off-world location specified
        /// </summary>
        /// <param name="x">will be forced between 0 and less than 256</param>
        /// <param name="y">will be forced between 0 and less than 256</param>
        /// <returns>the nearest land parcel object</returns>
        public ILandObject GetNearestLandObjectInRegion(float x, float y)
        {
            Util.ForceValidRegionXY(ref x, ref y);
            return GetLandObject(x, y);
        }

#endregion

#region Parcel Modification

        public void ResetAllLandPrimCounts()
        {
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    p.resetLandPrimCounts();
                }
            }
        }

        public void SetPrimsTainted()
        {
            m_landPrimCountTainted = true;
        }

        public bool IsLandPrimCountTainted()
        {
            return m_landPrimCountTainted;
        }

        public void AddPrimToLandPrimCounts(SceneObjectGroup obj)
        {
            Vector3 position = obj.AbsolutePosition;
            ILandObject landUnderPrim = GetLandObject(position.X, position.Y);
            if (landUnderPrim != null)
            {
                landUnderPrim.addPrimToCount(obj);
            }
        }

        public void RemovePrimFromLandPrimCounts(SceneObjectGroup obj)
        {
            
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    p.removePrimFromCount(obj);
                }
            }
        }

        public void FinalizeLandPrimCountUpdate()
        {
            //Get Simwide prim count for owner
            Dictionary<UUID, List<LandObject>> landOwnersAndParcels = new Dictionary<UUID, List<LandObject>>();
            lock (m_landList)
            {
                int total = 0;
                foreach (LandObject p in m_landList.Values)
                {
                    total += p.landData.OwnerPrims + p.landData.OtherPrims + p.landData.GroupPrims + p.landData.SelectedPrims;

                    if (!landOwnersAndParcels.ContainsKey(p.landData.OwnerID))
                    {
                        List<LandObject> tempList = new List<LandObject>();
                        tempList.Add(p);
                        landOwnersAndParcels.Add(p.landData.OwnerID, tempList);
                    }
                    else
                    {
                        landOwnersAndParcels[p.landData.OwnerID].Add(p);
                    }
                }
                m_scene.RegionInfo.PrimTotal = total;
            }

            foreach (UUID owner in landOwnersAndParcels.Keys)
            {
                int simArea = 0;
                int simPrims = 0;
                foreach (LandObject p in landOwnersAndParcels[owner])
                {
                    simArea += p.landData.Area;
                    simPrims += p.landData.OwnerPrims + p.landData.OtherPrims + p.landData.GroupPrims +
                                p.landData.SelectedPrims;
                }

                foreach (LandObject p in landOwnersAndParcels[owner])
                {
                    p.landData.SimwideArea = simArea;
                    p.landData.SimwidePrims = simPrims;
                }
            }
        }

        public void UpdateLandPrimCounts()
        {
            Dictionary<UUID, int> botLandImpacts = new Dictionary<UUID, int>();
            foreach (EntityBase obj in m_scene.Entities)
            {
                if ((obj != null) && (obj is ScenePresence) && ((ScenePresence)obj).IsBot)
                {
                    //Bot attachments are counted in the parcel prim counts, so add them in
                    ScenePresence botSP = (ScenePresence)obj;
                    int landImpact = 0;
                    foreach (SceneObjectGroup grp in botSP.GetAttachments())
                        landImpact += grp.LandImpact;
                    botLandImpacts[botSP.UUID] = landImpact;
                }
            }

            lock (m_landList)
            {
                ResetAllLandPrimCounts();
                foreach (EntityBase obj in m_scene.Entities)
                {
                    if (obj != null)
                    {
                        if (obj is SceneObjectGroup)
                        {
                            if (!obj.IsDeleted && !((SceneObjectGroup)obj).IsAttachment)
                            {
                                m_scene.EventManager.TriggerParcelPrimCountAdd((SceneObjectGroup)obj);
                            }
                        }
                        else
                            if (obj is ScenePresence)
                            {
                                ScenePresence botSP = (ScenePresence)obj;
                                if (botLandImpacts.ContainsKey(botSP.UUID))
                                {
                                    int landImpact = botLandImpacts[botSP.UUID];
                                    Vector3 pos = botSP.AbsolutePosition;
                                    ILandObject landUnderPrim = GetNearestLandObjectInRegion(pos.X, pos.Y);
                                    if (landUnderPrim != null)
                                    {
                                        if (botSP.OwnerID == landUnderPrim.landData.OwnerID)
                                        {
                                            landUnderPrim.landData.OwnerPrims += landImpact;
                                        }
                                        else
                                        {
                                            landUnderPrim.landData.OtherPrims += landImpact;
                                        }
                                    }
                                }
                            }
                    }
                }
                FinalizeLandPrimCountUpdate();
                m_landPrimCountTainted = false;
            }
        }

        public void PerformParcelPrimCountUpdate()
        {
            lock (m_landList)
            {
                ResetAllLandPrimCounts();
                m_scene.EventManager.TriggerParcelPrimCountUpdate();
                FinalizeLandPrimCountUpdate();
                m_landPrimCountTainted = false;
            }
        }

        /// <summary>
        /// Subdivides a piece of land
        /// </summary>
        /// <param name="start_x">West Point</param>
        /// <param name="start_y">South Point</param>
        /// <param name="end_x">East Point</param>
        /// <param name="end_y">North Point</param>
        /// <param name="attempting_user_id">UUID of user who is trying to subdivide</param>
        /// <returns>Returns true if successful</returns>
        private void subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            //First, lets loop through the points and make sure they are all in the same peice of land
            //Get the land object at start

            ILandObject startLandObject = GetLandObject(start_x, start_y);

            if (startLandObject == null) return;

            //Loop through the points
            try
            {
                int totalX = end_x - start_x;
                int totalY = end_y - start_y;
                for (int y = 0; y < totalY; y++)
                {
                    for (int x = 0; x < totalX; x++)
                    {
                        ILandObject tempLandObject = GetLandObject(start_x + x, start_y + y);
                        if (tempLandObject == null) return;
                        if (tempLandObject != startLandObject) return;
                    }
                }
            }
            catch (Exception)
            {
                return;
            }

            //If we are still here, then they are subdividing within one piece of land
            //Check owner
            if (!m_scene.Permissions.CanEditParcel(attempting_user_id, startLandObject, GroupPowers.LandDivideJoin))
            {
                return;
            }

            //Lets create a new land object with bitmap activated at that point (keeping the old land objects info)
            ILandObject newLand = startLandObject.Copy();
            newLand.landData.Name = newLand.landData.Name;
            newLand.landData.GlobalID = UUID.Random();

            newLand.setLandBitmap(newLand.getSquareLandBitmap(start_x, start_y, end_x, end_y));

            //Now, lets set the subdivision area of the original to false
            int startLandObjectIndex = startLandObject.landData.LocalID;
            lock (m_landList)
            {
                m_landList[startLandObjectIndex].setLandBitmap(
                    newLand.modifyLandBitmapSquare(startLandObject.getLandBitmap(), start_x, start_y, end_x, end_y, false));
                m_landList[startLandObjectIndex].forceUpdateLandInfo();
            }

            SetPrimsTainted();

            //Now add the new land object
            ILandObject result = AddLandObject(newLand);
            m_log.WarnFormat("[LAND]: Subdivided land parcel {0} [{1}] into {2} [{3}]", 
                startLandObject.landData.LocalID, startLandObject.landData.GlobalID, result.landData.LocalID, result.landData.GlobalID);
            UpdateLandObject(startLandObject.landData.LocalID, startLandObject.landData);
            result.sendLandUpdateToAvatarsOverParcel();
        }

        /// <summary>
        /// Join 2 land objects together
        /// </summary>
        /// <param name="start_x">x value in first piece of land</param>
        /// <param name="start_y">y value in first piece of land</param>
        /// <param name="end_x">x value in second peice of land</param>
        /// <param name="end_y">y value in second peice of land</param>
        /// <param name="attempting_user_id">UUID of the avatar trying to join the land objects</param>
        /// <returns>Returns true if successful</returns>
        private void join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            end_x -= 4;
            end_y -= 4;

            List<ILandObject> selectedLandObjects = new List<ILandObject>();
            int stepYSelected;
            for (stepYSelected = start_y; stepYSelected <= end_y; stepYSelected += 4)
            {
                int stepXSelected;
                for (stepXSelected = start_x; stepXSelected <= end_x; stepXSelected += 4)
                {
                    ILandObject p = GetLandObject(stepXSelected, stepYSelected);

                    if (p != null)
                    {
                        if (!selectedLandObjects.Contains(p))
                        {
                            selectedLandObjects.Add(p);
                        }
                    }
                }
            }
            ILandObject masterLandObject = selectedLandObjects[0];
            selectedLandObjects.RemoveAt(0);

            if (selectedLandObjects.Count < 1)
            {
                return;
            }
            if (!m_scene.Permissions.CanEditParcel(attempting_user_id, masterLandObject, GroupPowers.LandDivideJoin))
            {
                return;
            }
            foreach (ILandObject p in selectedLandObjects)
            {
                if (p.landData.OwnerID != masterLandObject.landData.OwnerID)
                {
                    return;
                }
            }
            
            lock (m_landList)
            {
                foreach (ILandObject slaveLandObject in selectedLandObjects)
                {
                    m_landList[masterLandObject.landData.LocalID].setLandBitmap(
                        slaveLandObject.mergeLandBitmaps(masterLandObject.getLandBitmap(), slaveLandObject.getLandBitmap()));
                    performFinalLandJoin(masterLandObject, slaveLandObject);
                }
            }
            SetPrimsTainted();

            masterLandObject.sendLandUpdateToAvatarsOverParcel();
        }

#endregion

#region Parcel Updating

        /// <summary>
        /// Where we send the ParcelOverlay packet to the client
        /// </summary>
        /// <param name="remote_client">The object representing the client</param>
        public void SendParcelOverlay(IClientAPI remote_client)
        {
            const int LAND_BLOCKS_PER_PACKET = 1024;

            byte[] byteArray = new byte[LAND_BLOCKS_PER_PACKET];
            int byteArrayCount = 0;
            int sequenceID = 0;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    byte tempByte = 0; //This represents the byte for the current 4x4

                    ILandObject currentParcelBlock = GetLandObject(x * 4, y * 4);

                    if (currentParcelBlock != null)
                    {
                        if (currentParcelBlock.landData.OwnerID == remote_client.AgentId)
                        {
                            //Owner Flag
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_OWNED_BY_REQUESTER);
                        }
                        else if (currentParcelBlock.landData.SalePrice > 0 &&
                                 (currentParcelBlock.landData.AuthBuyerID == UUID.Zero ||
                                  currentParcelBlock.landData.AuthBuyerID == remote_client.AgentId))
                        {
                            //Sale Flag
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_IS_FOR_SALE);
                        }
                        else if (currentParcelBlock.landData.OwnerID == UUID.Zero)
                        {
                            //Public Flag
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_PUBLIC);
                        }
                        else if (currentParcelBlock.landData.IsGroupOwned)
                        {
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_OWNED_BY_GROUP);
                        }
                        else
                        {
                            //Other Flag
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_TYPE_OWNED_BY_OTHER);
                        }

                        // Now the "restrict sounds locally" option
                        if ((currentParcelBlock.landData.Flags & (uint)ParcelFlags.SoundLocal) != 0)
                        {
                            //Owner Flag
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_SOUND_LOCAL);
                        }

                        //Now for border control

                        ILandObject westParcel = null;
                        ILandObject southParcel = null;
                        if (x > 0)
                        {
                            westParcel = GetLandObject((x - 1) * 4, y * 4);
                        }
                        if (y > 0)
                        {
                            southParcel = GetLandObject(x * 4, (y - 1) * 4);
                        }

                        if (x == 0)
                        {
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_WEST);
                        }
                        else if (westParcel != null && westParcel != currentParcelBlock)
                        {
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_WEST);
                        }

                        if (y == 0)
                        {
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH);
                        }
                        else if (southParcel != null && southParcel != currentParcelBlock)
                        {
                            tempByte = Convert.ToByte(tempByte | LandChannel.LAND_FLAG_PROPERTY_BORDER_SOUTH);
                        }

                        byteArray[byteArrayCount] = tempByte;
                        byteArrayCount++;
                        if (byteArrayCount >= LAND_BLOCKS_PER_PACKET)
                        {
                            remote_client.SendLandParcelOverlay(byteArray, sequenceID);
                            byteArrayCount = 0;
                            sequenceID++;
                            byteArray = new byte[LAND_BLOCKS_PER_PACKET];
                        }
                    }
                }
            }
        }

        public void handleParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id,
                                                  bool snap_selection, IClientAPI remote_client)
        {
            //Get the land objects within the bounds
            List<ILandObject> temp = new List<ILandObject>();
            int inc_x = end_x - start_x;
            int inc_y = end_y - start_y;
            for (int x = 0; x < inc_x; x++)
            {
                for (int y = 0; y < inc_y; y++)
                {
                    ILandObject currentParcel = GetLandObject(start_x + x, start_y + y);

                    if (currentParcel != null)
                    {
                        if (!temp.Contains(currentParcel))
                        {
                            currentParcel.updateLandInfoIfNeeded();
                            temp.Add(currentParcel);
                        }
                    }
                }
            }

            int requestResult = LandChannel.LAND_RESULT_SINGLE;
            if (temp.Count > 1)
            {
                requestResult = LandChannel.LAND_RESULT_MULTIPLE;
            }

            for (int i = 0; i < temp.Count; i++)
            {
                temp[i].sendLandProperties(sequence_id, snap_selection, requestResult, remote_client);
            }

            SendParcelOverlay(remote_client);
        }

        public void handleParcelPropertiesUpdateRequest(LandUpdateArgs args, int localID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(localID, out land);
            }

            if (land != null)
            {
                land.updateLandProperties(args, remote_client);
                parcelInfoCache.Invalidate(land.landData.GlobalID.ToString());
            }
        }

        public void handleParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            if ((m_scene.RegionInfo.Product == ProductRulesUse.PlusUse) && (!m_scene.IsGodUser(remote_client.AgentId)))
                remote_client.SendAgentAlertMessage("You are not permitted to subdivide parcels in this region.", false);
            else
                subdivide(west, south, east, north, remote_client.AgentId);
        }

        public void handleParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client)
        {
            if ((m_scene.RegionInfo.Product == ProductRulesUse.PlusUse) && (!m_scene.IsGodUser(remote_client.AgentId)))
                remote_client.SendAgentAlertMessage("You are not permitted to join parcels in this region.", false);
            else
                join(west, south, east, north, remote_client.AgentId);
        }

        public void handleParcelSelectObjectsRequest(int local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                land.sendForceObjectSelect(local_id, request_type, returnIDs, remote_client);
            }
            else
            {
                m_log.WarnFormat("[PARCEL]: Invalid land object {0} passed for parcel object owner request", local_id);
            }
        }

        public void handleParcelObjectOwnersRequest(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                land.sendLandObjectOwners(remote_client);
            }
            else
            {
                m_log.WarnFormat("[PARCEL]: Invalid land object {0} passed for parcel object owner request", local_id);
            }
        }

        public void handleParcelGodForceOwner(int local_id, UUID ownerID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                if (m_scene.Permissions.IsGod(remote_client.AgentId))
                {
                    land.landData.OwnerID = ownerID;
                    land.landData.ClearSaleInfo();

                    m_log.WarnFormat("[LAND]: handleParcelGodForceOwner for land parcel {0} [{1}] flags {2} by {3}",
                        land.landData.LocalID, land.landData.GlobalID, land.landData.Flags.ToString("X8"), remote_client.Name);

                    m_scene.Broadcast(SendParcelOverlay);
                    land.SendSelectedLandUpdate(remote_client);
                    land.InspectParcelForAutoReturn();
                }
            }
        }

        public readonly String WEBSITE_ABANDON = "http://inworldz.com/plus/abandon";
        public void handleParcelAbandonRequest(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                if (m_scene.Permissions.CanAbandonParcel(remote_client.AgentId, land))
                {
                    if (m_scene.RegionInfo.Product == ProductRulesUse.PlusUse)
                    {
                        remote_client.SendAgentAlertMessage("To abandon this Plus parcel, please do so from the website at " + WEBSITE_ABANDON, false);
                        return;
                    }
                    if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                        land.landData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                    else
                        land.landData.OwnerID = m_scene.RegionInfo.MasterAvatarAssignedUUID;
                    land.landData.IsGroupOwned = false;
                    land.landData.GroupID = UUID.Zero;
                    land.landData.ClearSaleInfo();

                    m_log.WarnFormat("[LAND]: handleParcelAbandonRequest for land parcel {0} [{1}] flags {2} by {3}",
                        land.landData.LocalID, land.landData.GlobalID, land.landData.Flags.ToString("X8"), remote_client.Name);

                    m_scene.Broadcast(SendParcelOverlay);
                    land.SendSelectedLandUpdate(remote_client);
                    land.InspectParcelForAutoReturn();
                }
            }
        }

        public void handleParcelReclaim(int local_id, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(local_id, out land);
            }

            if (land != null)
            {
                if (m_scene.Permissions.CanReclaimParcel(remote_client.AgentId, land))
                {
                    if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                        land.landData.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                    else
                        land.landData.OwnerID = m_scene.RegionInfo.MasterAvatarAssignedUUID;
                    land.landData.ClaimDate = Util.UnixTimeSinceEpoch();
                    land.landData.IsGroupOwned = false;
                    land.landData.GroupID = UUID.Zero;
                    land.landData.ClearSaleInfo();

                    m_log.WarnFormat("[LAND]: handleParcelAbandonRequest for land parcel {0} [{1}] flags {2} by {3}",
                        land.landData.LocalID, land.landData.GlobalID, land.landData.Flags.ToString("X8"), remote_client.Name);

                    m_scene.Broadcast(SendParcelOverlay);
                    land.SendSelectedLandUpdate(remote_client);
                    land.InspectParcelForAutoReturn();
                }
            }
        }
#endregion

        // After receiving a land buy packet, first the data needs to
        // be validated. This method validates the right to buy the parcel.
        public void handleLandValidationRequest(Object o, EventManager.LandBuyArgs e)
        {
            Scene scene = (Scene)o;

            if (e.landValidated == false)
            {
                ScenePresence SP = scene.GetScenePresence(e.agentId);
                IClientAPI client = (SP == null) ? null : SP.ControllingClient;
                ILandObject lob = null;

                lock (m_landList)
                {
                    m_landList.TryGetValue(e.requestLocalID, out lob);
                }

                if ((lob == null) || (lob.landData == null))
                {
                    if (client != null)
                        client.SendAgentAlertMessage("Could not validate land parcel for purchase.", false);
                }

                UUID AuthorizedID = lob.landData.AuthBuyerID;
                int AuthorizedPrice = lob.landData.SalePrice;
                UUID pOwnerID = lob.landData.OwnerID;

                if ((lob.landData.Flags & (uint)ParcelFlags.ForSale) == 0)
                {
                    if (client != null)
                        client.SendAgentAlertMessage("That parcel does not appear to be for sale.", false);
                    return;
                }

                if (AuthorizedID != UUID.Zero && AuthorizedID != e.agentId)
                {
                    if (client != null)
                        client.SendAgentAlertMessage("You are not authorized to purchase that land parcel.", false);
                    return;
                }
                    
                if (e.requestPrice != AuthorizedPrice)
                {
                    if (client != null)
                        client.SendAgentAlertMessage("Payment amount does not match the parcel sale price.", false);
                    return;
                }

                if (lob.landData.IsGroupOwned && AuthorizedPrice != 0)
                {
                    if (client != null)
                        client.SendAgentAlertMessage("Sale of group-deeded land is not currently supported unless the price is 0.", false);
                    return;
                }

                // Authorize this land parcel sale.
                e.parcel = lob;
                // save the following parcel fields because they are about to change on transfer
                e.originalParcelOwner = lob.landData.OwnerID;
                e.originalIsGroup = lob.landData.IsGroupOwned;
                e.originalParcelPrice = lob.landData.SalePrice;
                e.landValidated = true;
            }
        }

        // If the economy has been validated by the economy module, and land
        // has been validated as well, this method transfersthe land ownership.
        public void handleLandBuyRequest(Object o, EventManager.LandBuyArgs e)
        {
            if (e.economyValidated && e.landValidated && e.parcel != null && e.parcel.landData != null)
            {
                Scene scene = (Scene)o;
                e.parcel.updateLandSold(e.agentId, e.groupId, e.buyForGroup);
                SendSelectedParcelUpdate(e.parcel, scene, e.agentId);
            }
        }

        void handleParcelDeedToGroup(int parcelLocalID, UUID groupID, IClientAPI remote_client)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(parcelLocalID, out land);
            }

            if (!m_scene.Permissions.CanDeedParcel(remote_client.AgentId, land))
                return;

            if (land != null)
            {
                land.deedToGroup(groupID);
                m_log.WarnFormat("[LAND]: handleDeedToGroup for land parcel {0} [{1}] flags {2} by {3}",
                    land.landData.LocalID, land.landData.GlobalID, land.landData.Flags.ToString("X8"), remote_client.Name);
                land.SendSelectedLandUpdate(remote_client);
            }

        }

#region Land Object From Storage Functions

        public void IncomingLandObjectsFromStorage(List<LandData> data)
        {
            for (int i = 0; i < data.Count; i++)
            {
                IncomingLandObjectFromStorage(data[i]);
            }
        }

        public void IncomingLandObjectFromStorage(LandData data)
        {
            ILandObject new_land = new LandObject(data.OwnerID, data.IsGroupOwned, m_scene);
            new_land.landData = data.Copy();
            new_land.setLandBitmapFromByteArray();
            new_land.landData.UpdateForcedAutoReturn(m_scene.RegionInfo.MaxAutoReturn);
            AddLandObject(new_land);
        }

        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient)
        {
            ILandObject selectedParcel = null;
            lock (m_landList)
            {
                m_landList.TryGetValue(localID, out selectedParcel);
            }

            if (selectedParcel == null)
            {
                if (localID != -1)//A full sim request
                    return;
                foreach (ILandObject parcel in AllParcels())
                {
                    parcel.returnLandObjects(returnType, agentIDs, taskIDs, remoteClient);
                }
                return;
            }

            selectedParcel.returnLandObjects(returnType, agentIDs, taskIDs, remoteClient);
        }

        // Pass parcel==null for all parcels in the region, or parcel != null for a specific parcel
        // or all parcels owned by the same owner.
        public int ScriptedReturnObjectsInParcelByOwner(TaskInventoryItem scriptItem, UUID targetAgentID, LandData patternParcel, bool sameOwner)
        {
            int count = 0;

            // Applies to all parcels in the region
            foreach (ILandObject landObject in AllParcels())
            {
                bool includeParcel = (patternParcel == null);  // all parcels
                if (patternParcel != null)  // a more specific criteria
                {
                    if (patternParcel.LocalID == landObject.landData.LocalID)
                        includeParcel = true;
                    else
                    if (sameOwner && (patternParcel.OwnerID == landObject.landData.OwnerID))
                        includeParcel = true;
                }

                if (includeParcel)
                {
                    int rc = landObject.scriptedReturnLandObjectsByOwner(scriptItem, targetAgentID);
                    // if we get an error, stop. If we've returned items, return the count, otherwise error code.
                    if (rc < 0) // error
                        return (count > 0) ? count : rc;
                    count += rc;
                }
            }

            return count;
        }

        public int ScriptedReturnObjectsInParcelByIDs(SceneObjectPart callingPart, TaskInventoryItem scriptItem, List<UUID> targetIDs, int parcelLocalID)
        {
            if (!m_landList.ContainsKey(parcelLocalID))
                return 0;

            ILandObject parcel = m_landList[parcelLocalID];
            return parcel.scriptedReturnLandObjectsByIDs(callingPart, scriptItem, targetIDs);
        }

        public void NoLandDataFromStorage()
        {
            ResetSimLandObjects();
        }

#endregion

        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            lock (m_landList)
            {
                foreach (LandObject obj in m_landList.Values)
                {
                    obj.setParcelObjectMaxOverride(overrideDel);
                }
            }
        }

        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
        }

#region CAPS handler

        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler(
                "RemoteParcelRequest",
                new RestStreamHandler(
                    "POST",
                    capsBase + remoteParcelRequestPath,
                    (request, path, param, httpRequest, httpResponse)
                        => RemoteParcelRequest(request, path, param, agentID, caps),
                    "RemoteParcelRequest",
                    agentID.ToString()));

            UUID parcelCapID = UUID.Random();
            caps.RegisterHandler(
                "ParcelPropertiesUpdate",
                new RestStreamHandler(
                    "POST",
                    "/CAPS/" + parcelCapID,
                        (request, path, param, httpRequest, httpResponse)
                            => ProcessPropertiesUpdate(request, path, param, agentID, caps),
                    "ParcelPropertiesUpdate",
                    agentID.ToString()));            
        }

        private string ProcessPropertiesUpdate(string request, string path, string param, UUID agentID, Caps caps)
        {
            IClientAPI client;
            if (!m_scene.TryGetClient(agentID, out client))
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Unable to retrieve IClientAPI for {0}", agentID);
                return LLSDHelpers.SerializeLLSDReply(new LLSDEmpty());
            }

            ParcelPropertiesUpdateMessage properties = new ParcelPropertiesUpdateMessage();
            OpenMetaverse.StructuredData.OSDMap args = (OpenMetaverse.StructuredData.OSDMap)OSDParser.DeserializeLLSDXml(request);

            properties.Deserialize(args);

            LandUpdateArgs land_update = new LandUpdateArgs();
            int parcelID = properties.LocalID;
            land_update.AuthBuyerID = properties.AuthBuyerID;
            land_update.Category = properties.Category;
            land_update.Desc = properties.Desc;
            land_update.GroupID = properties.GroupID;
            land_update.LandingType = (byte)properties.Landing;
            land_update.MediaAutoScale = (byte)Convert.ToInt32(properties.MediaAutoScale);
            land_update.MediaID = properties.MediaID;
            land_update.MediaURL = properties.MediaURL;
            land_update.MusicURL = properties.MusicURL;
            land_update.Name = properties.Name;
            land_update.ParcelFlags = (uint)properties.ParcelFlags;
            land_update.PassHours = (int)properties.PassHours;
            land_update.PassPrice = (int)properties.PassPrice;
            land_update.SalePrice = (int)properties.SalePrice;
            land_update.SnapshotID = properties.SnapshotID;
            land_update.UserLocation = properties.UserLocation;
            land_update.UserLookAt = properties.UserLookAt;
            land_update.MediaDescription = properties.MediaDesc;
            land_update.MediaType = properties.MediaType;
            land_update.MediaWidth = properties.MediaWidth;
            land_update.MediaHeight = properties.MediaHeight;
            land_update.MediaLoop = properties.MediaLoop;
            land_update.ObscureMusic = properties.ObscureMusic;
            land_update.ObscureMedia = properties.ObscureMedia;

            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(parcelID, out land);
            }

            if (land != null)
            {
                land.updateLandProperties(land_update, client);
                parcelInfoCache.Invalidate(land.landData.GlobalID.ToString());
                m_scene.EventManager.TriggerOnParcelPropertiesUpdateRequest(land_update, parcelID, client);
            }
            else
            {
                m_log.WarnFormat("[LAND MANAGEMENT MODULE]: Unable to find parcelID {0}", parcelID);
            }
            return LLSDHelpers.SerializeLLSDReply(new LLSDEmpty());
        }


        // we cheat here: As we don't have (and want) a grid-global parcel-store, we can't return the
        // "real" parcelID, because we wouldn't be able to map that to the region the parcel belongs to.
        // So, we create a "fake" parcelID by using the regionHandle (64 bit), and the local (integer) x
        // and y coordinate (each 8 bit), encoded in a UUID (128 bit).
        //
        // Request format:
        // <llsd>
        //   <map>
        //     <key>location</key>
        //     <array>
        //       <real>1.23</real>
        //       <real>45..6</real>
        //       <real>78.9</real>
        //     </array>
        //     <key>region_id</key>
        //     <uuid>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</uuid>
        //   </map>
        // </llsd>
        private string RemoteParcelRequest(string request, string path, string param, UUID agentID, Caps caps)
        {
            UUID parcelID = UUID.Zero;
            try
            {
                Hashtable hash = new Hashtable();
                hash = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));
                if (hash.ContainsKey("location"))
                {
                    ArrayList list = (ArrayList)hash["location"];
                    uint x = (uint)(double)list[0];
                    uint y = (uint)(double)list[1];
                    if (hash.ContainsKey("region_handle"))
                    {
                        // if you do a "About Landmark" on a landmark a second time, the viewer sends the
                        // region_handle it got earlier via RegionHandleRequest
                        ulong regionHandle = Util.BytesToUInt64Big((byte[])hash["region_handle"]);
                        parcelID = Util.BuildFakeParcelID(regionHandle, x, y);
                    }
                    else
                    {   // region_id case
                        UUID regionID = UUID.Zero;
                        if (hash.ContainsKey("region_id"))
                            regionID = (UUID)hash["region_id"];
                        if (regionID == UUID.Zero)
                            m_log.Warn("[LAND]: RemoteParcelRequest got null or missing region ID.");
                        else
                        if (regionID == m_scene.RegionInfo.RegionID)
                        {
                            // a parcel request for a local parcel => no need to query the grid
                            parcelID = Util.BuildFakeParcelID(m_scene.RegionInfo.RegionHandle, x, y);
                        }
                        else
                        {
                            // a parcel request for a parcel in another region. Ask the grid about the region
                            RegionInfo info = m_scene.CommsManager.GridService.RequestNeighbourInfo(regionID);
                            if (info != null)
                                parcelID = Util.BuildFakeParcelID(info.RegionHandle, x, y);
                        }
                    }
                }
            }
            catch (LLSD.LLSDParseException e)
            {
                m_log.ErrorFormat("[LAND]: Fetch error: {0}", e.Message);
                m_log.ErrorFormat("[LAND]: ... in request {0}", request);
            }
            catch(InvalidCastException)
            {
                m_log.ErrorFormat("[LAND]: Wrong type in request {0}", request);
            }

            LLSDRemoteParcelResponse response = new LLSDRemoteParcelResponse();
            response.parcel_id = parcelID;
            m_log.DebugFormat("[LAND]: got parcelID {0}", parcelID);

            return LLSDHelpers.SerializeLLSDReply(response);
        }

#endregion

        private void handleParcelDwell(int localID, IClientAPI remoteClient)
        {
            ILandObject selectedParcel = null;
            lock (m_landList)
            {
                if (!m_landList.TryGetValue(localID, out selectedParcel))
                    return;
            }
            
            remoteClient.SendParcelDwellReply(localID, selectedParcel.landData.GlobalID,  selectedParcel.landData.Dwell);
        }

        private Vector3 AnyParcelLocation(LandData landData)
        {
            // This just finds the midpoint, which might not be 
            // within the parcel for an irregularly shaped parcel,
            // but is a more likely choice than somethin closer to the edge.
            // Max is actually lower corner of the 4x4 so size is max+4 - min.
            float x = landData.AABBMin.X + (((landData.AABBMax.X+4) - landData.AABBMin.X) / 2);
            float y = landData.AABBMin.Y + (((landData.AABBMax.Y+4) - landData.AABBMin.Y) / 2);
            return new Vector3(x, y, 0.0f);
        }

        private ExtendedLandData QueryParcelID(UUID parcelID)
        {
            ExtendedLandData extData = null;

            using (ISimpleDB db = _connFactory.GetConnection())
            {
                // See of the parcel ID is a real one defined in the land table (e.g. from a Places search).
                string query = "select land.RegionUUID, land.LocalLandID from land where land.UUID=?parcelID";
                Dictionary<string, object> parms = new Dictionary<string, object>();
                parms.Add("?parcelID", parcelID.ToString());

                List<Dictionary<string, string>> results = DoLandQueryAndCombine(db, query, parms);
                if (results.Count > 1)
                    m_log.ErrorFormat("[Land]: Found {0} results searching for parcel ID {1}", results.Count, parcelID.ToString());
                if (results.Count > 0)
                {
                    LandData landData = null;
                    ulong regionHandle = 0;

                    Dictionary<string, string> row = results[0];
                    int localLandID = Convert.ToInt32(row["LocalLandID"]);
                    UUID regionID = new UUID(row["RegionUUID"].ToString());

                    // for this region or for somewhere else?
                    if (regionID == m_scene.RegionInfo.RegionID)
                    {
                        ILandObject parcel = this.GetLandObject(localLandID);
                        if ((parcel != null) && (parcel.landData != null))
                            landData = parcel.landData;
                        regionHandle = m_scene.RegionInfo.RegionHandle;
                    }
                    else
                    {
                        RegionInfo info = m_scene.CommsManager.GridService.RequestNeighbourInfo(regionID);
                        if (info != null)
                        {
                            landData = m_scene.CommsManager.GridService.RequestLandData(info.RegionHandle, localLandID);
                            regionHandle = info.RegionHandle;
                        }
                    }

                    if (landData != null)
                    {
                        extData = new ExtendedLandData();
                        extData.regionHandle = regionHandle;
                        extData.landData = landData;
                        // Prefer the parcel-specified landing area if available, even if set to Anywhere.
                        extData.x = (uint)landData.UserLocation.X;
                        extData.y = (uint)landData.UserLocation.Y;
                        if ((extData.x == 0) && (extData.x == 0))
                        {
                            Vector3 where = AnyParcelLocation(landData);
                            extData.x = (uint)where.X;
                            extData.y = (uint)where.Y;
                        }
                    }
                }
            }
            return extData;
        }

        private ExtendedLandData QueryFakeParcelID(UUID parcelID)
        {
            LandData landData = null;
            ulong regionHandle = 0;
            uint posx = 0;
            uint posy = 0;

            // We didn't find the specified parcel ID so attempt to interpret it as a fake parcel ID.
            Util.ParseFakeParcelID(parcelID, out regionHandle, out posx, out posy);

            // m_log.DebugFormat("[LAND]: got parcelinfo request for regionHandle {0}, x/y {1}/{2}", extLandData.regionHandle, extLandData.x, extLandData.y);

            // for this region or for somewhere else?
            if (regionHandle == m_scene.RegionInfo.RegionHandle)
            {
                ILandObject parcel = this.GetLandObject(posx, posy);
                if (parcel != null)
                    landData = parcel.landData;
            }
            else
                landData = m_scene.CommsManager.GridService.RequestLandData(regionHandle, posx, posy);

            if (landData == null)
                return null;

            ExtendedLandData extLandData = new ExtendedLandData();
            extLandData.landData = landData;
            extLandData.regionHandle = regionHandle;
            extLandData.x = posx;
            extLandData.y = posy;
            return extLandData;
        }

        private ExtendedLandData ParcelNotFound(IClientAPI remoteClient)
        {
            LandData landData = new LandData();
            landData.Name = "Land Parcel Not Found";
            landData.Description = "This land parcel could not be found: the region may be down or no longer be part of the grid.\n\n"
                                 + "(The current region has been substituted for this Search result.)";

            ExtendedLandData extLandData = new ExtendedLandData();
            extLandData.landData = landData;
            extLandData.regionHandle = m_scene.RegionInfo.RegionHandle;
            extLandData.x = 128; 
            extLandData.y = 128;

            // Fill in the current position of the avatar to prevent a beacon from appearing on Show On Map
            ScenePresence SP = m_scene.GetScenePresence(remoteClient.AgentId);
            if (SP != null)
            {
                Vector3 pos = SP.AbsolutePosition;
                extLandData.x = (uint)pos.X;
                extLandData.y = (uint)pos.Y;
            }

            return extLandData;
        }

        private void handleParcelInfo(IClientAPI remoteClient, UUID parcelID)
        {
            ExtendedLandData data = null;

            if (parcelID != UUID.Zero)
            {
                data = (ExtendedLandData)parcelInfoCache.Get(parcelID.ToString(), delegate(string id)
                {
                    UUID parcel = UUID.Zero;
                    UUID.TryParse(id, out parcel);

                    // First try this parcel ID as a valid real parcel ID in the db.
                    ExtendedLandData extLandData = QueryParcelID(parcelID);
                    if (extLandData == null)
                        extLandData = QueryFakeParcelID(parcelID);
                    return extLandData;
                });
            }
            if (data == null)
            {
                m_log.Debug("[LAND]: got no parcelinfo; substituting dummy parcel at current location.");
                data = ParcelNotFound(remoteClient);
            }

            // if we found some data, send it
            RegionInfo info = null;
            if (data.regionHandle == m_scene.RegionInfo.RegionHandle)
            {
                info = m_scene.RegionInfo;
            }
            else
            {
                // most likely still cached from building the extLandData entry
                info = m_scene.CommsManager.GridService.RequestNeighbourInfo(data.regionHandle);
            }

            if (info == null)
            {
                m_log.Debug("[LAND]: parcelinfo could not find region; substituting dummy location.");
                data = ParcelNotFound(remoteClient);
                info = m_scene.RegionInfo;
            }

            // we need to transfer the same parcelID, possibly fake, not the one in landData, so the viewer can match it to the landmark.
            // m_log.DebugFormat("[LAND]: got parcelinfo for parcel {0} in region {1}; sending...", data.landData.Name, data.regionHandle);
            remoteClient.SendParcelInfo(info, data.landData, parcelID, data.x, data.y);
        }

        public void setParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime)
        {
            ILandObject land;
            lock (m_landList)
            {
                m_landList.TryGetValue(localID, out land);
            }

            if (land == null) return;

            if (!m_scene.Permissions.CanEditParcel(remoteClient.AgentId, land, GroupPowers.LandOptions))
                return;

            land.landData.OtherCleanTime = otherCleanTime;
            land.landData.UpdateForcedAutoReturn(m_scene.RegionInfo.MaxAutoReturn);
            m_log.WarnFormat("[LAND]: Updating auto-return time for land parcel {0} [{1}] to {2}",
                land.landData.LocalID, land.landData.GlobalID, otherCleanTime);
            UpdateLandObject(localID, land.landData);
        }

        // BanUser() includes land parcel permissions checks and returns false if not permitted.
        public bool BanUser(IClientAPI client, ILandObject land, ScenePresence target_presence)
        {
            if (land == null) return false;
            if (target_presence == null) return false;
            if (target_presence.IsBot) return false;

            if (!m_scene.Permissions.CanEditParcel(client.AgentId, land, GroupPowers.LandManageBanned))
                return false;

            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = target_presence.UUID;
            entry.Flags = AccessList.Ban;
            entry.Time = new DateTime();
            land.landData.ParcelAccessList.Add(entry);
            return true;
        }

        public void OnParcelFreezeUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            // m_log.DebugFormat("OnParcelFreezeUser: target {0} by {1} options {2}", target, parcelowner.ToString(), flags);
            ScenePresence target_presence = m_scene.GetScenePresence(target);
            if (target_presence == null) return;

            ILandObject land = GetLandObject(target_presence.AbsolutePosition.X, target_presence.AbsolutePosition.Y);

            if (m_scene.Permissions.CanEditParcel(client.AgentId, land, GroupPowers.LandEjectAndFreeze))
            {
                bool freeze = ((flags & 1) == 0);
                target_presence.ControllingClient.FreezeMe(flags, client.AgentId, client.Name);
                if (freeze)
                    client.SendAlertMessage("You have frozen " + target_presence.Name);
                else
                    client.SendAlertMessage("You have unfrozen " + target_presence.Name);
            }
        }

        public void OnParcelEjectUser(IClientAPI client, UUID parcelowner, uint flags, UUID target)
        {
            // m_log.DebugFormat("OnParcelEjectUser: target {0} by {1} options {2}", target, parcelowner.ToString(), flags);
            ScenePresence target_presence = m_scene.GetScenePresence(target); 
            if (target_presence == null) return;

            Vector3 pos = target_presence.AbsolutePosition;
            ILandObject land = GetLandObject(pos.X, pos.Y);

            if (m_scene.Permissions.CanEditParcel(client.AgentId, land, GroupPowers.LandEjectAndFreeze))
            {
                ulong originalRegion = target_presence.RegionHandle;
                bool alsoBan = ((flags & 1) != 0);

                if (alsoBan)   // not just eject, but also eject and ban
                    BanUser(client, land, target_presence); // ban first, then eject (TP home)

                IClientAPI targetClient = target_presence.ControllingClient;
                targetClient.SendTeleportLocationStart();
                target_presence.Scene.TeleportClientHome(target, target_presence.ControllingClient);
                if (alsoBan && (target_presence.RegionHandle == originalRegion))
                {
                    m_log.WarnFormat("[LAND]: User {0} ejected and banned but did not leave region, disconnecting...", target);
                    targetClient.Kick("You are banned from your Home location. You may log back in to a different region.");
                    targetClient.Close();
                }
            }
        }

        private LandObject FindParcelByUUID(UUID parcelID)
        {
            lock (m_landList)
            {
                foreach (LandObject p in m_landList.Values)
                {
                    if (parcelID == p.landData.GlobalID)
                        return p;
                }
            }
            return null;
        }

        public LandData ClaimPlusParcel(UUID parcelID, UUID userID)
        {
            LandObject parcel = FindParcelByUUID(parcelID);
            if (parcel == null) return null;

            parcel.updateLandSold(userID, UUID.Zero, false);
            return parcel.landData;
        }

        public LandData AbandonPlusParcel(UUID parcelID)
        {
            LandObject parcel = FindParcelByUUID(parcelID);
            if (parcel == null) return null;

            parcel.updateLandSold(m_scene.RegionInfo.EstateSettings.EstateOwner, UUID.Zero, false);
            return parcel.landData;
        }
    }

}
