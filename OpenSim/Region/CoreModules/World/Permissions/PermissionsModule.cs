/*
 * Copyright (c) 2015, InWorldz Halcyon Developers
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice, this
 *     list of conditions and the following disclaimer.
 * 
 *   * Redistributions in binary form must reproduce the above copyright notice,
 *     this list of conditions and the following disclaimer in the documentation
 *     and/or other materials provided with the distribution.
 * 
 *   * Neither the name of halcyon nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

// Modified to apply friend edit permissions
// (c) 2009 Inworldz LLC
//

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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Permissions
{
    public class PermissionsModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
                
        protected Scene m_scene;

        #region Constants
        // These are here for testing.  They will be taken out

        //private const uint PERM_ALL = (uint)2147483647;
        private const uint PERM_COPY = (uint)32768;
        private const uint PERM_MODIFY = (uint)16384;
        private const uint PERM_MOVE = (uint)524288;
        private const uint PERM_TRANS = (uint)8192;
        private const uint PERM_LOCKED = PERM_MOVE | PERM_MODIFY;  
        
        /// <value>
        /// Different user set names that come in from the configuration file.
        /// </value>
        enum UserSet
        {
            All,
            Administrators
        };

        #endregion   

        #region Bypass Permissions / Debug Permissions Stuff

        // Bypasses the permissions engine
        private bool m_bypassPermissions = true;
        private bool m_bypassPermissionsValue = true;
        private bool m_propagatePermissions = false;
        private bool m_debugPermissions = false;
        private bool m_allowGridGods = false;

        #endregion

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource config)
        {
            m_scene = scene;

            IConfig myConfig = config.Configs["Startup"];

            string permissionModules = myConfig.GetString("permissionmodules", "DefaultPermissionsModule");

            List<string> modules=new List<string>(permissionModules.Split(','));

            if (!modules.Contains("DefaultPermissionsModule"))
            {
                m_log.Info("[PERMISSIONS]: Startup option: 'permissionmodules' does not include 'DefaultPermissionsModule', PermissionModule not initialized.");
                return;
            }

            m_allowGridGods = myConfig.GetBoolean("allow_grid_gods", false);
            m_bypassPermissions = !myConfig.GetBoolean("serverside_object_permissions", false);
            m_propagatePermissions = myConfig.GetBoolean("propagate_permissions", true);

            if (m_bypassPermissions)
                m_log.Info("[PERMISSIONS]: serviceside_object_permissions = false in ini file so disabling all region service permission checks");
            else
                m_log.Debug("[PERMISSIONS]: Enabling all region service permission checks");

            //Register functions with Scene External Checks!
            m_scene.Permissions.OnBypassPermissions += BypassPermissions;
            m_scene.Permissions.OnSetBypassPermissions += SetBypassPermissions;
            m_scene.Permissions.OnPropagatePermissions += PropagatePermissions;
            m_scene.Permissions.OnGenerateClientFlags += GenerateClientFlags;
            m_scene.Permissions.OnAbandonParcel += CanAbandonParcel;
            m_scene.Permissions.OnReclaimParcel += CanReclaimParcel;
            m_scene.Permissions.OnDeedParcel += CanDeedParcel;
            m_scene.Permissions.OnDeedObject += CanDeedObject;
            m_scene.Permissions.OnIsGod += IsGod;
            m_scene.Permissions.OnDuplicateObject += CanDuplicateObject;
            m_scene.Permissions.OnDeleteObject += CanDeleteObject;
            m_scene.Permissions.OnEditObject += CanEditObject;
            m_scene.Permissions.OnEditParcel += CanEditParcel;
            m_scene.Permissions.OnInstantMessage += CanInstantMessage;
            m_scene.Permissions.OnInventoryTransfer += CanInventoryTransfer; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnIssueEstateCommand += CanIssueEstateCommand; //FULLY IMPLEMENTED
            m_scene.Permissions.OnMoveObject += CanMoveObject;
            m_scene.Permissions.OnObjectEntry += CanObjectEntry;
            m_scene.Permissions.OnReturnObject += CanReturnObject;
            m_scene.Permissions.OnRezObject += CanRezObject;
            m_scene.Permissions.OnRunConsoleCommand += CanRunConsoleCommand;
            m_scene.Permissions.OnRunScript += CanRunScript; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnTakeObject += CanTakeObject;
            m_scene.Permissions.OnTakeCopyObject += CanTakeCopyObject;
            m_scene.Permissions.OnTerraformLand += CanTerraformLand;
            m_scene.Permissions.OnLinkObject += CanLinkObject; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnDelinkObject += CanDelinkObject; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnBuyLand += CanBuyLand; //NOT YET IMPLEMENTED
            
            m_scene.Permissions.OnViewNotecard += CanViewNotecard; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnViewScript += CanViewScript; //NOT YET IMPLEMENTED                       
            m_scene.Permissions.OnEditNotecard += CanEditNotecard; //NOT YET IMPLEMENTED            
            m_scene.Permissions.OnEditScript += CanEditScript; //NOT YET IMPLEMENTED            
            
            m_scene.Permissions.OnCreateObjectInventory += CanCreateObjectInventory; //NOT IMPLEMENTED HERE 
            m_scene.Permissions.OnEditObjectInventory += CanEditObjectInventory;//MAYBE FULLY IMPLEMENTED            
            m_scene.Permissions.OnCopyObjectInventory += CanCopyObjectInventory; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnDeleteObjectInventory += CanDeleteObjectInventory; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnResetScript += CanResetScript;
            
            m_scene.Permissions.OnCreateUserInventory += CanCreateUserInventory; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnCopyUserInventory += CanCopyUserInventory; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnEditUserInventory += CanEditUserInventory; //NOT YET IMPLEMENTED
            m_scene.Permissions.OnDeleteUserInventory += CanDeleteUserInventory; //NOT YET IMPLEMENTED
            
            m_scene.Permissions.OnTeleport += CanTeleport; //NOT YET IMPLEMENTED

            m_scene.Permissions.OnUseObjectReturn += CanUseObjectReturn;

            m_scene.Permissions.OnStartScript += CanStartScript;
            m_scene.Permissions.OnStopScript += CanStopScript;

            m_scene.AddCommand(this, "bypass permissions",
                    "bypass permissions <true / false>",
                    "Bypass permission checks",
                    HandleBypassPermissions);

            m_scene.AddCommand(this, "force permissions",
                    "force permissions <true / false>",
                    "Force permissions on or off",
                    HandleForcePermissions);

            m_scene.AddCommand(this, "debug permissions",
                    "debug permissions <true / false>",
                    "Enable permissions debugging",
                    HandleDebugPermissions);
        }

        public void HandleBypassPermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_bypassPermissions = val;

                m_log.InfoFormat(
                    "[PERMISSIONS]: Set permissions bypass to {0} for {1}", 
                    m_bypassPermissions, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleForcePermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (!m_bypassPermissions)
            {
                m_log.Error("[PERMISSIONS] Permissions can't be forced unless they are bypassed first");
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_bypassPermissionsValue = val;

                m_log.InfoFormat("[PERMISSIONS] Forced permissions to {0} in {1}", m_bypassPermissionsValue, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleDebugPermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_debugPermissions = val;

                m_log.InfoFormat("[PERMISSIONS] Set permissions debugging to {0} in {1}", m_debugPermissions, m_scene.RegionInfo.RegionName);
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
            get { return "PermissionsModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region Helper Functions
        protected void SendPermissionError(UUID user, string reason)
        {
            m_scene.EventManager.TriggerPermissionError(user, reason);
        }
        
        protected void DebugPermissionInformation(string permissionCalled)
        {
            if (m_debugPermissions)
                m_log.Debug("[PERMISSIONS]: " + permissionCalled + " was called from " + m_scene.RegionInfo.RegionName);
        }
    
        // Checks if the given group is active and if the user is a group member
        // with the powers requested (powers = 0 for no powers check)
        protected bool IsGroupActiveRole(UUID groupID, UUID userID, ulong powers)
        {
            ScenePresence sp = m_scene.GetScenePresence(userID);
            if (sp == null)
                return false;

            IClientAPI client = sp.ControllingClient;
            return ((groupID == client.ActiveGroupId) && (client.ActiveGroupPowers != 0) &&
                ((powers == 0) || ((client.ActiveGroupPowers & powers) == powers)));
        }


        /// <summary>
        /// Maximum size for the group power cache
        /// </summary>
        private const int MAX_POWER_CACHE_SIZE = 250;

        /// <summary>
        /// Number of seconds before an item in the cache is no longer considered viable
        /// </summary>
        private const int POWER_CACHE_ITEM_EXPIRY = 15;  // seconds

        private LRUCache<Tuple<UUID, UUID>, TimestampedItem<ulong?>> m_cachedGroupPowers
            = new LRUCache<Tuple<UUID, UUID>, TimestampedItem<ulong?>>(MAX_POWER_CACHE_SIZE);

        // client can be null here, but it's more costly to look it up then (so use the cache)
        // returns null if the user isn't in the group at all (or other error)
        protected ulong? GetGroupPowersOrNull(IClientAPI client, UUID userID, UUID groupID)
        {
            // First use the client group powers if there's an agent.
            if (client == null)
            {
                ScenePresence sp = m_scene.GetScenePresence(userID);
                if (sp != null)
                {
                    client = sp.ControllingClient;
                }
            }
            if (client != null)
            {
                return client.GetGroupPowersOrNull(groupID);
            }

            // If no agent available, try the cache.
            ulong? cachedPowers;
            if (TryFindCachedGroupPowers(groupID, userID, out cachedPowers))
            {
                if (cachedPowers.HasValue)
                {
                    return cachedPowers.Value;
                }
            }

            IGroupsModule groupsModule = m_scene.RequestModuleInterface<IGroupsModule>();
            if (groupsModule == null)
                return 0;

            GroupMembershipData[] groupMembership = groupsModule.GetMembershipData(userID);
            if (groupMembership == null)
                return 0;

            //Make sure they are in the group
            foreach(var grp in groupMembership)
            {
                if (grp.GroupID == groupID)
                {
                    CacheGroupPower(ref groupID, ref userID, grp.GroupPowers);
                    return grp.GroupPowers;
                }
            }

            //negative cache
            CacheGroupPower(ref groupID, ref userID, null);
            return 0;
        }
        // This variant should only be used when comparing against a specific group ability (see HasGroupPower below).
        protected ulong GetGroupPowers(IClientAPI client, UUID userID, UUID groupID)
        {
            return GetGroupPowersOrNull(client, userID, groupID) ?? 0;
        }

        // This returns false if the user does not have the power or is not a group member.
        // Pass 0 for power if you only want to check group membership.
        protected bool HasGroupPower(IClientAPI client, UUID userID, UUID groupID, ulong power)
        {
            ulong? powers = GetGroupPowersOrNull(client, userID, groupID);
            if (!powers.HasValue)
                return false;   // not even a group member

            if (power == 0) // group membership
                return true;

            return (powers.Value & power) != 0;
        }

        // Same as above but where the group doesn't need to be active
        // The user must still be visible to this region as a ScenePresence.
        protected bool IsAgentInGroupRole(UUID groupID, UUID userID, ulong powers)
        {
            return HasGroupPower(null, userID, groupID, powers);
        }

        private void CacheGroupPower(ref UUID groupID, ref UUID userID, ulong? grpPower)
        {
            lock (m_cachedGroupPowers)
            {
                m_cachedGroupPowers.Add(new Tuple<UUID, UUID>(groupID, userID), new TimestampedItem<ulong?>(grpPower));
            }
        }

        private bool TryFindCachedGroupPowers(UUID groupID, UUID userID, out ulong? powers)
        {
            //try the cache
            lock (m_cachedGroupPowers)
            {
                var groupAndUser = new Tuple<UUID, UUID>(groupID, userID);

                TimestampedItem<ulong?> cachedPowers;
                if (m_cachedGroupPowers.TryGetValue(groupAndUser, out cachedPowers))
                {
                    if (cachedPowers.ElapsedSeconds < POWER_CACHE_ITEM_EXPIRY)
                    {
                        powers = cachedPowers.Item;
                        return true;
                    }
                    else
                    {
                        m_cachedGroupPowers.Remove(groupAndUser);
                    }
                }

                powers = 0;
                return false;
            }
        }

        /// <summary>
        /// Parse a user set configuration setting
        /// </summary>
        /// <param name="config"></param>
        /// <param name="settingName"></param>
        /// <param name="defaultValue">The default value for this attribute</param>
        /// <returns>The parsed value</returns>
        private static UserSet ParseUserSetConfigSetting(IConfig config, string settingName, UserSet defaultValue)
        {
            UserSet userSet = defaultValue;
            
            string rawSetting = config.GetString(settingName, defaultValue.ToString());
            
            // Temporary measure to allow 'gods' to be specified in config for consistency's sake.  In the long term
            // this should disappear.
            if ("gods" == rawSetting.ToLower())
                rawSetting = UserSet.Administrators.ToString();
            
            // Doing it this was so that we can do a case insensitive conversion
            try
            {
                userSet = (UserSet)Enum.Parse(typeof(UserSet), rawSetting, true);
            }
            catch 
            {
                m_log.ErrorFormat(
                    "[PERMISSIONS]: {0} is not a valid {1} value, setting to {2}",
                    rawSetting, settingName, userSet);
            }            
            
            m_log.DebugFormat("[PERMISSIONS]: {0} {1}", settingName, userSet);
            
            return userSet;
        }

        /// <summary>
        /// Is the given user an a god level user?
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected bool IsGodUser(UUID user)
        {
            if (user == UUID.Zero) return false;
        
            /*if (m_scene.RegionInfo.MasterAvatarAssignedUUID != UUID.Zero)
            {
                if (m_RegionOwnerIsGod && (m_scene.RegionInfo.MasterAvatarAssignedUUID == user))
                    return true;
            }
            */
            
            if (m_allowGridGods)
            {
                UserProfileData profile = m_scene.CommsManager.UserService.GetUserProfile(user);
                return (profile != null) && (profile.GodLevel >= 200);
            }

            return false;
        }
