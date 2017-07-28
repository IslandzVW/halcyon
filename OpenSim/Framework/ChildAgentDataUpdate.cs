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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using ProtoBuf;

namespace OpenSim.Framework
{
    // Soon to be dismissed
    public class ChildAgentDataUpdate
    {
        public Guid ActiveGroupID;
        public Guid AgentID;
        public bool alwaysrun;
        public float AVHeight;
        public sLLVector3 cameraPosition;
        public float drawdistance;
        public float godlevel;
        public uint GroupAccess;
        public sLLVector3 Position;
        public ulong regionHandle;
        public byte[] throttles;
        public sLLVector3 Velocity;


        public ChildAgentDataUpdate()
        {
        }
    }

    public interface IAgentData
    {
        UUID AgentID { get; set; }

        OSDMap Pack();
        void Unpack(OSDMap map);
    }

    /// <summary>
    /// Replacement for ChildAgentDataUpdate. Used over RESTComms and LocalComms.
    /// </summary>
    public class AgentPosition : IAgentData
    {
        private UUID m_id;
        public UUID AgentID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        public ulong RegionHandle;
        public uint CircuitCode;
        public UUID SessionID;

        public float Far;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 AtAxis;
        public Vector3 LeftAxis;
        public Vector3 UpAxis;
        public bool ChangedGrid;

        // This probably shouldn't be here
        public byte[] Throttles;


        public OSDMap Pack()
        {
            OSDMap args = new OSDMap();
            args["message_type"] = OSD.FromString("AgentPosition");

            args["region_handle"] = OSD.FromString(RegionHandle.ToString());
            args["circuit_code"] = OSD.FromString(CircuitCode.ToString());
            args["agent_uuid"] = OSD.FromUUID(AgentID);
            args["session_uuid"] = OSD.FromUUID(SessionID);

            args["position"] = OSD.FromString(Position.ToString());
            args["velocity"] = OSD.FromString(Velocity.ToString());
            args["center"] = OSD.FromString(Center.ToString());
            args["size"] = OSD.FromString(Size.ToString());
            args["at_axis"] = OSD.FromString(AtAxis.ToString());
            args["left_axis"] = OSD.FromString(LeftAxis.ToString());
            args["up_axis"] = OSD.FromString(UpAxis.ToString());

            args["far"] = OSD.FromReal(Far);
            args["changed_grid"] = OSD.FromBoolean(ChangedGrid);

            if ((Throttles != null) && (Throttles.Length > 0))
                args["throttles"] = OSD.FromBinary(Throttles);

            return args;
        }

        public void Unpack(OSDMap args)
        {
            if (args.ContainsKey("region_handle"))
                UInt64.TryParse(args["region_handle"].AsString(), out RegionHandle);

            if (args.ContainsKey("circuit_code"))
                UInt32.TryParse((string)args["circuit_code"].AsString(), out CircuitCode);

            if (args.ContainsKey("agent_uuid"))
                AgentID = args["agent_uuid"].AsUUID();

            if (args.ContainsKey("session_uuid"))
                SessionID = args["session_uuid"].AsUUID();

            if (args.ContainsKey("position"))
                Vector3.TryParse(args["position"].AsString(), out Position);

            if (args.ContainsKey("velocity"))
                Vector3.TryParse(args["velocity"].AsString(), out Velocity);

            if (args.ContainsKey("center"))
                Vector3.TryParse(args["center"].AsString(), out Center);

            if (args.ContainsKey("size"))
                Vector3.TryParse(args["size"].AsString(), out Size);

            if (args.ContainsKey("at_axis"))
                Vector3.TryParse(args["at_axis"].AsString(), out AtAxis);

            if (args.ContainsKey("left_axis"))
                Vector3.TryParse(args["left_axis"].AsString(), out AtAxis);

            if (args.ContainsKey("up_axis"))
                Vector3.TryParse(args["up_axis"].AsString(), out AtAxis);

            if (args.ContainsKey("changed_grid"))
                ChangedGrid = args["changed_grid"].AsBoolean();

            if (args.ContainsKey("far"))
                Far = (float)(args["far"].AsReal());

            if (args.ContainsKey("throttles"))
                Throttles = args["throttles"].AsBinary();
        }

