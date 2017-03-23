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
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class Constants
    {
        public const uint RegionSize = 256;
        public const byte TerrainPatchSize = 16;
        public const float OUTSIDE_REGION = (float)RegionSize;  // first invalid location (use < OUTSIDE_REGION)
        public const float OUTSIDE_REGION_POSITIVE_EDGE = OUTSIDE_REGION - 0.001f;  // highest valid location
        public const float OUTSIDE_REGION_NEGATIVE_EDGE = 0.0f; // first valid location (use >= OUTSIDE_REGION_NEGATIVE_EDGE)
        public const float REGION_MINIMUM_Z = -128.0f;
        public const float REGION_MAXIMUM_Z = 10000.0f;
        public const float REGION_VALID_X = 128.0f;
        public const float REGION_VALID_Y = 128.0f;
        public const float REGION_VALID_Z = 128.0f;

        // LSL constants (must match LSLConstants.cs from the script engine)
        public const int LINK_SET = -1;
        public const int LINK_ROOT = 1;
        public const int LINK_ALL_OTHERS = -2;
        public const int LINK_ALL_CHILDREN = -3;
        public const int LINK_THIS = -4;
        // Returned by llReturnObjectsByOwner and llReturnObjectsByID
        public const int ERR_GENERIC = -1;
        public const int ERR_PARCEL_PERMISSIONS = -2;
        public const int ERR_MALFORMED_PARAMS = -3;
        public const int ERR_RUNTIME_PERMISSIONS = -4;
        public const int ERR_THROTTLED = -5;

        public const uint MaxGroups = 100;  // maximum number of groups a user can be a member of
        public const GroupPowers DefaultEveryonePowers = GroupPowers.AllowSetHome | GroupPowers.JoinChat | GroupPowers.AllowVoiceChat | GroupPowers.ReceiveNotices;
        public const GroupPowers OWNER_GROUP_POWERS = (GroupPowers)ulong.MaxValue;

        public const string DefaultTexture = "89556747-24cb-43ed-920b-47caed15465f";

        // Avatar "bounce" when descending into a no-entry parcel (e.g. banned)
        public const float AVATAR_BOUNCE = 10.0f;

        // Summary:
        //     Used by EstateOwnerMessage packets
        public enum EstateAccessDeltaCommands
        {
            // the 'All Estates' variants set the low bit (or second lowest,
            // accept either) always bit 0 in testing.
            AddUserAsAllowed = 4,
            RemoveUserAsAllowed = 8,
            AddGroupAsAllowed = 16,
            RemoveGroupAsAllowed = 32,
            BanUser = 64,
            UnbanUser = 128,
            AddManager = 256,
            RemoveManager = 512,
            NoReply = 1024          // server should not send a 'setaccess' response
        }

        public enum EstateAccessDeltaResponse : uint
        {
            AllowedUsers = 1,
            AllowedGroups = 2,
            EstateBans = 4,
            EstateManagers = 8
        }

        /// <summary>
        /// Used by internal server implementations of LSL functions.
        /// </summary>
        public enum GenericReturnCodes : uint
        {
            SUCCESS = 0,    // success
            NOCHANGE = 1,   // ignored, the request was redundant, nothing needed to be done
            ERROR = 2,      // generic error, internal server failure (like module not available)
            PARAMETER = 3,  // generic validation failed, one or more of the parameters is invalid
            ACCESS = 4,     // forbidden due to region/parcel access restrictions
            PERMISSION = 5, // forbidden: permissions error or missing group role
            MUTED = 6,      // operation not permitted due to muting
            NOTFOUND = 7,   // the target does not exist
        }

    }
}
