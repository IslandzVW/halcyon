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

using OpenMetaverse;
using System.Collections.Generic;

namespace OpenSim.Framework.Communications
{
    public interface IAvatarService
    {
        /// <summary>
        /// Get avatar appearance information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        AvatarAppearance GetUserAppearance(UUID user);

        /// <summary>
        /// Get bot outfit
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        AvatarAppearance GetBotOutfit(UUID user, string outfitName);

        /// <summary>
        /// Update avatar appearance information
        /// </summary>
        /// <param name="user"></param>
        /// <param name="appearance"></param>        
        void UpdateUserAppearance(UUID user, AvatarAppearance appearance);

        /// <summary>
        /// Adds an appearance for a bot to be able to use later with the given outfitName
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="outfitName"></param>
        /// <param name="appearance"></param>
        void AddOrUpdateBotOutfit(UUID userID, string outfitName, AvatarAppearance appearance);

        /// <summary>
        /// Removes a bot outfit from the database
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="outfitName"></param>
        void RemoveBotOutfit(UUID userID, string outfitName);

        /// <summary>
        /// Get the outfit names owned by the user
        /// </summary>
        /// <param name="userID">The user who owns the outfits</param>
        /// <returns>A list of outfit names that the user owns</returns>
        List<string> GetBotOutfitsByOwner(UUID userID);

        List<CachedAgentArgs> GetCachedBakedTextures(List<CachedAgentArgs> request);

        void SetCachedBakedTextures(Dictionary<UUID, UUID> request);
    }
}
