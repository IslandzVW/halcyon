using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSim.Framework
{
    /// <summary>
    ///     Specifies the fields that have been changed when sending a prim or
    ///     avatar update
    /// </summary>
    [Flags]
    public enum PrimUpdateFlags : uint
    {
        None = 0,
        AttachmentPoint = 1 << 0,
        Material = 1 << 1,
        ClickAction = 1 << 2,
        Shape = 1 << 3,
        ParentID = 1 << 4,
        PrimFlags = 1 << 5,
        PrimData = 1 << 6,
        MediaURL = 1 << 7,
        ScratchPad = 1 << 8,
        Textures = 1 << 9,
        TextureAnim = 1 << 10,
        NameValue = 1 << 11,
        Position = 1 << 12,
        Rotation = 1 << 13,
        Velocity = 1 << 14,
        Acceleration = 1 << 15,
        AngularVelocity = 1 << 16,
        CollisionPlane = 1 << 17,
        Text = 1 << 18,
        Particles = 1 << 19,
        ExtraData = 1 << 20,
        Sound = 1 << 21,
        Joint = 1 << 22,
        FindBest = 1 << 23,
        ForcedFullUpdate = UInt32.MaxValue - 1,
        FullUpdate = UInt32.MaxValue,

        TerseUpdate = Position | Rotation | Velocity
                      | Acceleration | AngularVelocity
    }

    public static class PrimUpdateFlagsExtensions
    {
        public static bool HasFlag(this PrimUpdateFlags updateFlags, PrimUpdateFlags flag)
        {
            return (updateFlags & flag) == flag;
        }
    }

    public interface IObjectCache
    {
        bool UseCachedObject(UUID AgentID, uint localID, uint CurrentEntityCRC);
        void AddCachedObject(UUID AgentID, uint localID, uint CurrentEntityCRC);
        void RemoveObject(UUID AgentID, uint localID, byte cacheMissType);
    }
}