        /// <summary>
        /// Soon to be decommissioned
        /// </summary>
        /// <param name="cAgent"></param>
        public void CopyFrom(ChildAgentDataUpdate cAgent)
        {
            AgentID = new UUID(cAgent.AgentID);

            Size = new Vector3();
            Size.Z = cAgent.AVHeight;

            Center = new Vector3(cAgent.cameraPosition.x, cAgent.cameraPosition.y, cAgent.cameraPosition.z);
            Far = cAgent.drawdistance;
            Position = new Vector3(cAgent.Position.x, cAgent.Position.y, cAgent.Position.z);
            RegionHandle = cAgent.regionHandle;
            Throttles = cAgent.throttles;
            Velocity = new Vector3(cAgent.Velocity.x, cAgent.Velocity.y, cAgent.Velocity.z);
        }

    }

    [ProtoContract]
    public class AgentGroupData
    {
        [ProtoMember(1)]
        private Guid _GroupID
        {
            get { return GroupID.Guid; }
            set { GroupID = new UUID(value); }
        }

        public UUID GroupID;

        [ProtoMember(2)]
        public ulong GroupPowers;
        [ProtoMember(3)]
        public bool AcceptNotices;

        public AgentGroupData()
        {
            //required for protobuf
        }

        public AgentGroupData(UUID id, ulong powers, bool notices)
        {
            GroupID = id;
            GroupPowers = powers;
            AcceptNotices = notices;
        }

        public AgentGroupData(OSDMap args)
        {
            UnpackUpdateMessage(args);
        }

        public OSDMap PackUpdateMessage()
        {
            OSDMap groupdata = new OSDMap();
            groupdata["group_id"] = OSD.FromUUID(GroupID);
            groupdata["group_powers"] = OSD.FromString(GroupPowers.ToString());
            groupdata["accept_notices"] = OSD.FromBoolean(AcceptNotices);

            return groupdata;
        }

        public void UnpackUpdateMessage(OSDMap args)
        {
            if (args.ContainsKey("group_id"))
                GroupID = args["group_id"].AsUUID();
            if (args.ContainsKey("group_powers"))
                UInt64.TryParse((string)args["group_powers"].AsString(), out GroupPowers);
            if (args.ContainsKey("accept_notices"))
                AcceptNotices = args["accept_notices"].AsBoolean();
        }
    }

    [Flags]
    public enum AgentLocomotionFlags
    {
        Teleport = (1 << 0),
        Crossing = (1 << 1)
    }

    [Flags]
    public enum PresenceFlags
    {
        DebugCrossings = (1 << 0),
        LimitNeighbors = (1 << 1)   // when set, neighbors range is only 1
    }

    public class AgentData : IAgentData
    {
        public ulong AgentDataCreatedOn = Util.GetLongTickCount();

        private UUID m_id;
        public UUID AgentID
        {
            get { return m_id; }
            set { m_id = value; }
        }
        public ulong RegionHandle;
        public uint CircuitCode;
        public UUID SessionID;

        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 AtAxis;
        public Vector3 LeftAxis;
        public Vector3 UpAxis;
        public bool ChangedGrid;

        public float Far;
        public float Aspect;
        public byte[] Throttles;

        public uint LocomotionState;
        public Quaternion HeadRotation;
        public Quaternion BodyRotation;
        public uint ControlFlags;
        public float EnergyLevel;
        public Byte GodLevel;
        public bool AlwaysRun;
        public UUID PreyAgent;
        public Byte AgentAccess;
        public UUID ActiveGroupID;

        public AgentGroupData[] Groups;
        public Animation[] Anims;

        // Appearance
        public AvatarAppearance Appearance;

        // AgentPreferences from viewer
        public AgentPreferencesData AgentPrefs;

        public string CallbackURI;

        public UUID SatOnGroup;
        public UUID SatOnPrim;
        public Vector3 SatOnPrimOffset;

        public List<byte[]> SerializedAttachments;

        public AgentLocomotionFlags LocomotionFlags;

        public List<RemotePresenceInfo> RemoteAgents;

