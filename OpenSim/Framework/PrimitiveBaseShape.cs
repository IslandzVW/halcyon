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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public enum ProfileShape : byte
    {
        Circle = 0,
        Square = 1,
        IsometricTriangle = 2,
        EquilateralTriangle = 3,
        RightTriangle = 4,
        HalfCircle = 5
    }

    public enum HollowShape : byte
    {
        Same = 0,
        Circle = 16,
        Square = 32,
        Triangle = 48
    }

    public enum PCodeEnum : byte
    {
        Primitive = 9,
        Avatar = 47,
        Grass = 95,
        NewTree = 111,
        ParticleSystem = 143,
        Tree = 255
    }

    public enum Extrusion : byte
    {
        Straight = 16,
        Curve1 = 32,
        Curve2 = 48,
        Flexible = 128
    }

    [Serializable]
    public class PrimitiveBaseShape
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly UUID DEFAULT_TEXTURE_ID = new UUID("89556747-24cb-43ed-920b-47caed15465f");

        #region Properties

        // TextureEntries
        Primitive.TextureEntry m_textures;

        [XmlIgnore]
        public Primitive.TextureEntry Textures
        {
            get { return m_textures; }
            set
            {
                m_textures = value;
                m_textureEntryBytes = null;  // parallel data no longer in sync
            }
        }

        // Provided for better documentation/readability to document places where byte array accessors are used
        byte[] m_textureEntryBytes;  // persisted byte version of m_textures

        [XmlIgnore]
        public byte[] TextureEntryBytes
        {
            get
            {
                // Already calculated the bytes
                if (m_textureEntryBytes != null)
                    return m_textureEntryBytes;
                // need to recalc the bytes
                return m_textures.GetBytes();
            }
            set
            {
                m_textureEntryBytes = value;
                // Now initialize m_textures to match the byte[] m_textureEntry
                if (value == null)
                    m_textures = new Primitive.TextureEntry(UUID.Zero);
                else
                    m_textures = BytesToTextureEntry(value);
            }
        }

        // Called only during during serialization/deserialization/viewer packets
        // Provided for serialization compatibility, use Textures/TextureEntryBytes instead.
        public byte[] TextureEntry
        {
            get { return TextureEntryBytes; }
            set { TextureEntryBytes = value; }
        }

        public ushort PathBegin { set; get; }
        public byte PathCurve { get; set; }
        public ushort PathEnd { get; set; }
        public sbyte PathRadiusOffset { get; set; }
        public byte PathRevolutions { get; set; }
        public byte PathScaleX { get; set; }
        public byte PathScaleY { get; set; }
        public byte PathShearX { get; set; }
        public byte PathShearY { get; set; }
        public sbyte PathSkew { get; set; }
        public sbyte PathTaperX { get; set; }
        public sbyte PathTaperY { get; set; }
        public sbyte PathTwist { get; set; }
        public sbyte PathTwistBegin { get; set; }
        public byte PCode { get; set; }
        public ushort ProfileBegin { get; set; }
        public ushort ProfileEnd { get; set; }

        public byte ProfileCurve
        {
            get { return (byte)((byte)HollowShape | (byte)ProfileShape); }

            set
            {
                // Handle hollow shape component
                byte hollowShapeByte = (byte)(value & 0xf0);

                if (!Enum.IsDefined(typeof(HollowShape), hollowShapeByte))
                {
                    m_log.WarnFormat(
                        "[SHAPE]: Attempt to set a ProfileCurve with a hollow shape value of {0}, which isn't a valid enum.  Replacing with default shape.",
                        hollowShapeByte);

                    this.HollowShape = HollowShape.Same;
                }
                else
                {
                    this.HollowShape = (HollowShape)hollowShapeByte;
                }

                // Handle profile shape component
                byte profileShapeByte = (byte)(value & 0xf);

                if (!Enum.IsDefined(typeof(ProfileShape), profileShapeByte))
                {
                    m_log.WarnFormat(
                        "[SHAPE]: Attempt to set a ProfileCurve with a profile shape value of {0}, which isn't a valid enum.  Replacing with square.",
                        profileShapeByte);

                    this.ProfileShape = ProfileShape.Square;
                }
                else
                {
                    this.ProfileShape = (ProfileShape)profileShapeByte;
                }
            }
        }

        public ushort ProfileHollow { get; set; }
        public ProfileShape ProfileShape { get; set; }
        public HollowShape HollowShape { get; set; }

        public Vector3 Scale { get; set; }
        public byte State { get; set; }

        // Physics
        public PhysicsShapeType PreferredPhysicsShape { get; set; }

        // Materials
        [XmlIgnore]
        public RenderMaterials RenderMaterials { get; set; }

        // Used for XML Serialization
        public byte[] RenderMaterialsBytes
        {
            get { return RenderMaterials.ToBytes(); }
            set { RenderMaterials = RenderMaterials.FromBytes(value, 0); }
        }

        // Sculpted
        public bool SculptEntry { get; set; }
        public UUID SculptTexture { get; set; }
        public byte SculptType { get; set; }

        [XmlIgnore]
        public byte[] SculptData { get; set; }

        // Mesh
        public int VertexCount { get; set; }
        public int HighLODBytes { get; set; }
        public int MidLODBytes { get; set; }
        public int LowLODBytes { get; set; }
        public int LowestLODBytes { get; set; }

        // Flexi
        public bool FlexiEntry { get; set; }
        public int FlexiSoftness { get; set; }
        public float FlexiTension { get; set; }
        public float FlexiDrag { get; set; }
        public float FlexiGravity { get; set; }
        public float FlexiWind { get; set; }
        public float FlexiForceX { get; set; }
        public float FlexiForceY { get; set; }
        public float FlexiForceZ { get; set; }

        //Bright n sparkly
        public bool LightEntry { get; set; }
        public float LightColorR { get; set; }
        public float LightColorG { get; set; }
        public float LightColorB { get; set; }

        float _lightColorA = 1f;
        public float LightColorA
        {
            get { return _lightColorA; }
            set { _lightColorA = value; }
        }

        public float LightRadius { get; set; }
        public float LightCutoff { get; set; }
        public float LightFalloff { get; set; }

        float _lightIntensity = 1f;
        public float LightIntensity
        {
            get { return _lightIntensity; }
            set { _lightIntensity = Math.Min(value, 1.0f); }
        }

        // Light Projection Filter
        public bool ProjectionEntry { get; set; }

        /// <summary>
        /// Gets or sets the UUID of the texture the projector emits.
        /// </summary>
        /// <value>The projection texture UUID.</value>
        public UUID ProjectionTextureUUID { get; set; }

        /// <summary>
        /// Gets or sets the projection Field of View in radians.
        /// Valid range is from 0.0 to 3.0. Invalid values are clamped to the valid range.
        /// </summary>
        /// <value>The projection FOV.</value>
        float _projectionFOV;
        public float ProjectionFOV
        {
            get { return _projectionFOV; }
            set { _projectionFOV = Util.Clip(value, 0.0f, 3.0f); }
        }

        /// <summary>
        /// Gets or sets the projection focus - aka how far away from the source prim the texture will be sharp.  Beyond this value the texture and its border will gradually get blurry out to the limit of the effective range.
        /// </summary>
        /// <value>The projection focus distance in meters.</value>
        public float ProjectionFocus { get; set; }

        /// <summary>
        /// Gets or sets the projection ambiance - the brightness of a very blurred edition of the projected texture that is placed on all faces of all objects within the projector's FOV and effective range.
        /// Valid range is from 0.0 on up. Invalid values are clamped to the valid range.
        /// </summary>
        /// <value>The projection ambiance brightness.</value>
        float _projectionAmb;
        public float ProjectionAmbiance
        {
            get { return _projectionAmb; }
            set { _projectionAmb = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Entries to store media textures on each face
        /// </summary>
        /// Do not change this value directly - always do it through an IMoapModule.
        /// Lock before manipulating.
        public PrimMedia Media { get; set; }

        #endregion

        #region Constructors

        public PrimitiveBaseShape()
        {
            SculptTexture = UUID.Zero;
            SculptData = new byte[0];
            PCode = (byte)PCodeEnum.Primitive;
            ExtraParams = new byte[1];
            Textures = new Primitive.TextureEntry(DEFAULT_TEXTURE_ID);
            RenderMaterials = new RenderMaterials();
        }

        /// <summary>
        /// Construct a PrimitiveBaseShape object from a OpenMetaverse.Primitive object
        /// </summary>
        /// <param name="prim"></param>
        public PrimitiveBaseShape(Primitive prim)
        {
            //            m_log.DebugFormat("[PRIMITIVE BASE SHAPE]: Creating from {0}", prim.ID);
            PCode = (byte)prim.PrimData.PCode;
            ExtraParams = new byte[1];

            State = prim.PrimData.State;
            PathBegin = Primitive.PackBeginCut(prim.PrimData.PathBegin);
            PathEnd = Primitive.PackEndCut(prim.PrimData.PathEnd);
            PathScaleX = Primitive.PackPathScale(prim.PrimData.PathScaleX);
            PathScaleY = Primitive.PackPathScale(prim.PrimData.PathScaleY);
            PathShearX = (byte)Primitive.PackPathShear(prim.PrimData.PathShearX);
            PathShearY = (byte)Primitive.PackPathShear(prim.PrimData.PathShearY);
            PathSkew = Primitive.PackPathTwist(prim.PrimData.PathSkew);
            ProfileBegin = Primitive.PackBeginCut(prim.PrimData.ProfileBegin);
            ProfileEnd = Primitive.PackEndCut(prim.PrimData.ProfileEnd);
            Scale = prim.Scale;
            PathCurve = (byte)prim.PrimData.PathCurve;
            ProfileCurve = (byte)prim.PrimData.ProfileCurve;
            ProfileHollow = Primitive.PackProfileHollow(prim.PrimData.ProfileHollow);
            PathRadiusOffset = Primitive.PackPathTwist(prim.PrimData.PathRadiusOffset);
            PathRevolutions = Primitive.PackPathRevolutions(prim.PrimData.PathRevolutions);
            PathTaperX = Primitive.PackPathTaper(prim.PrimData.PathTaperX);
            PathTaperY = Primitive.PackPathTaper(prim.PrimData.PathTaperY);
            PathTwist = Primitive.PackPathTwist(prim.PrimData.PathTwist);
            PathTwistBegin = Primitive.PackPathTwist(prim.PrimData.PathTwistBegin);

            Textures = prim.Textures;   // also updates TextureEntry (and TextureEntryBytes)

            if (prim.Sculpt != null)
            {
                SculptEntry = (prim.Sculpt.Type != OpenMetaverse.SculptType.None);
                SculptData = prim.Sculpt.GetBytes();
                SculptTexture = prim.Sculpt.SculptTexture;
                SculptType = (byte)prim.Sculpt.Type;
            }
            else
            {
                SculptType = (byte)OpenMetaverse.SculptType.None;
                SculptTexture = UUID.Zero;
                SculptData = new byte[0];
            }

            RenderMaterials = new RenderMaterials();
        }

        #endregion


        private Primitive.TextureEntry BytesToTextureEntry(byte[] data)
        {
            try
            {
                return new Primitive.TextureEntry(data, 0, data.Length);
            }
            catch
            {
                m_log.WarnFormat("[SHAPE]: Failed to decode texture entries, length={0}", (data == null) ? 0 : data.Length);
                return new Primitive.TextureEntry(UUID.Zero);
            }
        }

        public static PrimitiveBaseShape Default
        {
            get
            {
                PrimitiveBaseShape boxShape = CreateBox();

                boxShape.SetScale(0.5f);

                return boxShape;
            }
        }

        public static PrimitiveBaseShape Create()
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();
            return shape;
        }

        public static PrimitiveBaseShape CreateBox()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Straight;
            shape.ProfileShape = ProfileShape.Square;
            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public static PrimitiveBaseShape CreateSphere()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Curve1;
            shape.ProfileShape = ProfileShape.HalfCircle;
            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public static PrimitiveBaseShape CreateCylinder()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Curve1;
            shape.ProfileShape = ProfileShape.Square;

            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public void SetScale(float side)
        {
            Scale = new Vector3(side, side, side);
        }

        public void SetHeight(float height)
        {
            Scale = new Vector3(Scale.X, Scale.Y, height);
        }

        public void SetRadius(float radius)
        {
            float diameter = radius * 2f;
            Scale = new Vector3(diameter, diameter, Scale.Z);
        }

        public PrimitiveBaseShape Copy()
        {
            PrimitiveBaseShape shape = (PrimitiveBaseShape)MemberwiseClone();
            shape.TextureEntryBytes = (byte[])TextureEntryBytes.Clone();
            shape.Media = new PrimMedia(Media);
            shape.RenderMaterials = RenderMaterials.Copy();

            return shape;
        }

        public static PrimitiveBaseShape CreateCylinder(float radius, float height)
        {
            PrimitiveBaseShape shape = CreateCylinder();

            shape.SetHeight(height);
            shape.SetRadius(radius);

            return shape;
        }

        public void SetPathRange(Vector3 pathRange)
        {
            PathBegin = Primitive.PackBeginCut(pathRange.X);
            PathEnd = Primitive.PackEndCut(pathRange.Y);
        }

        public void SetPathRange(float begin, float end)
        {
            PathBegin = Primitive.PackBeginCut(begin);
            PathEnd = Primitive.PackEndCut(end);
        }

        public void SetSculptProperties(byte sculptType, UUID SculptTextureUUID)
        {
            SculptType = sculptType;
            SculptTexture = SculptTextureUUID;
        }

        public void SetProfileRange(Vector3 profileRange)
        {
            ProfileBegin = Primitive.PackBeginCut(profileRange.X);
            ProfileEnd = Primitive.PackEndCut(profileRange.Y);
        }

        public void SetProfileRange(float begin, float end)
        {
            ProfileBegin = Primitive.PackBeginCut(begin);
            ProfileEnd = Primitive.PackEndCut(end);
        }

        [XmlIgnore]
        public byte[] ExtraParams
        {
            get
            {
                return ExtraParamsToBytes();
            }
            set
            {
                ReadInExtraParamsBytes(value);
            }
        }



        /// <summary>
        /// Calculate a hash value over fields that can affect the underlying physics shape.
        /// Things like RenderMaterials and TextureEntry data are not included.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="lod"></param>
        /// <returns>ulong - a calculated hash value</returns>
        public ulong GetMeshKey(Vector3 size, float lod)
        {
            ulong hash = 5381;

            hash = djb2(hash, this.PathCurve);
            hash = djb2(hash, (byte)((byte)this.HollowShape | (byte)this.ProfileShape));
            hash = djb2(hash, this.PathBegin);
            hash = djb2(hash, this.PathEnd);
            hash = djb2(hash, this.PathScaleX);
            hash = djb2(hash, this.PathScaleY);
            hash = djb2(hash, this.PathShearX);
            hash = djb2(hash, this.PathShearY);
            hash = djb2(hash, (byte)this.PathTwist);
            hash = djb2(hash, (byte)this.PathTwistBegin);
            hash = djb2(hash, (byte)this.PathRadiusOffset);
            hash = djb2(hash, (byte)this.PathTaperX);
            hash = djb2(hash, (byte)this.PathTaperY);
            hash = djb2(hash, this.PathRevolutions);
            hash = djb2(hash, (byte)this.PathSkew);
            hash = djb2(hash, this.ProfileBegin);
            hash = djb2(hash, this.ProfileEnd);
            hash = djb2(hash, this.ProfileHollow);

            // TODO: Separate scale out from the primitive shape data (after
            // scaling is supported at the physics engine level)
            byte[] scaleBytes = size.GetBytes();
            for (int i = 0; i < scaleBytes.Length; i++)
                hash = djb2(hash, scaleBytes[i]);

            // Include LOD in hash, accounting for endianness
            byte[] lodBytes = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes(lod), 0, lodBytes, 0, 4);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(lodBytes, 0, 4);
            }
            for (int i = 0; i < lodBytes.Length; i++)
                hash = djb2(hash, lodBytes[i]);

            // include sculpt UUID
            if (this.SculptEntry)
            {
                scaleBytes = this.SculptTexture.GetBytes();
                for (int i = 0; i < scaleBytes.Length; i++)
                    hash = djb2(hash, scaleBytes[i]);

                hash = djb2(hash, this.SculptType);
            }

            return hash;
        }

        private ulong djb2(ulong hash, byte c)
        {
            return ((hash << 5) + hash) + (ulong)c;
        }

        private ulong djb2(ulong hash, ushort c)
        {
            hash = ((hash << 5) + hash) + (ulong)((byte)c);
            return ((hash << 5) + hash) + (ulong)(c >> 8);
        }

        public byte[] ExtraParamsToBytes()
        {
//            m_log.DebugFormat("[EXTRAPARAMS]: Called ExtraParamsToBytes()");

            bool hasSculptEntry = false;

            ushort FlexiEP = 0x10;
            ushort LightEP = 0x20;
            ushort SculptEP = 0x30;
            ushort ProjectionEP = 0x40;

            int i = 0;
            uint TotalBytesLength = 1; // ExtraParamsNum

            uint ExtraParamsNum = 0;
            if (FlexiEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 16;// data
                TotalBytesLength += 2 + 4; // type
            }

            if (LightEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 16;// data
                TotalBytesLength += 2 + 4; // type
            }

            if (SculptEntry)
            {
                hasSculptEntry = true;
                ExtraParamsNum++;
                TotalBytesLength += 17;// data
                TotalBytesLength += 2 + 4; // type
            }

            if (ProjectionEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 28;// data
                TotalBytesLength += 2 + 4;// type
            }

            byte[] returnbytes = new byte[TotalBytesLength];

            // uint paramlength = ExtraParamsNum;

            // Stick in the number of parameters
            returnbytes[i++] = (byte)ExtraParamsNum;

            if (FlexiEntry)
            {
                byte[] FlexiData = GetFlexiBytes();

                returnbytes[i++] = (byte)(FlexiEP % 256);
                returnbytes[i++] = (byte)((FlexiEP >> 8) % 256);

                returnbytes[i++] = (byte)(FlexiData.Length % 256);
                returnbytes[i++] = (byte)((FlexiData.Length >> 8) % 256);
                returnbytes[i++] = (byte)((FlexiData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((FlexiData.Length >> 24) % 256);
                Array.Copy(FlexiData, 0, returnbytes, i, FlexiData.Length);
                i += FlexiData.Length;
            }

            if (LightEntry)
            {
                byte[] LightData = GetLightBytes();

                returnbytes[i++] = (byte)(LightEP % 256);
                returnbytes[i++] = (byte)((LightEP >> 8) % 256);

                returnbytes[i++] = (byte)(LightData.Length % 256);
                returnbytes[i++] = (byte)((LightData.Length >> 8) % 256);
                returnbytes[i++] = (byte)((LightData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((LightData.Length >> 24) % 256);
                Array.Copy(LightData, 0, returnbytes, i, LightData.Length);
                i += LightData.Length;
            }

            if (hasSculptEntry)
            {
                byte[] SculptData = GetSculptBytes();

                returnbytes[i++] = (byte)(SculptEP % 256);
                returnbytes[i++] = (byte)((SculptEP >> 8) % 256);

                returnbytes[i++] = (byte)(SculptData.Length % 256);
                returnbytes[i++] = (byte)((SculptData.Length >> 8) % 256);
                returnbytes[i++] = (byte)((SculptData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((SculptData.Length >> 24) % 256);
                Array.Copy(SculptData, 0, returnbytes, i, SculptData.Length);
                i += SculptData.Length;
            }

            if (ProjectionEntry)
            {
                byte[] ProjectionData = GetProjectionBytes();

                returnbytes[i++] = (byte)(ProjectionEP % 256);
                returnbytes[i++] = (byte)((ProjectionEP >> 8) % 256);
                returnbytes[i++] = (byte)((ProjectionData.Length) % 256);
                returnbytes[i++] = (byte)((ProjectionData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((ProjectionData.Length >> 20) % 256);
                returnbytes[i++] = (byte)((ProjectionData.Length >> 24) % 256);
                Array.Copy(ProjectionData, 0, returnbytes, i, ProjectionData.Length);
                i += ProjectionData.Length;
            }

            if (!FlexiEntry && !LightEntry && !SculptEntry && !ProjectionEntry)
            {
                byte[] returnbyte = new byte[1];
                returnbyte[0] = 0;
                return returnbyte;
            }

            return returnbytes;
        }

        public void ReadInUpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            const ushort FlexiEP = 0x10;
            const ushort LightEP = 0x20;
            const ushort SculptEP = 0x30;
            const ushort ProjectionEP = 0x40;

            switch (type)
            {
                case FlexiEP:
                    if (!inUse)
                    {
                        FlexiEntry = false;
                        return;
                    }
                    ReadFlexiData(data, 0);
                    break;

                case LightEP:
                    if (!inUse)
                    {
                        LightEntry = false;
                        return;
                    }
                    ReadLightData(data, 0);
                    break;

                case SculptEP:
                    if (!inUse)
                    {
                        SculptEntry = false;
                        return;
                    }
                    ReadSculptData(data, 0);
                    break;
                case ProjectionEP:
                    if (!inUse)
                    {
                        ProjectionEntry = false;
                        return;
                    }
                    ReadProjectionData(data, 0);
                    break;
            }
        }

        public void ReadInExtraParamsBytes(byte[] data)
        {
            if (data == null || data.Length == 1)
                return;

            const ushort FlexiEP = 0x10;
            const ushort LightEP = 0x20;
            const ushort SculptEP = 0x30;
            const ushort ProjectionEP = 0x40;

            bool lGotFlexi = false;
            bool lGotLight = false;
            bool lGotSculpt = false;
            bool lGotFilter = false;

            int i = 0;
            byte extraParamCount = 0;
            if (data.Length > 0)
            {
                extraParamCount = data[i++];
            }

            for (int k = 0; k < extraParamCount; k++)
            {
                ushort epType = Utils.BytesToUInt16(data, i);

                i += 2;
                // uint paramLength = Helpers.BytesToUIntBig(data, i);

                i += 4;
                switch (epType)
                {
                    case FlexiEP:
                        ReadFlexiData(data, i);
                        i += 16;
                        lGotFlexi = true;
                        break;

                    case LightEP:
                        ReadLightData(data, i);
                        i += 16;
                        lGotLight = true;
                        break;

                    case SculptEP:
                        ReadSculptData(data, i);
                        i += 17;
                        lGotSculpt = true;
                        break;
                    case ProjectionEP:
                        ReadProjectionData(data, i);
                        i += 28;
                        lGotFilter = true;
                        break;
                }
            }

            if (!lGotFlexi)
                FlexiEntry = false;
            if (!lGotLight)
                LightEntry = false;
            if (!lGotSculpt)
                SculptEntry = false;
            if (!lGotFilter)
                ProjectionEntry = false;
        }

        public void ReadSculptData(byte[] data, int pos)
        {
            byte[] SculptTextureUUID = new byte[16];
            UUID SculptUUID = UUID.Zero;
            byte SculptTypel = data[16+pos];

            if (data.Length+pos >= 17)
            {
                SculptEntry = true;
                SculptTextureUUID = new byte[16];
                SculptTypel = data[16 + pos];
                Array.Copy(data, pos, SculptTextureUUID,0, 16);
                SculptUUID = new UUID(SculptTextureUUID, 0);
            }
            else
            {
                SculptEntry = false;
                SculptUUID = UUID.Zero;
                SculptTypel = 0x00;
            }

            if (SculptEntry)
            {
                if (SculptType != (byte)1 && SculptType != (byte)2 && SculptType != (byte)3 && SculptType != (byte)4)
                    SculptType = 4;
            }

            SculptTexture = SculptUUID;
            SculptType = SculptTypel;
            //m_log.Info("[SCULPT]:" + SculptUUID.ToString());
        }

        public byte[] GetSculptBytes()
        {
            byte[] data = new byte[17];

            SculptTexture.GetBytes().CopyTo(data, 0);
            data[16] = (byte)SculptType;

            return data;
        }

        /// <summary>
        /// Simple routine that clamps a float value to a min/max
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bmin"></param>
        /// <param name="bmax"></param>
        /// <returns>The calculated value in min/max range.</returns>
        private static float llclamp(float b, float bmin, float bmax)
        {
            if (b < bmin) b = bmin;
            if (b > bmax) b = bmax;
            return b;
        }

        /// <summary>
        /// Get Server Weight for a Mesh based on the vertex count against an average # of vertices.
        /// </summary>
        /// <returns>A floating point server weight cost "score" </returns>
        public float GetServerWeight()
        {
            return (GetServerWeight(VertexCount));
        }

        /// <summary>
        /// Get Server Weight for a Mesh based on the vertex count against an average # of vertices.
        /// </summary>
        /// <returns>A floating point server weight cost "score" </returns>
        public static float GetServerWeight(int vertexCount)
        {
            const int AverageVerticesPerPrim = 422;
            return ((float)vertexCount / (float)AverageVerticesPerPrim);
        }

        /// <summary>
        /// Get Streaming Cost for a Mesh based on the byte count (which corresponds to tri-count) for each of the LOD
        /// levels we stored when the mesh was uploaded.
        /// </summary>
        /// <returns>A floating point streaming cost "score" </returns>
        public float GetStreamingCost()
        {
            return (GetStreamingCost(Scale, HighLODBytes, MidLODBytes, LowestLODBytes, LowestLODBytes));
        }

        /// <summary>
        /// Get Streaming Cost for a Mesh based on the byte count (which corresponds to tri-count) for each of the LOD
        /// levels we stored when the mesh was uploaded.
        /// </summary>
        /// <returns>A floating point streaming cost "score" </returns>
        public static float GetStreamingCost(Vector3 scale, int hibytes, int midbytes, int lowbytes, int lowestbytes)
        {
            float streaming_cost = 0.0f;

            const float MAX_AREA = 102932.0f;           // The area of a circle that encompasses a region.
            const float MIN_AREA = 1.0f;                // Minimum area we will consider.
            const float MAX_DISTANCE = 512.0f;          // Distance in meters
            const float METADATA_DISCOUNT = 128.0f;     // Number of bytes to deduct for metadata when determining streaming cost.
            const float MINIMUM_SIZE = 16.0f;           // Minimum number of bytes per LoD block when determining streaming cost
            const float BYTES_PER_TRIANGLE = 16.0f;     // Approximation of bytes per triangle to use for determining mesh streaming cost.
            const float TRIANGLE_BUDGET = 250000.0f;    // Target visible triangle budget to use when estimating streaming cost.
            const float MESH_COST_SCALAR = 15000.0f;    // Prim budget. Prims per region max

            try
            {
                float radius = (Vector3.Mag(scale) / (float)2.0);

                // LOD Distances
                float dlowest = Math.Min(radius / 0.03f, MAX_DISTANCE);
                float dlow = Math.Min(radius / 0.06f, MAX_DISTANCE);
                float dmid = Math.Min(radius / 0.24f, MAX_DISTANCE);

                int bytes_high = hibytes;
                int bytes_mid = midbytes;
                int bytes_low = lowbytes;
                int bytes_lowest = lowestbytes;

                if (bytes_high <= 0)
                    return 0.0f;
                if (bytes_mid <= 0)
                    bytes_mid = bytes_high;
                if (bytes_low <= 0)
                    bytes_low = bytes_mid;
                if (bytes_lowest <= 0)
                    bytes_lowest = bytes_low;

                float triangles_lowest = Math.Max((float)bytes_lowest - METADATA_DISCOUNT, MINIMUM_SIZE) / BYTES_PER_TRIANGLE;
                float triangles_low = Math.Max((float)bytes_low - METADATA_DISCOUNT, MINIMUM_SIZE) / BYTES_PER_TRIANGLE;
                float triangles_mid = Math.Max((float)bytes_mid - METADATA_DISCOUNT, MINIMUM_SIZE) / BYTES_PER_TRIANGLE;
                float triangles_high = Math.Max((float)bytes_high - METADATA_DISCOUNT, MINIMUM_SIZE) / BYTES_PER_TRIANGLE;

                float high_area = Math.Min((float)Math.PI * dmid * dmid, MAX_AREA);
                float mid_area = Math.Min((float)Math.PI * dlow * dlow, MAX_AREA);
                float low_area = Math.Min((float)Math.PI * dlowest * dlowest, MAX_AREA);
                float lowest_area = MAX_AREA;

                lowest_area -= low_area;
                low_area -= mid_area;
                mid_area -= high_area;

                high_area = llclamp(high_area, MIN_AREA, MAX_AREA);
                mid_area = llclamp(mid_area, MIN_AREA, MAX_AREA);
                low_area = llclamp(low_area, MIN_AREA, MAX_AREA);
                lowest_area = llclamp(lowest_area, MIN_AREA, MAX_AREA);

                float total_area = high_area + mid_area + low_area + lowest_area;

                high_area /= total_area;
                mid_area /= total_area;
                low_area /= total_area;
                lowest_area /= total_area;

                float weighted_average = (triangles_high * high_area) + (triangles_mid * mid_area) +
                                         (triangles_low * low_area) + (triangles_lowest * lowest_area);

                streaming_cost = weighted_average / TRIANGLE_BUDGET * MESH_COST_SCALAR;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MESH]: exception calculating streaming cost: {0}", e);
                streaming_cost = 0.0f;
            }

            return streaming_cost;
        }

        public void ReadFlexiData(byte[] data, int pos)
        {
            if (data.Length-pos >= 16)
            {
                FlexiEntry = true;
                FlexiSoftness = ((data[pos] & 0x80) >> 6) | ((data[pos + 1] & 0x80) >> 7);

                FlexiTension = (float)(data[pos++] & 0x7F) / 10.0f;
                FlexiDrag = (float)(data[pos++] & 0x7F) / 10.0f;
                FlexiGravity = (float)(data[pos++] / 10.0f) - 10.0f;
                FlexiWind = (float)data[pos++] / 10.0f;
                Vector3 lForce = new Vector3(data, pos);
                FlexiForceX = lForce.X;
                FlexiForceY = lForce.Y;
                FlexiForceZ = lForce.Z;
            }
            else
            {
                FlexiEntry = false;
                FlexiSoftness = 0;

                FlexiTension = 0.0f;
                FlexiDrag = 0.0f;
                FlexiGravity = 0.0f;
                FlexiWind = 0.0f;
                FlexiForceX = 0f;
                FlexiForceY = 0f;
                FlexiForceZ = 0f;
            }
        }

        public byte[] GetFlexiBytes()
        {
            byte[] data = new byte[16];
            int i = 0;

            // Softness is packed in the upper bits of tension and drag
            data[i] = (byte)((FlexiSoftness & 2) << 6);
            data[i + 1] = (byte)((FlexiSoftness & 1) << 7);

            data[i++] |= (byte)((byte)(FlexiTension * 10.01f) & 0x7F);
            data[i++] |= (byte)((byte)(FlexiDrag * 10.01f) & 0x7F);
            data[i++] = (byte)((FlexiGravity + 10.0f) * 10.01f);
            data[i++] = (byte)(FlexiWind * 10.01f);
            Vector3 lForce = new Vector3(FlexiForceX, FlexiForceY, FlexiForceZ);
            lForce.GetBytes().CopyTo(data, i);

            return data;
        }

        public void ReadLightData(byte[] data, int pos)
        {
            if (data.Length - pos >= 16)
            {
                LightEntry = true;
                Color4 lColor = new Color4(data, pos, false);
                LightIntensity = Math.Min(lColor.A, 1.0f);
                LightColorA = 1f;
                LightColorR = lColor.R;
                LightColorG = lColor.G;
                LightColorB = lColor.B;

                LightRadius = Utils.BytesToFloat(data, pos + 4);
                LightCutoff = Utils.BytesToFloat(data, pos + 8);
                LightFalloff = Utils.BytesToFloat(data, pos + 12);
            }
            else
            {
                LightEntry = false;
                LightColorA = 1f;
                LightColorR = 0f;
                LightColorG = 0f;
                LightColorB = 0f;
                LightRadius = 0f;
                LightCutoff = 0f;
                LightFalloff = 0f;
                LightIntensity = 0f;
            }
        }

        public byte[] GetLightBytes()
        {
            byte[] data = new byte[16];

            // Alpha channel in color is intensity
            Color4 tmpColor = new Color4(LightColorR,LightColorG,LightColorB, Math.Min(LightIntensity, 1.0f));

            tmpColor.GetBytes().CopyTo(data, 0);
            Utils.FloatToBytes(LightRadius).CopyTo(data, 4);
            Utils.FloatToBytes(LightCutoff).CopyTo(data, 8);
            Utils.FloatToBytes(LightFalloff).CopyTo(data, 12);

            return data;
        }

        public void ReadProjectionData(byte[] data, int pos)
        {
            byte[] projectionTextureUUID = new byte[16];

            if (data.Length - pos >= 28)
            {
                ProjectionEntry = true;
                Array.Copy(data, pos, projectionTextureUUID,0, 16);
                ProjectionTextureUUID = new UUID(projectionTextureUUID, 0);

                ProjectionFOV = Utils.BytesToFloat(data, pos + 16);
                ProjectionFocus = Utils.BytesToFloat(data, pos + 20);
                ProjectionAmbiance = Utils.BytesToFloat(data, pos + 24);
            }
            else
            {
                ProjectionEntry = false;
                ProjectionTextureUUID = UUID.Zero;
                ProjectionFOV = 0f;
                ProjectionFocus = 0f;
                ProjectionAmbiance = 0f;
            }
        }

        public byte[] GetProjectionBytes()
        {
            byte[] data = new byte[28];

            ProjectionTextureUUID.GetBytes().CopyTo(data, 0);
            Utils.FloatToBytes(ProjectionFOV).CopyTo(data, 16);
            Utils.FloatToBytes(ProjectionFocus).CopyTo(data, 20);
            Utils.FloatToBytes(ProjectionAmbiance).CopyTo(data, 24);

            return data;
        }

        /// <summary>
        /// Return a single MaterialID for a given Face
        /// </summary>
        /// <returns>The UUID of the Material or UUID.Zero if none is set</returns>
        public UUID GetMaterialID(int face)
        {
            UUID id;

            if (face < 0)
            {
                return Textures.DefaultTexture.MaterialID;
            }
            else
            {
                var faceEntry = Textures.CreateFace((uint)face);
                return faceEntry.MaterialID;
            }
        }

        /// <summary>
        /// Return a list of deduplicated materials ids from the texture entry.
        /// We remove duplicates because a materialid may be used across faces and we only
        /// need to represent it here once.
        /// </summary>
        /// <returns>The List of UUIDs found, possibly empty if no materials are in use.</returns>
        public List<UUID> GetMaterialIDs()
        {
            List<UUID> matIds = new List<UUID>();

            if (Textures != null)
            {
                if ((Textures.DefaultTexture != null) &&
                    (Textures.DefaultTexture.MaterialID != UUID.Zero))
                {
                    matIds.Add(Textures.DefaultTexture.MaterialID);
                }

                foreach (var face in Textures.FaceTextures)
                {
                    if ((face != null) && (face.MaterialID != UUID.Zero))
                    {
                        if (matIds.Contains(face.MaterialID) == false)
                            matIds.Add(face.MaterialID);
                    }
                }
            }

            return matIds;
        }

        /// <summary>
        /// Creates a OpenMetaverse.Primitive and populates it with converted PrimitiveBaseShape values
        /// </summary>
        /// <returns></returns>
        public Primitive ToOmvPrimitive()
        {
            // position and rotation defaults here since they are not available in PrimitiveBaseShape
            return ToOmvPrimitive(new Vector3(0.0f, 0.0f, 0.0f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f));
        }


        /// <summary>
        /// Creates a OpenMetaverse.Primitive and populates it with converted PrimitiveBaseShape values
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public Primitive ToOmvPrimitive(Vector3 position, Quaternion rotation)
        {
            OpenMetaverse.Primitive prim = new OpenMetaverse.Primitive();

            prim.Scale = this.Scale;
            prim.Position = position;
            prim.Rotation = rotation;

            if (this.SculptEntry)
            {
                prim.Sculpt = new Primitive.SculptData();
                prim.Sculpt.Type = (OpenMetaverse.SculptType)this.SculptType;
                prim.Sculpt.SculptTexture = this.SculptTexture;
            }

            prim.PrimData.PathShearX = Primitive.UnpackPathShear((sbyte)this.PathShearX);
            prim.PrimData.PathShearY = Primitive.UnpackPathShear((sbyte)this.PathShearY);
            prim.PrimData.PathBegin = (float)this.PathBegin * 2.0e-5f;
            prim.PrimData.PathEnd = 1.0f - (float)this.PathEnd * 2.0e-5f;

            prim.PrimData.PathScaleX = (200 - this.PathScaleX) * 0.01f;
            prim.PrimData.PathScaleY = (200 - this.PathScaleY) * 0.01f;

            prim.PrimData.PathTaperX = this.PathTaperX * 0.01f;
            prim.PrimData.PathTaperY = this.PathTaperY * 0.01f;

            prim.PrimData.PathTwistBegin = this.PathTwistBegin * 0.01f;
            prim.PrimData.PathTwist = this.PathTwist * 0.01f;

            prim.PrimData.ProfileBegin = (float)this.ProfileBegin * 2.0e-5f;
            prim.PrimData.ProfileEnd = 1.0f - (float)this.ProfileEnd * 2.0e-5f;
            prim.PrimData.ProfileHollow = (float)this.ProfileHollow * 2.0e-5f;

            prim.PrimData.profileCurve = this.ProfileCurve;
            prim.PrimData.ProfileHole = (HoleType)this.HollowShape;

            prim.PrimData.PathCurve = (PathCurve)this.PathCurve;
            prim.PrimData.PathRadiusOffset = 0.01f * this.PathRadiusOffset;
            prim.PrimData.PathRevolutions = 1.0f + 0.015f * this.PathRevolutions;
            prim.PrimData.PathSkew = 0.01f * this.PathSkew;

            prim.PrimData.PCode = OpenMetaverse.PCode.Prim;
            prim.PrimData.State = 0;

            if (this.FlexiEntry)
            {
                prim.Flexible = new Primitive.FlexibleData();
                prim.Flexible.Drag = this.FlexiDrag;
                prim.Flexible.Force = new Vector3(this.FlexiForceX, this.FlexiForceY, this.FlexiForceZ);
                prim.Flexible.Gravity = this.FlexiGravity;
                prim.Flexible.Softness = this.FlexiSoftness;
                prim.Flexible.Tension = this.FlexiTension;
                prim.Flexible.Wind = this.FlexiWind;
            }

            if (this.LightEntry)
            {
                prim.Light = new Primitive.LightData();
                prim.Light.Color = new Color4(this.LightColorR, this.LightColorG, this.LightColorB, this.LightColorA);
                prim.Light.Cutoff = this.LightCutoff;
                prim.Light.Falloff = this.LightFalloff;
                prim.Light.Intensity = this.LightIntensity;
                prim.Light.Radius = this.LightRadius;
            }

            prim.Textures = this.Textures;

            prim.Properties = new Primitive.ObjectProperties();
            prim.Properties.Name = "Primitive";
            prim.Properties.Description = String.Empty;
            prim.Properties.CreatorID = UUID.Zero;
            prim.Properties.GroupID = UUID.Zero;
            prim.Properties.OwnerID = UUID.Zero;
            prim.Properties.Permissions = new Permissions();
            prim.Properties.SalePrice = 10;
            prim.Properties.SaleType = new SaleType();

            return prim;
        }

        /// <summary>
        /// Encapsulates a list of media entries.
        /// </summary>
        /// This class is necessary because we want to replace auto-serialization of PrimMedia with something more
        /// OSD like and less vulnerable to change.
        public class PrimMedia : IXmlSerializable
        {
            public const string MEDIA_TEXTURE_TYPE = "sl";

            // keys are 0-based (face number - 1)
//            protected Dictionary<int,MediaEntry> m_MediaList = new Dictionary<int,MediaEntry>();
            [NonSerialized]
            protected MediaEntry[] m_MediaFaces = null;

            public PrimMedia() : base() 
            {
                New(0);
            }
            public PrimMedia(int capacity) : base()
            {
                New(capacity);
            }
            public PrimMedia(MediaEntry[] entries) : base()
            {
                m_MediaFaces = entries.Clone() as MediaEntry[];
            }
            public PrimMedia(PrimMedia other) : base()
            {
                if ((other == null) || (other.m_MediaFaces == null))
                {
                    New(0);
                }
                else
                {
                    lock (this)
                    {
                        m_MediaFaces = other.CopyArray();
                    }
                }
            }

            public int Count
            {
                get { return (m_MediaFaces == null) ? 0 : m_MediaFaces.Length; }
            }

            public MediaEntry this[int face]
            {
                get { lock (this) return m_MediaFaces[face]; }
                set { lock (this) m_MediaFaces[face] = value; }
            }

            public void Resize(int newSize)
            {
                lock (this)
                {
                    if (m_MediaFaces == null)
                    {
                        New(newSize);
                        return;
                    }

                    Array.Resize<MediaEntry>(ref m_MediaFaces, newSize);
                }
            }

            // This should be implemented in OpenMetaverse.MediaEntry but isn't.
            private MediaEntry MediaEntryCopy(MediaEntry entry)
            {
                if (entry == null) return null;

                MediaEntry newEntry = new MediaEntry();
                newEntry.AutoLoop = entry.AutoLoop;
                newEntry.AutoPlay = entry.AutoPlay;
                newEntry.AutoScale = entry.AutoScale;
                newEntry.AutoZoom = entry.AutoZoom;
                newEntry.ControlPermissions = entry.ControlPermissions;
                newEntry.Controls = entry.Controls;
                newEntry.CurrentURL = entry.CurrentURL;
                newEntry.EnableAlterntiveImage = entry.EnableAlterntiveImage;
                newEntry.EnableWhiteList = entry.EnableWhiteList;
                newEntry.Height = entry.Height;
                newEntry.HomeURL = entry.HomeURL;
                newEntry.InteractOnFirstClick = entry.InteractOnFirstClick;
                newEntry.InteractPermissions = entry.InteractPermissions;
                newEntry.Width = entry.Width;
                if (entry.WhiteList != null)
                    newEntry.WhiteList = (string[])entry.WhiteList.Clone();
                else
                    entry.WhiteList = null;
                newEntry.Width = entry.Width;

                return newEntry;
            }

            public MediaEntry[] CopyArray()
            {
                lock (this)
                {
                    if (m_MediaFaces == null)
                        return null;

                    int len = m_MediaFaces.Length;
                    MediaEntry[] copyFaces = new MediaEntry[len];
                    for (int x=0; x<len; x++)
                    {
                        copyFaces[x] = MediaEntryCopy(m_MediaFaces[x]);
                    }
                    return copyFaces;
                }
            }

            /// <summary>
            /// This method frees the media array if capacity is less than 1 (media list with no entries), otherwise allocates a new array of the specified size.
            /// </summary>
            /// <param name="capacity"></param>
            public void New(int capacity)
            {
                lock (this)
                {
                    // m_MediaList = new Dictionary<int, MediaEntry>();
                    if (capacity <= 0)
                        m_MediaFaces = null;
                    else
                        m_MediaFaces = new MediaEntry[capacity];
                }
            }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public string ToXml()
            {
                lock (this)
                {
                    using (StringWriter sw = new StringWriter())
                    {
                        using (XmlTextWriter xtw = new XmlTextWriter(sw))
                        {
                            xtw.WriteStartElement("OSMedia");
                            xtw.WriteAttributeString("type", MEDIA_TEXTURE_TYPE);
                            xtw.WriteAttributeString("version", "0.1");

                            OSDArray meArray = new OSDArray();
                            lock (this)
                            {
                                if (m_MediaFaces != null)
                                {
                                    foreach (MediaEntry me in m_MediaFaces)
                                    {
                                        OSD osd = (null == me ? new OSD() : me.GetOSD());
                                        meArray.Add(osd);
                                    }
                                }
                            }

                            xtw.WriteStartElement("OSData");
                            xtw.WriteRaw(OSDParser.SerializeLLSDXmlString(meArray));
                            xtw.WriteEndElement();

                            xtw.WriteEndElement();

                            xtw.Flush();
                            return sw.ToString();
                        }
                    }
                }
            }

            public void WriteXml(XmlWriter writer)
            {
                writer.WriteRaw(ToXml());
            }

            public static PrimMedia FromXml(string rawXml)
            {
                PrimMedia ml = new PrimMedia();
                ml.ReadXml(rawXml);
                return ml;
            }

            public void ReadXml(string rawXml)
            {
                if (rawXml.StartsWith("&lt;"))
                {
                    rawXml = rawXml.Replace("&lt;", "<").Replace("&gt;", ">");
                }

                using (StringReader sr = new StringReader(rawXml))
                {
                    using (XmlTextReader xtr = new XmlTextReader(sr))
                    {
                        xtr.MoveToContent();

                        string type = xtr.GetAttribute("type");
                        //m_log.DebugFormat("[MOAP]: Loaded media texture entry with type {0}", type);

                        if (type != MEDIA_TEXTURE_TYPE)
                            return;

                        xtr.ReadStartElement("OSMedia");

                        OSDArray osdMeArray = (OSDArray)OSDParser.DeserializeLLSDXml(xtr.ReadInnerXml());
                        lock (this)
                        {
                            this.New(osdMeArray.Count);
                            int index = 0;
                            foreach (OSD osdMe in osdMeArray)
                            {
                                m_MediaFaces[index++] = (osdMe is OSDMap ? MediaEntry.FromOSD(osdMe) : new MediaEntry());
                            }
                        }

                        xtr.ReadEndElement();
                    }
                }
            }

            public void ReadXml(XmlReader reader)
            {
                if (reader.IsEmptyElement)
                    return;

                ReadXml(reader.ReadInnerXml());
            }
        }
    }
}
