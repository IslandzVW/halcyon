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
using OpenMetaverse;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Framework.Communications
{
    public interface IUserService
    {
        /// <summary>
        /// Add a temporary user profile.
        /// </summary>
        /// A temporary user profile is one that should exist only for the lifetime of the process.
        /// <param name="userProfile"></param>
        void AddTemporaryUserProfile(UserProfileData userProfile);

        /// <summary>
        /// Remove a temporary user profile.
        /// </summary>
        /// Reverses the effect of AddTemporaryUserProfile
        /// <param name="uuid">The UUID of the profile to remove.</param>
        void RemoveTemporaryUserProfile(UUID uuid);

        /// <summary>
        /// Loads a user profile by name
        /// </summary>
        /// <param name="firstName">First name</param>
        /// <param name="lastName">Last name</param>
        /// <returns>A user profile.  Returns null if no profile is found</returns>
        UserProfileData GetUserProfile(string firstName, string lastName, bool forceRefresh);
        UserProfileData GetUserProfile(string firstName, string lastName);
        UserProfileData GetUserProfile(string name);
        //UserInterestsData GetUserInterests(UUID uuid);

        /// <summary>
        /// Loads a user profile from a database by UUID
        /// </summary>
        /// <param name="userId">The target UUID</param>
        /// <returns>A user profile.  Returns null if no user profile is found.</returns>
        UserProfileData GetUserProfile(UUID uuid, bool forceRefresh);
        UserProfileData GetUserProfile(UUID uuid);

        // Just call these if all you need is the name from cache.
        UUID Name2Key(string firstName, string lastName);
        UUID Name2Key(string name);
        string Key2Name(UUID uuid, bool onlyIfCached);
        bool Key2Names(UUID uuid, bool onlyIfCached, out string firstName, out string lastName);
        string GetLastName(UUID uuid, bool onlyIfCached);
        string GetFirstName(UUID uuid, bool onlyIfCached);

        UserAgentData GetUserAgent(UUID uuid, bool forceRefresh);
        UserAgentData GetUserAgent(UUID uuid);

        void ClearUserAgent(UUID avatarID);
        List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID QueryID, string Query);

        UserProfileData SetupMasterUser(string firstName, string lastName);
        UserProfileData SetupMasterUser(string firstName, string lastName, string password);
        UserProfileData SetupMasterUser(UUID uuid);

        /// <summary>
        /// Update the user's profile.
        /// </summary>
        /// <param name="data">UserProfileData object with updated data. Should be obtained 
        ///                    via a call to GetUserProfile().</param>
        /// <returns>true if the update could be applied, false if it could not be applied.</returns>
        bool UpdateUserProfile(UserProfileData data);

        //bool UpdateUserInterests(UserInterestsData data);

        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms);

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        void RemoveUserFriend(UUID friendlistowner, UUID friend);

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms);

        /// <summary>
        /// Check if this user can access another's items.
        /// </summary>
        /// <param name="friendlistowner">This user (to check).</param>
        /// <param name="friendId">The ID of the other user (friend owner of the items).</param>
        /// <param name="permissionMask">Desired permission.</param>
        /// <param name="noFetch">If true, don't make any net/storage calls. Memory only.</param>
        /// <returns>true if permission is available</returns>
        bool UserHasFriendPerms(UUID friendlistowner, UUID friendId, uint permissionMask, bool noFetch);

        /// <summary>
        /// Logs off a user on the user server
        /// </summary>
        /// <param name="userid">UUID of the user</param>
        /// <param name="regionid">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="position">final position</param>
        /// <param name="lookat">final lookat</param>
        void LogOffUser(UUID uuid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat);

        /// <summary>
        /// Logs off a user on the user server (deprecated as of 2008-08-27)
        /// </summary>
        /// <param name="userid">UUID of the user</param>
        /// <param name="regionid">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="posx">final position x</param>
        /// <param name="posy">final position y</param>
        /// <param name="posz">final position z</param>
        void LogOffUser(UUID uuid, UUID regionid, ulong regionhandle, float posx, float posy, float posz);

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship 
        /// for UUID friendslistowner
        /// </summary>
        /// 
        /// <param name="friendlistowner">The agent for whom we're retreiving the friends Data.</param>
        /// <returns>
        /// A List of FriendListItems that contains info about the user's friends.  
        /// Always returns a list even if the user has no friends
        /// </returns>        
        List<FriendListItem> GetUserFriendList(UUID friendlistowner);

        // This probably shouldn't be here, it belongs to IAuthentication
        // But since Scenes only have IUserService references, I'm placing it here for now.
        bool VerifySession(UUID uuid, UUID sessionID);

        /// <summary>
        /// Save user preferences.  See UserPreferencesData for more information
        /// </summary>
        void SaveUserPreferences(UserPreferencesData userPrefs);

        /// <summary>
        /// Retrieve preferences.  See UserPreferencesData for more information
        /// </summary>
        UserPreferencesData RetrieveUserPreferences(UUID uuid);

                /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// 
        /// It isn't strictly necessary to make this call since user data can be lazily requested later on.  However, 
        /// it might be helpful in order to avoid an initial response delay later on
        /// 
        /// <param name="userID"></param>
        void CacheUser(UUID uuid);

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        bool UncacheUser(UUID uuid);

        /// <summary>
        /// This function keeps the user profile in memory for the duration of an agent being in a region.
        /// These two functions are more of a migrate from/to one cache to/from the local user cache, than add/remove.
        /// </summary>
        /// <param name="userID"></param>
        void MakeLocalUser(UUID uuid);
        void UnmakeLocalUser(UUID uuid);

        /// <summary>
        /// Get details of the given user.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.  
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        CachedUserInfo GetUserDetails(UUID uuid);

        /// <summary>
        /// Get details of the given user.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.  
        /// <param name="fname"></param>
        /// <param name="lname"></param>
        /// <returns>null if no user details are found</returns>
        CachedUserInfo GetUserDetails(string fname, string lname);
    }
}