        public Vector3 ConstantForces;
        public bool ConstantForcesAreLocal;

        public ulong PresenceFlags;

        public bool AvatarAsAPrim;

        public virtual OSDMap Pack()
        {
            OSDMap args = new OSDMap();
            args["message_type"] = OSD.FromString("AgentData");

            args["region_handle"] = OSD.FromString(RegionHandle.ToString());
            args["circuit_code"] = OSD.FromString(CircuitCode.ToString());
            args["agent_uuid"] = OSD.FromUUID(AgentID);
            args["session_uuid"] = OSD.FromUUID(SessionID);

            args["position"] = OSD.FromString(Position.ToString());
            args["velocity"] = OSD.FromString(Velocity.ToString());
            args["center"] = OSD.FromString(Center.ToString());
            args["size"] = OSD.FromString(Size.ToString());
            args["at_axis"] = OSD.FromString(AtAxis.ToString());
            args["left_axis"] = OSD.FromString(LeftAxis.ToString());
            args["up_axis"] = OSD.FromString(UpAxis.ToString());

            args["changed_grid"] = OSD.FromBoolean(ChangedGrid);
            args["far"] = OSD.FromReal(Far);
            args["aspect"] = OSD.FromReal(Aspect);

            if ((Throttles != null) && (Throttles.Length > 0))
                args["throttles"] = OSD.FromBinary(Throttles);

            args["locomotion_state"] = OSD.FromString(LocomotionState.ToString());
            args["head_rotation"] = OSD.FromString(HeadRotation.ToString());
            args["body_rotation"] = OSD.FromString(BodyRotation.ToString());
            args["control_flags"] = OSD.FromString(ControlFlags.ToString());

            args["energy_level"] = OSD.FromReal(EnergyLevel);
            args["god_level"] = OSD.FromString(GodLevel.ToString());
            args["always_run"] = OSD.FromBoolean(AlwaysRun);
            args["prey_agent"] = OSD.FromUUID(PreyAgent);
            args["agent_access"] = OSD.FromString(AgentAccess.ToString());

            args["active_group_id"] = OSD.FromUUID(ActiveGroupID);

            if ((Groups != null) && (Groups.Length > 0))
            {
                OSDArray groups = new OSDArray(Groups.Length);
                foreach (AgentGroupData agd in Groups)
                    groups.Add(agd.PackUpdateMessage());
                args["groups"] = groups;
            }

            if ((Anims != null) && (Anims.Length > 0))
            {
                OSDArray anims = new OSDArray(Anims.Length);
                foreach (Animation aanim in Anims)
                    anims.Add(aanim.PackUpdateMessage());
                args["animations"] = anims;
            }

            if (Appearance != null)
            {
                // We might not pass this in all cases...
                if (Appearance.Texture != null)
                    args["texture_entry"] = OSD.FromBinary(Appearance.Texture.GetBytes());
                if ((Appearance.VisualParams != null) && (Appearance.VisualParams.Length > 0))
                    args["visual_params"] = OSD.FromBinary(Appearance.VisualParams);

                OSDArray wears = new OSDArray(AvatarWearable.MAX_WEARABLES * 2);
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                {
                    AvatarWearable wearable = Appearance.GetWearableOfType(i);
                    wears.Add(OSD.FromUUID(wearable.ItemID));
                    wears.Add(OSD.FromUUID(wearable.AssetID));
                }

                args["wearables"] = wears;
            }

            // AgentPreferences from viewer
            if (AgentPrefs != null)
            {
                args["hover_height"] = AgentPrefs.HoverHeight;
                args["access_prefs"] = AgentPrefs.AccessPrefs;
                args["perm_everyone"] = AgentPrefs.PermEveryone;
                args["perm_group"] = AgentPrefs.PermGroup;
                args["perm_next_owner"] = AgentPrefs.PermNextOwner;
                args["language"] = AgentPrefs.Language;
                args["language_is_public"] = AgentPrefs.LanguageIsPublic;
                args["principal_id"] = m_id;
            }

            if (!String.IsNullOrEmpty(CallbackURI))
                args["callback_uri"] = OSD.FromString(CallbackURI);

            if (SatOnGroup != UUID.Zero)
            {
                args["sat_on_group"] = OSD.FromUUID(SatOnGroup);
                args["sat_on_prim"] = OSD.FromUUID(SatOnPrim);
                args["sit_offset"] = OSD.FromString(SatOnPrimOffset.ToString());
            }

            args["avatar_as_a_prim"] = OSD.FromBoolean(AvatarAsAPrim);

            return args;
        }

