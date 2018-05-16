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
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace InWorldz.Region.Data.Thoosa.Tests
{
    internal class Util
    {
        private readonly static Random rand = new Random();

        public static Vector3 RandomVector()
        {
            return new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
        }

        public static Quaternion RandomQuat()
        {
            return new Quaternion((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
        }

        internal static byte RandomByte()
        {
            return (byte)rand.Next(byte.MaxValue);
        }

        public static SceneObjectPart RandomSOP(string name, uint localId)
        {
            var shape = new OpenSim.Framework.PrimitiveBaseShape();
            shape.ExtraParams = new byte[] { 0xA, 0x9, 0x8, 0x7, 0x6, 0x5 };
            shape.FlexiDrag = 0.3f;
            shape.FlexiEntry = true;
            shape.FlexiForceX = 1.0f;
            shape.FlexiForceY = 2.0f;
            shape.FlexiForceZ = 3.0f;
            shape.FlexiGravity = 10.0f;
            shape.FlexiSoftness = 1;
            shape.FlexiTension = 999.4f;
            shape.FlexiWind = 9292.33f;
            shape.HollowShape = OpenSim.Framework.HollowShape.Square;
            shape.LightColorA = 0.3f;
            shape.LightColorB = 0.22f;
            shape.LightColorG = 0.44f;
            shape.LightColorR = 0.77f;
            shape.LightCutoff = 0.4f;
            shape.LightEntry = true;
            shape.LightFalloff = 7474;
            shape.LightIntensity = 0.0f;
            shape.LightRadius = 10.0f;
            shape.Media = new OpenSim.Framework.PrimitiveBaseShape.PrimMedia();
            shape.Media.New(2);
            shape.Media[0] = new MediaEntry
            {
                AutoLoop = true,
                AutoPlay = true,
                AutoScale = true,
                AutoZoom = true,
                ControlPermissions = MediaPermission.All,
                Controls = MediaControls.Standard,
                CurrentURL = "bam.com",
                EnableAlterntiveImage = true,
                EnableWhiteList = false,
                Height = 1,
                HomeURL = "anotherbam.com",
                InteractOnFirstClick = true,
                InteractPermissions = MediaPermission.Group,
                WhiteList = new string[] { "yo mamma" },
                Width = 5
            };
            shape.Media[1] = new MediaEntry
            {
                AutoLoop = true,
                AutoPlay = true,
                AutoScale = true,
                AutoZoom = true,
                ControlPermissions = MediaPermission.All,
                Controls = MediaControls.Standard,
                CurrentURL = "kabam.com",
                EnableAlterntiveImage = true,
                EnableWhiteList = true,
                Height = 1,
                HomeURL = "anotherbam.com",
                InteractOnFirstClick = true,
                InteractPermissions = MediaPermission.Group,
                WhiteList = new string[] { "ur mamma" },
                Width = 5
            };

            shape.PathBegin = 3;
            shape.PathCurve = 127;
            shape.PathEnd = 10;
            shape.PathRadiusOffset = 127;
            shape.PathRevolutions = 2;
            shape.PathScaleX = 50;
            shape.PathScaleY = 100;
            shape.PathShearX = 33;
            shape.PathShearY = 44;
            shape.PathSkew = 126;
            shape.PathTaperX = 110;
            shape.PathTaperY = 66;
            shape.PathTwist = 99;
            shape.PathTwistBegin = 3;
            shape.PCode = 3;
            shape.PreferredPhysicsShape = PhysicsShapeType.Prim;
            shape.ProfileBegin = 77;
            shape.ProfileCurve = 5;
            shape.ProfileEnd = 7;
            shape.ProfileHollow = 9;
            shape.ProfileShape = OpenSim.Framework.ProfileShape.IsometricTriangle;
            shape.ProjectionAmbiance = 0.1f;
            shape.ProjectionEntry = true;
            shape.ProjectionFocus = 3.4f;
            shape.ProjectionFOV = 4.0f;
            shape.ProjectionTextureUUID = UUID.Random();
            shape.Scale = Util.RandomVector();
            shape.SculptEntry = true;
            shape.SculptTexture = UUID.Random();
            shape.SculptType = 40;
            shape.VertexCount = 1;
            shape.HighLODBytes = 2;
            shape.MidLODBytes = 3;
            shape.LowLODBytes = 4;
            shape.LowestLODBytes = 5;

            SceneObjectPart part = new SceneObjectPart(UUID.Zero, shape, new Vector3(1, 2, 3), new Quaternion(4, 5, 6, 7), Vector3.Zero, false);
            part.Name = name;
            part.Description = "Desc";
            part.AngularVelocity = Util.RandomVector();
            part.BaseMask = 0x0876;
            part.Category = 10;
            part.ClickAction = 5;
            part.CollisionSound = UUID.Random();
            part.CollisionSoundVolume = 1.1f;
            part.CreationDate = OpenSim.Framework.Util.UnixTimeSinceEpoch();
            part.CreatorID = UUID.Random();
            part.EveryoneMask = 0x0543;
            part.Flags = PrimFlags.CameraSource | PrimFlags.DieAtEdge;
            part.GroupID = UUID.Random();
            part.GroupMask = 0x0210;
            part.LastOwnerID = UUID.Random();
            part.LinkNum = 4;
            part.LocalId = localId;
            part.Material = 0x1;
            part.MediaUrl = "http://bam";
            part.NextOwnerMask = 0x0234;
            part.CreatorID = UUID.Random();
            part.ObjectFlags = 10101;
            part.OwnerID = UUID.Random();
            part.OwnerMask = 0x0567;
            part.OwnershipCost = 5;
            part.ParentID = 0202;
            part.ParticleSystem = new byte[] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, };
            part.PassTouches = true;
            part.PhysicalAngularVelocity = Util.RandomVector();
            part.RegionHandle = 1234567;
            part.RegionID = UUID.Random();
            part.RotationOffset = Util.RandomQuat();
            part.SalePrice = 42;
            part.SavedAttachmentPoint = 6;
            part.SavedAttachmentPos = Util.RandomVector();
            part.SavedAttachmentRot = Util.RandomQuat();
            part.ScriptAccessPin = 87654;
            part.SerializedPhysicsData = new byte[] { 0xA, 0xB, 0xC, 0xD, 0xE, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, };
            part.ServerFlags = 0;
            part.ServerWeight = 3.0f;
            part.StreamingCost = 2.0f;
            part.SitName = "Sitting";
            part.Sound = UUID.Random();
            part.SoundGain = 3.4f;
            part.SoundOptions = 9;
            part.SoundRadius = 10.3f;
            part.Text = "Test";
            part.TextColor = System.Drawing.Color.FromArgb(1, 2, 3, 4);
            part.TextureAnimation = new byte[] { 0xA, 0xB, 0xC, 0xD, 0xE, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD };
            part.TouchName = "DoIt";
            part.UUID = UUID.Random();
            part.Velocity = Util.RandomVector();
            part.FromItemID = UUID.Random();
            part.ServerFlags |= (uint)ServerPrimFlags.SitTargetStateSaved;  // This one has been migrated to the new sit target storage

            part.SetSitTarget(true, Util.RandomVector(), Util.RandomQuat(), false);

            return part;
        }
    }
}
