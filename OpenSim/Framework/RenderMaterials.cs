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
    public struct RenderMaterial : ICloneable
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

        public static readonly Color4 DEFAULT_SPECULAR_LIGHT_COLOR = Color4.White;
        public const float  MATERIALS_MULTIPLIER = 10000.0f;

        public static readonly RenderMaterial DefaultMaterial = new RenderMaterial(UUID.Zero, UUID.Zero);

        #region Properties

        public UUID NormalID;

        [ProtoMember(1)]
        public Guid SerializableNormalID {
            get { return NormalID.Guid; }
            set { NormalID = new UUID(value); }
        }

        [ProtoMember(2)]
        public float NormalOffsetX;

        [ProtoMember(3)]
        public float NormalOffsetY;

        [ProtoMember(4)]
        public float NormalRepeatX;

        [ProtoMember(5)]
        public float NormalRepeatY;

        [ProtoMember(6)]
        public float NormalRotation;

        public UUID SpecularID;

        [ProtoMember(7)]
        public Guid SerializableSpecularID {
            get { return SpecularID.Guid; }
            set { SpecularID = new UUID(value); }
        }

        [ProtoMember(8)]
        public float SpecularOffsetX;

        [ProtoMember(9)]
        public float SpecularOffsetY;

        [ProtoMember(10)]
        public float SpecularRepeatX;

        [ProtoMember(11)]
        public float SpecularRepeatY;

        [ProtoMember(12)]
        public float SpecularRotation;

        [ProtoMember(13)]
        public byte SpecularLightColorR;

        [ProtoMember(14)]
        public byte SpecularLightColorG;

        [ProtoMember(15)]
        public byte SpecularLightColorB;

        [ProtoMember(16)]
        public byte SpecularLightColorA;

        [ProtoMember(17)]
        public byte SpecularLightExponent;

        [ProtoMember(18)]
        public byte EnvironmentIntensity;

        [ProtoMember(19)]
        public byte DiffuseAlphaMode;

        [ProtoMember(20)]
        public byte AlphaMaskCutoff;

        #endregion Properties

        public RenderMaterial(
            UUID normalID,
            UUID specularID,
            float normalOffsetX = 0.0f,
            float normalOffsetY = 0.0f,
            float normalRepeatX = 1.0f,
            float normalRepeatY = 1.0f,
            float normalRotation = 0.0f,
            float specularOffsetX = 0.0f,
            float specularOffsetY = 0.0f,
            float specularRepeatX = 1.0f,
            float specularRepeatY = 1.0f,
            float specularRotation = 0.0f,
            byte specularLightColorR = 255,
            byte specularLightColorG = 255,
            byte specularLightColorB = 255,
            byte specularLightColorA = 255,
            byte specularLightExponent = DEFAULT_SPECULAR_LIGHT_EXPONENT,
            byte environmentIntensity = DEFAULT_ENV_INTENSITY,
            eDiffuseAlphaMode diffuseAlphaMode = eDiffuseAlphaMode.DIFFUSE_ALPHA_MODE_BLEND,
            byte alphaMaskCutoff = 0
            )
        {
            NormalID = normalID;
            NormalOffsetX = normalOffsetX;
            NormalOffsetY = normalOffsetY;
            NormalRepeatX = normalRepeatX;
            NormalRepeatY = normalRepeatY;
            NormalRotation = normalRotation;

            SpecularID = specularID;
            SpecularOffsetX = specularOffsetX;
            SpecularOffsetY = specularOffsetY;
            SpecularRepeatX = specularRepeatX;
            SpecularRepeatY = specularRepeatY;
            SpecularRotation = specularRotation;

            SpecularLightColorR = specularLightColorR;
            SpecularLightColorG = specularLightColorG;
            SpecularLightColorB = specularLightColorB;
            SpecularLightColorA = specularLightColorA;

            SpecularLightExponent = specularLightExponent;
            EnvironmentIntensity = environmentIntensity;
            DiffuseAlphaMode = (byte)diffuseAlphaMode;
            AlphaMaskCutoff = alphaMaskCutoff;
        }

        public byte[] ComputeMD5Hash()
        {
            using (var md5 = MD5.Create())
                return md5.ComputeHash(ToBytes());
        }

        public static UUID GenerateMaterialID(RenderMaterial material)
        {
            return (new UUID(material.ComputeMD5Hash(), 0));
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
                SpecularLightColorR == other.SpecularLightColorR &&
                SpecularLightColorG == other.SpecularLightColorG &&
                SpecularLightColorB == other.SpecularLightColorB &&
                SpecularLightColorA == other.SpecularLightColorA &&
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
                    SpecularLightColorR.GetHashCode () ^
                    SpecularLightColorG.GetHashCode() ^
                    SpecularLightColorB.GetHashCode() ^
                    SpecularLightColorA.GetHashCode() ^
                    SpecularLightExponent.GetHashCode () ^
                    EnvironmentIntensity.GetHashCode () ^ 
                    DiffuseAlphaMode.GetHashCode () ^ 
                    AlphaMaskCutoff.GetHashCode ();
            }
        }

        public override string ToString ()
        {
            return string.Format (
                "NormalID : {0}, NormalOffsetX : {1}, NormalOffsetY : {2}, NormalRepeatX : {3}, NormalRepeatY : {4}, NormalRotation : {5}, " +
                "SpecularID : {6}, SpecularOffsetX : {7}, SpecularOffsetY : {8}, SpecularRepeatX : {9}, SpecularRepeatY : {10}, SpecularRotation : {11}, " +
                "SpecularLightColorR : {12}, SpecularLightColorG : {13}, SpecularLightColorB : {14}, SpecularLightColorA : {15}, SpecularLightExponent : {16}, " +
                "EnvironmentIntensity : {17}, DiffuseAlphaMode : {18}, AlphaMaskCutoff : {19}", 
                NormalID, NormalOffsetX, NormalOffsetY, NormalRepeatX, NormalRepeatY, NormalRotation, 
                SpecularID, SpecularOffsetX, SpecularOffsetY, SpecularRepeatX, SpecularRepeatY, SpecularRotation, 
                SpecularLightColorR, SpecularLightColorG, SpecularLightColorB, SpecularLightColorA, SpecularLightExponent, 
                EnvironmentIntensity, DiffuseAlphaMode, AlphaMaskCutoff);
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

            OSDArray specularColor = new OSDArray();
            specularColor.Add(OSD.FromInteger(SpecularLightColorR));
            specularColor.Add(OSD.FromInteger(SpecularLightColorG));
            specularColor.Add(OSD.FromInteger(SpecularLightColorB));
            specularColor.Add(OSD.FromInteger(SpecularLightColorA));

            material_data[MATERIALS_CAP_SPECULAR_COLOR_FIELD] = specularColor;
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

            OSDArray specularColor = map[MATERIALS_CAP_SPECULAR_COLOR_FIELD] as OSDArray;

            material.SpecularLightColorR = (byte)specularColor[0].AsInteger ();
            material.SpecularLightColorG = (byte)specularColor[1].AsInteger();
            material.SpecularLightColorB = (byte)specularColor[2].AsInteger();
            material.SpecularLightColorA = (byte)specularColor[3].AsInteger();

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
        protected Dictionary<Guid, RenderMaterial> Materials {
            get;
            set;
        }
#endregion

        public RenderMaterials()
        {
            Materials = new Dictionary<Guid, RenderMaterial> ();
        }

        public bool RemoveMaterial(UUID id)
        {
            lock (Materials)
            {
                if (Materials.ContainsKey(id.Guid))
                {
                    Materials.Remove(id.Guid);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public UUID AddMaterial(RenderMaterial mat)
        {
            lock (Materials)
            {
                UUID key = RenderMaterial.GenerateMaterialID(mat);

                if (Materials.ContainsKey(key.Guid) == false)
                    Materials[key.Guid] = mat;

                return key;
            }
        }

        public bool ContainsMaterial(UUID id)
        {
            lock (Materials)
            {
                return (Materials.ContainsKey(id.Guid));
            }
        }

        public RenderMaterial GetMaterial(UUID id)
        {
            lock (Materials)
            {
                if (Materials.ContainsKey(id.Guid))
                    return Materials[id.Guid];
            }

            // If we get here, there is no material by that ID.
            // It's a struct (can't return a null), we must return *some* material.
            return (RenderMaterial)RenderMaterial.DefaultMaterial.Clone();
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
                    UUID key = RenderMaterial.GenerateMaterialID(material);
                    Materials[key.Guid] = material;
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

        public RenderMaterials Copy()
        {
            return RenderMaterials.FromBytes(this.ToBytes(), 0);
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
                foreach (KeyValuePair<Guid, RenderMaterial> entry in Materials)
                    builder.AppendFormat (" MaterialId : {0}, RenderMaterial : {{ {1} }} ", entry.Key.ToString(), entry.Value.ToString ());
                builder.Append(" ]");
                return builder.ToString();
            }
        }
    }
}

