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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Represents a connection that we have to a remote region
    /// </summary>
    public class AvatarRemotePresence : IEquatable<AvatarRemotePresence>
    {
        /// <summary>
        /// Information about the remote presence such as the region the 
        /// presence is on and the caps seed URL for the region
        /// </summary>
        public RemotePresenceInfo PresenceInfo { get; set; }

        /// <summary>
        /// The state this connection is in
        /// </summary>
        public RemotePresenceState State { get; set; }

        /// <summary>
        /// Is this presence intentionally far away (as in established due to a teleport attempt)
        /// </summary>
        public bool IsFarPresence { get; set; }

        /// <summary>
        /// The task we're waiting on for presence establishment
        /// </summary>
        public Task<Tuple<EstablishPresenceResult, string>> EstablishTask { get; set; }


        public bool Equals(AvatarRemotePresence other)
        {
            return this.PresenceInfo.RegionInfo.Equals(PresenceInfo.RegionInfo);
        }

        public override bool Equals(object obj)
        {
            AvatarRemotePresence other = obj as AvatarRemotePresence;
            if (other == null) return false;

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return this.PresenceInfo.RegionInfo.GetHashCode();
        }
    }
}