#endregion

        public bool PropagatePermissions()
        {
            if (m_bypassPermissions)
                return false;

            return m_propagatePermissions;
        }

        public bool BypassPermissions()
        {
            return m_bypassPermissions;
        }

        public void SetBypassPermissions(bool value)
        {
            m_bypassPermissions=value;
        }

        #region Object Permissions

        public bool FriendHasEditPermission(UUID objectOwner, UUID requestingFriend, bool fastCheck)
        {
            // There's one easy optimization we should ensure isn't the case before proceeding further.
            if (requestingFriend == objectOwner)
                return true;

            return m_scene.CommsManager.UserService.UserHasFriendPerms(requestingFriend, objectOwner, (uint)FriendRights.CanModifyObjects, fastCheck);
        }

        public uint GenerateClientFlags(UUID user, UUID objID, bool fastCheck)
        {
            // Here's the way this works,
            // ObjectFlags and Permission flags are two different enumerations
            // ObjectFlags, however, tells the client to change what it will allow the user to do.
            // So, that means that all of the permissions type ObjectFlags are /temporary/ and only
            // supposed to be set when customizing the objectflags for the client.

            // These temporary objectflags get computed and added in this function based on the
            // Permission mask that's appropriate!
            // Outside of this method, they should never be added to objectflags!
            // -teravus

            SceneObjectPart task = m_scene.GetSceneObjectPart(objID);

            // this shouldn't ever happen..     return no permissions/objectflags.
            if (task == null)
                return (uint)0;

            uint baseflags = (uint)task.GetEffectiveObjectFlags();    // folded (PrimFlags) type, not PermissionsMask
            UUID objectOwner = task.OwnerID;
            bool isOwner = false;

            // Remove any of the objectFlags that are temporary.  
            // These will get added back if appropriate in the next bit of code
            baseflags &= (uint)
                ~(PrimFlags.ObjectCopy            | // Tells client you can copy the object
                  PrimFlags.ObjectModify        | // tells client you can modify the object
                  PrimFlags.ObjectMove            | // tells client that you can move the object (only, no mod)
                  PrimFlags.ObjectTransfer        | // tells the client that you can /take/ the object if you don't own it
                  PrimFlags.ObjectYouOwner        | // Tells client that you're the owner of the object
                  PrimFlags.ObjectOwnerModify    | // Tells client that you're the owner of the object
                  PrimFlags.ObjectYouOfficer      // Tells client that you've got group object editing permission. Used when ObjectGroupOwned is set
                    );

            // Start by calculating the common/base rights to apply to everyone including the owner.
            // Add bits in as rights allow, then make an override pass to turn off bits as needed at the end.

            // Only remove any owner if the object actually doesn't have any owner
            if (objectOwner == UUID.Zero)
            {
                baseflags &= (uint)~PrimFlags.ObjectAnyOwner;
            }
            else
            {
                //there is an owner, make sure the bit is set
                baseflags |= (uint)PrimFlags.ObjectAnyOwner;
                if (user == objectOwner)
                    isOwner = true;
            }

            // Start with a mask for the owner and a friend with Edit perms.
            uint objflags = AddClientFlags(task.OwnerMask, baseflags);    // common flags for those who can edit
            // Object owners edit their own content unrestricted by other user checks
            if (isOwner)
            {
                // Add the owner-specific flags to the OwnerMask
                return objflags | (uint)PrimFlags.ObjectYouOwner | (uint)PrimFlags.ObjectOwnerModify;
            }

            ScenePresence sp = m_scene.GetScenePresence(user);
            // bots don't have friends let alone friend edit perms, skip the costly checks.
            if (sp != null && !sp.IsBot)
            {
                if (m_bypassPermissions || // no perms checks
                    sp.GodLevel >= 200 || // Admin should be able to edit anything else in the sim (including admin objects)
                    FriendHasEditPermission(objectOwner, user, fastCheck)) // friend with permissions
                {
                    return RestrictClientFlags(task, objflags);    // minimal perms checks, act like owner
                }
            }

            /////////////////////////////////////////////////////////////
            // No returns from the function after this, now we add flags,
            // then apply retriction overrides when returning.
            // Not the owner, or a friend with Edit permissions, or admin,
            // so start again. Reset again to baseflags, and start adding
            // Note that more than one test may apply,
            // "else" or "return" isn't necessarily correct
            objflags = AddClientFlags(task.EveryoneMask, baseflags);

            if (task.OwnerID != UUID.Zero)
                objflags |= (uint)PrimFlags.ObjectAnyOwner;
            if (m_scene.IsLandOwner(user, task.AbsolutePosition))
            {
                // On Plus regions, non-EO parcel owners users can only move their own objects
                if ((task.OwnerID == user) || m_scene.IsEstateManager(user) || (m_scene.RegionInfo.Product != ProductRulesUse.PlusUse))
                    objflags |= (uint)PrimFlags.ObjectMove;
            }

            if (HasGroupPermission(user, task.AbsolutePosition, 0))
            {
                objflags |= GenerateGroupLandFlags(user, task.AbsolutePosition, objflags, task.EveryoneMask);
            }

            // Group permissions
            if ((task.GroupID != UUID.Zero) && IsAgentInGroupRole(task.GroupID, user, 0))
            {
                objflags |= AddClientFlags(task.GroupMask, objflags);
            }

            return RestrictClientFlags(task, objflags);
        }

        private uint GenerateGroupLandFlags(UUID user, Vector3 parcelLocation, uint objflags, uint objectEveryoneMask)
        {
            if (user == UUID.Zero) return objectEveryoneMask;

            ILandObject parcel = m_scene.LandChannel.GetLandObject(parcelLocation.X, parcelLocation.Y);
            if (parcel == null) return objectEveryoneMask;

            if (!parcel.landData.IsGroupOwned)
            {
                return objectEveryoneMask;
            }

            bool assignedPerms = false;
            if (IsAgentInGroupRole(parcel.landData.GroupID, user, (ulong)GroupPowers.ReturnGroupOwned))
            {
                objflags |= (uint)PrimFlags.ObjectAnyOwner ;
                assignedPerms = true;
            }

            if (IsAgentInGroupRole(parcel.landData.GroupID, user, (ulong)GroupPowers.ReturnNonGroup))
            {
                objflags |= (uint)PrimFlags.ObjectAnyOwner ;
                assignedPerms = true;
            }

            if (!assignedPerms) return objectEveryoneMask;
            else return objflags;
        }

        // Adds flags to the second parameter (PrimFlags) based on the first parameter (PermissionsMask) and returns the result.
        // Accepts collections of PermissionsMask bits and PrimFlags bits, respectively.
        // Returns a collection of PrimFlags bits, not PermissionsMask bits.
        private uint AddClientFlags(uint permissionMask, uint primFlags)
        {
            // We are adding the temporary objectflags to the object's objectflags based on the
            // permission flag given.  These change the F flags on the client.
            if ((permissionMask & (uint)PermissionMask.Copy) != 0)
            {
                primFlags |= (uint)PrimFlags.ObjectCopy;
            }

            if ((permissionMask & (uint)PermissionMask.Move) != 0)
            {
                primFlags |= (uint)PrimFlags.ObjectMove;
            }

            if ((permissionMask & (uint)PermissionMask.Modify) != 0)
            {
                primFlags |= (uint)PrimFlags.ObjectModify;
            }

            if ((permissionMask & (uint)PermissionMask.Transfer) != 0)
            {
                primFlags |= (uint)PrimFlags.ObjectTransfer;
            }

            return primFlags;
        }

        // Filters out flags from the second parameter (task (SOP) PrimFlags) based on the first parameter (PermissionsMask) and returns the result.
        // Intended to be applied to flags other than the owner
        // Accepts and returns collections of PrimFlags bits, further filtering the ones returned in the function above.
        private uint RestrictClientFlags(SceneObjectPart task, uint primFlags)
        {
            // Continue to reduce the folded perms as appropriate for friends with Edit and others
            if ((task.OwnerMask & (uint)PermissionMask.Transfer) == 0)
            {
                // without transfer, a friend with edit cannot copy or transfer
                primFlags &= ~(uint)PrimFlags.ObjectCopy;
                primFlags &= ~(uint)PrimFlags.ObjectTransfer;
            }
            else
            if ((task.OwnerMask & (uint)PermissionMask.Copy) == 0)    // Transfer but it is no-copy
            {
                // don't allow a friend with edit to take the only copy
                primFlags &= ~(uint)PrimFlags.ObjectCopy;
                primFlags &= ~(uint)PrimFlags.ObjectTransfer;
            }

            if (task.IsAttachment)    // attachment and not the owner
            {
                // Disable others editing the owner's attachments
                primFlags &= (uint)~(PrimFlags.ObjectModify | PrimFlags.ObjectMove);
            }

            return primFlags;
        }

        protected bool HasReturnPermission(UUID currentUser, UUID objId, bool denyOnLocked)
        {
            SceneObjectGroup group = FindGroup(objId);
            if (group == null)
                return false;

            //admins can do what they need
            if (IsGodUser(currentUser))
            {
                return true;
            }

            //object owners are always allowed to return objects to themselves
            if (group.OwnerID == currentUser)
            {
                return true;
            }

            //master avatars can make returns of anything on their regions
            if (m_scene.IsMasterAvatar(currentUser))
            {
               return true;
            }

            if (m_scene.RegionInfo.Product == ProductRulesUse.PlusUse)
            {
                // On a Plus region, only the EO (InWorldz Mainland) can return objects owned by that EO.
                // We already checked for object owner deleting above, now check for others deleting EO stuff.
                if (m_scene.IsEstateOwner(group.OwnerID))
                    return false;
            }
            
            //estate owners/managers can return anything on their estates
            if (m_scene.IsEstateOwner(currentUser) || m_scene.IsEstateManager(currentUser))
            {
                return true;
            }

            //parcel owners can return anything on the region
            if (m_scene.IsParcelOwner(currentUser, group.AbsolutePosition))
            {
                // Plus parcels do not provide owner with extra return rights
                if (m_scene.RegionInfo.Product != ProductRulesUse.PlusUse)
                    return true;
            }

            //if the land is deeded to group, a group member with the proper permissions
            //can return an object on the deeded land
            if (HasGroupPermission(currentUser, group.AbsolutePosition, GroupPowers.ReturnNonGroup))
            {
                // Group role allows return here, except we'll add an exception.
                // Deny the return if the object is locked and owned by the EO/EM.
                bool notLocked = ((group.RootPart.OwnerMask & (uint)PermissionMask.Move) != 0);
                bool normalOwner = !(m_scene.IsEstateManager(group.OwnerID) || m_scene.IsEstateOwner(group.OwnerID));
                if (notLocked || normalOwner)
                        return true;
            }
            
            return false;
        }

        bool HasGroupPermission(UUID userId, Vector3 parcelLocation, GroupPowers powerRequested)
        {
            if (userId == UUID.Zero) return false;

            ILandObject parcel = m_scene.LandChannel.GetLandObject(parcelLocation.X, parcelLocation.Y);
            if (parcel == null) return false;

            if (parcel.landData.IsGroupOwned && IsAgentInGroupRole(parcel.landData.GroupID, userId, (ulong)powerRequested))
            {
                return true;
            }

            return false;
        }

        private SceneObjectGroup FindObjectGroup(UUID objId)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(objId);
            if (part != null)
                return part.ParentGroup;
            return null;
        }

        protected SceneObjectGroup FindGroup(UUID objId)
        {
            EntityBase ent = null;
            if (!m_scene.Entities.TryGetValue(objId, out ent))
                return null;

            // If it's not an object, we cant edit it.
            if (ent is SceneObjectGroup)
                return ent as SceneObjectGroup;

            return null;
        }

        protected struct GenericPermissionResult
        {
            public enum ResultReason
            {
                Basics,
                NoPerm,
                Locked,
                Owner,
                Attachment,
                FriendEdit,
                Group,
                Admin,
                Other
            }

            public bool Success;
            public ResultReason Reason; 
        }

        /// <summary>
        /// General permissions checks for any operation involving an object.  These supplement more specific checks
        /// implemented by callers.
        /// </summary>
        /// <param name="currentUser"></param>
        /// <param name="objId"></param>
        /// <param name="denyOnLocked"></param>
        /// <returns></returns>
        protected GenericPermissionResult GenericObjectPermission(UUID currentUser, UUID objId, bool denyOnLocked, uint requiredPermissionMask)
        {
            SceneObjectGroup group = FindGroup(objId);
            if (group == null)
            {
                return new GenericPermissionResult 
                { 
                    Reason = GenericPermissionResult.ResultReason.Basics, 
                    Success = false 
                };
            }

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsGodUser(currentUser))
            {
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.Admin,
                    Success = true
                };
            }

            // People shouldn't be able to do anything with locked objects, except the Administrator
            // The 'set permissions' runs through a different permission check, so when an object owner
            // sets an object locked, the only thing that they can do is unlock it.
            //
            // Nobody but the object owner can set permissions on an object
            //
            bool locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);   // only locked when neither bit is on
            if (locked && denyOnLocked)
            {
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.Locked,
                    Success = false
                };
            }

            if ((group.RootPart.OwnerMask & requiredPermissionMask) != requiredPermissionMask)
            {
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.NoPerm,
                    Success = false
                };
            }

            UUID objectOwner = group.OwnerID;
            if (currentUser == objectOwner)
            {
                // Object owners should be able to edit their own content
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.Owner,
                    Success = true
                };
            }

            if (group.IsAttachment)
            {
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.Attachment,
                    Success = false
                };
            }

            if (FriendHasEditPermission(objectOwner, currentUser, false))
            {
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.FriendEdit,
                    Success = true
                };
            }

            // Group members should be able to edit group objects
            if ((group.GroupID != UUID.Zero) && ((group.RootPart.GroupMask & (uint)PermissionMask.Modify) == (uint)PermissionMask.Modify) && IsAgentInGroupRole(group.GroupID, currentUser, 0))
            {
                // Return immediately, so that the administrator can shares group objects
                return new GenericPermissionResult
                {
                    Reason = GenericPermissionResult.ResultReason.Group,
                    Success = true
                };
            }

            return new GenericPermissionResult
            {
                Reason = GenericPermissionResult.ResultReason.NoPerm,
                Success = false
            };
        }

        #endregion

        #region Generic Permissions
        protected bool GenericCommunicationPermission(UUID user, UUID target)
        {
            // Setting this to true so that cool stuff can happen until we define what determines Generic Communication Permission
            bool permission = true;
            // string reason = "Only registered users may communicate with another account.";

            // Uhh, we need to finish this before we enable it..   because it's blocking all sorts of goodies and features
            /*if (IsAdministrator(user))
                permission = true;

            if (IsEstateManager(user))
                permission = true;

            if (!permission)
                SendPermissionError(user, reason);
            */
            //
            // !! RIGHT NOW ANYONE CAN IM/CONTACT ANYONE ELSE.  THERE IS NO SENSE IN LOADING PROFILES !!
            //

            return permission;
        }

        public bool GenericEstatePermission(UUID user)
        {
            // Estate admins should be able to use estate tools
            if (m_scene.IsEstateManager(user))
                return true;

            // Administrators always have permission
            return IsGodUser(user);
        }

        protected bool GenericParcelPermission(UUID user, ILandObject parcel, ulong groupPowers)
        {
            if (parcel.landData.OwnerID == user)
                return true;

            if ((parcel.landData.GroupID != UUID.Zero) && IsAgentInGroupRole(parcel.landData.GroupID, user, groupPowers))
                return true;

            if (m_scene.IsEstateManager(user))
                return true;

            if (IsGodUser(user))
                return true;

            return false;
        }
    
        protected bool GenericParcelOwnerPermission(UUID user, ILandObject parcel, ulong groupPowers)
        {
            if (parcel == null)
                return false;

            // First the simple check, calling context matches the land owner.
            // This also includes group-deeded objects on group-deeded land.
            if (parcel.landData.OwnerID == user)
                return true;

            if (parcel.landData.IsGroupOwned)
            {
                // Normally the check above should have matched in the case of group-owned land, but this extra
                // check avoids the inconsistencies with group-owned land where the ownerID is not set to the group.
                if (parcel.landData.GroupID == user)
                    return true;

                // The context is not a matching group, let's see if it's a user context that is allowed on group land
                if (IsAgentInGroupRole(parcel.landData.GroupID, user, groupPowers))
                    return true;
            }
            else
            {   // not group-owned land

                if ((parcel.landData.GroupID != UUID.Zero) && (groupPowers == (ulong)GroupPowers.AllowSetHome))
                {
                    // AllowSetHome has a special exception. It doesn't need to be group-owned land, just group-tagged.
                    if (IsAgentInGroupRole(parcel.landData.GroupID, user, (ulong)GroupPowers.AllowSetHome))
                        return true;
                }
            }

            if (m_scene.IsEstateManager(user))
                return true;

            if (IsGodUser(user))
                return true;

            return false;
        }

        protected bool GenericParcelPermission(UUID user, Vector3 pos, ulong groupPowers)
        {
            ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
            if (parcel == null) return false;
            return GenericParcelPermission(user, parcel, groupPowers);
        }
