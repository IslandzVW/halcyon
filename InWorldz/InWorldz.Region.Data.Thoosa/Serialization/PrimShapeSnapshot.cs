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
using OpenMetaverse;

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    /// <summary>
    /// Protobuf serializable PrimitiveBaseShape
    /// </summary>
    [ProtoContract]
    public class PrimShapeSnapshot
    {
        [ProtoMember(1)]
        public byte ProfileCurve;

        [ProtoMember(2)]
        public byte[] TextureEntry;

        [ProtoMember(3)]
        public byte[] ExtraParams;

        [ProtoMember(4)]
        public ushort PathBegin;

        [ProtoMember(5)]
        public byte PathCurve;

        [ProtoMember(6)]
        public ushort PathEnd;

        [ProtoMember(7)]
        public sbyte PathRadiusOffset;

        [ProtoMember(8)]
        public byte PathRevolutions;

        [ProtoMember(9)]
        public byte PathScaleX;

        [ProtoMember(10)]
        public byte PathScaleY;

        [ProtoMember(11)]
        public byte PathShearX;

        [ProtoMember(12)]
        public byte PathShearY;

        [ProtoMember(13)]
        public sbyte PathTwist;

        [ProtoMember(14)]
        public sbyte PathTwistBegin;

        [ProtoMember(15)]
        public byte PCode;

        [ProtoMember(16)]
        public ushort ProfileBegin;

        [ProtoMember(17)]
        public ushort ProfileEnd;

        [ProtoMember(18)]
        public ushort ProfileHollow;

        [ProtoMember(19)]
        public OpenMetaverse.Vector3 Scale;

        [ProtoMember(20)]
        public byte State;

        [ProtoMember(21)]
        public ProfileShape ProfileShape;

        [ProtoMember(22)]
        public HollowShape HollowShape;

        [ProtoMember(23)]
        public Guid SculptTexture;

        [ProtoMember(24)]
        public byte SculptType;

        [Obsolete("This attribute is no longer serialized")]
        [ProtoMember(25)]
        public byte[] SculptData
        {
            get { return null; }
            set { }
        }

        [ProtoMember(26)]
        public int FlexiSoftness;

        [ProtoMember(27)]
        public float FlexiTension;

        [ProtoMember(28)]
        public float FlexiDrag;

        [ProtoMember(29)]
        public float FlexiGravity;

        [ProtoMember(30)]
        public float FlexiWind;

        [ProtoMember(31)]
        public float FlexiForceX;

        [ProtoMember(32)]
        public float FlexiForceY;

        [ProtoMember(33)]
        public float FlexiForceZ;

        [ProtoMember(34)]
        public float[] LightColor;

        [ProtoMember(35)]
        public float LightRadius;

        [ProtoMember(36)]
        public float LightCutoff;

        [ProtoMember(37)]
        public float LightIntensity;

        [ProtoMember(38)]
        public bool FlexiEntry;

        [ProtoMember(39)]
        public bool LightEntry;

        [ProtoMember(40)]
        public bool SculptEntry;

        [ProtoMember(41)]
        public bool ProjectionEntry;

        [ProtoMember(42)]
        public Guid ProjectionTextureId;

        [ProtoMember(43)]
        public float ProjectionFOV;

        [ProtoMember(44)]
        public float ProjectionFocus;

        [ProtoMember(45)]
        public float ProjectionAmbiance;

        [ProtoMember(46)]
        public OpenMetaverse.PhysicsShapeType PreferredPhysicsShape;

        [ProtoMember(47)]
        public MediaEntrySnapshot[] MediaList;

        [ProtoMember(48)]
        public sbyte PathSkew;

        [ProtoMember(49)]
        public sbyte PathTaperX;

        [ProtoMember(50)]
        public sbyte PathTaperY;

        [ProtoMember(51)]
        public int VertexCount;

        [ProtoMember(52)]
        public int HighLODBytes;

        [ProtoMember(53)]
        public int MidLODBytes;

        [ProtoMember(54)]
        public int LowLODBytes;

        [ProtoMember(55)]
        public int LowestLODBytes;

        [ProtoMember(56)]
        public RenderMaterials RenderMaterials;

        static PrimShapeSnapshot()
        {
            ProtoBuf.Serializer.PrepareSerializer<PrimShapeSnapshot>();
        }

        public static PrimShapeSnapshot FromShape(PrimitiveBaseShape primitiveBaseShape)
        {
            return new PrimShapeSnapshot
            {
                ExtraParams = primitiveBaseShape.ExtraParams,
                FlexiDrag = primitiveBaseShape.FlexiDrag,
                FlexiEntry = primitiveBaseShape.FlexiEntry,
                FlexiForceX = primitiveBaseShape.FlexiForceX,
                FlexiForceY = primitiveBaseShape.FlexiForceY,
                FlexiForceZ = primitiveBaseShape.FlexiForceZ,
                FlexiGravity = primitiveBaseShape.FlexiGravity,
                FlexiSoftness = primitiveBaseShape.FlexiSoftness,
                FlexiTension = primitiveBaseShape.FlexiTension,
                FlexiWind = primitiveBaseShape.FlexiWind,
                HollowShape = primitiveBaseShape.HollowShape,
                LightColor = new float[] { primitiveBaseShape.LightColorA, primitiveBaseShape.LightColorR, primitiveBaseShape.LightColorG, primitiveBaseShape.LightColorB },
                LightCutoff = primitiveBaseShape.LightCutoff,
                LightEntry = primitiveBaseShape.LightEntry,
                LightIntensity = primitiveBaseShape.LightIntensity,
                LightRadius = primitiveBaseShape.LightRadius,
                PathBegin = primitiveBaseShape.PathBegin,
                PathCurve = primitiveBaseShape.PathCurve,
                PathEnd = primitiveBaseShape.PathEnd,
                PathRadiusOffset = primitiveBaseShape.PathRadiusOffset,
                PathRevolutions = primitiveBaseShape.PathRevolutions,
                PathScaleX = primitiveBaseShape.PathScaleX,
                PathScaleY = primitiveBaseShape.PathScaleY,
                PathShearX = primitiveBaseShape.PathShearX,
                PathShearY = primitiveBaseShape.PathShearY,
                PathSkew = primitiveBaseShape.PathSkew,
                PathTaperX = primitiveBaseShape.PathTaperX,
                PathTaperY = primitiveBaseShape.PathTaperY,
                PathTwist = primitiveBaseShape.PathTwist,
                PathTwistBegin = primitiveBaseShape.PathTwistBegin,
                PCode = primitiveBaseShape.PCode,
                PreferredPhysicsShape = primitiveBaseShape.PreferredPhysicsShape,
                ProfileBegin = primitiveBaseShape.ProfileBegin,
                ProfileCurve = primitiveBaseShape.ProfileCurve,
                ProfileEnd = primitiveBaseShape.ProfileEnd,
                ProfileHollow = primitiveBaseShape.ProfileHollow,
                ProfileShape = primitiveBaseShape.ProfileShape,
                ProjectionAmbiance = primitiveBaseShape.ProjectionAmbiance,
                ProjectionEntry = primitiveBaseShape.ProjectionEntry,
                ProjectionFocus = primitiveBaseShape.ProjectionFocus,
                ProjectionFOV = primitiveBaseShape.ProjectionFOV,
                ProjectionTextureId = primitiveBaseShape.ProjectionTextureUUID.Guid,
                Scale = primitiveBaseShape.Scale,
                SculptEntry = primitiveBaseShape.SculptEntry,
                SculptTexture = primitiveBaseShape.SculptTexture.Guid,
                SculptType = primitiveBaseShape.SculptType,
                State = primitiveBaseShape.State,
                TextureEntry = primitiveBaseShape.TextureEntryBytes,
                MediaList = MediaEntrySnapshot.SnapshotArrayFromList(primitiveBaseShape.Media),
                VertexCount = primitiveBaseShape.VertexCount,
                HighLODBytes = primitiveBaseShape.HighLODBytes,
                MidLODBytes = primitiveBaseShape.MidLODBytes,
                LowLODBytes = primitiveBaseShape.LowLODBytes,
                LowestLODBytes = primitiveBaseShape.LowestLODBytes,
                RenderMaterials = primitiveBaseShape.RenderMaterials
            };
        }

        public PrimitiveBaseShape ToPrimitiveBaseShape()
        {
            return new PrimitiveBaseShape
            {
                ExtraParams = this.ExtraParams,
                FlexiDrag = this.FlexiDrag,
                FlexiEntry = this.FlexiEntry,
                FlexiForceX = this.FlexiForceX,
                FlexiForceY = this.FlexiForceY,
                FlexiForceZ = this.FlexiForceZ,
                FlexiGravity = this.FlexiGravity,
                FlexiSoftness = this.FlexiSoftness,
                FlexiTension = this.FlexiTension,
                FlexiWind = this.FlexiWind,
                HollowShape = this.HollowShape,
                LightColorA = this.LightColor[0],
                LightColorR = this.LightColor[1],
                LightColorG = this.LightColor[2],
                LightColorB = this.LightColor[3],
                LightCutoff = this.LightCutoff,
                LightEntry = this.LightEntry,
                LightIntensity = this.LightIntensity,
                LightRadius = this.LightRadius,
                PathBegin = this.PathBegin,
                PathCurve = this.PathCurve,
                PathEnd = this.PathEnd,
                PathRadiusOffset = this.PathRadiusOffset,
                PathRevolutions = this.PathRevolutions,
                PathScaleX = this.PathScaleX,
                PathScaleY = this.PathScaleY,
                PathShearX = this.PathShearX,
                PathShearY = this.PathShearY,
                PathSkew = this.PathSkew,
                PathTaperX = this.PathTaperX,
                PathTaperY = this.PathTaperY,
                PathTwist = this.PathTwist,
                PathTwistBegin = this.PathTwistBegin,
                PCode = this.PCode,
                PreferredPhysicsShape = this.PreferredPhysicsShape,
                ProfileBegin = this.ProfileBegin,
                ProfileCurve = this.ProfileCurve,
                ProfileEnd = this.ProfileEnd,
                ProfileHollow = this.ProfileHollow,
                ProfileShape = this.ProfileShape,
                ProjectionAmbiance = this.ProjectionAmbiance,
                ProjectionEntry = this.ProjectionEntry,
                ProjectionFocus = this.ProjectionFocus,
                ProjectionFOV = this.ProjectionFOV,
                ProjectionTextureUUID = new OpenMetaverse.UUID(this.ProjectionTextureId),
                Scale = this.Scale,
                SculptEntry = this.SculptEntry,
                SculptTexture = new OpenMetaverse.UUID(this.SculptTexture),
                SculptType = this.SculptType,
                State = this.State,
                TextureEntryBytes = this.TextureEntry,
                Media = MediaEntrySnapshot.SnapshotArrayToList(this.MediaList),
                VertexCount = this.VertexCount,
                HighLODBytes = this.HighLODBytes,
                MidLODBytes = this.MidLODBytes,
                LowLODBytes = this.LowLODBytes,
                LowestLODBytes = this.LowestLODBytes,
                RenderMaterials = this.RenderMaterials != null ? this.RenderMaterials : new RenderMaterials()
            };
        }
    }
}
