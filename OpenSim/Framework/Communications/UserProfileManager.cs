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
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Data;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Base class for user management (create, read, etc)
    /// </summary>
    public abstract class UserProfileManager : IUserService, IUserAdminService, IAvatarService, IMessagingService, IAuthentication
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_isUserServer = false;

        protected CommunicationsManager m_commsManager;

        /// <summary>
        /// Implements all costly operations such as XMLRPC calls or database lookups/writes.
        /// This is the interface to network/disk operations, a clear separation of responsibility.
        /// </summary>
        private UserProfileManagerData m_storage = new UserProfileManagerData();

        /// <summary>
        /// Maximum size for the profile and agent caches
        /// </summary>
        private const int MAX_CACHE_SIZE = 250;

        /// <summary>
        /// Number of seconds before an item in the cache is no longer considered viable
        /// </summary>
        private const int CACHE_ITEM_EXPIRY = 300;  // seconds, 5 minutes

        ///////////// Caches for UserAgentData //////////////

        /// <summary>
        /// LRU cache for UserAgentData
        /// </summary>
        private LRUCache<UUID, TimestampedItem<UserAgentData>> m_agentDataByUUID    //not used/cached in User servers
            = new LRUCache<UUID, TimestampedItem<UserAgentData>>(MAX_CACHE_SIZE);

        ///////////// Caches for UserProfileData //////////////

        /// <summary>
        /// Controls access to UserProfileData structures
        /// </summary>
        protected internal object m_userDataLock = new object();

        /// <summary>
        /// LRU cache for UserProfileData
        /// </summary>
        private LRUCache<UUID, TimestampedItem<UserProfileData>> m_userDataByUUID
            = new LRUCache<UUID, TimestampedItem<UserProfileData>>(MAX_CACHE_SIZE);

        /// <summary>
        /// Special cache for profile data for local users in the region
        /// </summary>
        private Dictionary<UUID, UserProfileData> m_localUser
            = new Dictionary<UUID, UserProfileData>();

        /// <summary>
        /// Temporary profiles for local region-specific users (bots). No persistence of visibility elsewhere.
        /// </summary>
        private Dictionary<UUID, UserProfileData> m_tempDataByUUID = new Dictionary<UUID, UserProfileData>();

        /// <summary>
        /// User profiles indexed by name
        /// This MUST be kept in sync with all UserProfileData lists
        /// </summary>
        private readonly Dictionary<string, UUID> m_userByName
            = new Dictionary<string, UUID>();

        ///////////// Caches for CachedUserInfo //////////////

        /// <summary>
        /// Controls access to CachedUserInfo structures
        /// </summary>
        protected internal object m_userInfoLock = new object();

        /// <summary>
        /// LRU cache for profile data
        /// </summary>
        private LRUCache<UUID, TimestampedItem<CachedUserInfo>> m_userInfoByUUID
            = new LRUCache<UUID, TimestampedItem<CachedUserInfo>>(MAX_CACHE_SIZE);

        /// <summary>
        /// User profiles indexed by name
        /// This will be kept in sync with either mCachedUserInfo or mCachedProfileData
        /// </summary>
        private readonly Dictionary<string, CachedUserInfo> m_userInfoByName
            = new Dictionary<string, CachedUserInfo>();

        public const string CUSTOM_TYPE_DELETED = "DELETED";
        private UUID m_deletedUserAccount = UUID.Zero;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        public UserProfileManager(CommunicationsManager commsManager)
        {
            m_commsManager = commsManager;
            m_userInfoByUUID.OnItemPurged += _cachedUserInfo_OnItemPurged;

            m_isUserServer = System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("OpenSim.Grid.UserServer");
        }

        public void InitConfig(UserConfig cfg)
        {
            // Currently we only support DeletedUserAccount from the XML config.
            string deletedStr = cfg.DeletedUserAccount;
            if (deletedStr != String.Empty)
            {
                UUID uuid = UUID.Zero;
                if (UUID.TryParse(deletedStr.Trim(), out uuid))
                {
                    m_deletedUserAccount = uuid;
                }
            }
        }

        public UUID DeletedUserAccount
        {
            get
            {
                return m_deletedUserAccount;
            }
            set
            {
                m_deletedUserAccount = value;
            }
        }

        public bool IsDeletedUserAccount(UserProfileData user)
        {
            return (user != null) && (user.ID == m_deletedUserAccount);
        }

        public bool IsCustomTypeDeleted(string customType)
        {
            return customType.Trim().ToUpper() == CUSTOM_TYPE_DELETED;
        }

        /// <summary>
        /// Add a new user data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="plugin">The plugin that will provide user data</param>
        public void AddPlugin(IUserDataPlugin plugin)
        {
            m_storage.AddPlugin(plugin);
        }

        /// <summary>
        /// Adds a list of user data plugins, as described by `provider' and
        /// `connect', to `_plugins'.
        /// </summary>
        /// <param name="provider">
        /// The filename of the inventory server plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public void AddPlugin(string provider, string connect)
        {
            m_storage.AddPlugin(provider, connect);
        }

        #region GetUserProfileData

        private UserProfileData TryGetUserProfile(UUID uuid, bool ignoreTimeout)
        {
            lock (m_userDataLock)
            {
                if (m_localUser.ContainsKey(uuid))
                    return m_localUser[uuid];   // never updates on timer (!)

                if (m_tempDataByUUID.ContainsKey(uuid))
                    return m_tempDataByUUID[uuid];

                TimestampedItem<UserProfileData> item;
                if (m_userDataByUUID.TryGetValue(uuid, out item))
                {
                    if (ignoreTimeout || (item.ElapsedSeconds < CACHE_ITEM_EXPIRY))
                        return item.Item;

                    // Else cache expired, or forcing a refresh.
                    // Leave it in here for now in case someone does a name lookup or something fairly static
                }
            }

            return null;    // not in cache
        }

        private string CombineNames(string fname, string lname)
        {
            return fname.Trim() + " " + lname.Trim();
        }
        private string DictName(string name)
        {
            return name.Trim().ToUpper();
        }
        private string DictName(string fname, string lname)
        {
            return DictName(CombineNames(fname, lname));
        }

        private UUID TryGetUUIDByName(string name, bool removeIfFound)
        {
            string dictName = DictName(name);
            UUID uuid;
            lock (m_userDataLock)
            {
                // m_userByName includes both regular and local UserProfileData entries.
                if (!m_userByName.TryGetValue(dictName, out uuid))
                    return UUID.Zero;    // not known

                if (removeIfFound)
                    m_userByName.Remove(dictName);
            }

            return uuid;
        }

        private UUID TryGetUUIDByName(string fname, string lname, bool removeIfFound)
        {
            string name = CombineNames(fname, lname);
            return TryGetUUIDByName(name, removeIfFound);
        }

        private void RemoveName(string name)
        {
            TryGetUUIDByName(name, true);
        }

        private void RemoveUserData(UUID uuid)
        {
            // never remove temp profiles this way
            lock (m_userDataLock)
            {
                UserProfileData profile;
                if (m_localUser.TryGetValue(uuid, out profile))
                {
                    m_localUser.Remove(uuid);
                    RemoveName(profile.Name);
                }
                TimestampedItem<UserProfileData> item;
                if (m_userDataByUUID.TryGetValue(uuid, out item))
                {
                    m_userDataByUUID.Remove(uuid);
                    RemoveName(item.Item.Name);
                }
            }
        }

        private void ReplaceUserData(UserProfileData profile)
        {
            lock (m_userDataLock)
            {
                if (m_userDataByUUID.Contains(profile.ID))
                    m_userDataByUUID.Remove(profile.ID);
                RemoveName(profile.Name);
                m_userByName.Add(DictName(profile.Name), profile.ID);
                m_userDataByUUID.Add(profile.ID, new TimestampedItem<UserProfileData>(profile));
            }
        }

        private Dictionary<UUID, object> m_fetchLocks = new Dictionary<UUID, object>();

        private UserProfileData _GetUserProfileData(UserProfileData profile)
        {
            if (profile == null)
                return null;

            // Continue on right away if it's not a deleted account.
            if (profile.CustomType.Trim().ToUpper() != CUSTOM_TYPE_DELETED)
                return profile;

            // Check if it's THE substitute deleted account
            if (profile.ID == DeletedUserAccount)
                return profile; // avoid infinite recursion

            // Otherwise it's a special customType==DELETED and remap the UUID to the special account.
            return GetUserProfile(DeletedUserAccount);
        }

        private UserProfileData _GetUserProfileData(UUID uuid)
        {
            UserProfileData profile = m_storage.GetUserProfileData(uuid);
            return _GetUserProfileData(profile);
        }

        private UserProfileData _GetUserProfileData(string firstName, string lastName)
        {
            UserProfileData profile = m_storage.GetUserProfileData(firstName, lastName);
            return _GetUserProfileData(profile);
        }

        public UserProfileData GetUserProfile(UUID uuid, bool forceRefresh)
        {
            if (uuid == UUID.Zero)
                return null;    // fast exit for no user specified

            UserProfileData profile;

            const int ATTEMPTS = 3;
            int attempts = 0;
            do
            {

                // Temp profiles do not exist in permanent storage, cannot force refresh.
                lock (m_userDataLock)
                {
                    if (m_tempDataByUUID.TryGetValue(uuid, out profile))
                        return profile;

                    if (!forceRefresh)
                    {
                        profile = TryGetUserProfile(uuid, false);
                        if (profile != null)
                        {
                            // Make sure we also have an AgentData for the profile.
                            profile.CurrentAgent = GetUserAgent(uuid, forceRefresh);
                            return profile;
                        }
                    }
                }

                // Else cache expired, or forcing a refresh of a normal cached profile.
                object myLock = new object();
                object uuidLock = null;
                // grab a tentative claim on fetching this uuid by locking the new object
                lock (myLock)
                {
                    // now see if we're the first ones in on this uuid
                    lock (m_fetchLocks)
                    {
                        // check if someone else is working on this uuid, use their lock object
                        if (!m_fetchLocks.TryGetValue(uuid, out uuidLock))
                        {
                            // nope, this is the first one in, myLock will be the lock object
                            m_fetchLocks[uuid] = myLock;
                            uuidLock = myLock;
                        }
                    }

                    // Now try to lock it. If original owner/fetcher, we already have it locked above on lock(fetchLock).
                    // If not the original owner, this will block until the original fetcher finishes.
                    lock (uuidLock)
                    {
                        if (uuidLock == myLock)
                        {
                            attempts = 0; // We're in, no more retries.

                            // Now that we've got a "write lock" on the profile data, 
                            // try to find it in the cache again in case it was added between locks
                            // Temp profiles do not exist in permanent storage, cannot force refresh.
                            if (!m_tempDataByUUID.TryGetValue(uuid, out profile))
                            {
                                // not a temp profile (bot)
                                profile = TryGetUserProfile(uuid, false);
                                if (profile == null)
                                {
                                    // still not found, get it from User service (or db if this is User).
                                    profile = _GetUserProfileData(uuid);
                                    if (profile != null)
                                    {
                                        // Refresh agent data (possibly forced refresh)
                                        profile.CurrentAgent = GetUserAgent(uuid, forceRefresh);
                                        ReplaceUserData(profile);
                                    }
                                    else
                                    {
                                        // not found in storage. If this is now known in temp profiles, return it,
                                        // otherwise remove it from non-temp profile info.
                                        if (!m_tempDataByUUID.TryGetValue(uuid, out profile))
                                        {
                                            RemoveUserData(uuid);
                                        }
                                    }
                                }
                                else
                                {
                                    // Refresh agent data (possibly forced refresh)
                                    profile.CurrentAgent = GetUserAgent(uuid, forceRefresh);
                                    ReplaceUserData(profile);
                                }
                            }

                            // no longer outstanding
                            lock (m_fetchLocks)
                            {
                                m_fetchLocks.Remove(uuid);
                            }
                        }
                        else
                        {
                            if (attempts == 0) // first time in
                                attempts = ATTEMPTS;
                            else
                                attempts--;
                        }
                    }
                }
            } while (attempts > 0);

            return profile;
        }

        public UserProfileData GetUserProfile(UUID uuid)
        {
            return GetUserProfile(uuid, false);
        }

        public UserProfileData GetUserProfile(string firstName, string lastName, bool forceRefresh)
        {
            string name = firstName.Trim() + " " + lastName.Trim();
            UUID uuid = UUID.Zero;

            if (name == " ")
                return null;    // fast exit for no user specified

            UserProfileData profile;

            uuid = TryGetUUIDByName(name, false);

            // Now if we know the UUID, just use the other function.
            if (uuid != UUID.Zero)
                return GetUserProfile(uuid, forceRefresh);  // in case it has expired

            // Not cached, UUID unknown, fetch from storage/XMLRPC by name.
            profile = _GetUserProfileData(firstName, lastName);
            lock (m_userDataLock)
            {
                // Now that it's locked again, ensure the lists have the correct data.
                if (profile != null)
                {
                    // We fetched a profile above, just use the function above.
                    profile.CurrentAgent = GetUserAgent(profile.ID);
                    ReplaceUserData(profile);
                }
            }

            return profile;
        }
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName, lastName, false);
        }
        public UserProfileData GetUserProfile(string name)
        {
            string[] names = name.Split(' ');
            return GetUserProfile(names[0], names[1], false);
        }

        // Just call these if all you need is the name from cache.
        public UUID Name2Key(string firstName, string lastName)
        {
            UUID uuid = TryGetUUIDByName(firstName, lastName, false);
            if (uuid != UUID.Zero)
                return uuid;

            UserProfileData profile = GetUserProfile(firstName, lastName, false);   // also adds to cache
            if (profile != null)
                return profile.ID;

            return UUID.Zero;
        }
        public UUID Name2Key(string name)
        {
            string[] names = name.Split(' ');
            return Name2Key(names[0], names[1]);
        }

        public bool Key2Names(UUID uuid, bool onlyIfCached, out string firstName, out string lastName)
        {
            UserProfileData profile;
            lock (m_userDataLock)
            {
                if (m_tempDataByUUID.TryGetValue(uuid, out profile))
                {
                    firstName = profile.FirstName;
                    lastName = profile.SurName;
                    return true;
                }

                if (m_localUser.TryGetValue(uuid, out profile))
                {
                    firstName = profile.FirstName;
                    lastName = profile.SurName;
                    return true;
                }

                TimestampedItem<UserProfileData> item;
                if (m_userDataByUUID.TryGetValue(uuid, out item))
                {
                    firstName = item.Item.FirstName;
                    lastName = item.Item.SurName;
                    return true;
                }
            }

            firstName = String.Empty;
            lastName = String.Empty;

            if (onlyIfCached)
                return false;

            profile = GetUserProfile(uuid, false);   // also adds to cache
            if (profile != null)
            {
                firstName = profile.FirstName;
                lastName = profile.SurName;
                return true;
            }

            return false;
        }

        public string Key2Name(UUID uuid, bool onlyIfCached)
        {
            string firstName = String.Empty;
            string lastName = String.Empty;
            if (!Key2Names(uuid, onlyIfCached, out firstName, out lastName))
                return String.Empty;

            return firstName + " " + lastName;
        }

        public string GetLastName(UUID uuid, bool onlyIfCached)
        {
            string firstName = String.Empty;
            string lastName = String.Empty;
            if (!Key2Names(uuid, onlyIfCached, out firstName, out lastName))
                return String.Empty;

            return lastName;
        }

        public string GetFirstName(UUID uuid, bool onlyIfCached)
        {
            string firstName = String.Empty;
            string lastName = String.Empty;
            if (!Key2Names(uuid, onlyIfCached, out firstName, out lastName))
                return String.Empty;

            return firstName;
        }

        #endregion

        #region GetUserAgentData

        private void RemoveAgentData(UUID uuid)
        {
            if (!m_isUserServer)
            {
                lock (m_agentDataByUUID)
                {
                    TimestampedItem<UserAgentData> item;
                    if (m_agentDataByUUID.TryGetValue(uuid, out item))
                    {
                        m_agentDataByUUID.Remove(uuid);
                    }
                }
            }
        }

        private void ReplaceAgentData(UserAgentData agent)
        {
            if (!m_isUserServer)
            {
                lock (m_agentDataByUUID)
                {
                    TimestampedItem<UserAgentData> item;
                    if (m_agentDataByUUID.TryGetValue(agent.ProfileID, out item))
                    {
                        m_agentDataByUUID.Remove(agent.ProfileID);
                    }
                    m_agentDataByUUID.Add(agent.ProfileID, new TimestampedItem<UserAgentData>(agent));
                }
            }
        }

        /// <summary>
        /// Loads a user agent by uuid (not called directly)
        /// </summary>
        /// <param name="uuid">The agent's UUID</param>
        /// <returns>Agent profiles</returns>
        public UserAgentData GetUserAgent(UUID uuid, bool forceRefresh)
        {
            if ((!m_isUserServer) && (!forceRefresh))   // don't do this if User or forced
            {
                lock (m_agentDataByUUID)
                {
                    TimestampedItem<UserAgentData> item;
                    if (m_agentDataByUUID.TryGetValue(uuid, out item))
                    {
                        if ((item.ElapsedSeconds < CACHE_ITEM_EXPIRY) && !forceRefresh)
                        {
                            UserAgentData agentData = item.Item;
                            // The profile may have been initialized when the user was not logged in, if the cache was warmed by EO or owners of prims in the region.
                            if (agentData.ProfileID != UUID.Zero)   // agentData is initialized?
                            {
                                // m_log.WarnFormat("[PROFILE]: AgentData cached: {0} at {1} {2}", agentData.ProfileID, Util.RegionHandleToLocationString(agentData.Handle), agentData.Position);
                                return agentData;
                            }
                        }

                        // Else cache expired, or forcing a refresh.
                        // m_log.WarnFormat("[PROFILE]: AgentData cache expired or forced: {0} at {1} {2}", item.Item.ProfileID, Util.RegionHandleToLocationString(item.Item.Handle), item.Item.Position);
                    }
                }
            }

            UserAgentData agent = m_storage.GetAgentData(uuid);
            if (agent != null)
            {
                // m_log.WarnFormat("[PROFILE]: Updating AgentData: {0} at {1} {2}", agent.ProfileID, Util.RegionHandleToLocationString(agent.Handle), agent.Position);
                ReplaceAgentData(agent);
            }
            else
            {
                // m_log.WarnFormat("[PROFILE]: Removing AgentData for {0}", uuid);
                RemoveAgentData(uuid);
            }

            return agent;
        }
        public UserAgentData GetUserAgent(UUID uuid)
        {
            return GetUserAgent(uuid, false);
        }

        #endregion

        #region CachedUserInfo

        /// <summary>
        /// Upgrade a UserProfileData to a CachedUserInfo.
        /// </summary>
        /// <param name="userProfile"></param>
        /// <param name="friends">friends can be null when called from the User grid server itself.</param>
        /// <returns></returns>
        protected CachedUserInfo NewCachedUserInfo(UserProfileData userProfile, List<FriendListItem> friends)
        {
//            if ((friends == null) && (m_commsManager.UserService != null))
//                friends = m_commsManager.UserService.GetUserFriendList(userProfile.ID);
            return new CachedUserInfo(m_commsManager, userProfile, friends);
        }

        public void FlushCachedInfo(UUID uuid)
        {
            lock (m_userDataLock)
            {
                UserProfileData profile;
                if (m_localUser.TryGetValue(uuid, out profile))
                {
                    if (m_userInfoByName.ContainsKey(profile.Name))
                        m_userInfoByName.Remove(profile.Name);
                }
            }
            lock (m_userInfoLock)
            {
                TimestampedItem<CachedUserInfo> item;
                if (m_userInfoByUUID.TryGetValue(uuid, out item))
                {
                    if (m_userInfoByName.ContainsKey(item.Item.UserProfile.Name))
                        m_userInfoByName.Remove(item.Item.UserProfile.Name);
                    m_userInfoByUUID.Remove(uuid);
                }
            }
        }

        private void ReplaceUserInfo(CachedUserInfo userInfo)
        {
            UUID uuid = userInfo.UserProfile.ID;

            lock (m_userInfoLock)
            {
                // Let's handle the UUID caches first, then name cache is common between them.
                if (m_userInfoByUUID.Contains(uuid))
                    m_userInfoByUUID.Remove(uuid);
                m_userInfoByUUID.Add(uuid, new TimestampedItem<CachedUserInfo>(userInfo));

                // Now do the name cache.
                if (m_userInfoByName.ContainsKey(userInfo.UserProfile.Name))
                    m_userInfoByName.Remove(userInfo.UserProfile.Name);
                m_userInfoByName.Add(userInfo.UserProfile.Name, userInfo);
            }
        }

        /// <summary>
        /// Populate caches with the given cached user profile
        /// </summary>
        /// <param name="userInfo"></param>
        protected void AddToUserInfoCache(CachedUserInfo userInfo)
        {
            lock (m_userInfoLock)
            {
                UUID uuid = userInfo.UserProfile.ID;
                // Add or update the regular profiles data.
                if (m_userInfoByUUID.Contains(uuid))
                    m_userInfoByUUID.Remove(uuid);
                m_userInfoByUUID.Add(uuid, new TimestampedItem<CachedUserInfo>(userInfo));
                m_userInfoByName[userInfo.UserProfile.Name] = userInfo;
            }
        }

        /// <summary>
        /// Populate caches with the given user profile (allocate userInfo).
        /// </summary>
        /// <param name="profile"></param>
        protected CachedUserInfo AddToUserInfoCache(UserProfileData profile)
        {
            CachedUserInfo userInfo = NewCachedUserInfo(profile, null);

            AddToUserInfoCache(userInfo);
            return userInfo;
        }

        // This is called when a call to ADD a profile to _cachedProfileData caused
        // an automatic purge (e.g. when at capacity limit). Use this to keep the
        // parallel data structures (in this case m_userProfilesByName) in sync.
        void _cachedUserInfo_OnItemPurged(TimestampedItem<CachedUserInfo> item)
        {
            // this is called when it's removed from _cachedProfileData, 
            // so we just need to clean up the parallel name dictionary.
            lock (m_userInfoLock)
            {
                // normal name cleanup of a regular cached profile that expired.
                if (m_userInfoByName.ContainsKey(item.Item.UserProfile.Name))
                    m_userInfoByName.Remove(item.Item.UserProfile.Name);
            }
        }

        /// <summary>
        /// Remove profile belong to the given uuid from the user profile caches.
        /// Does not remove from the user agent cache (_cachedAgentData).
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>true if there was a profile to remove, false otherwise</returns>
        protected bool RemoveFromUserInfoCache(UUID uuid)
        {
            lock (m_userInfoLock)
            {
                TimestampedItem<CachedUserInfo> timedItem = null;
                if (m_userInfoByUUID.TryGetValue(uuid, out timedItem))
                {
                    m_userInfoByUUID.Remove(uuid);
                    m_userInfoByName.Remove(timedItem.Item.UserProfile.Name);
                    return true;
                }
            }

            return false;
        }

        public virtual bool UpdateUserProfile(UserProfileData profile)
        {
            ReplaceUserData(profile);
            return m_storage.UpdateUserProfileData(profile);
        }

        public virtual CachedUserInfo GetUserInfo(UUID uuid)
        {
            if (uuid == UUID.Zero)
                return null;

            lock (m_userInfoLock)
            {
                TimestampedItem<CachedUserInfo> timedItem;
                if (m_userInfoByUUID.TryGetValue(uuid, out timedItem))
                {
                    if (timedItem.ElapsedSeconds < CACHE_ITEM_EXPIRY)
                        return timedItem.Item;
                }
            }

            // Need to update UserAgentData. Also check if UserProfile needs an update.
            UserProfileData profile = GetUserProfile(uuid, false);
            if (profile == null)
            {
                FlushCachedInfo(uuid);
                return null;
            }

            profile.CurrentAgent = GetUserAgent(uuid, true);
            return AddToUserInfoCache(profile);
        }

        #endregion

        #region TempUserProfiles

        /// <summary>
        /// Temporary profiles are used for bot users, they have no persistence.
        /// </summary>
        /// <param name="userProfile">the bot user profile</param>
        public virtual void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            AddToUserInfoCache(userProfile);

            lock (m_userDataLock)
            {
                if (m_tempDataByUUID.ContainsKey(userProfile.ID))
                    m_tempDataByUUID.Remove(userProfile.ID);
                m_tempDataByUUID.Add(userProfile.ID, userProfile);

                string dictName = DictName(userProfile.Name);
                RemoveName(dictName);
                m_userByName.Add(dictName, userProfile.ID);
            }
        }

        public virtual void RemoveTemporaryUserProfile(UUID uuid)
        {
            RemoveFromUserInfoCache(uuid);

            lock (m_userDataLock)
            {
                if (m_tempDataByUUID.ContainsKey(uuid))
                {
                    string name = m_tempDataByUUID[uuid].Name;
                    RemoveName(name);
                    m_tempDataByUUID.Remove(uuid);
                }
            }
        }
        #endregion

        #region MiscInterfaces

        public void LogoutUsers(UUID regionID)
        {
            m_storage.LogoutUsers(regionID);
        }

        public void ResetAttachments(UUID uuid)
        {
            m_storage.ResetAttachments(uuid);
        }

        /*
        public virtual UserInterestsData GetUserInterests(UUID uuid)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserInterestsData interests = plugin.GetUserInterests(uuid);

                return interests;
            }
            return null;
        }
        */

        public virtual List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            return m_storage.GenerateAgentPickerRequestResponse(queryID, query);
        }
        
        /*
        public virtual bool UpdateUserInterests(UserInterestsData data)
        {
            return plugin.UpdateUserInterests(data);
        }
        */
        #endregion

        public virtual List<FriendListItem> GetUserFriendList(UUID ownerID)
        {
            return m_storage.GetUserFriendList(ownerID);
        }

        public virtual Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            return m_storage.GetFriendRegionInfos(uuids);
        }
        
        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey)
        {
            m_storage.StoreWebLoginKey(agentID, webLoginKey);
        }

        public virtual void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            m_storage.AddNewUserFriend(friendlistowner, friend, perms);

            CachedUserInfo userInfo = GetUserInfo(friend);
            if (userInfo != null) userInfo.AdjustPermissionsFromFriend(friendlistowner, perms);
        }

        public virtual void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            m_storage.RemoveUserFriend(friendlistowner, friend);

            CachedUserInfo userInfo = GetUserInfo(friend);
            if (userInfo != null) userInfo.RemoveFromFriendsCache(friendlistowner);
        }

        public virtual void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            if (friendlistowner == UUID.Zero) return;
            m_storage.UpdateUserFriendPerms(friendlistowner, friend, perms);

            CachedUserInfo userInfo = GetUserInfo(friend);
            if (userInfo != null) userInfo.AdjustPermissionsFromFriend(friendlistowner, perms);
        }

        /// <summary>
        /// Check if this user can access another's items.
        /// </summary>
        /// <param name="friendlistowner">This user (to check).</param>
        /// <param name="friendId">The ID of the other user (friend owner of the items).</param>
        /// <param name="permissionMask">Desired permission.</param>
        /// <param name="noFetch">If true, don't make any net/storage calls. Memory only.</param>
        /// <returns>true if permission is available</returns>
        public bool UserHasFriendPerms(UUID requestingFriend, UUID objectOwner, uint permissionMask, bool noFetch)
        {
            CachedUserInfo userInfo = null;

            if (noFetch)
            {
                // Can be called this way on crossings to prevent lookups.
                TimestampedItem<CachedUserInfo> item;
                lock (m_userInfoLock)
                {
                    // Ignore timeouts etc if this is a noFetch/fastCheck call.
                    if (!m_userInfoByUUID.TryGetValue(requestingFriend, out item))
                        return false;   // user will need to repeat the operation not in a crossing.
                }
                userInfo = item.Item;
            } else {
                userInfo = GetUserInfo(requestingFriend);
            }

            if (userInfo == null)
                return false;

            return userInfo.HasPermissionFromFriend(objectOwner, permissionMask);
        }

        /// <summary>
        /// Resets the currentAgent in the user profile
        /// </summary>
        /// <param name="agentID">The agent's ID</param>
        public virtual void ClearUserAgent(UUID agentID)
        {
            UserProfileData profile = GetUserProfile(agentID, false);
            if (profile == null)
                return;

            profile.CurrentAgent = null;
            UpdateUserProfile(profile);
        }

        #region CreateAgent

        /// <summary>
        /// Creates and initializes a new user agent - make sure to use CommitAgent when done to submit to the DB
        /// This method is only ever invoked by the User server (not regions).
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="request">The users loginrequest</param>
        public void CreateAgent(UserProfileData profile, XmlRpcRequest request)
        {
            // m_log.DebugFormat("[USER CACHE]: Creating agent {0} {1}", profile.Name, profile.ID);
            
            UserAgentData agent = new UserAgentData();

            // User connection
            agent.AgentOnline = true;

            if (request.Params.Count > 1)
            {
                if (request.Params[1] != null)
                {
                    IPEndPoint RemoteIPEndPoint = (IPEndPoint)request.Params[1];
                    agent.AgentIP = RemoteIPEndPoint.Address.ToString();
                    agent.AgentPort = (uint)RemoteIPEndPoint.Port;
                }
            }

            // Generate sessions
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];
            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            agent.SecureSessionID = new UUID(randDataSS, 0);
            agent.SessionID = new UUID(randDataS, 0);

            // Profile UUID
            agent.ProfileID = profile.ID;

            // Current location/position/alignment
            if (profile.CurrentAgent != null)
            {
                m_log.InfoFormat("[USER CACHE]: Creating agent {0} {1} at {2} {3} was {4} {5}", profile.Name, profile.ID, Util.RegionHandleToLocationString(profile.CurrentAgent.Handle), profile.CurrentAgent.Position, Util.RegionHandleToLocationString(agent.Handle), agent.Position);
                agent.Region = profile.CurrentAgent.Region;
                agent.Handle = profile.CurrentAgent.Handle;
                agent.Position = profile.CurrentAgent.Position;
                agent.LookAt = profile.CurrentAgent.LookAt;
            }
            else
            {
                m_log.InfoFormat("[USER CACHE]: Creating agent {0} {1} at HOME {2} {3} was {4} {5}", profile.Name, profile.ID, Util.RegionHandleToLocationString(profile.HomeRegion), profile.HomeLocation, Util.RegionHandleToLocationString(agent.Handle), agent.Position);
                agent.Region = profile.HomeRegionID;
                agent.Handle = profile.HomeRegion;
                agent.Position = profile.HomeLocation;
                agent.LookAt = profile.HomeLookAt;
            }

            // What time did the user login?
            agent.LoginTime = Util.UnixTimeSinceEpoch();
            agent.LogoutTime = 0;

            // if (m_isUserServer)
            //     m_log.WarnFormat("[USER CACHE]: Creating new agent data for {0} SSID={1} at {2} {3}", agent.ProfileID, agent.SecureSessionID, Util.RegionHandleToLocationString(agent.Handle), agent.Position); 

            profile.CurrentAgent = agent;
        }

        // This method is only ever invoked by the User server (not regions).
        public void CreateAgent(UserProfileData profile, OSD request)
        {
            //m_log.DebugFormat("[USER CACHE]: Creating agent {0} {1}", profile.Name, profile.ID);
            
            UserAgentData agent = new UserAgentData();

            // User connection
            agent.AgentOnline = true;

            //if (request.Params.Count > 1)
            //{
            //    IPEndPoint RemoteIPEndPoint = (IPEndPoint)request.Params[1];
            //    agent.AgentIP = RemoteIPEndPoint.Address.ToString();
            //    agent.AgentPort = (uint)RemoteIPEndPoint.Port;
            //}

            // Generate sessions
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];
            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            agent.SecureSessionID = new UUID(randDataSS, 0);
            agent.SessionID = new UUID(randDataS, 0);

            // Profile UUID
            agent.ProfileID = profile.ID;

            // Current location/position/alignment
            if (profile.CurrentAgent != null)
            {
                agent.Region = profile.CurrentAgent.Region;
                agent.Handle = profile.CurrentAgent.Handle;
                agent.Position = profile.CurrentAgent.Position;
                agent.LookAt = profile.CurrentAgent.LookAt;
            }
            else
            {
                agent.Region = profile.HomeRegionID;
                agent.Handle = profile.HomeRegion;
                agent.Position = profile.HomeLocation;
                agent.LookAt = profile.HomeLookAt;
            }

            // What time did the user login?
            agent.LoginTime = Util.UnixTimeSinceEpoch();
            agent.LogoutTime = 0;

            profile.CurrentAgent = agent;
        }

        /// <summary>
        /// Saves a target agent to the database
        /// This method is only ever invoked by the User server (not regions).
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
            // if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: CommitAgent: {0} SSID={1} at {2} {3}", profile.ID, profile.CurrentAgent.SecureSessionID, Util.RegionHandleToLocationString(profile.CurrentAgent.Handle), profile.CurrentAgent.Position);

            // TODO: how is this function different from setUserProfile?  -> Add AddUserAgent() here and commit both tables "users" and "agents"
            // TODO: what is the logic should be?
            bool ret = false;
            ret = AddUserAgent(profile.CurrentAgent);
            ret = ret & UpdateUserProfile(profile);
            return ret;
        }

        /// <summary>
        /// Process a user logoff from OpenSim.
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="position"></param>
        /// <param name="lookat"></param>
        public virtual void LogOffUser(UUID uuid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            if (StatsManager.UserStats != null)
                StatsManager.UserStats.AddLogout();

            UserProfileData userProfile = GetUserProfile(uuid, true);

            if (userProfile != null)
            {
                UserAgentData userAgent = userProfile.CurrentAgent;
                if (userAgent != null)
                {
                    userAgent.AgentOnline = false;
                    userAgent.LogoutTime = Util.UnixTimeSinceEpoch();
                    //userAgent.sessionID = UUID.Zero;
                    if (regionid != UUID.Zero)
                    {
                        userAgent.Region = regionid;
                    }
                    m_log.WarnFormat("[LOGOFF]: User {0} at {1} {2} was at {3} {4}", userAgent.ProfileID, Util.RegionHandleToLocationString(userAgent.Handle), userAgent.Position, regionhandle, position);
                    userAgent.Handle = regionhandle;
                    userAgent.Position = position;
                    userAgent.LookAt = lookat;
                    //userProfile.CurrentAgent = userAgent;
                    userProfile.LastLogin = userAgent.LogoutTime;

                    CommitAgent(ref userProfile);
                }
                else
                {
                    // If currentagent is null, we can't reference it here or the UserServer crashes!
                    m_log.Warn("[LOGOUT]: didn't save logout position: " + uuid.ToString());
                }
            }
            else
            {
                m_log.Warn("[LOGOUT]: Unknown User logged out");
            }
        }

        public void LogOffUser(UUID uuid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            LogOffUser(uuid, regionid, regionhandle, new Vector3(posx, posy, posz), new Vector3());
        }

        #endregion

        /// <summary>
        /// Add a new user
        /// </summary>
        /// <param name="firstName">first name</param>
        /// <param name="lastName">last name</param>
        /// <param name="password">password</param>
        /// <param name="email">email</param>
        /// <param name="regX">location X</param>
        /// <param name="regY">location Y</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        public virtual UUID AddUser(string firstName, string lastName, string password, string email, uint regX, uint regY)
        {
            return AddUser(firstName, lastName, password, email, regX, regY, UUID.Random());
        }

        /// <summary>
        /// Add a new user
        /// </summary>
        /// <param name="firstName">first name</param>
        /// <param name="lastName">last name</param>
        /// <param name="password">password</param>
        /// <param name="email">email</param>
        /// <param name="regX">location X</param>
        /// <param name="regY">location Y</param>
        /// <param name="uuid">UUID of avatar.</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        public virtual UUID AddUser(string firstName, string lastName, string password, string email, uint regX, uint regY, UUID uuid)
        {
            string salt = Util.RandomString(32);
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + salt);

            UserProfileData userProf = GetUserProfile(firstName, lastName);
            if (userProf != null)
            {
                m_log.Error("[USERSTORAGE]: Not creating user. User already exists ");
                return UUID.Zero;
            }

            UserProfileData user = new UserProfileData();
            user.HomeLocation = new Vector3(128, 128, 100);
            user.ID = uuid;
            user.FirstName = firstName;
            user.SurName = lastName;
            user.PasswordHash = md5PasswdHash;
            user.PasswordSalt = salt;
            user.Created = Util.UnixTimeSinceEpoch();
            user.HomeLookAt = new Vector3(100, 100, 100);
            user.HomeRegionX = regX;
            user.HomeRegionY = regY;
            user.Email = email;

            m_storage.AddUser(user);

            userProf = GetUserProfile(uuid);
            if (userProf == null)
            {
                return UUID.Zero;
            }
            else
            {
                CreateInventorySkel(userProf);
                return userProf.ID;
            }
        }

        private void CreateInventorySkel(UserProfileData userProf)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            IInventoryStorage storage = inventorySelect.GetProvider(userProf.ID);

            InventoryFolderBase folder = new InventoryFolderBase();

            UUID user = userProf.ID;

            folder.ParentID = UUID.Zero;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "My Inventory";
            folder.Type = (short)AssetType.Folder;
            folder.Version = 1;
            storage.CreateFolder(folder);

            UUID rootFolder = folder.ID;

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Animations";
            folder.Type = (short)AssetType.Animation;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Body Parts";
            folder.Type = (short)AssetType.Bodypart;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Calling Cards";
            folder.Type = (short)AssetType.CallingCard;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Clothing";
            folder.Type = (short)AssetType.Clothing;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Gestures";
            folder.Type = (short)AssetType.Gesture;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Landmarks";
            folder.Type = (short)AssetType.Landmark;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Lost And Found";
            folder.Type = (short)FolderType.LostAndFound;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Notecards";
            folder.Type = (short)AssetType.Notecard;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Objects";
            folder.Type = (short)AssetType.Object;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Photo Album";
            folder.Type = (short)FolderType.Snapshot;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Scripts";
            folder.Type = (short)AssetType.LSLText;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Sounds";
            folder.Type = (short)AssetType.Sound;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Textures";
            folder.Type = (short)AssetType.Texture;
            folder.Version = 1;
            storage.CreateFolder(folder);

            folder = new InventoryFolderBase();
            folder.ParentID = rootFolder;
            folder.Owner = user;
            folder.ID = UUID.Random();
            folder.Name = "Trash";
            folder.Type = (short)FolderType.Trash;
            folder.Version = 1;
            storage.CreateFolder(folder);
        }

        /// <summary>
        /// Reset a user password.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="newPassword"></param>
        /// <returns>true if the update was successful, false otherwise</returns>
        public virtual bool ResetUserPassword(string firstName, string lastName, string newPassword)
        {
            string salt = Util.RandomString(32);
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(newPassword) + ":" + salt);

            UserProfileData profile = GetUserProfile(firstName, lastName);

            if (null == profile)
            {
                m_log.ErrorFormat("[USER CACHE]: Could not find user {0} {1}", firstName, lastName);
                return false;
            }

            profile.PasswordHash = md5PasswdHash;
            profile.PasswordSalt = salt;

            UpdateUserProfile(profile);

            return true;
        }

        public abstract UserProfileData SetupMasterUser(string firstName, string lastName);
        public abstract UserProfileData SetupMasterUser(string firstName, string lastName, string password);
        public abstract UserProfileData SetupMasterUser(UUID uuid);

        /// <summary>
        /// Add an agent using data plugins.
        /// </summary>
        /// <param name="agent">The agent data to be added</param>
        /// <returns>
        /// true if at least one plugin added the user agent.  false if no plugin successfully added the agent
        /// </returns>
        public virtual bool AddUserAgent(UserAgentData agent)
        {
            if (!m_isUserServer)
            {
                lock (m_agentDataByUUID)
                {
                    UUID uuid = agent.ProfileID;

                    TimestampedItem<UserAgentData> timedItem = null;
                    if (m_agentDataByUUID.TryGetValue(uuid, out timedItem))
                        m_agentDataByUUID.Remove(uuid);

                    // Must also add the updated record to the cache, inside the lock, or risk
                    // a race condition with other threads refreshing the cache from the database 
                    // before plugin.AddNewUserAgent below has finished committing the change.
                    m_agentDataByUUID.Add(uuid, new TimestampedItem<UserAgentData>(agent));
                }
            }

            return m_storage.AddNewUserAgent(agent);
        }

        /// <summary>
        /// Get avatar appearance information
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetUserAppearance(UUID uuid)
        {
            return m_storage.GetUserAppearance(uuid);
        }

        /// <summary>
        /// Get bot outfit
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetBotOutfit(UUID uuid, string outfitName)
        {
            return m_storage.GetBotOutfit(uuid, outfitName);
        }

        public virtual void UpdateUserAppearance(UUID uuid, AvatarAppearance appearance)
        {
            m_storage.UpdateUserAppearance(uuid, appearance);
        }

        public virtual void AddOrUpdateBotOutfit(UUID uuid, string outfitName, AvatarAppearance appearance)
        {
            m_storage.AddOrUpdateBotOutfit(uuid, outfitName, appearance);
        }

        public virtual void RemoveBotOutfit(UUID uuid, string outfitName)
        {
            m_storage.RemoveBotOutfit(uuid, outfitName);
        }

        public virtual List<string> GetBotOutfitsByOwner(UUID uuid)
        {
            return m_storage.GetBotOutfitsByOwner(uuid);
        }

        #region IAuthentication

        protected Dictionary<UUID, List<string>> m_userKeys = new Dictionary<UUID, List<string>>();

        /// <summary>
        /// This generates authorization keys in the form
        /// http://userserver/uuid
        /// after verifying that the caller is, indeed, authorized to request a key
        /// </summary>
        /// <param name="url">URL of the user server</param>
        /// <param name="userID">The user ID requesting the new key</param>
        /// <param name="authToken">The original authorization token for that user, obtained during login</param>
        /// <returns></returns>
        public string GetNewKey(string url, UUID userID, UUID authToken)
        {
            UserProfileData profile = GetUserProfile(userID);
            string newKey = String.Empty;
            if (!url.EndsWith("/"))
                url = url + "/";

            if (profile != null)
            {
                // I'm overloading webloginkey for this, so that no changes are needed in the DB
                // The uses of webloginkey are fairly mutually exclusive
                
                if (profile.WebLoginKey.Equals(authToken))
                {
                    newKey = UUID.Random().ToString();
                    List<string> keys;
                    lock (m_userKeys)
                    {
                        if (m_userKeys.ContainsKey(userID))
                        {
                            keys = m_userKeys[userID];
                        }
                        else
                        {
                            keys = new List<string>();
                            m_userKeys.Add(userID, keys);
                        }
                        keys.Add(newKey);
                    }
                    m_log.InfoFormat("[USER AUTH]: Successfully generated new auth key for user {0}", userID);
                }
                else
                    m_log.Warn("[USER AUTH]: Unauthorized key generation request. Denying new key.");
                 
            }
            else
                m_log.Warn("[USER AUTH]: User not found.");

            return url + newKey;
        }

        /// <summary>
        /// This verifies the uuid portion of the key given out by GenerateKey
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool VerifyKey(UUID userID, string key)
        {
            lock (m_userKeys)
            {
                if (m_userKeys.ContainsKey(userID))
                {
                    List<string> keys = m_userKeys[userID];
                    if (keys.Contains(key))
                    {
                        // Keys are one-time only, so remove it
                        keys.Remove(key);
                        return true;
                    }
                    return false;
                }
                else
                    return false;
            }
        }
        
        public virtual bool VerifySession(UUID userID, UUID sessionID)
        {
            UserProfileData userProfile = GetUserProfile(userID);

            if (userProfile != null && userProfile.CurrentAgent != null)
            {
                m_log.DebugFormat("[USER AUTH]: Verifying session {0} for {1}; current  session {2}", sessionID, userID, userProfile.CurrentAgent.SessionID);
                if (userProfile.CurrentAgent != null)
                {
                    if (userProfile.CurrentAgent.SessionID == sessionID)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        #endregion

        #region IUserService Members


        public void SaveUserPreferences(UserPreferencesData userPrefs)
        {
            m_storage.SaveUserPreferences(userPrefs);
        }

        public UserPreferencesData RetrieveUserPreferences(UUID uuid)
        {
            return m_storage.RetrieveUserPreferences(uuid);
        }

        #endregion

        #region IAvatarService - Cached baked textures

        public virtual List<CachedAgentArgs> GetCachedBakedTextures(List<CachedAgentArgs> request)
        {
            return m_storage.GetCachedBakedTextures(request);
        }

        public virtual void SetCachedBakedTextures(Dictionary<UUID, UUID> request)
        {
            m_storage.SetCachedBakedTextures(request);
        }

        #endregion

        #region - Level 2 user profile cache

        public CachedUserInfo GetUserDetails(string fname, string lname)
        {
            string name = fname.Trim() + " " + lname.Trim();

            lock (m_userInfoLock)
            {
                if (m_userInfoByName.ContainsKey(name))
                {
                    return m_userInfoByName[name];
                }
            }

            UserProfileData userProfile = GetUserProfile(fname, lname);
            if (userProfile != null)
            {
                return AddToUserInfoCache(userProfile);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Get details of the given user.  This is the old CachedUserInfo interface.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.  
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(UUID uuid)
        {
            return GetUserInfo(uuid);
        }

        /// <summary>
        /// Preloads User data into the region cache. Modules may use this service to add non-standard clients
        /// </summary>
        /// <param name="userData"></param>
        public void PreloadUserCache(UserProfileData userData)
        {
            AddToUserInfoCache(userData);
        }

        public void UpdateFriendPerms(UUID uuid, UUID friendID, uint perms)
        {
            //if the friend is here we need to change their permissions for the given user
            CachedUserInfo cachedUserDetails = this.GetUserDetails(friendID);
            if (cachedUserDetails != null)
            {
                cachedUserDetails.AdjustPermissionsFromFriend(uuid, perms);
            }
        }

        /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// 
        /// It isn't strictly necessary to make this call since user data can be lazily requested later on.  However, 
        /// it might be helpful in order to avoid an initial response delay later on
        /// 
        /// <param name="uuid"></param>
        public void CacheUser(UUID uuid)
        {
            if (uuid == UUID.Zero)
                return;

            //m_log.DebugFormat("[USER CACHE]: Adding user profile for {0}", userID);
            GetUserDetails(uuid);
        }

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool UncacheUser(UUID uuid)
        {
            return RemoveFromUserInfoCache(uuid);
        }

        // Add and remove of local user profiles is more a migration from one list to the other, if already available.
        public void MakeLocalUser(UUID uuid)
        {
            // Because profile changes can be made outside of the region the user is in (e.g. partnering), 
            // we'll provide a way for users to force a profile refetch to the current region.
            // We'll for an refresh of the user's profile when they enter or leave a region.

            UserProfileData profile = null;

            lock (m_userDataLock)
            {
                if (m_localUser.ContainsKey(uuid))
                    return; // nothing to do

                profile = TryGetUserProfile(uuid, true);

                // This is the same code as below but is included here so that it's inside one atomic run with the lock.
                if (profile != null)
                {
                    RemoveUserData(uuid);

                    m_localUser[uuid] = profile;
                    m_userByName[DictName(profile.Name)] = profile.ID;
                }
            }
            if (profile != null)
            {
                // we're all done here
                // m_log.DebugFormat("[USER CACHE]: Converted cache profile to local user for: {0} {1}", uuid, profile.Name);
                return;
            }

            // Wasn't already in the cache somewhere, fetch and add it.
            profile = GetUserProfile(uuid, false);

            // Now deal with the cached local profile storage.
            lock (m_userInfoLock)
            {
                if (profile == null)
                {
                    m_log.WarnFormat("[USER CACHE]: Could not fetch profile for: {0}", uuid);
                    return;
                }

                RemoveUserData(uuid);

                m_localUser[uuid] = profile;
                m_userByName[DictName(profile.Name)] = profile.ID;

            }

            // m_log.DebugFormat("[USER CACHE]: Added profile to local user cache for: {0} {1}", uuid, profile.Name);
        }

        public void UnmakeLocalUser(UUID uuid)
        {
            // Because profile changes can be made outside of the region the user is in (e.g. partnering), 
            // we'll provide a way for users to force a profile refetch to the current region.
            // We'll for an refresh of the user's profile when they enter or leave a region.

            UserProfileData profile = null;

            lock (m_userDataLock)
            {
                if (!m_localUser.ContainsKey(uuid))
                    return; // nothing to do

                profile = TryGetUserProfile(uuid, true);
                if (profile != null)
                {
                    RemoveUserData(uuid);
                    ReplaceUserData(profile);
                }
            }

            // if (profile != null) m_log.DebugFormat("[USER CACHE]: Converted cache profile from local user to normal user for: {0} {1}", uuid, profile.Name);
        }

        #endregion
    }
}
