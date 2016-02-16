using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using ProtoBuf;

namespace OpenSim.Framework
{
    /// <summary>
    /// A single textured face. Don't instantiate this class yourself, use the
    /// methods in RenderMaterials
    /// </summary>
    [ProtoContract]
    public class RenderMaterial : ICloneable
    {
        public enum eDiffuseAlphaMode : byte
        {
            DIFFUSE_ALPHA_MODE_NONE = 0,
            DIFFUSE_ALPHA_MODE_BLEND = 1,
            DIFFUSE_ALPHA_MODE_MASK = 2,
            DIFFUSE_ALPHA_MODE_EMISSIVE = 3,
            DIFFUSE_ALPHA_MODE_DEFAULT = 4
        };

        public enum eShaderCount : byte
        {
            SHADER_COUNT = 16,
            ALPHA_SHADER_COUNT = 4
        };

        public const string  MATERIALS_CAP_NORMAL_MAP_FIELD            = "NormMap";
        public const string  MATERIALS_CAP_NORMAL_MAP_OFFSET_X_FIELD   = "NormOffsetX";
        public const string  MATERIALS_CAP_NORMAL_MAP_OFFSET_Y_FIELD   = "NormOffsetY";
        public const string  MATERIALS_CAP_NORMAL_MAP_REPEAT_X_FIELD   = "NormRepeatX";
        public const string  MATERIALS_CAP_NORMAL_MAP_REPEAT_Y_FIELD   = "NormRepeatY";
        public const string  MATERIALS_CAP_NORMAL_MAP_ROTATION_FIELD   = "NormRotation";

        public const string  MATERIALS_CAP_SPECULAR_MAP_FIELD          = "SpecMap";
        public const string  MATERIALS_CAP_SPECULAR_MAP_OFFSET_X_FIELD = "SpecOffsetX";
        public const string  MATERIALS_CAP_SPECULAR_MAP_OFFSET_Y_FIELD = "SpecOffsetY";
        public const string  MATERIALS_CAP_SPECULAR_MAP_REPEAT_X_FIELD = "SpecRepeatX";
        public const string  MATERIALS_CAP_SPECULAR_MAP_REPEAT_Y_FIELD = "SpecRepeatY";
        public const string  MATERIALS_CAP_SPECULAR_MAP_ROTATION_FIELD = "SpecRotation";

        public const string  MATERIALS_CAP_SPECULAR_COLOR_FIELD        = "SpecColor";
        public const string  MATERIALS_CAP_SPECULAR_EXP_FIELD          = "SpecExp";
        public const string  MATERIALS_CAP_ENV_INTENSITY_FIELD         = "EnvIntensity";
        public const string  MATERIALS_CAP_ALPHA_MASK_CUTOFF_FIELD     = "AlphaMaskCutoff";
        public const string  MATERIALS_CAP_DIFFUSE_ALPHA_MODE_FIELD    = "DiffuseAlphaMode";

        public const byte    DEFAULT_SPECULAR_LIGHT_EXPONENT = ((byte)(0.2f * 255));
        public const byte    DEFAULT_ENV_INTENSITY = 0;

        public static Color4 DEFAULT_SPECULAR_LIGHT_COLOR = new Color4 (255, 255, 255, 255);

#region Properties

        public UUID NormalID {
            get;
            set;
        }

        [ProtoMember(1)]
        public Guid SerializableNormalID {
            get { return NormalID.Guid; }
            set { NormalID = new UUID(value); }
        }

        [ProtoMember(2)]
        public float NormalOffsetX {
            get;
            set;
        }

        [ProtoMember(3)]
        public float NormalOffsetY {
            get;
            set;
        }

        [ProtoMember(4)]
        public float NormalRepeatX {
            get;
            set;
        }

        [ProtoMember(5)]
        public float NormalRepeatY {
            get;
            set;
        }

        [ProtoMember(6)]
        public float NormalRotation {
            get;
            set;
        }

        public UUID SpecularID {
            get;
            set;
        }

        [ProtoMember(7)]
        public Guid SerializableSpecularID {
            get { return SpecularID.Guid; }
            set { SpecularID = new UUID(value); }
        }

        [ProtoMember(8)]
        public float SpecularOffsetX {
            get;
            set;
        }

        [ProtoMember(9)]
        public float SpecularOffsetY {
            get;
            set;
        }

