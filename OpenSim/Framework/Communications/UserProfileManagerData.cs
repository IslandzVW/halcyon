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
using OpenSim.Data;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Base class for user management (create, read, etc)
    /// </summary>
    public class UserProfileManagerData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// List of plugins to search for user data
        /// </value>
        private List<IUserDataPlugin> m_plugins = new List<IUserDataPlugin>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        public UserProfileManagerData()
        {
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

        // Fetches a profile from the db via XMLRPC
        public UserProfileData GetUserProfileData(string fname, string lname)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetUserProfileData plugin request for {0} {1}", fname, lname);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserProfileData profile = plugin.GetUserByName(fname, lname);
                if (profile != null)
                    return profile;
            }

            return null;
        }
        public UserProfileData GetUserProfileData(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetUserProfileData plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserProfileData profile = plugin.GetUserByUUID(uuid);
                if (profile != null)
                    return profile;
            }

            return null;
        }

        public virtual UserAgentData GetAgentData(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetAgentData plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                UserAgentData agent = plugin.GetAgentByUUID(uuid);
                if (agent != null)
                {
                    // Whatever plugin returns an agent data structure, ensure the UUID is filled in.
                    agent.ProfileID = uuid;
                    return agent;
                }
            }

            return null;
        }

        public void LogoutUsers(UUID regionID)
        {
            m_log.DebugFormat("[PROFILE]: LogoutUsers plugin request for {0}", regionID);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.LogoutUsers(regionID);
            }
        }

        public void AddUser(UserProfileData profile)
        {
            // m_log.DebugFormat("[USERSTORAGE]: AddUser plugin request for {0} {1}", profile.ID, profile.Name);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddNewUserProfile(profile);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to add user via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }

        public bool AddNewUserAgent(UserAgentData agent)
        {
            // m_log.DebugFormat("[USERSTORAGE]: AddNewUserAgent plugin request for {0}", agent.ProfileID);
            bool result = false;
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddNewUserAgent(agent);
                    result = true;
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to add agent via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
            return result;
        }

        public void ResetAttachments(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: ResetAttachments plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                plugin.ResetAttachments(uuid);
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
            // m_log.DebugFormat("[USERSTORAGE]: GenerateAgentPickerRequestResponse plugin request: {0}", queryID);
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
        
        public virtual bool UpdateUserProfileData(UserProfileData profile)
        {
            // m_log.DebugFormat("[USERSTORAGE]: UpdateUserProfileData plugin request for {0} {1}", profile.ID, profile.Name);
            bool result = false;

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

        #region Get UserAgentData

        /// <summary>
        /// Loads a user agent by uuid (not called directly)
        /// </summary>
        /// <param name="uuid">The agent's UUID</param>
        /// <returns>Agent profiles</returns>
        public UserAgentData GetUserAgentData(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetUserAgentData plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserAgentData result = plugin.GetAgentByUUID(uuid);
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
        /// <param name="name">The agent's name</param>
        /// <returns>A user agent</returns>
        public UserAgentData GetUserAgentData(string name)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetUserAgentData plugin request for {0}", name);
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
        public UserAgentData GetUserAgentData(string fname, string lname)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetUserAgentData plugin request for {0} {1}", fname, lname);
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
            // m_log.DebugFormat("[USERSTORAGE]: GetUserFriendList plugin request for {0}", ownerID);
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
            // m_log.DebugFormat("[USERSTORAGE]: GetFriendRegionInfos plugin request");
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

        public void StoreWebLoginKey(UUID uuid, UUID webLoginKey)
        {
            // m_log.DebugFormat("[USERSTORAGE]: StoreWebLoginKey plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.StoreWebLoginKey(uuid, webLoginKey);
                }
                catch (Exception e)
                {
                    m_log.Error("[USERSTORAGE]: Unable to Store WebLoginKey via " + plugin.Name + "(" + e.ToString() + ")");
                }
            }
        }
        
        public virtual void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            // m_log.DebugFormat("[USERSTORAGE]: AddNewUserFriend plugin request for {0}", friendlistowner);
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
            // m_log.DebugFormat("[USERSTORAGE]: RemoveUserFriend plugin request for {0}", friendlistowner);
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
            // m_log.DebugFormat("[USERSTORAGE]: UpdateUserFriendPerms plugin request for {0}", friendlistowner);
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

        #endregion

        /// <summary>
        /// Get avatar appearance information
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetUserAppearance(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetUserAppearance plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    AvatarAppearance appearance = plugin.GetUserAppearance(uuid);
                    
                    if (appearance != null)
                        return appearance;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to find user appearance {0} via {1} ({2})", uuid.ToString(), plugin.Name, e.ToString());
                }
            }
            
            return null;
        }

        /// <summary>
        /// Get bot outfit
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual AvatarAppearance GetBotOutfit(UUID uuid, string outfitName)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetBotOutfit plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    AvatarAppearance appearance = plugin.GetBotOutfit(uuid, outfitName);
                    if (appearance != null)
                        return appearance;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to find user appearance {0} via {1} ({2})", uuid.ToString(), plugin.Name, e.ToString());
                }
            }

            return null;
        }

        public virtual void UpdateUserAppearance(UUID uuid, AvatarAppearance appearance)
        {
            // m_log.DebugFormat("[USERSTORAGE]: UpdateUserAppearance plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.UpdateUserAppearance(uuid, appearance);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to update user appearance {0} via {1} ({2})", uuid.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        public virtual void AddOrUpdateBotOutfit(UUID uuid, string outfitName, AvatarAppearance appearance)
        {
            // m_log.DebugFormat("[USERSTORAGE]: AddOrUpdateBotOutfit plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.AddOrUpdateBotOutfit(uuid, outfitName, appearance);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to add bot outfit {0} via {1} ({2})", uuid.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        public virtual void RemoveBotOutfit(UUID uuid, string outfitName)
        {
            // m_log.DebugFormat("[USERSTORAGE]: RemoveBotOutfit plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    plugin.RemoveBotOutfit(uuid, outfitName);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to remove bot outfit {0} via {1} ({2})", uuid.ToString(), plugin.Name, e.ToString());
                }
            }
        }

        public virtual List<string> GetBotOutfitsByOwner(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetBotOutfitsByOwner plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    var outfits = plugin.GetBotOutfitsByOwner(uuid);
                    if (outfits != null)
                        return outfits;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to get bot outfits {0} via {1} ({2})", uuid.ToString(), plugin.Name, e.ToString());
                }
            }
            return null;
        }

        #region IUserService Members

        public void SaveUserPreferences(UserPreferencesData userPrefs)
        {
            // m_log.DebugFormat("[USERSTORAGE]: SaveUserPreferences plugin request for {0}", userPrefs.UserId);
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

        public UserPreferencesData RetrieveUserPreferences(UUID uuid)
        {
            // m_log.DebugFormat("[USERSTORAGE]: RetrieveUserPreferences plugin request for {0}", uuid);
            foreach (IUserDataPlugin plugin in m_plugins)
            {
                try
                {
                    UserPreferencesData userPrefs = plugin.RetrieveUserPreferences(uuid);
                    if (userPrefs != null)
                        return userPrefs;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USERSTORAGE]: Unable to retrieve user preferences {0} via {1} ({2})", uuid, plugin.Name, e.ToString());
                }
            }

            return null;
        }

        #endregion

        #region IAvatarService - Cached baked textures

        public virtual List<CachedAgentArgs> GetCachedBakedTextures(List<CachedAgentArgs> request)
        {
            // m_log.DebugFormat("[USERSTORAGE]: GetCachedBakedTextures plugin request");
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
            // m_log.DebugFormat("[USERSTORAGE]: SetCachedBakedTextures plugin request");
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

    }
}