        /// <summary>
        /// Deserialization of agent data.
        /// Avoiding reflection makes it painful to write, but that's the price!
        /// </summary>
        /// <param name="hash"></param>
        public virtual void Unpack(OSDMap args)
        {
            if (args.ContainsKey("region_handle"))
                UInt64.TryParse(args["region_handle"].AsString(), out RegionHandle);

            if (args.ContainsKey("circuit_code"))
                UInt32.TryParse((string)args["circuit_code"].AsString(), out CircuitCode);

            if (args.ContainsKey("agent_uuid"))
                AgentID = args["agent_uuid"].AsUUID();

            if (args.ContainsKey("session_uuid"))
                SessionID = args["session_uuid"].AsUUID();

            if (args.ContainsKey("position"))
                Vector3.TryParse(args["position"].AsString(), out Position);

            if (args.ContainsKey("velocity"))
                Vector3.TryParse(args["velocity"].AsString(), out Velocity);

            if (args.ContainsKey("center"))
                Vector3.TryParse(args["center"].AsString(), out Center);

            if (args.ContainsKey("size"))
                Vector3.TryParse(args["size"].AsString(), out Size);

            if (args.ContainsKey("at_axis"))
                Vector3.TryParse(args["at_axis"].AsString(), out AtAxis);

            if (args.ContainsKey("left_axis"))
                Vector3.TryParse(args["left_axis"].AsString(), out AtAxis);

            if (args.ContainsKey("up_axis"))
                Vector3.TryParse(args["up_axis"].AsString(), out AtAxis);

            if (args.ContainsKey("changed_grid"))
                ChangedGrid = args["changed_grid"].AsBoolean();

            if (args.ContainsKey("far"))
                Far = (float)(args["far"].AsReal());

            if (args.ContainsKey("aspect"))
                Aspect = (float)args["aspect"].AsReal();

            if (args.ContainsKey("throttles"))
                Throttles = args["throttles"].AsBinary();

            if (args.ContainsKey("locomotion_state"))
                UInt32.TryParse(args["locomotion_state"].AsString(), out LocomotionState);

            if (args.ContainsKey("head_rotation"))
                Quaternion.TryParse(args["head_rotation"].AsString(), out HeadRotation);

            if (args.ContainsKey("body_rotation"))
                Quaternion.TryParse(args["body_rotation"].AsString(), out BodyRotation);

            if (args.ContainsKey("control_flags"))
                UInt32.TryParse(args["control_flags"].AsString(), out ControlFlags);

            if (args.ContainsKey("energy_level"))
                EnergyLevel = (float)(args["energy_level"].AsReal());

            if (args.ContainsKey("god_level"))
                Byte.TryParse(args["god_level"].AsString(), out GodLevel);

            if (args.ContainsKey("always_run"))
                AlwaysRun = args["always_run"].AsBoolean();

            if (args.ContainsKey("prey_agent"))
                PreyAgent = args["prey_agent"].AsUUID();

            if (args.ContainsKey("agent_access"))
                Byte.TryParse(args["agent_access"].AsString(), out AgentAccess);

            if (args.ContainsKey("active_group_id"))
                ActiveGroupID = args["active_group_id"].AsUUID();

            if ((args.ContainsKey("groups")) && (args["groups"]).Type == OSDType.Array)
            {
                OSDArray groups = (OSDArray)(args["groups"]);
                Groups = new AgentGroupData[groups.Count];
                int i = 0;
                foreach (OSD o in groups)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Groups[i++] = new AgentGroupData((OSDMap)o);
                    }
                }
            }