        [ProtoMember(10)]
        public float SpecularRepeatX {
            get;
            set;
        }

        [ProtoMember(11)]
        public float SpecularRepeatY {
            get;
            set;
        }

        [ProtoMember(12)]
        public float SpecularRotation {
            get;
            set;
        }
            
        public Color4 SpecularLightColor {
            get;
            set;
        }

        [ProtoMember(13)]
        public byte[] SerializableSpecularLightColor
        {
            get { return SpecularLightColor.GetBytes(); }
            set { SpecularLightColor.FromBytes(value, 0, false); }
        }

        [ProtoMember(14)]
        public byte SpecularLightExponent {
            get;
            set;
        }

        [ProtoMember(15)]
        public byte EnvironmentIntensity {
            get;
            set;
        }

        [ProtoMember(16)]
        public byte DiffuseAlphaMode {
            get;
            set;
        }

        [ProtoMember(17)]
        public byte AlphaMaskCutoff {
            get;
            set;
        }

#endregion Properties

        public RenderMaterial ()
        {
            NormalOffsetX = 0.0f;
            NormalOffsetY = 0.0f;
            NormalRepeatX = 1.0f;
            NormalRepeatY = 1.0f;
            NormalRotation = 0.0f;
            SpecularOffsetX = 0.0f;
            SpecularOffsetY = 0.0f;
            SpecularRepeatX = 1.0f;
            SpecularRepeatY = 1.0f;
            SpecularRotation = 0.0f;
            SpecularLightColor = DEFAULT_SPECULAR_LIGHT_COLOR;
            SpecularLightExponent = (byte)DEFAULT_SPECULAR_LIGHT_EXPONENT;
            EnvironmentIntensity = (byte)DEFAULT_ENV_INTENSITY;
            DiffuseAlphaMode = (byte)eDiffuseAlphaMode.DIFFUSE_ALPHA_MODE_BLEND;
            AlphaMaskCutoff = 0;
        }

        public override bool Equals (object obj)
        {
            if (obj == null)
                return false;
            if (ReferenceEquals (this, obj))
                return true;
            if (obj.GetType () != typeof(RenderMaterial))
                return false;
            
            RenderMaterial other = (RenderMaterial)obj;
            return  NormalID == other.NormalID && 
                NormalOffsetX == other.NormalOffsetX && 
                NormalOffsetY == other.NormalOffsetY && 
                NormalRepeatX == other.NormalRepeatX && 
                NormalRepeatY == other.NormalRepeatY && 
                NormalRotation == other.NormalRotation && 
                SpecularID == other.SpecularID && 
                SpecularOffsetX == other.SpecularOffsetX && 
                SpecularOffsetY == other.SpecularOffsetY && 
                SpecularRepeatX == other.SpecularRepeatX && 
                SpecularRepeatY == other.SpecularRepeatY && 
                SpecularRotation == other.SpecularRotation && 
                SpecularLightColor == other.SpecularLightColor && 
                SpecularLightExponent == other.SpecularLightExponent && 
                EnvironmentIntensity == other.EnvironmentIntensity && 
                DiffuseAlphaMode == other.DiffuseAlphaMode && 
                AlphaMaskCutoff == other.AlphaMaskCutoff;
        }

        public override int GetHashCode ()
        {
            unchecked {
                return 
                    NormalID.GetHashCode () ^ 
                    NormalOffsetX.GetHashCode () ^ 
                    NormalOffsetY.GetHashCode () ^ 
                    NormalRepeatX.GetHashCode () ^ 
                    NormalRepeatY.GetHashCode () ^ 
                    NormalRotation.GetHashCode () ^ 
                    SpecularID.GetHashCode () ^ 
                    SpecularOffsetX.GetHashCode () ^ 
                    SpecularOffsetY.GetHashCode () ^ 
                    SpecularRepeatX.GetHashCode () ^ 
                    SpecularRepeatY.GetHashCode () ^ 
                    SpecularRotation.GetHashCode () ^ 
                    SpecularLightColor.GetHashCode () ^ 
                    SpecularLightExponent.GetHashCode () ^
                    EnvironmentIntensity.GetHashCode () ^ 
                    DiffuseAlphaMode.GetHashCode () ^ 
                    AlphaMaskCutoff.GetHashCode ();
            }
        }

