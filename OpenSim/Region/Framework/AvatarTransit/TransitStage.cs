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

namespace OpenSim.Region.Framework.AvatarTransit
{
    /// <summary>
    /// The current state of an avatar transit
    /// </summary>
    public enum TransitStage
    {
        /// <summary>
        /// We have begun sending the avatar to a new region
        /// </summary>
        SendBegin,

        /// <summary>
        /// We are establishing a child presence on the new region
        /// </summary>
        SendEstablishChildPresence,

        /// <summary>
        /// We are handing off the avatar to the new region
        /// </summary>
        SendAvatarHandoff,

        /// <summary>
        /// The send completed successfully
        /// </summary>
        SendCompletedSuccess,

        /// <summary>
        /// There was an error sending the avatar to the new region
        /// </summary>
        SendError,

        /// <summary>
        /// We have received a request to create a new child presence on this region
        /// </summary>
        ReceiveCreateChildPresence,

        /// <summary>
        /// We are creating a root presence on this region for the avatar
        /// </summary>
        ReceiveSetupRoot,

        /// <summary>
        /// We are establishing links to known neighbors on this region for the avatar
        /// </summary>
        ReceiveEstablishChildAgentLinks
    }
}
