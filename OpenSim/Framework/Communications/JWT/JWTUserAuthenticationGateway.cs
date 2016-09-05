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

namespace OpenSim.Framework.Communications.JWT
{
    /// <summary>
    /// Provides access to JWT tokens that are granted by authenticating a user
    /// against the halcyon user table
    /// </summary>
    public class JWTUserAuthenticationGateway
    {
        private readonly IUserService _userService;

        public JWTUserAuthenticationGateway(IUserService userService)
        {
            _userService = userService;
        }


        /// <summary>
        /// Authenticates a user and returns a JWT token serialized as JSON
        /// </summary>
        /// <param name="username">The username to authenticate</param>
        /// <param name="password">The user's password</param>
        /// <param name="minLevel">The minimum godlevel this user must be at to generate a token</param>
        /// <param name="payloadOptions">Options for the generated payload</param>
        /// <returns>JWT token string</returns>
        public string Authenticate(string username, string password, int minLevel,
            PayloadOptions payloadOptions)
        {
            UserProfileData profile = _userService.GetUserProfile(username, password, true);
            if (profile == null)
            {
                throw new AuthenticationException(AuthenticationFailureCause.UserNameNotFound);
            }

            return "";
        }
    }
}