        public override string ToString ()
        {
            return string.Format ("NormalID : {0}, NormalOffsetX : {1}, NormalOffsetY : {2}, NormalRepeatX : {3}, NormalRepeatY : {4}, NormalRotation : {5}, SpecularID : {6}, SpecularOffsetX : {7}, SpecularOffsetY : {8}, SpecularRepeatX : {9}, SpecularRepeatY : {10}, SpecularRotation : {11}, SpecularLightColor : {12}, SpecularLightExponent : {13}, EnvironmentIntensity : {14}, DiffuseAlphaMode : {15}, AlphaMaskCutoff : {16}", 
                NormalID, NormalOffsetX, NormalOffsetY, NormalRepeatX, NormalRepeatY, NormalRotation, SpecularID, SpecularOffsetX, SpecularOffsetY, SpecularRepeatX, SpecularRepeatY, SpecularRotation, SpecularLightColor, SpecularLightExponent, EnvironmentIntensity, DiffuseAlphaMode, AlphaMaskCutoff);
        }

        public object Clone ()
        {
            RenderMaterial ret = (RenderMaterial)this.MemberwiseClone ();
            ret.NormalID = new UUID (this.NormalID);
            ret.SpecularID = new UUID (this.SpecularID);
            ret.SpecularLightColor = new Color4 (ret.SpecularLightColor);
            return ret;
        }

        public OSD GetOSD ()
        {
            OSDMap material_data = new OSDMap (17);

            material_data [MATERIALS_CAP_NORMAL_MAP_FIELD] = OSD.FromUUID(NormalID);
            material_data [MATERIALS_CAP_NORMAL_MAP_OFFSET_X_FIELD] = OSD.FromReal (NormalOffsetX);
            material_data [MATERIALS_CAP_NORMAL_MAP_OFFSET_Y_FIELD] = OSD.FromReal (NormalOffsetY);
            material_data [MATERIALS_CAP_NORMAL_MAP_REPEAT_X_FIELD] = OSD.FromReal (NormalRepeatX);
            material_data [MATERIALS_CAP_NORMAL_MAP_REPEAT_Y_FIELD] = OSD.FromReal (NormalRepeatY);
            material_data [MATERIALS_CAP_NORMAL_MAP_ROTATION_FIELD] = OSD.FromReal (NormalRotation);

            material_data [MATERIALS_CAP_SPECULAR_MAP_FIELD] = OSD.FromUUID (SpecularID);
            material_data [MATERIALS_CAP_SPECULAR_MAP_OFFSET_X_FIELD] = OSD.FromReal (SpecularOffsetX);
            material_data [MATERIALS_CAP_SPECULAR_MAP_OFFSET_Y_FIELD] = OSD.FromReal (SpecularOffsetY);
            material_data [MATERIALS_CAP_SPECULAR_MAP_REPEAT_X_FIELD] = OSD.FromReal (SpecularRepeatX);
            material_data [MATERIALS_CAP_SPECULAR_MAP_REPEAT_Y_FIELD] = OSD.FromReal (SpecularRepeatY);
            material_data [MATERIALS_CAP_SPECULAR_MAP_ROTATION_FIELD] = OSD.FromReal (SpecularRotation);

            material_data [MATERIALS_CAP_SPECULAR_COLOR_FIELD] = OSD.FromColor4 (SpecularLightColor);
            material_data [MATERIALS_CAP_SPECULAR_EXP_FIELD] = OSD.FromInteger ((int)SpecularLightExponent);
            material_data [MATERIALS_CAP_ENV_INTENSITY_FIELD] = OSD.FromInteger ((int)EnvironmentIntensity);
            material_data [MATERIALS_CAP_DIFFUSE_ALPHA_MODE_FIELD] = OSD.FromInteger ((int)DiffuseAlphaMode);
            material_data [MATERIALS_CAP_ALPHA_MASK_CUTOFF_FIELD] = OSD.FromInteger ((int)AlphaMaskCutoff);

            return material_data;
        }

