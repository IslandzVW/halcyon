/*
 * Copyright (c) 2016, InWorldz Halcyon Developers
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
using OpenMetaverse;

namespace InWorldz.JWT
{
    /// <summary>
    /// Options that can are passed through a JWT authentication payload
    /// </summary>
    public class PayloadOptions
    {
        /// <summary>
        /// The expiration date for this payload
        /// </summary>
        public DateTime Exp { get; set; }

        /// <summary>
        /// The auth scope that this authentication token applies to (eg: remote-console)
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// The username that was used to generate this authentication payload
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The UUID of the user that was used to generate this authentication payload
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// The account creation date of the user that was used to generate this authentication payload
        /// </summary>
        public DateTime BirthDate { get; set; }

        /// <summary>
        /// The UUID of the registered partner of the user that was used to generate this authentication payload at the time the payload was generated
        /// </summary>
        public string PartnerId { get; set; }
    }
}
