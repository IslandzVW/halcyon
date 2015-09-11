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

using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using System.Threading;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Holds user profile information and retrieves it from backend services.
    /// </summary>
    public class UserProfileCacheService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>
        private readonly CommunicationsManager m_commsManager;

        public InventoryFolderImpl LibraryRoot
        {
            get {
                return m_commsManager.LibraryRoot;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        /// <param name="libraryRootFolder"></param>
        public UserProfileCacheService(CommunicationsManager commsManager)
        {
            m_commsManager = commsManager;
        }

        /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// 
        /// It isn't strictly necessary to make this call since user data can be lazily requested later on.  However, 
        /// it might be helpful in order to avoid an initial response delay later on
        /// 
        /// <param name="userID"></param>
        public void AddNewUser(UUID userId)
        {
            m_commsManager.UserService.AddCachedUser(userId);
        }

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool RemoveUser(UUID userId)
        {
            return m_commsManager.UserService.RemoveCachedUser(userId);
        }

        /// <summary>
        /// Get details of the given user.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.  
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>        
        public CachedUserInfo GetUserDetails(string fname, string lname)
        {
            return m_commsManager.UserService.GetUserDetails(fname, lname);
        }
       
        /// <summary>
        /// Get details of the given user.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.  
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(UUID userID)
        {
            if (userID == UUID.Zero)
                return null;
            return m_commsManager.UserService.GetUserDetails(userID);
        }

        public void UpdateUserFriendPerms(UUID ownerID, UUID friendID, uint perms)
        {
            if (ownerID == UUID.Zero) return;
            m_commsManager.UserService.UpdateUserFriendPerms(ownerID, friendID, perms);
        }
    }
}
