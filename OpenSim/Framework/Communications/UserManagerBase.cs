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

//#define DUMP_STATUS

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Data;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Base class for user management (create, read, etc)
    /// </summary>
    public abstract class UserManagerBase : IUserService, IUserAdminService, IAvatarService, IMessagingService, IAuthentication
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private bool m_isUserServer = false;

        /// <value>
        /// List of plugins to search for user data
        /// </value>
        private List<IUserDataPlugin> m_plugins = new List<IUserDataPlugin>();

        protected CommunicationsManager m_commsManager;

        /// <summary>
        /// Maximum size for the profile and agent caches
        /// </summary>
        private const int MAX_CACHE_SIZE = 250;

        /// <summary>
        /// Number of seconds before an item in the cache is no longer considered viable
        /// </summary>
        private const int CACHE_ITEM_EXPIRY = 300;  // seconds, 5 minutes

        /// <summary>
        /// LRU cache for agent data
        /// </summary>
        private LRUCache<UUID, TimestampedItem<UserAgentData>> _cachedAgentData
            = new LRUCache<UUID, TimestampedItem<UserAgentData>>(MAX_CACHE_SIZE);

        /// <summary>
        /// LRU cache for profile data
        /// </summary>
        private LRUCache<UUID, TimestampedItem<CachedUserInfo>> _cachedProfileData
            = new LRUCache<UUID, TimestampedItem<CachedUserInfo>>(MAX_CACHE_SIZE);

        /// <summary>
        /// User profiles indexed by name
        /// This will be kept in sync with either _cachedLocalProfiles or _cachedProfileData
        /// </summary>
        private readonly Dictionary<string, CachedUserInfo> m_userProfilesByName
            = new Dictionary<string, CachedUserInfo>();

        /// <summary>
        /// Special cache for profile data for local users in the region
        /// </summary>
        private Dictionary<UUID, CachedUserInfo> _cachedLocalProfiles
            = new Dictionary<UUID, CachedUserInfo>();

        /// <summary>
        /// Controls access to _cachedProfileData, _cachedLocalProfiles and m_userProfilesByName
        /// </summary>
        protected internal object _userProfilesLock = new object();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        public UserManagerBase(CommunicationsManager commsManager)
        {
            m_commsManager = commsManager;
            _cachedProfileData.OnItemPurged += _cachedProfileData_OnItemPurged;

            m_isUserServer = System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("OpenSim.Grid.UserServer");
        }

        /// <summary>
        /// Add a new user data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="plugin">The plugin that will provide user data</param>
        public void AddPlugin(IUserDataPlugin plugin)
        {
            m_plugins.Add(plugin);
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
            m_plugins.AddRange(DataPluginFactory.LoadDataPlugins<IUserDataPlugin>(provider, connect));
        }

        #region UserProfile

        private void DumpStatus(string context)
        {
#if DUMP_STATUS
            m_log.WarnFormat("[USER CACHE]: {0}: names={1} normal={2} local={3}", context, m_userProfilesByName.Count, _cachedProfileData.Count, _cachedLocalProfiles.Count);
#endif
        }

        // Fetches a profile from the db via XMLRPC
        private UserProfileData _GetUserProfile(string fname, string lname)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserProfileData profile = plugin.GetUserByName(fname, lname);

                if (profile != null)
                {
					// We're forcing an update above, of the profile via the plugin, 
					// need to also force an update of the current agent data.
                    profile.CurrentAgent = GetUserAgent(profile.ID, true);
                    return profile;
                }
            }

            return null;
        }
        public virtual UserProfileData GetUserProfile(string fname, string lname)
        {
            return _GetUserProfile(fname, lname);
        }

        public UserProfileData GetUserProfile(Uri uri)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserProfileData profile = plugin.GetUserByUri(uri);

                if (null != profile)
                    return profile;
            }

            return null;
        }

        public virtual UserAgentData GetAgentByUUID(UUID userId)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserAgentData agent = plugin.GetAgentByUUID(userId);

                if (agent != null)
                {
                    return agent;
                }
            }

            return null;
        }

        public Uri GetUserUri(UserProfileData userProfile)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Upgrade a UserProfileData to a CachedUserInfo for common cache storage.
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

        /// <summary>
        /// Populate caches with the given cached user profile
        /// </summary>
        /// <param name="userProfile"></param>
        protected void AddToProfileCache(CachedUserInfo userInfo)
        {
            DumpStatus("AddToProfileCache(entry)");
            lock (_userProfilesLock)
            {
                UUID uuid = userInfo.UserProfile.ID;
                if (_cachedLocalProfiles.ContainsKey(uuid))
                {
                    // Already cached as a local profile, force an update
                    _cachedLocalProfiles[uuid] = userInfo;
                    m_userProfilesByName[userInfo.UserProfile.Name] = userInfo;
                }
                else
                {
                    // Add or update the regular profiles data.
                    if (_cachedProfileData.Contains(uuid))
                        _cachedProfileData.Remove(uuid);
                    _cachedProfileData.Add(uuid, new TimestampedItem<CachedUserInfo>(userInfo));
                    m_userProfilesByName[userInfo.UserProfile.Name] = userInfo;
                }
            }
            DumpStatus("AddToProfileCache(exit)");
        }

        /// <summary>
        /// Populate caches with the given user profile (allocate userInfo).
        /// </summary>
        /// <param name="userProfile"></param>
        protected CachedUserInfo AddToProfileCache(UserProfileData userProfile)
        {
            CachedUserInfo userInfo = NewCachedUserInfo(userProfile, null);

            AddToProfileCache(userInfo);
            return userInfo;
        }

        // This is called when a call to ADD a profile to _cachedProfileData caused
        // an automatic purge (e.g. when at capacity limit). Use this to keep the
        // parallel data structures (in this case m_userProfilesByName) in sync.
        void _cachedProfileData_OnItemPurged(TimestampedItem<CachedUserInfo> item)
        {
            // this is called when it's removed from _cachedProfileData, 
            // so we just need to clean up the parallel name dictionary.
            lock (_userProfilesLock)
            {
                // Don't remove it from the names cache if it's cached as a LOCAL profile.
                if (!_cachedLocalProfiles.ContainsKey(item.Item.UserProfile.ID))
                {
                    // normal name cleanup of a regular cached profile that expired.
                    if (m_userProfilesByName.ContainsKey(item.Item.UserProfile.Name))
                        m_userProfilesByName.Remove(item.Item.UserProfile.Name);
                }
            }
        }

        /// <summary>
        /// Remove profile belong to the given uuid from the user profile caches.
        /// Does not remove from the user agent cache (_cachedAgentData).
        /// </summary>
        /// <param name="userUuid"></param>
        /// <returns>true if there was a profile to remove, false otherwise</returns>
        protected bool RemoveFromProfileCache(UUID userId)
        {
            lock (_userProfilesLock)
            {
                TimestampedItem<CachedUserInfo> timedItem = null;
                if (_cachedProfileData.TryGetValue(userId, out timedItem))
                {
                    _cachedProfileData.Remove(userId);
                    // Shouldn't be here but don't remove it from the names cache if it's cached as a LOCAL profile.
                    if (!_cachedLocalProfiles.ContainsKey(userId))
                        m_userProfilesByName.Remove(timedItem.Item.UserProfile.Name);
                    return true;
                }
            }

            return false;
        }

        // This purges from BOTH the profile and agent caches.
        public void PurgeUserFromCaches(UUID userID)
        {
            // remove from _cachedProfileData and m_userProfilesByName
            RemoveFromProfileCache(userID);

            // Now remove from _cachedAgentData too
            lock (_cachedAgentData)
            {
                TimestampedItem<UserAgentData> timedItem;
                if (_cachedAgentData.TryGetValue(userID, out timedItem))
                {
//                    if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: PurgeUserFromCaches: Purging agent data for {0} SSID={1}", userID, timedItem.Item.SecureSessionID);
                    _cachedAgentData.Remove(userID);
                }
            }
        }

        public virtual CachedUserInfo GetUserInfo(UUID uuid)
        {
            if (uuid == UUID.Zero)
                return null;

            lock (_userProfilesLock)
            {
                // Check local users first
                if (_cachedLocalProfiles.ContainsKey(uuid))
                    return _cachedLocalProfiles[uuid];

                TimestampedItem<CachedUserInfo> timedItem;
                if (_cachedProfileData.TryGetValue(uuid, out timedItem))
                {
                    if (timedItem.ElapsedSeconds < CACHE_ITEM_EXPIRY)
                    {
                        return timedItem.Item;
                    }
                    else
                    {
                        _cachedProfileData.Remove(uuid);
                    }
                }
            }

            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserProfileData profile = plugin.GetUserByUUID(uuid);

                if (null != profile)
                {
                    profile.CurrentAgent = GetUserAgent(uuid, false);
                    return AddToProfileCache(profile);
                }
            }

            return null;
        }

        // see IUserService
        public virtual UserProfileData GetUserProfile(UUID uuid)
        {
            CachedUserInfo userInfo = GetUserInfo(uuid);

            return (userInfo != null) ? userInfo.UserProfile : null;
        }

        public virtual void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            DumpStatus("AddTemporaryUserProfile(entry)"); 

            AddToProfileCache(userProfile);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.AddTemporaryUserProfile(userProfile);
            }
            DumpStatus("AddTemporaryUserProfile(exit)");
        }

        public virtual void RemoveTemporaryUserProfile(UUID userid)
        {
            DumpStatus("RemoveTemporaryUserProfile(entry)");
            RemoveFromProfileCache(userid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.RemoveTemporaryUserProfile(userid);
            }

            DumpStatus("RemoveTemporaryUserProfile(exit)");
        }

        public void LogoutUsers(UUID regionID)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.LogoutUsers(regionID);
            }
        }

        public void ResetAttachments(UUID userID)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.ResetAttachments(userID);
            }
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
            List<AvatarPickerAvatar> allPickerList = new List<AvatarPickerAvatar>();
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    List<AvatarPickerAvatar> pickerList = plugin.GeneratePickerResults(queryID, query);
                    if (pickerList != null)
                        allPickerList.AddRange(pickerList);
                }
                catch (Exception)
                {
                    m_log.Error(
                        "[USERSTORAGE]: Unable to generate AgentPickerData via  " + plugin.Name + "(" + query + ")");
                }
            }

            return allPickerList;
        }
        
        public virtual bool UpdateUserProfile(UserProfileData profile)
        {
            bool result = false;

            lock (_userProfilesLock)
            {
                _cachedProfileData.Remove(profile.ID);
            }
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserProfile(profile);
                    result = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[USERSTORAGE]: Unable to set user {0} {1} via {2}: {3}", 
                        profile.FirstName, profile.SurName, plugin.Name, e.ToString());
                }
            }
            
            return result;
        }

        /*
        public virtual bool UpdateUserInterests(UserInterestsData data)
        {
            bool result = false;

            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserInterests(data);
                    result = true;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[USERSTORAGE]: Unable to set user {0} {1} via {2}: {3}",
                        data.ID, e.ToString());
                }
            }
            return result;
        }
        */
        #endregion

        #region Get UserAgent

        /// <summary>
        /// Loads a user agent by uuid (not called directly)
        /// </summary>
        /// <param name="uuid">The agent's UUID</param>
        /// <returns>Agent profiles</returns>
        public UserAgentData GetUserAgent(UUID uuid, bool forceRefresh)
        {
            lock (_cachedAgentData)
            {
                TimestampedItem<UserAgentData> item;
                if (_cachedAgentData.TryGetValue(uuid, out item))
                {
                    if ((!forceRefresh) && (item.ElapsedSeconds < CACHE_ITEM_EXPIRY))
                    {
//                        if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: Returning cached agent data for {0} SSID={1}", uuid, item.Item.SecureSessionID);
                        return item.Item;
                    }
                    else
                    {
//                        if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: Removing (refreshing) agent data for {0} SSID={1}", uuid, item.Item.SecureSessionID);
                        _cachedAgentData.Remove(uuid);
                    }
                }
            }

            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByUUID(uuid);

                    if (result != null)
                    {
                        lock (_cachedAgentData)
                        {
                            // Now that it's locked again, ensure it's not back in the list.
                            if (_cachedAgentData.Contains(uuid))
                                _cachedAgentData.Remove(uuid);
//                            if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: Setting agent data for {0} SSID={1}", uuid, result.SecureSessionID); 
                            _cachedAgentData.Add(uuid, new TimestampedItem<UserAgentData>(result));
                            return result;
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="name">The agent's name</param>
        /// <returns>A user agent</returns>
        public UserAgentData GetUserAgent(string name)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByName(name);
                    
                    if (result != null)
                        return result;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a user agent by name (not called directly)
        /// </summary>
        /// <param name="fname">The agent's firstname</param>
        /// <param name="lname">The agent's lastname</param>
        /// <returns>A user agent</returns>
        public UserAgentData GetUserAgent(string fname, string lname)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByName(fname, lname);
                    
                    if (result != null)
                        return result;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to find user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        public virtual List<FriendListItem> GetUserFriendList(UUID ownerID)
        {
            List<FriendListItem> allFriends = new List<FriendListItem>();
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    List<FriendListItem> friends = plugin.GetUserFriendList(ownerID);

                    if (friends != null)
                        allFriends.AddRange(friends);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to GetUserFriendList via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            return allFriends;
        }

        public virtual Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            //Dictionary<UUID, FriendRegionInfo> allFriendRegions = new Dictionary<UUID, FriendRegionInfo>();
            
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    Dictionary<UUID, FriendRegionInfo> friendRegions = plugin.GetFriendRegionInfos(uuids);

                    if (friendRegions != null)
                        return friendRegions;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to GetFriendRegionInfos via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            
            return new Dictionary<UUID, FriendRegionInfo>();
        }

        
        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.StoreWebLoginKey(agentID, webLoginKey);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to Store WebLoginKey via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }
        

        public virtual void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddNewUserFriend(friendlistowner, friend, perms);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to AddNewUserFriend via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public virtual void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.RemoveUserFriend(friendlistowner, friend);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to RemoveUserFriend via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public virtual void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserFriendPerms(friendlistowner, friend, perms);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to UpdateUserFriendPerms via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        /// <summary>
        /// Resets the currentAgent in the user profile
        /// </summary>
        /// <param name="agentID">The agent's ID</param>
        public virtual void ClearUserAgent(UUID agentID)
        {
            UserProfileData profile = GetUserProfile(agentID);

            if (profile == null)
            {
                return;
            }

            profile.CurrentAgent = null;

            UpdateUserProfile(profile);
        }

        #endregion

        #region CreateAgent

        /// <summary>
        /// Creates and initializes a new user agent - make sure to use CommitAgent when done to submit to the DB
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="request">The users loginrequest</param>
        public void CreateAgent(UserProfileData profile, XmlRpcRequest request)
        {
            //m_log.DebugFormat("[USER MANAGER]: Creating agent {0} {1}", profile.Name, profile.ID);
            
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

//            if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: Creating new agent data for {0} SSID={1}", agent.ProfileID, agent.SecureSessionID); 
            profile.CurrentAgent = agent;
        }

        public void CreateAgent(UserProfileData profile, OSD request)
        {
            //m_log.DebugFormat("[USER MANAGER]: Creating agent {0} {1}", profile.Name, profile.ID);
            
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
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
//            if (m_isUserServer) m_log.WarnFormat("[USER MANAGER]: CommitAgent: {0} {1}", profile.ID, profile.CurrentAgent.SecureSessionID);

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
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="position"></param>
        /// <param name="lookat"></param>
        public virtual void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            if (StatsManager.UserStats != null)
                StatsManager.UserStats.AddLogout();

            UserProfileData userProfile = GetUserProfile(userid);

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
                    m_log.Info("[LOGOUT]: didn't save logout position: " + userid.ToString());
                }
            }
            else
            {
                m_log.Warn("[LOGOUT]: Unknown User logged out");
            }
        }

        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            LogOffUser(userid, regionid, regionhandle, new Vector3(posx, posy, posz), new Vector3());
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
        /// <param name="SetUUID">UUID of avatar.</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        public virtual UUID AddUser(
            string firstName, string lastName, string password, string email, uint regX, uint regY, UUID SetUUID)
        {
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + String.Empty);

            UserProfileData userProf = GetUserProfile(firstName, lastName);
            if (userProf != null)
            {
                m_log.Error("[USERSTORAGE]: Not creating user. User already exists ");
                return UUID.Zero;
            }

            UserProfileData user = new UserProfileData();
            user.HomeLocation = new Vector3(128, 128, 100);
            user.ID = SetUUID;
            user.FirstName = firstName;
            user.SurName = lastName;
            user.PasswordHash = md5PasswdHash;
            user.PasswordSalt = String.Empty;
            user.Created = Util.UnixTimeSinceEpoch();
            user.HomeLookAt = new Vector3(100, 100, 100);
            user.HomeRegionX = regX;
            user.HomeRegionY = regY;
            user.Email = email;

            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddNewUserProfile(user);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to add user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }

            userProf = GetUserProfile(firstName, lastName);
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
            folder.Type = (short)AssetType.LostAndFoundFolder;
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
            folder.Type = (short)AssetType.SnapshotFolder;
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
            folder.Type = (short)AssetType.TrashFolder;
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
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(newPassword) + ":" + String.Empty);

            UserProfileData profile = GetUserProfile(firstName, lastName);

            if (null == profile)
            {
                m_log.ErrorFormat("[USERSTORAGE]: Could not find user {0} {1}", firstName, lastName);
                return false;
            }

            profile.PasswordHash = md5PasswdHash;
            profile.PasswordSalt = String.Empty;

            UpdateUserProfile(profile);

            return true;
        }

        public abstract UserProfileData SetupMasterUser(string firstName, string lastName);
        public abstract UserProfileData SetupMasterUser(string firstName, string lastName, string password);
        public abstract UserProfileData SetupMasterUser(UUID uuid);

        /// <summary>
        /// Add an agent using data plugins.
        /// </summary>
        /// <param name="agentdata">The agent data to be added</param>
        /// <returns>
        /// true if at least one plugin added the user agent.  false if no plugin successfully added the agent
        /// </returns>
        public virtual bool AddUserAgent(UserAgentData agentdata)
        {
            bool result = false;

            lock (_cachedAgentData)
            {
                UUID userId = agentdata.ProfileID;

                TimestampedItem<UserAgentData> timedItem = null;
                if (_cachedAgentData.TryGetValue(userId, out timedItem))
                {
//                    if (m_isUserServer && (timedItem.Item.SecureSessionID != agentdata.SecureSessionID))
//                        m_log.WarnFormat("[USER CACHE]: AddUserAgent: Removing old agent data for {0} SSID={1}", userId, timedItem.Item.SecureSessionID);
                    _cachedAgentData.Remove(userId);
                }

                // Must also add the updated record to the cache, inside the lock, or risk
                // a race condition with other threads refreshing the cache from the database 
                // before plugin.AddNewUserAgent below has finished committing the change.
                _cachedAgentData.Add(userId, new TimestampedItem<UserAgentData>(agentdata));
            }

            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
//                    if (m_isUserServer) m_log.WarnFormat("[USER CACHE]: AddUserAgent: Adding agent data for {0} SSID={1}", agentdata.ProfileID, agentdata.SecureSessionID);
                    plugin.AddNewUserAgent(agentdata);
                    result = true;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to add agent via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            
            return result;
        }

        /// <summary>
        /// Get avatar appearance information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetUserAppearance(UUID user)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    AvatarAppearance appearance = plugin.GetUserAppearance(user);
                    
                    if (appearance != null)
                        return appearance;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to find user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }
            
            return null;
        }

        /// <summary>
        /// Get bot outfit
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetBotOutfit(UUID user, string outfitName)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    AvatarAppearance appearance = plugin.GetBotOutfit(user, outfitName);

                    if (appearance != null)
                        return appearance;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to find user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }

            return null;
        }

        public virtual void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserAppearance(user, appearance);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to update user appearance {0} via {1} ({2})", user.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        public virtual void AddOrUpdateBotOutfit(UUID userID, string outfitName, AvatarAppearance appearance)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddOrUpdateBotOutfit(userID, outfitName, appearance);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to add bot outfit {0} via {1} ({2})", userID.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        public virtual void RemoveBotOutfit(UUID userID, string outfitName)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.RemoveBotOutfit(userID, outfitName);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to remove bot outfit {0} via {1} ({2})", userID.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        public virtual List<string> GetBotOutfitsByOwner(UUID userID)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    var outfits = plugin.GetBotOutfitsByOwner(userID);
                    if (outfits != null)
                        return outfits;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to get bot outfits {0} via {1} ({2})", userID.ToString(), plugin.Name, e.ToString());
                }
            }
            return null;
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
            string newKey = string.Empty;
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
                    m_log.InfoFormat("[USERAUTH]: Successfully generated new auth key for user {0}", userID);
                }
                else
                    m_log.Warn("[USERAUTH]: Unauthorized key generation request. Denying new key.");
                 
            }
            else
                m_log.Warn("[USERAUTH]: User not found.");

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
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.SaveUserPreferences(userPrefs);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to save user preferences {0} via {1} ({2})", userPrefs.UserId, plugin.Name, e.ToString());
                }
            }
        }

        public UserPreferencesData RetrieveUserPreferences(UUID userId)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserPreferencesData userPrefs = plugin.RetrieveUserPreferences(userId);

                    if (userPrefs != null)
                    {
                        return userPrefs;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to retrieve user preferences {0} via {1} ({2})", userId, plugin.Name, e.ToString());
                }
            }

            return null;
        }

        #endregion

        #region IAvatarService - Cached baked textures

        public virtual List<CachedAgentArgs> GetCachedBakedTextures(List<CachedAgentArgs> request)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    List<CachedAgentArgs> resp = plugin.GetCachedBakedTextures(request);
                    if (resp != null && resp.Count > 0)
                        return resp;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to get baked textures via {0} ({1})", plugin.Name, e.ToString());
                }
            }
            return null;
        }

        public virtual void SetCachedBakedTextures(Dictionary<UUID, UUID> request)
        {
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.SetCachedBakedTextures(request);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to set baked textures via {0} ({1})", plugin.Name, e.ToString());
                }
            }
        }

        #endregion

        #region - Level 2 user profile cache

        /// <value>
        /// Standard format for names.
        /// </value>
        public const string NAME_FORMAT = "{0} {1}";
        
        public CachedUserInfo GetUserDetails(string fname, string lname)
        {
            string name = string.Format(NAME_FORMAT, fname, lname);

            lock (_userProfilesLock)
            {
                if (m_userProfilesByName.ContainsKey(name))
                {
                    return m_userProfilesByName[name];
                }
            }

            UserProfileData userProfile = GetUserProfile(fname, lname);
            if (userProfile != null)
            {
                return AddToProfileCache(userProfile);
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
        public CachedUserInfo GetUserDetails(UUID userID)
        {
            return GetUserInfo(userID);
        }

        /// <summary>
        /// Preloads User data into the region cache. Modules may use this service to add non-standard clients
        /// </summary>
        /// <param name="userData"></param>
        public void PreloadUserCache(UserProfileData userData)
        {
            AddToProfileCache(userData);
        }

        public void UpdateFriendPerms(UUID ownerID, UUID friendID, uint perms)
        {
            //if the friend is here we need to change their permissions for the given user
            CachedUserInfo cachedUserDetails = this.GetUserDetails(friendID);
            if (cachedUserDetails != null)
            {
                cachedUserDetails.AdjustPermissionsFromFriend(ownerID, perms);
            }
        }

        /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// 
        /// It isn't strictly necessary to make this call since user data can be lazily requested later on.  However, 
        /// it might be helpful in order to avoid an initial response delay later on
        /// 
        /// <param name="userID"></param>
        public void AddCachedUser(UUID userID)
        {
            if (userID == UUID.Zero)
                return;

            //m_log.DebugFormat("[USER CACHE]: Adding user profile for {0}", userID);
            GetUserDetails(userID);
        }

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool RemoveCachedUser(UUID userId)
        {
            if (!RemoveFromProfileCache(userId))
            { 
                // With the replacment of normal user profile caching with local user profile caching, this is a normal now.
                // It wasn't in the normal user profile cache because it was in the local user profile cache.
                // We can't remove the call entirely because a late lookup may have added it again, so we need to remove if there.
                // m_log.WarnFormat( "[USER CACHE]: Tried to remove the profile of user {0}, but this was not in the scene", userId);
                return false;
            }

            return true;
        }

        public void AddLocalUser(UUID userID)
        {
            DumpStatus("AddLocalUser(entry)");
            // Because profile changes can be made outside of the region the user is in (e.g. partnering), 
            // we'll provide a way for users to force a profile refetch to the current region.
            // We'll for an refresh of the user's profile when they enter or leave a region.
            PurgeUserFromCaches(userID);
            CachedUserInfo userInfo = GetUserInfo(userID);

            // Now deal with the cached local profile storage.
            lock (_userProfilesLock)
            {
                if (userInfo == null)
                {
                    m_log.WarnFormat("[USER CACHE]: Could not fetch profile for: {0}", userID);
                    return;
                }

                // Remove it from the LRU now that it's cached as a local profile
                if (_cachedProfileData.Contains(userID))
                    _cachedProfileData.Remove(userID);

                m_log.InfoFormat("[USER CACHE]: Added profile to local user cache for: {0} {1}", userID, userInfo.UserProfile.Name);
                _cachedLocalProfiles[userID] = userInfo;
                m_userProfilesByName[userInfo.UserProfile.Name] = userInfo;
            }
            DumpStatus("AddLocalUser(exit)");
        }

        public void RemoveLocalUser(UUID userID)
        {
            // Because profile changes can be made outside of the region the user is in (e.g. partnering), 
            // we'll provide a way for users to force a profile refetch to the current region.
            // We'll for an refresh of the user's profile when they enter or leave a region.

            lock (_userProfilesLock)
            {
                // First deal with the cached local profile storage.
                if (_cachedLocalProfiles.ContainsKey(userID))
                {
                    CachedUserInfo profile = _cachedLocalProfiles[userID];
                    m_log.InfoFormat("[USER CACHE]: Removed profile from local user cache for: {0} {1}", userID, profile.UserProfile.Name);
                    _cachedLocalProfiles.Remove(userID);

                }
                // Now still inside the same lock, purge from the other caches too, if they are there.
                PurgeUserFromCaches(userID);
            }
        }

        #endregion
    }
}
