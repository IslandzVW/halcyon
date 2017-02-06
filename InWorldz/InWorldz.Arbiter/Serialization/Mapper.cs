using System;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace InWorldz.Arbiter.Serialization
{
    /// <summary>
    /// Maps between a flatbuffer primitive and a halcyon primitive and vice versa
    /// </summary>
    public class Mapper
    {
        /// <summary>
        /// Converts from a flatbuffer vector3 to an OMV vector3
        /// </summary>
        /// <param name="flatVector3"></param>
        /// <returns></returns>
        public static OpenMetaverse.Vector3 ToOmvVec3(Vector3? flatVector3)
        {
            if (flatVector3.HasValue)
            {
                return new OpenMetaverse.Vector3(flatVector3.Value.X, flatVector3.Value.Y, flatVector3.Value.Z);
            }

            return OpenMetaverse.Vector3.Zero;
        }

        /// <summary>
        /// Converts from a flatbuffer byte string to an OMV UUID
        /// </summary>
        /// <param name="bytes">The byte string</param>
        /// <returns>An OMV UUID</returns>
        public static UUID ToOmvUuid(ArraySegment<byte>? bytes)
        {
            return bytes.HasValue ? new UUID(bytes.Value.Array, bytes.Value.Offset) : new UUID();
        }

        /// <summary>
        /// Converts from a flatbuffer quaternion to an OMV quaternion
        /// </summary>
        /// <param name="flatQuat"></param>
        /// <returns></returns>
        public static OpenMetaverse.Quaternion ToOmvQuat(Quaternion? flatQuat)
        {
            return flatQuat.HasValue ? new OpenMetaverse.Quaternion(flatQuat.Value.X, flatQuat.Value.Y, flatQuat.Value.Z, flatQuat.Value.W) : OpenMetaverse.Quaternion.Identity;
        }

        /// <summary>
        /// Returns a new byte array from the given segment
        /// </summary>
        /// <param name="bytes">The segment to copy bytes from</param>
        /// <returns>A new array or null if the segment is null</returns>
        public static byte[] GetBytesFromSegment(ArraySegment<byte>? bytes)
        {
            if (bytes.HasValue)
            {
                byte[] array = new byte[bytes.Value.Count];
                Buffer.BlockCopy(bytes.Value.Array, bytes.Value.Offset, array, 0, bytes.Value.Count);
                return array;
            }

            return null;
        }

        /// <summary>
        /// Maps the flatbuffer primshape object to a halcyon primitivebaseshape
        /// </summary>
        /// <param name="flatPrimBaseShape">The flat</param>
        /// <returns></returns>
        public static PrimitiveBaseShape MapFlatbufferPrimBaseShapeToBaseShape(HalcyonPrimitiveBaseShape? flatPrimBaseShape)
        {
            if (flatPrimBaseShape == null)
            {
                return null;
            }

            return new PrimitiveBaseShape
            {
                ExtraParams = GetBytesFromSegment(flatPrimBaseShape.Value.GetExtraParamsBytes()),
                FlexiDrag = flatPrimBaseShape.Value.FlexiDrag,
                FlexiEntry = flatPrimBaseShape.Value.FlexiEntry,
                FlexiForceX = flatPrimBaseShape.Value.FlexiForceX,
                FlexiForceY = flatPrimBaseShape.Value.FlexiForceY,
                FlexiForceZ = flatPrimBaseShape.Value.FlexiForceZ,
                FlexiGravity = flatPrimBaseShape.Value.FlexiGravity,
                FlexiSoftness = flatPrimBaseShape.Value.FlexiSoftness,
                FlexiTension = flatPrimBaseShape.Value.FlexiTension,
                FlexiWind = flatPrimBaseShape.Value.FlexiWind,
                HollowShape = (OpenSim.Framework.HollowShape)flatPrimBaseShape.Value.HollowShape,
                LightColorA = flatPrimBaseShape.Value.LightColor(0),
                LightColorR = flatPrimBaseShape.Value.LightColor(1),
                LightColorG = flatPrimBaseShape.Value.LightColor(2),
                LightColorB = flatPrimBaseShape.Value.LightColor(3),
                LightCutoff = flatPrimBaseShape.Value.LightCutoff,
                LightEntry = flatPrimBaseShape.Value.LightEntry,
                LightIntensity = flatPrimBaseShape.Value.LightIntensity,
                LightRadius = flatPrimBaseShape.Value.LightRadius,
                PathBegin = flatPrimBaseShape.Value.PathBegin,
                PathCurve = flatPrimBaseShape.Value.PathCurve,
                PathEnd = flatPrimBaseShape.Value.PathEnd,
                PathRadiusOffset = flatPrimBaseShape.Value.PathRadiusOffset,
                PathRevolutions = flatPrimBaseShape.Value.PathRevolutions,
                PathScaleX = flatPrimBaseShape.Value.PathScaleX,
                PathScaleY = flatPrimBaseShape.Value.PathScaleY,
                PathShearX = flatPrimBaseShape.Value.PathShearX,
                PathShearY = flatPrimBaseShape.Value.PathShearY,
                PathSkew = flatPrimBaseShape.Value.PathSkew,
                PathTaperX = flatPrimBaseShape.Value.PathTaperX,
                PathTaperY = flatPrimBaseShape.Value.PathTaperY,
                PathTwist = flatPrimBaseShape.Value.PathTwist,
                PathTwistBegin = flatPrimBaseShape.Value.PathTwistBegin,
                PCode = flatPrimBaseShape.Value.Pcode,
                ProfileBegin = flatPrimBaseShape.Value.ProfileBegin,
                ProfileCurve = flatPrimBaseShape.Value.ProfileCurve,
                ProfileEnd = flatPrimBaseShape.Value.ProfileEnd,
                ProfileHollow = flatPrimBaseShape.Value.ProfileHollow,
                ProfileShape = (OpenSim.Framework.ProfileShape)flatPrimBaseShape.Value.ProfileShape,
                ProjectionAmbiance = flatPrimBaseShape.Value.ProjectionAmbiance,
                ProjectionEntry = flatPrimBaseShape.Value.ProjectionEntry,
                ProjectionFocus = flatPrimBaseShape.Value.ProjectionFocus,
                ProjectionFOV = flatPrimBaseShape.Value.ProjectionFov,
                ProjectionTextureUUID = ToOmvUuid(flatPrimBaseShape.Value.GetProjectionTextureIdBytes()),
                Scale = ToOmvVec3(flatPrimBaseShape.Value.Scale),
                SculptEntry = flatPrimBaseShape.Value.SculptEntry,
                SculptTexture = ToOmvUuid(flatPrimBaseShape.Value.GetSculptTextureBytes()),
                SculptType = flatPrimBaseShape.Value.SculptType,
                State = flatPrimBaseShape.Value.State,
                TextureEntryBytes = GetBytesFromSegment(flatPrimBaseShape.Value.GetTextureEntryBytes()),
                VertexCount = flatPrimBaseShape.Value.VertexCount,
                HighLODBytes = flatPrimBaseShape.Value.HighLodBytes,
                MidLODBytes = flatPrimBaseShape.Value.MidLodBytes,
                LowLODBytes = flatPrimBaseShape.Value.LowLodBytes,
                LowestLODBytes = flatPrimBaseShape.Value.LowestLodBytes
            };
        }
        
        /// <summary>
        /// Maps a flatbuffer primitive to a scene object part
        /// </summary>
        /// <param name="prim">The flatbuffer serialized primitive</param>
        /// <returns>A new SceneObjectPart from the serialized primitive</returns>
        public static SceneObjectPart MapFlatbufferPrimToPart(HalcyonPrimitive prim)
        {
            return new SceneObjectPart()
            {
                AngularVelocity = ToOmvVec3(prim.AngularVelocityTarget),
                PhysicalAngularVelocity = ToOmvVec3(prim.AngularVelocity),
                CreatorID = ToOmvUuid(prim.GetCreatorIdBytes()),
                Description = prim.Description,
                GroupPosition = ToOmvVec3(prim.GroupPosition),
                UUID = ToOmvUuid(prim.GetIdBytes()),
                LinkNum = prim.LinkNumber,
                LocalId = prim.LocalId,
                Name = prim.Name,
                ObjectFlags = (uint) prim.ObjectFlags,
                OffsetPosition = ToOmvVec3(prim.OffsetPosition),
                ParentID = prim.ParentId,
                RotationOffset = ToOmvQuat(prim.RotationOffset),
                Scale = ToOmvVec3(prim.Scale),
                Shape = MapFlatbufferPrimBaseShapeToBaseShape(prim.Shape),
                Sound = ToOmvUuid(prim.GetSoundBytes()),
                SoundOptions = prim.SoundFlags,
                SoundGain = prim.SoundGain,
                SoundRadius = prim.SoundRadius,
                TextureAnimation = GetBytesFromSegment(prim.GetTextureAnimationBytes()),
                Velocity = ToOmvVec3(prim.Velocity)
            };
        }
    }
}
