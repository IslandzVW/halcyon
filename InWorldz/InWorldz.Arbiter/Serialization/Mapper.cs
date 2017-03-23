using System;
using System.Diagnostics;
using FlatBuffers;
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
        /// Maps from a flatbuffer serialized group to a SOG
        /// </summary>
        /// <param name="inGroup">The flatbuffer group to serialize</param>
        /// <returns></returns>
        public static SceneObjectGroup MapFlatbufferGroupToSceneObjectGroup(HalcyonGroup inGroup)
        {
            if (!inGroup.Root.HasValue)
            {
                throw new NullReferenceException();
            }
            
            var group = new SceneObjectGroup();
            group.SetRootPart(MapFlatbufferPrimToPart(inGroup.Root.Value));
            
            for (int i = 0; i < inGroup.ChildPartsLength; i++)
            {
                if (! inGroup.ChildParts(i).HasValue) continue;

                SceneObjectPart childPart = Mapper.MapFlatbufferPrimToPart(inGroup.ChildParts(i).Value);

                int originalLinkNum = childPart.LinkNum;
                group.AddPart(childPart);

                // SceneObjectGroup.AddPart() tries to be smart and automatically set the LinkNum.
                // We override that here
                if (originalLinkNum != 0)
                    childPart.LinkNum = originalLinkNum;
            }

            return group;
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
                ObjectFlags = prim.ObjectFlags,
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

        /// <summary>
        /// Maps the given sceneobjectpart to a flatbuffer primitive
        /// </summary>
        /// <param name="sop">The scene object to be serialized</param>
        /// <param name="builder">A FlatBufferBuilder that has been reset</param>
        /// <param name="close">Whether to close the buffer or not</param>
        /// <returns>A flatbuffer primitive</returns>
        public static Offset<HalcyonPrimitive> MapPartToFlatbuffer(SceneObjectPart sop, FlatBufferBuilder builder, bool close=false)
        {
            var angularVelocity = Vector3.CreateVector3(builder, sop.PhysicalAngularVelocity.X, sop.PhysicalAngularVelocity.Y,
                sop.PhysicalAngularVelocity.Z);
            var angularVelocityTarget = Vector3.CreateVector3(builder, sop.AngularVelocity.X, sop.AngularVelocity.Y,
                sop.AngularVelocity.Z);
            var creatorId = HalcyonPrimitive.CreateCreatorIdVector(builder, sop.CreatorID.GetBytes());
            var description = builder.CreateString(sop.Description);
            var groupPosition = Vector3.CreateVector3(builder, sop.GroupPosition.X, sop.GroupPosition.Y,
                sop.GroupPosition.Z);
            var id = HalcyonPrimitive.CreateIdVector(builder, sop.UUID.GetBytes());
            var name = builder.CreateString(sop.Name);
            var offsetPosition = Vector3.CreateVector3(builder, sop.OffsetPosition.X, sop.OffsetPosition.Y,
                sop.OffsetPosition.Z);
            var rotationOffset = Quaternion.CreateQuaternion(builder, sop.RotationOffset.X, sop.RotationOffset.Y,
                sop.RotationOffset.Z, sop.RotationOffset.W);
            var scale = Vector3.CreateVector3(builder, sop.Scale.X, sop.Scale.Y, sop.Scale.Z);

            var extraParams = HalcyonPrimitiveBaseShape.CreateExtraParamsVector(builder, sop.Shape.ExtraParams);
            var lightColor = HalcyonPrimitiveBaseShape.CreateLightColorVector(builder, new []
            {
                sop.Shape.LightColorA,
                sop.Shape.LightColorR,
                sop.Shape.LightColorG,
                sop.Shape.LightColorB
            });

            var ss = sop.Shape;
            var projectionTextureId = HalcyonPrimitiveBaseShape.CreateProjectionTextureIdVector(builder,
                ss.ProjectionTextureUUID.GetBytes());
            var shapeScale = Vector3.CreateVector3(builder, ss.Scale.X, ss.Scale.Y, ss.Scale.Z);
            var sculptTextureId = HalcyonPrimitiveBaseShape.CreateSculptTextureVector(builder,
                ss.SculptTexture.GetBytes());
            var textureEntry = HalcyonPrimitiveBaseShape.CreateTextureEntryVector(builder, ss.TextureEntry);

            HalcyonPrimitiveBaseShape.StartHalcyonPrimitiveBaseShape(builder);
            HalcyonPrimitiveBaseShape.AddExtraParams(builder, extraParams);
            HalcyonPrimitiveBaseShape.AddFlexiDrag(builder, ss.FlexiDrag);
            HalcyonPrimitiveBaseShape.AddFlexiEntry(builder, ss.FlexiEntry);
            HalcyonPrimitiveBaseShape.AddFlexiForceX(builder, ss.FlexiForceX);
            HalcyonPrimitiveBaseShape.AddFlexiForceY(builder, ss.FlexiForceY);
            HalcyonPrimitiveBaseShape.AddFlexiForceZ(builder, ss.FlexiForceZ);
            HalcyonPrimitiveBaseShape.AddFlexiGravity(builder, ss.FlexiGravity);
            HalcyonPrimitiveBaseShape.AddFlexiSoftness(builder, ss.FlexiSoftness);
            HalcyonPrimitiveBaseShape.AddFlexiTension(builder, ss.FlexiTension);
            HalcyonPrimitiveBaseShape.AddFlexiWind(builder, ss.FlexiWind);
            HalcyonPrimitiveBaseShape.AddHighLodBytes(builder, ss.HighLODBytes);
            HalcyonPrimitiveBaseShape.AddHollowShape(builder, (HollowShape)ss.HollowShape);
            HalcyonPrimitiveBaseShape.AddLightColor(builder, lightColor);
            HalcyonPrimitiveBaseShape.AddLightCutoff(builder, ss.LightCutoff);
            HalcyonPrimitiveBaseShape.AddLightEntry(builder, ss.LightEntry);
            HalcyonPrimitiveBaseShape.AddLightIntensity(builder, ss.LightIntensity);
            HalcyonPrimitiveBaseShape.AddLightRadius(builder, ss.LightRadius);
            HalcyonPrimitiveBaseShape.AddLowLodBytes(builder, ss.LowLODBytes);
            HalcyonPrimitiveBaseShape.AddLowestLodBytes(builder, ss.LowestLODBytes);
            HalcyonPrimitiveBaseShape.AddMidLodBytes(builder, ss.MidLODBytes);
            HalcyonPrimitiveBaseShape.AddPathBegin(builder, ss.PathBegin);
            HalcyonPrimitiveBaseShape.AddPathCurve(builder, ss.PathCurve);
            HalcyonPrimitiveBaseShape.AddPathEnd(builder, ss.PathEnd);
            HalcyonPrimitiveBaseShape.AddPathRadiusOffset(builder, ss.PathRadiusOffset);
            HalcyonPrimitiveBaseShape.AddPathRevolutions(builder, ss.PathRevolutions);
            HalcyonPrimitiveBaseShape.AddPathScaleX(builder, ss.PathScaleX);
            HalcyonPrimitiveBaseShape.AddPathScaleY(builder, ss.PathScaleY);
            HalcyonPrimitiveBaseShape.AddPathShearX(builder, ss.PathShearX);
            HalcyonPrimitiveBaseShape.AddPathShearY(builder, ss.PathShearY);
            HalcyonPrimitiveBaseShape.AddPathTaperX(builder, ss.PathTaperX);
            HalcyonPrimitiveBaseShape.AddPathTaperY(builder, ss.PathTaperY);
            HalcyonPrimitiveBaseShape.AddPathTwist(builder, ss.PathTwist);
            HalcyonPrimitiveBaseShape.AddPathTwistBegin(builder, ss.PathTwistBegin);
            HalcyonPrimitiveBaseShape.AddPathSkew(builder, ss.PathSkew);
            HalcyonPrimitiveBaseShape.AddPcode(builder, ss.PCode);
            HalcyonPrimitiveBaseShape.AddProfileBegin(builder, ss.ProfileBegin);
            HalcyonPrimitiveBaseShape.AddProfileCurve(builder, ss.ProfileCurve);
            HalcyonPrimitiveBaseShape.AddProfileEnd(builder, ss.ProfileEnd);
            HalcyonPrimitiveBaseShape.AddProfileHollow(builder, ss.ProfileHollow);
            HalcyonPrimitiveBaseShape.AddProfileShape(builder, (ProfileShape)ss.ProfileShape);
            HalcyonPrimitiveBaseShape.AddProjectionAmbiance(builder, ss.ProjectionAmbiance);
            HalcyonPrimitiveBaseShape.AddProjectionEntry(builder, ss.ProjectionEntry);
            HalcyonPrimitiveBaseShape.AddProjectionFocus(builder, ss.ProjectionFocus);
            HalcyonPrimitiveBaseShape.AddProjectionFov(builder, ss.ProjectionFOV);
            HalcyonPrimitiveBaseShape.AddProjectionTextureId(builder, projectionTextureId);
            HalcyonPrimitiveBaseShape.AddScale(builder, shapeScale);
            HalcyonPrimitiveBaseShape.AddSculptEntry(builder, ss.SculptEntry);
            HalcyonPrimitiveBaseShape.AddSculptTexture(builder, sculptTextureId);
            HalcyonPrimitiveBaseShape.AddSculptType(builder, ss.SculptType);
            HalcyonPrimitiveBaseShape.AddState(builder, ss.State);
            HalcyonPrimitiveBaseShape.AddTextureEntry(builder, textureEntry);
            HalcyonPrimitiveBaseShape.AddVertexCount(builder, ss.VertexCount);

            var baseShapeOffset = HalcyonPrimitiveBaseShape.EndHalcyonPrimitiveBaseShape(builder);

            HalcyonPrimitive.StartHalcyonPrimitive(builder);
            HalcyonPrimitive.AddAngularVelocity(builder, angularVelocity);
            HalcyonPrimitive.AddAngularVelocityTarget(builder, angularVelocityTarget);
            HalcyonPrimitive.AddCreatorId(builder, creatorId);
            HalcyonPrimitive.AddDescription(builder, description);
            HalcyonPrimitive.AddGroupPosition(builder, groupPosition);
            HalcyonPrimitive.AddId(builder, id);
            HalcyonPrimitive.AddLinkNumber(builder, sop.LinkNum);
            HalcyonPrimitive.AddLocalId(builder, sop.LocalId);
            HalcyonPrimitive.AddName(builder, name);
            HalcyonPrimitive.AddObjectFlags(builder, sop.ObjectFlags);
            HalcyonPrimitive.AddOffsetPosition(builder, offsetPosition);
            HalcyonPrimitive.AddParentId(builder, sop.ParentID);
            HalcyonPrimitive.AddRotationOffset(builder, rotationOffset);
            HalcyonPrimitive.AddScale(builder, scale);
            HalcyonPrimitive.AddShape(builder, baseShapeOffset);
            var offset = HalcyonPrimitive.EndHalcyonPrimitive(builder);

            if (close)
            {
                HalcyonPrimitive.FinishHalcyonPrimitiveBuffer(builder, offset);
            }

            return offset;
        }
    }
}
