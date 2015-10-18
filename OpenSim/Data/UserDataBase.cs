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
using OpenSim.Framework;

namespace OpenSim.Data
{
    public abstract class UserDataBase : IUserDataPlugin
    {
        public abstract UserProfileData GetUserByUUID(UUID user);
        public abstract UserProfileData GetUserByName(string fname, string lname);
        public abstract UserAgentData GetAgentByUUID(UUID user);
        public abstract UserAgentData GetAgentByName(string name);
        public abstract UserAgentData GetAgentByName(string fname, string lname);
        public UserProfileData GetUserByUri(Uri uri) { return null; }        
        public abstract void StoreWebLoginKey(UUID agentID, UUID webLoginKey);
        public abstract void AddNewUserProfile(UserProfileData user);

        public virtual void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            // Temporary profiles are optional for database plugins.
        }
        public virtual void RemoveTemporaryUserProfile(UUID userid)
        {
            // Temporary profiles are optional for database plugins.
        }

        public abstract bool UpdateUserProfile(UserProfileData user);
        public abstract void AddNewUserAgent(UserAgentData agent);
        public abstract void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms);
        public abstract void RemoveUserFriend(UUID friendlistowner, UUID friend);
        public abstract void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms);
        public abstract List<FriendListItem> GetUserFriendList(UUID friendlistowner);
        public abstract Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids);
        public abstract bool MoneyTransferRequest(UUID from, UUID to, uint amount);
        public abstract bool InventoryTransferRequest(UUID from, UUID to, UUID inventory);
        public abstract List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query);
        public abstract AvatarAppearance GetUserAppearance(UUID user);
        public abstract AvatarAppearance GetBotOutfit(UUID user, string outfitName);
        public abstract void UpdateUserAppearance(UUID user, AvatarAppearance appearance);
        public abstract void AddOrUpdateBotOutfit(UUID userID, string outfitName, AvatarAppearance appearance);
        public abstract void RemoveBotOutfit(UUID userID, string outfitName);
        public abstract List<string> GetBotOutfitsByOwner(UUID userID);
        public abstract List<CachedAgentArgs> GetCachedBakedTextures(List<CachedAgentArgs> args);
        public abstract void SetCachedBakedTextures(Dictionary<UUID, UUID> bakedTextures);
        public abstract void ResetAttachments(UUID userID);

        public abstract void LogoutUsers(UUID regionID);

        public abstract string Version {get;}
        public abstract string Name {get;}
        public abstract void Initialize(string connect);
        public abstract void Initialize();
        public abstract void Dispose();

        public abstract void SaveUserPreferences(UserPreferencesData userPrefs);
        public abstract UserPreferencesData RetrieveUserPreferences(UUID userId);
   }
}
