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
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;

namespace OpenSim.Region.Framework.AvatarTransit
{
    /// <summary>
    /// The arguments required to begin a transit to a new region
    /// </summary>
    public class TransitArguments
    {
        /// <summary>
        /// The user that is in transit
        /// </summary>
        public UUID UserId { get; set; }

        /// <summary>
        /// The type of transit this is (inbound or outbound)
        /// </summary>
        public TransitType Type { get; set; }

        /// <summary>
        /// The region the user is going to or null if this is an inbound transit
        /// </summary>
        public SimpleRegionInfo DestinationRegion { get; set; }

        /// <summary>
        /// The group the user is riding on
        /// </summary>
        public SceneObjectGroup RideOnGroup { get; set; }

        /// <summary>
        /// The part the user is riding on
        /// </summary>
        public SceneObjectPart RideOnPart { get; set; }

        /// <summary>
        /// The location for the avatar in the destination region when the transit is complete
        /// </summary>
        public Vector3 LocationInDestination { get; set; }

        /// <summary>
        /// Teleport flags
        /// </summary>
        public TeleportFlags TeleportFlags { get; set; }
    }
}
