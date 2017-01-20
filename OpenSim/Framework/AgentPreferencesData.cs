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
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    /// <summary>
    /// Known information about a particular user preferences
    /// </summary>
    public class AgentPreferencesData
    {
        public UUID PrincipalID = UUID.Zero;
        public string AccessPrefs = "M";
        //public int GodLevel; // *TODO: Implement GodLevel (Unused by the viewer, afaict - 6/11/2015)
        public double HoverHeight = 0.0;
        public string Language = "en-us";
        public bool LanguageIsPublic = true;
        // DefaultObjectPermMasks
        public uint PermEveryone = 0;
        public uint PermGroup = 0;
        public uint PermNextOwner = 0; // Illegal value by design

        public AgentPreferencesData()
        {
        }

        public AgentPreferencesData(OSDMap map)
        {
            this.FromOSDMap(map);
        }

        public AgentPreferencesData(AgentPreferencesData data)
        {
            if (data != null)
            {
                this.HoverHeight = data.HoverHeight;
                this.AccessPrefs = data.AccessPrefs;
                this.Language = data.Language;
                this.LanguageIsPublic = data.LanguageIsPublic;
                this.PermEveryone = data.PermEveryone;
                this.PermGroup = data.PermGroup;
                this.PermNextOwner = data.PermNextOwner;
                this.PrincipalID = data.PrincipalID;
            }
        }

        public OSDMap ToOSDMap()
        {
            OSDMap result = new OSDMap();
            result["PrincipalID"] = PrincipalID;
            result["AccessPrefs"] = AccessPrefs;
            result["HoverHeight"] = HoverHeight;
            result["Language"] = Language;
            result["LanguageIsPublic"] = LanguageIsPublic;
            result["PermEveryone"] = PermEveryone;
            result["PermGroup"] = PermGroup;
            result["PermNextOwner"] = PermNextOwner;
            return result;
        }

        public void FromOSDMap(OSDMap map)
        {
            if (map.ContainsKey("PrincipalID"))
                UUID.TryParse(map["PrincipalID"].ToString(), out PrincipalID);
            if (map.ContainsKey("AccessPrefs"))
                AccessPrefs = map["AccessPrefs"].ToString();
            if (map.ContainsKey("HoverHeight"))
                HoverHeight = double.Parse(map["HoverHeight"].ToString());
            if (map.ContainsKey("Language"))
                Language = map["Language"].ToString();
            if (map.ContainsKey("LanguageIsPublic"))
                LanguageIsPublic = bool.Parse(map["LanguageIsPublic"].ToString());
            if (map.ContainsKey("PermEveryone"))
                PermEveryone = uint.Parse(map["PermEveryone"].ToString());
            if (map.ContainsKey("PermGroup"))
                PermGroup = uint.Parse(map["PermGroup"].ToString());
            if (map.ContainsKey("PermNextOwner"))
                PermNextOwner = uint.Parse(map["PermNextOwner"].ToString());            
        }
    }
}