#endregion

        #region Permission Checks
        private bool CanAbandonParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;
        
            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandRelease);
        }

        private bool CanReclaimParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericEstatePermission(user);
        }

        private bool CanDeedParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (parcel.landData.OwnerID != user) // Only the owner can deed!
                return false;

            if (!m_scene.RegionInfo.AllowDeeding) // Land transfer based on ProductUseRules
                return false;

            ScenePresence sp = scene.GetScenePresence(user);
            IClientAPI client = sp.ControllingClient;

            if ((client.GetGroupPowers(parcel.landData.GroupID) & (ulong)GroupPowers.LandDeed) == 0)
                return false;

            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandDeed);
        }

        private bool CanDeedObject(UUID user, UUID group, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            ScenePresence sp = scene.GetScenePresence(user);
            IClientAPI client = sp.ControllingClient;

            if((client.GetGroupPowers(group) & (ulong)GroupPowers.DeedObject) == 0)
                return false;

            return true;
        }

        private bool IsGod(UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return IsGodUser(user);
        }

        private bool CanDuplicateObject(int landImpact, UUID objectID, UUID owner, Scene scene, Vector3 objectPosition)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            GenericPermissionResult genericResult = GenericObjectPermission(owner, objectID, true, (uint)PermissionMask.Copy);
            if (!genericResult.Success)
            {
                //They can't even edit the object
                return false;
            }

            SceneObjectPart part = scene.GetSceneObjectPart(objectID);
            if (part == null)
                return false;

            uint perms = part.ParentGroup.GetEffectivePermissions(true);
            if ((perms & (uint)PermissionMask.Copy) != (uint)PermissionMask.Copy)
                return false;   // the object is no-copy or has something no-copy inside
            if (part.OwnerID != owner)  // someone else is interested in this
                if ((perms & (uint)PermissionMask.Transfer) != (uint)PermissionMask.Transfer)
                    return false;   // the object is no-trans or has something no-trans inside

            // If they don't have friend edit perms, they still need more permissions from something else.
            if (genericResult.Reason != GenericPermissionResult.ResultReason.FriendEdit)
            {
                if ((part.OwnerID != owner) && ((part.EveryoneMask & PERM_COPY) != PERM_COPY))
                {
                    // Not the owner, not set to allow anyone to copy, las thing is if they are in the group
                    if (part.GroupID == UUID.Zero)
                        return false;   //  no group set
                    if (!IsAgentInGroupRole(part.GroupID, owner, 0))
                        return false;
                    if ((part.GroupMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                    if ((part.GroupMask & (uint)PermissionMask.Copy) == 0)
                        return false;
                    if ((part.OwnerID == part.GroupID) && ((owner != part.LastOwnerID) || ((part.GroupMask & (uint)PermissionMask.Transfer) == 0)))
                        return false;
                }
            }

            //If they can rez, they can duplicate
            return CanRezObject(landImpact, owner, UUID.Zero, objectPosition, false, scene);
        }

        private bool CanDeleteObject(UUID objectID, UUID deleter, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(deleter, objectID, false, 0).Success;
        }

        private bool CanEditObject(UUID objectID, UUID editorID, Scene scene, uint requiredPermissionMask)
        {
            //DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // This could test (uint)PermissionMask.Modify but CanEditObject is used for a lot more.
            return GenericObjectPermission(editorID, objectID, false, requiredPermissionMask).Success;
        }

        private bool CanEditObjectInventory(UUID objectID, UUID editorID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);

            // If we selected a sub-prim to edit, the objectID won't represent the object, but only a part.
            // We have to check the permissions of the group, though.
            if (part.ParentGroup != null)
            {
                objectID = part.ParentGroup.UUID;
                part = m_scene.GetSceneObjectPart(objectID);
            }

            return GenericObjectPermission(editorID, objectID, false, (uint)PermissionMask.Modify).Success;
        }

        private bool CanEditParcel(UUID user, ILandObject parcel, GroupPowers p, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, (ulong)p);
        }

        /// <summary>
        /// Check whether the specified user can edit the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanEditScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;
            
            // Ordinarily, if you can view it, you can edit it
            // There is no viewing a no mod script
            //
            return CanViewScript(script, objectID, user, scene);
        }

        /// <summary>
        /// Check whether the specified user can edit the given notecard
        /// </summary>
        /// <param name="notecard"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>        
        private bool CanEditNotecard(UUID notecard, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                CachedUserInfo userInfo =
                        scene.CommsManager.UserService.GetUserDetails(user);
            
                if (userInfo == null)
                {
                    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for edit notecard check", user);
                    return false;
                }                                

                InventoryItemBase assetRequestItem = userInfo.FindItem(notecard);
                if (assetRequestItem == null) // Library item
                {
                    assetRequestItem = scene.CommsManager.LibraryRoot.FindItem(notecard);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // Notecards must be both mod and copy to be saveable
                // This is because of they're not copy, you can't read
                // them, and if they're not mod, well, then they're
                // not mod. Duh.
                //
                if ((assetRequestItem.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;

                if (part.OwnerID != user)
                {
                    if (part.GroupID == UUID.Zero)
                        return false;

                    if( !IsAgentInGroupRole(part.GroupID, user, 0) )
                        return false;
            
                    if ((part.GroupMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                } else {
                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;
                }

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                {
                    if (ti.GroupID == UUID.Zero)
                        return false;

                    if( !IsAgentInGroupRole(ti.GroupID, user, 0) )
                    return false;
                }

                // Require full perms
                if ((ti.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy))
                    return false;
            }

            return true;
        }

        private bool CanInstantMessage(UUID user, UUID target, Scene startScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // If the sender is an object, check owner instead
            //
            SceneObjectPart part = startScene.GetSceneObjectPart(user);
            if (part != null)
                user = part.OwnerID;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanInventoryTransfer(UUID user, UUID target, Scene startScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanIssueEstateCommand(UUID user, Scene requestFromScene, bool ownerCommand)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (IsGodUser(user))
                return true;

            if (m_scene.RegionInfo.EstateSettings.IsEstateOwner(user))
                return true;

            if (ownerCommand)
                return false;

            return GenericEstatePermission(user);
        }

        private bool CanMoveObject(UUID objectID, UUID moverID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions)
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);
                if (part.OwnerID != moverID)
                {
                    if (part.ParentGroup != null && !part.ParentGroup.IsDeleted)
                    {
                        if (part.ParentGroup.IsAttachment)
                            return false;
                    }
                }
                return m_bypassPermissionsValue;
            }

            bool permission = GenericObjectPermission(moverID, objectID, true, (uint)PermissionMask.Move).Success;
            if (!permission)
            {
                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // The client
                // may request to edit linked parts, and therefore, it needs
                // to also check for SceneObjectPart

                // If it's not an object, we cant edit it.
                if ((!(m_scene.Entities[objectID] is SceneObjectGroup)))
                {
                    return false;
                }


                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

                if (m_scene.RegionInfo.Product == ProductRulesUse.PlusUse)
                {
                    // On a Plus region, only the EO (InWorldz Mainland) can move objects owned by that EO.
                    if (task.OwnerID == moverID)
                        return true;
                    if (m_scene.IsEstateOwner(task.OwnerID))
                        return false;
                }

                // Estate managers and land owners
                if (m_scene.IsLandOwner(moverID, task.AbsolutePosition))
                {
                    permission = true;
                }

                // Note: ObjectManipulate has *nothing* to do with the current parcel.
                // ObjectManipulate refers to the current object, and if it is DEEDED to this group.
                // It means you can manipulate GROUP-OWNED objects.
                if ((task.OwnerID == task.GroupID) && IsAgentInGroupRole(task.GroupID, moverID, (ulong)GroupPowers.ObjectManipulate))
                {
                    permission = true;
                }

                // Anyone can move
                if ((task.RootPart.EveryoneMask & PERM_MOVE) != 0)
                    permission = true;

                // Locked
                if ((task.RootPart.OwnerMask & PERM_LOCKED) == 0)
                    permission = false;
            }
            else
            {
                bool locked = false;
                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // If it's not an object, we cant edit it.
                if ((!(m_scene.Entities[objectID] is SceneObjectGroup)))
                {
                    return false;
                }

                SceneObjectGroup group = (SceneObjectGroup)m_scene.Entities[objectID];

                UUID objectOwner = group.OwnerID;
                locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);

                // This is an exception to the generic object permission.
                // Administrators who lock their objects should not be able to move them,
                // however generic object permission should return true.
                // This keeps locked objects from being affected by random click + drag actions by accident
                // and allows the administrator to grab or delete a locked object.

                // Administrators and estate managers are still able to click+grab locked objects not
                // owned by them in the scene
                // This is by design.

                if (locked && (moverID == objectOwner))
                    return false;

                if (m_scene.RegionInfo.Product == ProductRulesUse.PlusUse)
                {
                    // On a Plus region, only the EO (InWorldz Mainland) can move objects owned by that EO.
                    if (group.OwnerID == moverID)
                        return true;
                    if (m_scene.IsEstateOwner(group.OwnerID))
                        return false;
                }
            }
            return permission;
        }

        private bool CanObjectEntry(UUID objectID, bool enteringRegion, Vector3 newPoint, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if ((newPoint.X >= Constants.RegionSize || newPoint.X < 0.0f || newPoint.Y >= Constants.RegionSize || newPoint.Y < 0.0f))
            {
                return true;
            }

            SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

            ILandObject land = m_scene.LandChannel.GetLandObject(newPoint.X, newPoint.Y);

            if (!enteringRegion)
            {
                ILandObject fromland = m_scene.LandChannel.GetLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y);

                if (fromland == land) // Not entering
                    return true;
            }

            if (land == null)
            {
                return false;
            }

            if ((land.landData.Flags & (uint)ParcelFlags.AllowAPrimitiveEntry) != 0)
            {
                return true;
            }

            if (!m_scene.Entities.ContainsKey(objectID))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[objectID] is SceneObjectGroup))
            {
                return false;
            }


            if (GenericParcelPermission(task.OwnerID, newPoint, 0))
            {
                return true;
            }

            //Otherwise, false!
            return false;
        }

        private bool CanReturnObject(UUID objectID, UUID returnerID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return HasReturnPermission(returnerID, objectID, false);
        }

        // If objectID is specified (not UUID.Zero), then that object (owned by "owner") is trying to rez another object.
        // pass 0 for landImpact if you do not want this method to do any Land Impact limit checks.
        private bool CheckRezPerms(ILandObject parcel, UUID owner, UUID objectID, Vector3 objectPosition, bool isTemp, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // User or object owner is an admin?
            if (IsGodUser(owner))
                return true;    // we're done here, no need to check more

            // This code block handles the Scenic option of EO (and partner) only.
            if (scene.RegionInfo.AllowRezzers == ProductRulesWho.OnlyEO)
            {
                // only the estate OWNER or PARTNER can rez in scenic regions unless it's a temprez object
                if (scene.IsEstateOwner(owner))
                    return true;
                if (scene.RegionInfo.AllowPartnerRez)
                    if (scene.IsEstateOwnerPartner(owner))
                        return true;
                if (!isTemp)    // non-temp rezzing not allowed
                    return false;
            }

            // At this point, we need to test a specific parcel.
            if (parcel == null) return false;

            // Is this a Plus region?
            if (scene.RegionInfo.Product == ProductRulesUse.PlusUse)
            {
                // If this is an unclaimed Plus parcel (or community land)
                if (scene.IsEstateOwner(parcel.landData.OwnerID))
                    return scene.IsEstateManager(owner);    // only EM can rez
                // Else only the Plus user (and possibly partner) can rez in a Plus parcel.
                if (owner == parcel.landData.OwnerID)
                    return true;
                // Otherwise, only the Plus user's partner can rez in a Plus parcel.
                if (scene.RegionInfo.AllowPartnerRez)
                {
                    UserProfileData parcelOwner = m_scene.CommsManager.UserService.GetUserProfile(parcel.landData.OwnerID);
                    if (parcelOwner != null)
                        if (parcelOwner.Partner != UUID.Zero)
                            if (parcelOwner.Partner == owner)
                                return true;
                }
                return false;
            }

            // Create enabled for everyone?
            if ((parcel.landData.Flags & ((int)ParcelFlags.CreateObjects)) == (uint)ParcelFlags.CreateObjects)
                return true;    // we're done here, no need to check more

            ulong roleNeeded = (ulong)GroupPowers.AllowRez;
            if ((parcel.landData.Flags & (uint)ParcelFlags.CreateGroupObjects) == (uint)ParcelFlags.CreateGroupObjects)
                roleNeeded = (ulong)GroupPowers.None; // if group rezzing is enabled, we just need to be a member, no specific role ability

            // Is an object rezzing the other object?
            if (objectID != UUID.Zero)
            {   // A scripted object is doing the rezzing...
                SceneObjectGroup group = this.FindObjectGroup(objectID);
                if (group == null) return false;

                // Now continue the rezzing permissions checks
                if (group.OwnerID == parcel.landData.OwnerID)
                    return true;
                // Owner doesn't match the land parcel, check partner perms.
                if (scene.RegionInfo.AllowPartnerRez)
                {   // this one is will not be called based on current products (Scenic, Plus) but completes the rule set for objects.
                    UserProfileData parcelOwner = m_scene.CommsManager.UserService.GetUserProfile(parcel.landData.OwnerID);
                    if (parcelOwner != null)
                        if (parcelOwner.Partner != UUID.Zero)
                            if (parcelOwner.Partner == group.OwnerID)
                                return true;
                }

                // Owner doesn't match the land parcel, check group perms.
                UUID activeGroupId = group.GroupID;
                // Handle the special case of active group for a worn attachment
                if (group.RootPart.IsAttachment)
                {
                    ScenePresence sp = m_scene.GetScenePresence(group.RootPart.AttachedAvatar);
                    if (sp == null)
                        return false;
                    IClientAPI client = sp.ControllingClient;
                    activeGroupId = client.ActiveGroupId;
                }

                // See if the agent or object doing the rezzing has permission itself.
                if ((parcel.landData.Flags & (uint)ParcelFlags.CreateGroupObjects) == (uint)ParcelFlags.CreateGroupObjects)
                {
                    if (activeGroupId == parcel.landData.GroupID)
                        return true;
                }

                // Create role enabled for this group member on group land?
                if (GenericParcelPermission(group.OwnerID, parcel, roleNeeded))
                    return true;    // we're done here, no need to check more

                return false;
            }

            // An agent/avatar is doing the rezzing...

            // Create role enabled for this group member on group land, or parcel is group-enabled?
            if (GenericParcelPermission(owner, parcel, roleNeeded))
                return true;    // we're done here, no need to check more

            // Owner doesn't match the land parcel, check partner perms.
            if (scene.RegionInfo.AllowPartnerRez)
            {
                // this one is will not be called based on current products (Scenic, Plus) but completes the rule set for the remaining cases.
                UserProfileData parcelOwner = m_scene.CommsManager.UserService.GetUserProfile(parcel.landData.OwnerID);
                if (parcelOwner != null)
                    if (parcelOwner.Partner != UUID.Zero)
                        if (parcelOwner.Partner == owner)
                            return true;
            } 
            
            return false;
        }

        // If objectID is specified (not UUID.Zero), then that object (owned by "owner") is trying to rez another object.
        // pass 0 for landImpact if you do not want this method to do any Land Impact limit checks.
        private bool CanRezObject(int landImpact, UUID owner, UUID objectID, Vector3 objectPosition, bool isTemp, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            ILandObject parcel = m_scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);

            if (!CheckRezPerms(parcel, owner, objectID, objectPosition, isTemp, scene))
                return false;

            // Optional test for land impact limits.
            if (landImpact > 0)
            {
                string reason = String.Empty;
                return m_scene.CheckLandImpact(parcel, landImpact, out reason);
            }

            return true;
        }

        private bool CanRunConsoleCommand(UUID user, Scene requestFromScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;


            return IsGodUser(user);
        }

        private bool CanRunScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanTakeObject(UUID objectID, UUID stealer, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            GenericPermissionResult genericPermissionResult = GenericObjectPermission(stealer,objectID, false,0);
            bool genericPermission = genericPermissionResult.Success;
            if (!genericPermission)
            {
                return false;
            }

            if (!m_scene.Entities.ContainsKey(objectID))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[objectID] is SceneObjectGroup))
            {
                return false;
            }

            SceneObjectGroup part = (SceneObjectGroup)m_scene.Entities[objectID];

            //generic object permission will return TRUE for someone who has friend edit permissions,
            //but we do not want them to take
            if (part.OwnerID != stealer && this.FriendHasEditPermission(part.OwnerID, stealer, false))
            {
                return false;
            }

            return true;
        }

        private bool CanTakeCopyObject(UUID objectID, UUID userID, Scene inScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            GenericPermissionResult genericResult = GenericObjectPermission(userID, objectID, false, (uint)PermissionMask.Copy);
            bool permission = genericResult.Success;
            if (!permission)
            {
                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // If it's not an object, we cant edit it.
                if (!(m_scene.Entities[objectID] is SceneObjectGroup))
                {
                    return false;
                }

                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];
                // UUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for
                // the administrator object permissions to take effect.
                // UUID objectOwner = task.OwnerID;

                if ((task.RootPart.EveryoneMask & PERM_COPY) != 0)
                    permission = true;

                if ((task.GetEffectivePermissions(true) & (PERM_COPY | PERM_TRANS)) != (PERM_COPY | PERM_TRANS))
                    permission = false;
            }
            else
            {
                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

                if (task.OwnerID != userID)
                {
                    if ((task.GetEffectivePermissions(true) & (PERM_COPY | PERM_TRANS)) != (PERM_COPY | PERM_TRANS))
                        permission = false;
                }
                else
                {
                    if ((task.GetEffectivePermissions(true) & PERM_COPY) != PERM_COPY)
                        permission = false;
                }
            }
            
            return permission;
        }

        private bool CanTerraformLand(UUID user, Vector3 position, Scene requestFromScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // Estate override (EO, EM and gods)
            if (GenericEstatePermission(user))
                return true;

            if (m_scene.RegionInfo.RegionSettings.BlockTerraform)
                return false;

            // Plus parcels cannot be terraformed by their owners
            if (m_scene.RegionInfo.Product == ProductRulesUse.PlusUse)
                return false;

            float X = position.X;
            float Y = position.Y;

            if (X > 255)
                X = 255;
            if (Y > 255)
                Y = 255;
            if (X < 0)
                X = 0;
            if (Y < 0)
                Y = 0;

            ILandObject parcel = m_scene.LandChannel.GetLandObject(X, Y);
            if (parcel == null)
                return false;

            // Others allowed to terraform?
            if ((parcel.landData.Flags & (uint)ParcelFlags.AllowTerraform) != 0)
                return true;

            // Group role ability to edit terrain?
            if (parcel != null && GenericParcelPermission(user, parcel, (ulong)GroupPowers.AllowEditLand))
                return true;

            return false;
        }

        /// <summary>
        /// Check whether the specified user can view the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>        
        private bool CanViewScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;           

            if (objectID == UUID.Zero) // User inventory
            {
                CachedUserInfo userInfo =
                        scene.CommsManager.UserService.GetUserDetails(user);
            
                if (userInfo == null)
                {
                    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for administrator check", user);
                    return false;
                }                  

                InventoryItemBase assetRequestItem = userInfo.FindItem(script);
                if (assetRequestItem == null) // Library item
                {
                    assetRequestItem = m_scene.CommsManager.LibraryRoot.FindItem(script);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // SL is rather harebrained here. In SL, a script you
                // have mod/copy no trans is readable. This subverts
                // permissions, but is used in some products, most
                // notably Hippo door plugin and HippoRent 5 networked
                // prim counter.
                // To enable this broken SL-ism, remove Transfer from
                // the below expressions.
                // Trying to improve on SL perms by making a script
                // readable only if it's really full perms
                //
                if ((assetRequestItem.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;
            
                if (part.OwnerID != user)
                {
                    if (part.GroupID == UUID.Zero)
                    return false;

                    if( !IsAgentInGroupRole(part.GroupID, user, 0) )
                        return false;
            
                    if ((part.GroupMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                } else {
                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                }

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(script);

                if (ti == null)
                    return false;
            
                if (ti.OwnerID != user)
                {
                    if (ti.GroupID == UUID.Zero)
                        return false;
        
                    if( !IsAgentInGroupRole(ti.GroupID, user, 0) )
                        return false;
                }

                // Require full perms
                if ((ti.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check whether the specified user can view the given notecard
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>         
        private bool CanViewNotecard(UUID notecard, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                CachedUserInfo userInfo =
                        scene.CommsManager.UserService.GetUserDetails(user);
            
                if (userInfo == null)
                {
                    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for view notecard check", user);
                    return false;
                }    

                InventoryItemBase assetRequestItem = userInfo.FindItem(notecard);
                if (assetRequestItem == null) // Library item
                {
                    assetRequestItem = m_scene.CommsManager.LibraryRoot.FindItem(notecard);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // Notecards are always readable unless no copy
                //
                if ((assetRequestItem.CurrentPermissions &
                        (uint)PermissionMask.Copy) !=
                        (uint)PermissionMask.Copy)
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;
            
                if (part.OwnerID != user)
                {
                    if (part.GroupID == UUID.Zero)
                        return false;
        
                    if( !IsAgentInGroupRole(part.GroupID, user, 0) )
                        return false;
                }

                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                {
                    if (ti.GroupID == UUID.Zero)
                        return false;
        
                    if( !IsAgentInGroupRole(ti.GroupID, user, 0) )
                        return false;
                }

                // Notecards are always readable unless no copy
                //
                if ((ti.CurrentPermissions &
                        (uint)PermissionMask.Copy) !=
                        (uint)PermissionMask.Copy)
                    return false;
            }

            return true;
        }

        #endregion

        private bool CanLinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanDelinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanBuyLand(UUID userID, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to directly create the given inventory type in a prim's
        /// inventory (e.g. the New Script button in the 1.21 Linden Lab client).
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="objectID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanCreateObjectInventory(int invType, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            
            return true;
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to create the given inventory type in their inventory.
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanCreateUserInventory(int invType, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            
            return true;            
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to copy the given inventory type in their inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanCopyUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;            
        }        
        
        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanEditUserInventory(UUID itemID, UUID userID)
        {            
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;            
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to delete the given inventory item from their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanDeleteUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;            
        }        

        private bool CanTeleport(UUID userID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanResetScript(UUID prim, UUID script, UUID agentID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(prim);

            // If we selected a sub-prim to reset, prim won't represent the object, but only a part.
            // We have to check the permissions of the object, though.
            if (part.ParentGroup != null) prim = part.ParentGroup.UUID;

            // You can reset the scripts in any object you can edit
            return GenericObjectPermission(agentID, prim, false, (uint)PermissionMask.Modify).Success;
        }

        private bool CanStartScript(SceneObjectPart part, UUID script, UUID agentID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            UUID prim = part.UUID;

            // If we selected a sub-prim to reset, prim won't represent the object, but only a part.
            // We have to check the permissions of the object, though.
            if (part.ParentGroup != null) prim = part.ParentGroup.UUID;

            // You can reset the scripts in any object you can edit
            return GenericObjectPermission(agentID, prim, false, 0).Success;
        }

        private bool CanStopScript(SceneObjectPart part, UUID script, UUID agentID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            UUID prim = part.UUID;

            // If we selected a sub-prim to reset, prim won't represent the object, but only a part.
            // We have to check the permissions of the object, though.
            if (part.ParentGroup != null) prim = part.ParentGroup.UUID;

            // You can reset the scripts in any object you can edit
            return GenericObjectPermission(agentID, prim, false, 0).Success;
        }

        // if client == null, it's being done by an object, owned by scriptOwnerID.
        private bool CanUseObjectReturn(ILandObject parcel, uint type, IClientAPI client, UUID scriptOwnerID, List<SceneObjectGroup> retlist, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            UUID agentId = scriptOwnerID;
            UUID landGroupId = parcel.landData.GroupID;
            if (client != null)
                agentId = client.AgentId;

            ulong? powers = null;   // defer possibly costly initialization to actual powers until needed

            switch (type)
            {
            case (uint)ObjectReturnType.Owner:
                // Don't let group members return owner's objects, ever
                //
                if (parcel.landData.IsGroupOwned)
                {
                    if (HasGroupPower(client, agentId, landGroupId, (long)GroupPowers.ReturnGroupOwned))
                        return true;
                }
                else
                {
                    if (parcel.landData.OwnerID != agentId)
                        return false;
                }
                return GenericParcelOwnerPermission(agentId, parcel, (ulong)GroupPowers.ReturnGroupOwned);

            case (uint)ObjectReturnType.Group:
                bool hasGroupSet = GenericParcelOwnerPermission(agentId, parcel, (ulong)GroupPowers.ReturnGroupSet);
                bool hasGroupOwned = GenericParcelOwnerPermission(agentId, parcel, (ulong)GroupPowers.ReturnGroupOwned);
                bool hasNonGroup = GenericParcelOwnerPermission(agentId, parcel, (ulong)GroupPowers.ReturnNonGroup);
                if (parcel.landData.OwnerID != agentId)
                {
                    // If permissionis granted through a group...
                    if (landGroupId != UUID.Zero)
                    {
                        powers = GetGroupPowers(null, agentId, landGroupId);
                    }

                    if ((powers != null) && ((powers & (long)GroupPowers.ReturnGroupSet) != 0))
                    {
                        foreach (SceneObjectGroup g in new List<SceneObjectGroup>(retlist))
                        {
                            // check for and remove group owned objects unless
                            // the user also has permissions to return those
                            //
                            if (g.OwnerID == g.GroupID &&
                                    ((powers & (long)GroupPowers.ReturnGroupOwned) == 0))
                            {
                                retlist.Remove(g);
                            }
                        }
                        // And allow the operation
                        //
                        return true;
                    }
                }
                return GenericParcelOwnerPermission(agentId, parcel, (ulong)GroupPowers.ReturnNonGroup);

            case (uint)ObjectReturnType.Other:
                if (landGroupId != UUID.Zero)
                {
                    powers = GetGroupPowers(null, agentId, landGroupId);
                }
                if ((powers != null) && ((powers & (long)GroupPowers.ReturnNonGroup) != 0))
                    return true;
                return GenericParcelOwnerPermission(agentId, parcel, (ulong)GroupPowers.ReturnNonGroup);

            case (uint)ObjectReturnType.List:
                    foreach (SceneObjectGroup g in new List<SceneObjectGroup>(retlist))
                    {
                        if (!CanReturnObject(g.UUID, agentId, scene))
                        {
                            retlist.Remove(g);
                        }
                    }
                    // And allow the operation
                    //
                    return true;
            }

            return GenericParcelPermission(agentId, parcel, 0);
        }
    }
}