            if ((args.ContainsKey("animations")) && (args["animations"]).Type == OSDType.Array)
            {
                OSDArray anims = (OSDArray)(args["animations"]);
                Anims = new Animation[anims.Count];
                int i = 0;
                foreach (OSD o in anims)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Anims[i++] = new Animation((OSDMap)o);
                    }
                }
            }

            // Initialize an Appearance
            Appearance = new AvatarAppearance(AgentID);

            if (args.ContainsKey("texture_entry"))
            {
                byte[] data = args["texture_entry"].AsBinary();
                Primitive.TextureEntry textureEntries = new Primitive.TextureEntry(data, 0, data.Length);
                Appearance.SetTextureEntries(textureEntries);
            }

            if (args.ContainsKey("visual_params"))
            {
                byte[] visualParams = args["visual_params"].AsBinary();
                Appearance.SetVisualParams(visualParams);
            }

            if ((args.ContainsKey("wearables")) && (args["wearables"]).Type == OSDType.Array)
            {
                OSDArray wears = (OSDArray)(args["wearables"]);
                List<AvatarWearable> wearables = new List<AvatarWearable>();

                int offset = 0;
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                {
                    if ((offset + 1) < wears.Count)
                    {
                        UUID itemID = wears[offset++].AsUUID();
                        UUID assetID = wears[offset++].AsUUID();
                        wearables.Add(new AvatarWearable(i, itemID, assetID));
                    }
                    else
                    {
                        break;
                    }
                }

                Appearance.SetWearables(wearables);
            }

            if (args.ContainsKey("callback_uri"))
                CallbackURI = args["callback_uri"].AsString();

            if (args.ContainsKey("avatar_as_a_prim"))
                AvatarAsAPrim = args["avatar_as_a_prim"].AsBoolean();

            if (args.ContainsKey("sat_on_group"))
            {
                SatOnGroup = args["sat_on_group"].AsUUID();
                SatOnPrim = args["sat_on_prim"].AsUUID();
                try
                {
                    // "sit_offset" previously used OSD.FromVector3(vec) was used to store the data.
                    // Other Vector3 storage uses OSD.FromString(vec.ToString()).
                    // If originating from old region code, that will still be the case
                    // and the TryParse will trigger a format exception.
                    Vector3.TryParse(args["sit_offset"].ToString(), out SatOnPrimOffset);
                }
                catch (Exception)
                {
                    // The following is compatible with OSD.FromVector3(vec), since Vector3.TryParse is not.
                    SatOnPrimOffset = args["sit_offset"].AsVector3();
                }
            }

            // Initialize AgentPrefs from viewer
            AgentPrefs = new AgentPreferencesData();
            if (args.ContainsKey("hover_height"))
            {
                AgentPrefs.HoverHeight = args["hover_height"];
            }
            if (args.ContainsKey("access_prefs"))
            {
                AgentPrefs.AccessPrefs = args["access_prefs"];
            }
            if (args.ContainsKey("perm_everyone"))
            {
                AgentPrefs.PermEveryone = args["perm_everyone"];
            }
            if (args.ContainsKey("perm_group"))
            {
                AgentPrefs.PermGroup = args["perm_group"];
            }
            if (args.ContainsKey("perm_next_owner"))
            {
                AgentPrefs.PermNextOwner = args["perm_next_owner"];
            }
            if (args.ContainsKey("language"))
            {
                AgentPrefs.Language = args["language"];
            }
            if (args.ContainsKey("language_is_public"))
            {
                AgentPrefs.LanguageIsPublic = args["language_is_public"];
            }
            if (args.ContainsKey("principal_id"))
            {
                AgentPrefs.PrincipalID = args["principal_id"];
            }
        }

        public AgentData()
        {
        }

        public void Dump()
        {
            System.Console.WriteLine("------------ AgentData ------------");
            System.Console.WriteLine("UUID: " + AgentID);
            System.Console.WriteLine("Region: " + RegionHandle);
            System.Console.WriteLine("Position: " + Position);
        }
    }

    public class CompleteAgentData : AgentData
    {
        public override OSDMap Pack() 
        {
            return base.Pack();
        }

        public override void Unpack(OSDMap map)
        {
            base.Unpack(map);
        }
    }
}
