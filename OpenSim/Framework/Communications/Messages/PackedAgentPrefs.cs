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
using ProtoBuf;
using OpenSim.Framework;

namespace OpenSim.Framework.Communications.Messages
{
    /// <summary>
    /// Network-ready message to pack up an avatars appearance to send over the wire
    /// </summary>
    [ProtoContract]
    public class PackedAgentPrefs
    {
        [ProtoMember(1)]
        public double Hover;            // e.g. 0.0

        [ProtoMember(2)]
        public bool LanguageIsPublic;   // e.g. true

        [ProtoMember(3)]
        public string Language;         // e.g. "en" or "en-us"

        [ProtoMember(4)]
        public string AccessPrefs;      // e.g. "M"

        // Default permissions for object creates
        [ProtoMember(5)]
        public uint PermEveryone;
        [ProtoMember(6)]
        public uint PermGroup;
        [ProtoMember(7)]
        public uint PermNextOwner;


        internal static PackedAgentPrefs FromAgentPrefs(AgentPreferencesData agentPreferences)
        {
            PackedAgentPrefs prefs = new PackedAgentPrefs
            {
                AccessPrefs = agentPreferences.AccessPrefs,
                Hover = agentPreferences.HoverHeight,
                Language = agentPreferences.Language,
                LanguageIsPublic = agentPreferences.LanguageIsPublic,
                // DefaultObjectPermMasks
                PermEveryone = agentPreferences.PermEveryone,
                PermGroup = agentPreferences.PermGroup,
                PermNextOwner = agentPreferences.PermNextOwner
            };

            return prefs;
        }

        internal AgentPreferencesData ToAgentPrefs(OpenMetaverse.UUID owner)
        {
            AgentPreferencesData agentPreferences =
                new AgentPreferencesData
                {
                    AccessPrefs = this.AccessPrefs,
                    HoverHeight = this.Hover,
                    Language = this.Language,
                    LanguageIsPublic = this.LanguageIsPublic,
                    // DefaultObjectPermMasks
                    PermEveryone = this.PermEveryone,
                    PermGroup = this.PermGroup,
                    PermNextOwner = this.PermNextOwner
                };

            return agentPreferences;
        }
    }
}
