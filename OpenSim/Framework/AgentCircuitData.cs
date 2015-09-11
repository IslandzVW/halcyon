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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.Text.RegularExpressions;
using ProtoBuf;
using System.IO;

namespace OpenSim.Framework
{
    [ProtoContract]
    public class AgentGroupDataList
    {
        [ProtoMember(1)]
        public List<AgentGroupData> GroupList = new List<AgentGroupData>();

        [ProtoMember(2)]
        public AgentGroupData ActiveGroup = new AgentGroupData();
    }

    public class AgentCircuitData
    {
        public UUID AgentID;
        public AvatarAppearance Appearance;
        public UUID BaseFolder;
        public string CapsPath = String.Empty;
        public bool child;
        public uint CircuitCode;
        public string FirstName;
        public UUID InventoryFolder;
        public string LastName;
        public UUID SecureSessionID;
        public UUID SessionID;
        public Vector3 startpos;
        public string ClientVersion = "Unknown";
        public AgentGroupDataList GroupPowers;

        public string FullName
        {
            get
            {
                return FirstName + " " + LastName;
            }
        }

        public AgentCircuitData()
        {
        }

        public OSDMap PackAgentCircuitData()
        {
            OSDMap args = new OSDMap();
            args["agent_id"] = OSD.FromUUID(AgentID);
            args["base_folder"] = OSD.FromUUID(BaseFolder);
            args["caps_path"] = OSD.FromString(CapsPath);

            args["child"] = OSD.FromBoolean(child);
            args["circuit_code"] = OSD.FromString(CircuitCode.ToString());
            args["first_name"] = OSD.FromString(FirstName);
            args["last_name"] = OSD.FromString(LastName);
            args["inventory_folder"] = OSD.FromUUID(InventoryFolder);
            args["secure_session_id"] = OSD.FromUUID(SecureSessionID);
            args["session_id"] = OSD.FromUUID(SessionID);
            args["start_pos"] = OSD.FromString(startpos.ToString());
            args["client_version"] = OSD.FromString(ClientVersion);

            if (GroupPowers != null)
            {
                using (var ostream = new MemoryStream())
                {
                    Serializer.Serialize(ostream, GroupPowers);
                    args["group_powers"] = OSD.FromBinary(ostream.ToArray());
                }
            }

            return args;
        }

        public void UnpackAgentCircuitData(OSDMap args)
        {
            if (args.ContainsKey("agent_id"))
                AgentID = args["agent_id"].AsUUID();
            if (args.ContainsKey("base_folder"))
                BaseFolder = args["base_folder"].AsUUID();
            if (args.ContainsKey("caps_path"))
                CapsPath = args["caps_path"].AsString();

            if (args.ContainsKey("child"))
                child = args["child"].AsBoolean();
            if (args.ContainsKey("circuit_code"))
                UInt32.TryParse(args["circuit_code"].AsString(), out CircuitCode);
            if (args.ContainsKey("first_name"))
                FirstName = args["first_name"].AsString();
            if (args.ContainsKey("last_name"))
                LastName = args["last_name"].AsString();
            if (args.ContainsKey("inventory_folder"))
                InventoryFolder = args["inventory_folder"].AsUUID();
            if (args.ContainsKey("secure_session_id"))
                SecureSessionID = args["secure_session_id"].AsUUID();
            if (args.ContainsKey("session_id"))
                SessionID = args["session_id"].AsUUID();
            if (args.ContainsKey("start_pos"))
                Vector3.TryParse(args["start_pos"].AsString(), out startpos);
            if (args.ContainsKey("client_version"))
                ClientVersion = args["client_version"].AsString();

            if (args.ContainsKey("group_powers"))
            {
                using (var memStream = new MemoryStream(args["group_powers"].AsBinary()))
                {
                    GroupPowers = Serializer.Deserialize<AgentGroupDataList>(memStream);
                }
            }

        }
    }
}
