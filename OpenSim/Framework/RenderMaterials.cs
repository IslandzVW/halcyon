using System;
using System.Security.Cryptography;
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

        public const float  MATERIALS_MULTIPLIER = 10000.0f;

        #region Properties

        /// <summary>
        /// MaterialID - A calculated value computed from the contents of the class. 
        /// Basically a hash value that given 2 classes are the same should produce the same value.
        /// </summary>
        private Object m_lock = new object();


        private UUID m_materialID = UUID.Zero;

        public UUID MaterialID
        {
            get
            {
                lock (m_lock)
                {
                    if (m_materialID == UUID.Zero)
                    {
                        using (var md5 = MD5.Create())
                            m_materialID = new UUID(md5.ComputeHash(ToBytes()), 0);
                    }

                    return m_materialID;
                }
            }
        }

        private UUID m_normalID = UUID.Zero;

        public UUID NormalID {
            get
            {
                lock (m_lock)
                {
                    return m_normalID;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_normalID = value;
                }
            }
        }

        [ProtoMember(1)]
        public Guid SerializableNormalID {
            get { return NormalID.Guid; }
            set { NormalID = new UUID(value); }
        }

        private float m_NormalOffsetX;

        [ProtoMember(2)]
        public float NormalOffsetX {
            get
            {
                lock (m_lock)
                {
                    return m_NormalOffsetX;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_NormalOffsetX = value;
                }
            }
        }

        private float m_NormalOffsetY;

        [ProtoMember(3)]
        public float NormalOffsetY {
            get
            {
                lock (m_lock)
                {
                    return m_NormalOffsetY;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_NormalOffsetY = value;
                }
            }
        }

        private float m_NormalRepeatX;

        [ProtoMember(4)]
        public float NormalRepeatX {
            get
            {
                lock (m_lock)
                {
                    return m_NormalRepeatX;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_NormalRepeatX = value;
                }
            }
        }

        private float m_NormalRepeatY;

        [ProtoMember(5)]
        public float NormalRepeatY {
            get
            {
                lock (m_lock)
                {
                    return m_NormalRepeatY;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_NormalRepeatY = value;
                }
            }
        }

        private float m_NormalRotation;

        [ProtoMember(6)]
        public float NormalRotation {
            get
            {
                lock (m_lock)
                {
                    return m_NormalRotation;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_NormalRotation = value;
                }
            }
        }

        private UUID m_SpecularID;

        public UUID SpecularID {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularID;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularID = value;
                }
            }
        }

        [ProtoMember(7)]
        public Guid SerializableSpecularID {
            get { return SpecularID.Guid; }
            set { SpecularID = new UUID(value); }
        }

        private float m_SpecularOffsetX;

        [ProtoMember(8)]
        public float SpecularOffsetX {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularOffsetX;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularOffsetX = value;
                }
            }
        }

        private float m_SpecularOffsetY;

        [ProtoMember(9)]
        public float SpecularOffsetY {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularOffsetY;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularOffsetY = value;
                }
            }
        }

        private float m_SpecularRepeatX;

        [ProtoMember(10)]
        public float SpecularRepeatX {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularRepeatX;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularRepeatX = value;
                }
            }
        }

        private float m_SpecularRepeatY;

        [ProtoMember(11)]
        public float SpecularRepeatY {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularRepeatY;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularRepeatY = value;
                }
            }
        }

        private float m_SpecularRotation;

        [ProtoMember(12)]
        public float SpecularRotation {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularRotation;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularRotation = value;
                }
            }
        }

        private Color4 m_SpecularLightColor;

        public Color4 SpecularLightColor {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularLightColor;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularLightColor = value;
                }
            }
        }

        [ProtoMember(13)]
        public byte[] SerializableSpecularLightColor
        {
            get { return SpecularLightColor.GetBytes(); }
            set { SpecularLightColor.FromBytes(value, 0, false); }
        }

        private byte m_SpecularLightExponent;

        [ProtoMember(14)]
        public byte SpecularLightExponent {
            get
            {
                lock (m_lock)
                {
                    return m_SpecularLightExponent;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_SpecularLightExponent = value;
                }
            }
        }

        private byte m_EnvironmentIntensity;

        [ProtoMember(15)]
        public byte EnvironmentIntensity {
            get
            {
                lock (m_lock)
                {
                    return m_EnvironmentIntensity;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_EnvironmentIntensity = value;
                }
            }
        }

        private byte m_DiffuseAlphaMode;

        [ProtoMember(16)]
        public byte DiffuseAlphaMode {
            get
            {
                lock (m_lock)
                {
                    return m_DiffuseAlphaMode;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_DiffuseAlphaMode = value;
                }
            }
        }

        private byte m_AlphaMaskCutoff;

        [ProtoMember(17)]
        public byte AlphaMaskCutoff {
            get
            {
                lock (m_lock)
                {
                    return m_AlphaMaskCutoff;
                }
            }
            set
            {
                lock (m_lock)
                {
                    m_materialID = UUID.Zero;
                    m_AlphaMaskCutoff = value;
                }
            }
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

        public byte[] ToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<RenderMaterial>(ms, this);
                return ms.ToArray();
            }
        }

        public static RenderMaterial FromBytes(byte[] bytes, int start, int length)
        {
            using (MemoryStream ms = new MemoryStream(bytes, start, length))
            {
                return ProtoBuf.Serializer.Deserialize<RenderMaterial>(ms);
            }
        }

        public object Clone ()
        {
            RenderMaterial ret = (RenderMaterial)this.MemberwiseClone ();
            ret.NormalID = new UUID (this.NormalID);
            ret.SpecularID = new UUID (this.SpecularID);
            ret.SpecularLightColor = new Color4 (ret.SpecularLightColor);
            return ret;
        }

        private int llRound(float f)
        {
            return (int)Math.Round(f, MidpointRounding.AwayFromZero);
        }

        public OSD GetOSD ()
        {
            OSDMap material_data = new OSDMap (17);

            material_data [MATERIALS_CAP_NORMAL_MAP_FIELD] = OSD.FromUUID(NormalID);

            material_data [MATERIALS_CAP_NORMAL_MAP_OFFSET_X_FIELD] = OSD.FromInteger(llRound(NormalOffsetX * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_NORMAL_MAP_OFFSET_Y_FIELD] = OSD.FromInteger(llRound(NormalOffsetY * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_NORMAL_MAP_REPEAT_X_FIELD] = OSD.FromInteger(llRound(NormalRepeatX * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_NORMAL_MAP_REPEAT_Y_FIELD] = OSD.FromInteger(llRound(NormalRepeatY * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_NORMAL_MAP_ROTATION_FIELD] = OSD.FromInteger(llRound(NormalRotation * MATERIALS_MULTIPLIER));

            material_data [MATERIALS_CAP_SPECULAR_MAP_FIELD] = OSD.FromUUID (SpecularID);
            material_data [MATERIALS_CAP_SPECULAR_MAP_OFFSET_X_FIELD] = OSD.FromInteger(llRound(SpecularOffsetX * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_SPECULAR_MAP_OFFSET_Y_FIELD] = OSD.FromInteger(llRound(SpecularOffsetY * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_SPECULAR_MAP_REPEAT_X_FIELD] = OSD.FromInteger(llRound(SpecularRepeatX * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_SPECULAR_MAP_REPEAT_Y_FIELD] = OSD.FromInteger(llRound(SpecularRepeatY * MATERIALS_MULTIPLIER));
            material_data [MATERIALS_CAP_SPECULAR_MAP_ROTATION_FIELD] = OSD.FromInteger(llRound(SpecularRotation * MATERIALS_MULTIPLIER));

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
            material.NormalOffsetX = (float)map [MATERIALS_CAP_NORMAL_MAP_OFFSET_X_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.NormalOffsetY = (float)map [MATERIALS_CAP_NORMAL_MAP_OFFSET_Y_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.NormalRepeatX = (float)map [MATERIALS_CAP_NORMAL_MAP_REPEAT_X_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.NormalRepeatY = (float)map [MATERIALS_CAP_NORMAL_MAP_REPEAT_Y_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.NormalRotation = (float)map [MATERIALS_CAP_NORMAL_MAP_ROTATION_FIELD].AsInteger() / MATERIALS_MULTIPLIER;

            material.SpecularID = map [MATERIALS_CAP_SPECULAR_MAP_FIELD].AsUUID ();
            material.SpecularOffsetX = (float)map [MATERIALS_CAP_SPECULAR_MAP_OFFSET_X_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.SpecularOffsetY = (float)map [MATERIALS_CAP_SPECULAR_MAP_OFFSET_Y_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.SpecularRepeatX = (float)map [MATERIALS_CAP_SPECULAR_MAP_REPEAT_X_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.SpecularRepeatY = (float)map [MATERIALS_CAP_SPECULAR_MAP_REPEAT_Y_FIELD].AsInteger() / MATERIALS_MULTIPLIER;
            material.SpecularRotation = (float)map [MATERIALS_CAP_SPECULAR_MAP_ROTATION_FIELD].AsInteger() / MATERIALS_MULTIPLIER;

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
        protected Dictionary<String, RenderMaterial> Materials {
            get;
            set;
        }
#endregion

        public RenderMaterials()
        {
            Materials = new Dictionary<String, RenderMaterial> ();
        }

        public bool RemoveMaterial(UUID id)
        {
            lock (Materials)
            {
                string key = id.ToString();

                if (Materials.ContainsKey(key))
                {
                    Materials.Remove(key);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public RenderMaterial AddMaterial(RenderMaterial mat)
        {
            lock (Materials)
            {
                string key = mat.MaterialID.ToString();

                if (Materials.ContainsKey(key))
                {
                    return Materials[key];
                }
                else
                {
                    Materials[key] = mat;
                    return mat;
                }
            }
        }

        public bool ContainsMaterial(UUID id)
        {
            lock (Materials)
            {
                return (Materials.ContainsKey(id.ToString()));
            }
        }

        public RenderMaterial FindMaterial(UUID id)
        {
            lock (Materials)
            {
                if (Materials.ContainsKey(id.ToString()))
                    return Materials[id.ToString()];
                else
                    return null;
            }
        }

        public List<RenderMaterial> GetMaterials()
        {
            lock (Materials)
            {
                return new List<RenderMaterial>(Materials.Values);
            }
        }

        public void SetMaterials(List<RenderMaterial> mats)
        {
            lock (Materials)
            {
                Materials.Clear();
                foreach (var material in mats)
                {
                    string key = material.MaterialID.ToString();
                    Materials[key] = material;
                }
            }
        }
        public List<UUID> GetMaterialIDs()
        {
            lock (Materials)
            {
                var keys = new List<UUID>();
                foreach (var key in Materials.Keys)
                    keys.Add(new UUID(key));

                return keys;
            }
        }

        public static RenderMaterials FromBytes(byte[] bytes, int pos)
        {
            return (FromBytes(bytes, pos, bytes.Length - pos));
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
            lock (Materials)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    ProtoBuf.Serializer.Serialize<RenderMaterials>(ms, this);
                    return ms.ToArray();
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != typeof(RenderMaterials))
                return false;

            RenderMaterials other = (RenderMaterials)obj;
            if (this.Materials.Count != other.Materials.Count)
                return false;

            foreach (var kvp in this.Materials)
            {
                RenderMaterial thisValue = kvp.Value;
                RenderMaterial otherValue ;
                if (!other.Materials.TryGetValue(kvp.Key, out otherValue))
                    return false;
                if (thisValue.Equals(otherValue) == false)
                    return false;
            }

            return true;
        }

        public override int GetHashCode ()
        {
            lock (Materials)
            {
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