        public static RenderMaterial FromOSD (OSD osd)
        {
            OSDMap map = osd as OSDMap;
            RenderMaterial material = new RenderMaterial ();

            material.NormalID = map [MATERIALS_CAP_NORMAL_MAP_FIELD].AsUUID ();
            material.NormalOffsetX = (float)map [MATERIALS_CAP_NORMAL_MAP_OFFSET_X_FIELD].AsReal ();
            material.NormalOffsetY = (float)map [MATERIALS_CAP_NORMAL_MAP_OFFSET_Y_FIELD].AsReal ();
            material.NormalRepeatX = (float)map [MATERIALS_CAP_NORMAL_MAP_REPEAT_X_FIELD].AsReal ();
            material.NormalRepeatY = (float)map [MATERIALS_CAP_NORMAL_MAP_REPEAT_Y_FIELD].AsReal ();
            material.NormalRotation = (float)map [MATERIALS_CAP_NORMAL_MAP_ROTATION_FIELD].AsReal ();

            material.SpecularID = map [MATERIALS_CAP_SPECULAR_MAP_FIELD].AsUUID ();
            material.SpecularOffsetX = (float)map [MATERIALS_CAP_SPECULAR_MAP_OFFSET_X_FIELD].AsReal ();
            material.SpecularOffsetY = (float)map [MATERIALS_CAP_SPECULAR_MAP_OFFSET_Y_FIELD].AsReal ();
            material.SpecularRepeatX = (float)map [MATERIALS_CAP_SPECULAR_MAP_REPEAT_X_FIELD].AsReal ();
            material.SpecularRepeatY = (float)map [MATERIALS_CAP_SPECULAR_MAP_REPEAT_Y_FIELD].AsReal ();
            material.SpecularRotation = (float)map [MATERIALS_CAP_SPECULAR_MAP_ROTATION_FIELD].AsReal ();

            material.SpecularLightColor = map [MATERIALS_CAP_SPECULAR_COLOR_FIELD].AsColor4 ();
            material.SpecularLightExponent = (byte)map [MATERIALS_CAP_SPECULAR_EXP_FIELD].AsInteger ();
            material.EnvironmentIntensity = (byte)map [MATERIALS_CAP_ENV_INTENSITY_FIELD].AsInteger ();
            material.DiffuseAlphaMode = (byte)map [MATERIALS_CAP_DIFFUSE_ALPHA_MODE_FIELD].AsInteger ();
            material.AlphaMaskCutoff = (byte)map [MATERIALS_CAP_ALPHA_MASK_CUTOFF_FIELD].AsInteger ();

            return material;
        }
    }

    /// <summary>
    /// Represents all of the materials faces for an object
    /// </summary>
    /// <remarks>Grid objects have infinite faces, with each face
    /// using the properties of the default face unless set otherwise. So if
    /// you have a RenderMaterial with a default texture uuid of X, and face 18
    /// has a texture UUID of Y, every face would be textured with X except for
    /// face 18 that uses Y. In practice however, primitives utilize a maximum
    /// of nine faces.  The values in this dictionary are linked through a UUID 
    /// key to the textures in a TextureEntry via MaterialID there.</remarks>
    [ProtoContract]
    public class RenderMaterials
    {
#region Properties

        [ProtoMember(1)]
        public Dictionary<String, RenderMaterial> Materials {
            get;
            private set;
        }
#endregion

        public RenderMaterials()
        {
            Materials = new Dictionary<String, RenderMaterial> ();
        }

        public static RenderMaterials FromBytes(byte[] bytes, int pos)
        {
            using (MemoryStream ms = new MemoryStream(bytes, pos, bytes.Length - pos))
            {
                return ProtoBuf.Serializer.Deserialize<RenderMaterials>(ms);
            }
        }

        public static RenderMaterials FromBytes(byte[] bytes, int start, int length)
        {
            using (MemoryStream ms = new MemoryStream(bytes, start, length))
            {
                return ProtoBuf.Serializer.Deserialize<RenderMaterials>(ms);
            }
        }

        public byte[] ToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<RenderMaterials>(ms, this);
                return ms.ToArray();
            }
        }

        public override int GetHashCode ()
        {
            lock (Materials) {
                int hashcode = 0;
                foreach (var mat in Materials.Values)
                    hashcode ^= mat.GetHashCode ();

                return hashcode;
            }
        }

        public override string ToString ()
        {
            lock (Materials) {
                StringBuilder builder = new StringBuilder ();
                builder.Append ("[ ");
                foreach (KeyValuePair<string, RenderMaterial> entry in Materials)
                    builder.AppendFormat (" MaterialId : {0}, RenderMaterial : {{ {1} }} ", entry.Key, entry.Value.ToString ());
                builder.Append(" ]");
                return builder.ToString();
            };
        }
    }
}

