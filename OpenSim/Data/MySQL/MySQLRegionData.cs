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
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Region Server
    /// </summary>
    public class MySQLDataStore : IRegionDataStore
    {
        //fron scriptbaseclass.  moved here so I dont have to create a circular reference
        private const int PERMISSION_DEBIT = 2;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ConnectionString;

        private object m_PrimDBLock = new object();

        public void Initialize(string connectionString)
        {
            m_ConnectionString = connectionString;
        }

        private MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(m_ConnectionString);
            conn.Open();

            return conn;
        }

        private IDataReader ExecuteReader(MySqlCommand c)
        {
            IDataReader r = null;
            bool errorSeen = false;

            while (true)
            {
                try
                {
                    r = c.ExecuteReader();
                }
                catch (Exception)
                {
                    if (!errorSeen)
                    {
                        errorSeen = true;
                        continue;
                    }
                    throw;
                }

                break;
            }

            return r;
        }

        private void ExecuteNonQuery(MySqlCommand c)
        {
            bool errorSeen = false;

            while (true)
            {
                try
                {
                    c.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    if (!errorSeen)
                    {
                        errorSeen = true;
                        continue;
                    }
                    throw;
                }

                break;
            }
        }

        public void Dispose() {}

        public void BulkStoreObjects(IEnumerable<SceneObjectGroup> groups)
        {
            const string basePrimQuery =
                "INSERT INTO prims (" +
                    "UUID, CreationDate, " +
                    "Name, Text, Description, " +
                    "SitName, TouchName, ObjectFlags, " +
                    "OwnerMask, NextOwnerMask, GroupMask, " +
                    "EveryoneMask, BaseMask, PositionX, " +
                    "PositionY, PositionZ, GroupPositionX, " +
                    "GroupPositionY, GroupPositionZ, VelocityX, " +
                    "VelocityY, VelocityZ, AngularVelocityX, " +
                    "AngularVelocityY, AngularVelocityZ, " +
                    "AccelerationX, AccelerationY, " +
                    "AccelerationZ, RotationX, " +
                    "RotationY, RotationZ, " +
                    "RotationW, SitTargetOffsetX, " +
                    "SitTargetOffsetY, SitTargetOffsetZ, " +
                    "SitTargetOrientW, SitTargetOrientX, " +
                    "SitTargetOrientY, SitTargetOrientZ, " +
                    "RegionUUID, CreatorID, " +
                    "OwnerID, GroupID, " +
                    "LastOwnerID, SceneGroupID, " +
                    "PayPrice, PayButton1, " +
                    "PayButton2, PayButton3, " +
                    "PayButton4, LoopedSound, " +
                    "LoopedSoundGain, TextureAnimation, " +
                    "OmegaX, OmegaY, OmegaZ, " +
                    "CameraEyeOffsetX, CameraEyeOffsetY, " +
                    "CameraEyeOffsetZ, CameraAtOffsetX, " +
                    "CameraAtOffsetY, CameraAtOffsetZ, " +
                    "ForceMouselook, ScriptAccessPin, " +
                    "AllowedDrop, DieAtEdge, " +
                    "SalePrice, SaleType, " +
                    "ColorR, ColorG, ColorB, ColorA, " +
                    "ParticleSystem, ClickAction, Material, " +
                    "CollisionSound, CollisionSoundVolume, " +
                    "LinkNumber, PassTouches, " +
                    "ServerWeight, StreamingCost, KeyframeAnimation) VALUES ";

            const string baseShapesQuery =
                "INSERT INTO primshapes (" +
                "UUID, Shape, ScaleX, ScaleY, " +
                "ScaleZ, PCode, PathBegin, PathEnd, " +
                "PathScaleX, PathScaleY, PathShearX, " +
                "PathShearY, PathSkew, PathCurve, " +
                "PathRadiusOffset, PathRevolutions, " +
                "PathTaperX, PathTaperY, PathTwist, " +
                "PathTwistBegin, ProfileBegin, ProfileEnd, " +
                "ProfileCurve, ProfileHollow, Texture, " +
                "ExtraParams, Media, Materials, State, PhysicsData, " +
                "PreferredPhysicsShape, VertexCount, HighLODBytes, " +
                "MidLODBytes, LowLODBytes, LowestLODBytes) values ";

            const string primQueryEpilogue =
                " ON DUPLICATE KEY UPDATE " +
                    "CreationDate=VALUES(CreationDate), " +
                    "Name=VALUES(Name), Text=VALUES(Text), Description=VALUES(Description), " +
                    "SitName=VALUES(SitName), TouchName=VALUES(TouchName), ObjectFlags=VALUES(ObjectFlags), " +
                    "OwnerMask=VALUES(OwnerMask), NextOwnerMask=VALUES(NextOwnerMask), GroupMask=VALUES(GroupMask), " +
                    "EveryoneMask=VALUES(EveryoneMask), BaseMask=VALUES(BaseMask), PositionX=VALUES(PositionX), " +
                    "PositionY=VALUES(PositionY), PositionZ=VALUES(PositionZ), GroupPositionX=VALUES(GroupPositionX), " +
                    "GroupPositionY=VALUES(GroupPositionY), GroupPositionZ=VALUES(GroupPositionZ), VelocityX=VALUES(VelocityX), " +
                    "VelocityY=VALUES(VelocityY), VelocityZ=VALUES(VelocityZ), AngularVelocityX=VALUES(AngularVelocityX), " +
                    "AngularVelocityY=VALUES(AngularVelocityY), AngularVelocityZ=VALUES(AngularVelocityZ), " +
                    "AccelerationX=VALUES(AccelerationX), AccelerationY=VALUES(AccelerationY), " +
                    "AccelerationZ=VALUES(AccelerationZ), RotationX=VALUES(RotationX), " +
                    "RotationY=VALUES(RotationY), RotationZ=VALUES(RotationZ), " +
                    "RotationW=VALUES(RotationW), SitTargetOffsetX=VALUES(SitTargetOffsetX), " +
                    "SitTargetOffsetY=VALUES(SitTargetOffsetY), SitTargetOffsetZ=VALUES(SitTargetOffsetZ), " +
                    "SitTargetOrientW=VALUES(SitTargetOrientW), SitTargetOrientX=VALUES(SitTargetOrientX), " +
                    "SitTargetOrientY=VALUES(SitTargetOrientY), SitTargetOrientZ=VALUES(SitTargetOrientZ), " +
                    "RegionUUID=VALUES(RegionUUID), CreatorID=VALUES(CreatorID), " +
                    "OwnerID=VALUES(OwnerID), GroupID=VALUES(GroupID), " +
                    "LastOwnerID=VALUES(LastOwnerID), SceneGroupID=VALUES(SceneGroupID), " +
                    "PayPrice=VALUES(PayPrice), PayButton1=VALUES(PayButton1), " +
                    "PayButton2=VALUES(PayButton2), PayButton3=VALUES(PayButton3), " +
                    "PayButton4=VALUES(PayButton4), LoopedSound=VALUES(LoopedSound), " +
                    "LoopedSoundGain=VALUES(LoopedSoundGain), TextureAnimation=VALUES(TextureAnimation), " +
                    "OmegaX=VALUES(OmegaX), OmegaY=VALUES(OmegaY), OmegaZ=VALUES(OmegaZ), " +
                    "CameraEyeOffsetX=VALUES(CameraEyeOffsetX), CameraEyeOffsetY=VALUES(CameraEyeOffsetY), " +
                    "CameraEyeOffsetZ=VALUES(CameraEyeOffsetZ), CameraAtOffsetX=VALUES(CameraAtOffsetX), " +
                    "CameraAtOffsetY=VALUES(CameraAtOffsetY), CameraAtOffsetZ=VALUES(CameraAtOffsetZ), " +
                    "ForceMouselook=VALUES(ForceMouseLook), ScriptAccessPin=VALUES(ScriptAccessPin), " +
                    "AllowedDrop=VALUES(AllowedDrop), DieAtEdge=VALUES(DieAtEdge), " +
                    "SalePrice=VALUES(SalePrice), SaleType=VALUES(SaleType), " +
                    "ColorR=VALUES(ColorR), ColorG=VALUES(ColorG), ColorB=VALUES(ColorB), ColorA=VALUES(ColorA), " +
                    "ParticleSystem=VALUES(ParticleSystem), ClickAction=VALUES(ClickAction), Material=VALUES(Material), " +
                    "CollisionSound=VALUES(CollisionSound), CollisionSoundVolume=VALUES(CollisionSoundVolume), " +
                    "LinkNumber=VALUES(LinkNumber), PassTouches=VALUES(PassTouches), " +
                    "ServerWeight=VALUES(ServerWeight), StreamingCost=VALUES(StreamingCost), KeyframeAnimation=VALUES(KeyframeAnimation)";

            const string shapeQueryEpilogue =
                " ON DUPLICATE KEY UPDATE " +
                "UUID=VALUES(UUID), Shape=VALUES(Shape), ScaleX=VALUES(ScaleX), ScaleY=VALUES(ScaleY), " +
                "ScaleZ=VALUES(ScaleZ), PCode=VALUES(PCode), PathBegin=VALUES(PathBegin), PathEnd=VALUES(PathEnd), " +
                "PathScaleX=VALUES(PathScaleX), PathScaleY=VALUES(PathScaleY), PathShearX=VALUES(PathShearX), " +
                "PathShearY=VALUES(PathShearY), PathSkew=VALUES(PathSkew), PathCurve=VALUES(PathCurve), " +
                "PathRadiusOffset=VALUES(PathRadiusOffset), PathRevolutions=VALUES(PathRevolutions), " +
                "PathTaperX=VALUES(PathTaperX), PathTaperY=VALUES(PathTaperY), PathTwist=VALUES(PathTwist), " +
                "PathTwistBegin=VALUES(PathTwistBegin), ProfileBegin=VALUES(ProfileBegin), ProfileEnd=VALUES(ProfileEnd), " +
                "ProfileCurve=VALUES(ProfileCurve), ProfileHollow=VALUES(ProfileHollow), Texture=VALUES(Texture), " +
                "ExtraParams=VALUES(ExtraParams), Media=VALUES(Media), Materials=VALUES(Materials), State=VALUES(State), " +
                "PhysicsData=VALUES(PhysicsData), PreferredPhysicsShape=VALUES(PreferredPhysicsShape), VertexCount=VALUES(VertexCount), " +
                "HighLODBytes=VALUES(HighLODBytes), MidLODBytes=VALUES(MidLODBytes), LowLODBytes=VALUES(LowLODBytes), LowestLODBytes=VALUES(LowestLODBytes);";

            StringBuilder currentPrimQuery = new StringBuilder(basePrimQuery);
            StringBuilder currentShapesQuery = new StringBuilder(baseShapesQuery);

            int i = 0;
            const int MAX_PRIMS = 128;

            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    using (MySqlCommand primCommand = conn.CreateCommand(),
                                         shapeCommand = conn.CreateCommand())
                    {
                        foreach (SceneObjectGroup group in groups)
                        {
                            foreach (SceneObjectPart prim in group.GetParts())
                            {
                                currentPrimQuery.Append(this.GeneratePrimValuesBlock(i));
                                this.FillPrimCommandNumbered(primCommand, prim, group.UUID, group.RegionUUID, i);

                                currentShapesQuery.Append(this.GenerateShapeValuesBlock(i));
                                this.FillShapeCommandNumbered(shapeCommand, prim, i);

                                if (i == MAX_PRIMS)
                                {
                                    currentPrimQuery.Append(primQueryEpilogue);
                                    currentShapesQuery.Append(shapeQueryEpilogue);
                                    primCommand.CommandText = currentPrimQuery.ToString();
                                    shapeCommand.CommandText = currentShapesQuery.ToString();

                                    ExecuteNonQuery(primCommand);
                                    ExecuteNonQuery(shapeCommand);

                                    primCommand.Parameters.Clear();
                                    shapeCommand.Parameters.Clear();

                                    currentPrimQuery.Length = 0;
                                    currentPrimQuery.Append(basePrimQuery);

                                    currentShapesQuery.Length = 0;
                                    currentShapesQuery.Append(baseShapesQuery);

                                    i = 0;
                                }
                                else
                                {
                                    i++;
                                }
                            }
                        }

                        if (i != 0)
                        {
                            currentPrimQuery.Append(primQueryEpilogue);
                            currentShapesQuery.Append(shapeQueryEpilogue);
                            primCommand.CommandText = currentPrimQuery.ToString();
                            shapeCommand.CommandText = currentShapesQuery.ToString();

                            ExecuteNonQuery(primCommand);
                            ExecuteNonQuery(shapeCommand);
                        }
                    }
                }
            }
        }

        private float FixFloat(float number)
        {
            if (float.IsNaN(number) || float.IsInfinity(number))
            {
                return 0.0f;
            }
            else
            {
                return number;
            }
        }

        private void FillShapeCommandNumbered(MySqlCommand cmd, SceneObjectPart prim, int number)
        {
            string numString = Convert.ToString(number);

            PrimitiveBaseShape s = prim.Shape;
            cmd.Parameters.AddWithValue("UUID" + numString, prim.UUID.ToString());
            // shape is an enum
            cmd.Parameters.AddWithValue("Shape" + numString, 0);
            // vectors
            cmd.Parameters.AddWithValue("ScaleX" + numString, (double)FixFloat(s.Scale.X));
            cmd.Parameters.AddWithValue("ScaleY" + numString, (double)FixFloat(s.Scale.Y));
            cmd.Parameters.AddWithValue("ScaleZ" + numString, (double)FixFloat(s.Scale.Z));
            // paths
            cmd.Parameters.AddWithValue("PCode" + numString, s.PCode);
            cmd.Parameters.AddWithValue("PathBegin" + numString, s.PathBegin);
            cmd.Parameters.AddWithValue("PathEnd" + numString, s.PathEnd);
            cmd.Parameters.AddWithValue("PathScaleX" + numString, s.PathScaleX);
            cmd.Parameters.AddWithValue("PathScaleY" + numString, s.PathScaleY);
            cmd.Parameters.AddWithValue("PathShearX" + numString, s.PathShearX);
            cmd.Parameters.AddWithValue("PathShearY" + numString, s.PathShearY);
            cmd.Parameters.AddWithValue("PathSkew" + numString, s.PathSkew);
            cmd.Parameters.AddWithValue("PathCurve" + numString, s.PathCurve);
            cmd.Parameters.AddWithValue("PathRadiusOffset" + numString, s.PathRadiusOffset);
            cmd.Parameters.AddWithValue("PathRevolutions" + numString, s.PathRevolutions);
            cmd.Parameters.AddWithValue("PathTaperX" + numString, s.PathTaperX);
            cmd.Parameters.AddWithValue("PathTaperY" + numString, s.PathTaperY);
            cmd.Parameters.AddWithValue("PathTwist" + numString, s.PathTwist);
            cmd.Parameters.AddWithValue("PathTwistBegin" + numString, s.PathTwistBegin);
            // profile
            cmd.Parameters.AddWithValue("ProfileBegin" + numString, s.ProfileBegin);
            cmd.Parameters.AddWithValue("ProfileEnd" + numString, s.ProfileEnd);
            cmd.Parameters.AddWithValue("ProfileCurve" + numString, s.ProfileCurve);
            cmd.Parameters.AddWithValue("ProfileHollow" + numString, s.ProfileHollow);
            cmd.Parameters.AddWithValue("Texture" + numString, s.TextureEntryBytes);
            cmd.Parameters.AddWithValue("ExtraParams" + numString, s.ExtraParams);
            cmd.Parameters.AddWithValue("Media" + numString, s.Media == null ? null : s.Media.ToXml());
            cmd.Parameters.AddWithValue("Materials" + numString, s.RenderMaterials == null ? null : s.RenderMaterials.ToBytes());
            cmd.Parameters.AddWithValue("State" + numString, s.State);
            cmd.Parameters.AddWithValue("PhysicsData" + numString, prim.SerializedPhysicsData);
            cmd.Parameters.AddWithValue("PreferredPhysicsShape" + numString, s.PreferredPhysicsShape);
            cmd.Parameters.AddWithValue("VertexCount" + numString, s.VertexCount);
            cmd.Parameters.AddWithValue("HighLODBytes" + numString, s.HighLODBytes);
            cmd.Parameters.AddWithValue("MidLODBytes" + numString, s.MidLODBytes);
            cmd.Parameters.AddWithValue("LowLODBytes" + numString, s.LowLODBytes);
            cmd.Parameters.AddWithValue("LowestLODBytes" + numString, s.LowestLODBytes);
        }

        private void FillPrimCommandNumbered(MySqlCommand cmd, SceneObjectPart prim, UUID sceneGroupID, UUID regionUUID, int number)
        {
            string numString = Convert.ToString(number);

            cmd.Parameters.AddWithValue("UUID" + numString, prim.UUID.ToString());
            cmd.Parameters.AddWithValue("RegionUUID" + numString, regionUUID.ToString());
            cmd.Parameters.AddWithValue("CreationDate" + numString, prim.CreationDate);
            cmd.Parameters.AddWithValue("Name" + numString, prim.Name);
            cmd.Parameters.AddWithValue("SceneGroupID" + numString, sceneGroupID.ToString());
            // the UUID of the root part for this SceneObjectGroup
            // various text fields
            cmd.Parameters.AddWithValue("Text" + numString, prim.Text);
            cmd.Parameters.AddWithValue("ColorR" + numString, prim.TextColor.R);
            cmd.Parameters.AddWithValue("ColorG" + numString, prim.TextColor.G);
            cmd.Parameters.AddWithValue("ColorB" + numString, prim.TextColor.B);
            cmd.Parameters.AddWithValue("ColorA" + numString, prim.TextColor.A);
            cmd.Parameters.AddWithValue("Description" + numString, prim.Description);
            cmd.Parameters.AddWithValue("SitName" + numString, prim.SitName);
            cmd.Parameters.AddWithValue("TouchName" + numString, prim.TouchName);
            // permissions
            cmd.Parameters.AddWithValue("ObjectFlags" + numString, prim.ObjectFlags);
            cmd.Parameters.AddWithValue("CreatorID" + numString, prim.CreatorID.ToString());
            cmd.Parameters.AddWithValue("OwnerID" + numString, prim.OwnerID.ToString());
            cmd.Parameters.AddWithValue("GroupID" + numString, prim.GroupID.ToString());
            cmd.Parameters.AddWithValue("LastOwnerID" + numString, prim.LastOwnerID.ToString());
            cmd.Parameters.AddWithValue("OwnerMask" + numString, prim.OwnerMask);
            cmd.Parameters.AddWithValue("NextOwnerMask" + numString, prim.NextOwnerMask);
            cmd.Parameters.AddWithValue("GroupMask" + numString, prim.GroupMask);
            cmd.Parameters.AddWithValue("EveryoneMask" + numString, prim.EveryoneMask);
            cmd.Parameters.AddWithValue("BaseMask" + numString, prim.BaseMask);
            // vectors
            cmd.Parameters.AddWithValue("PositionX" + numString, (double)FixFloat(prim.OffsetPosition.X));
            cmd.Parameters.AddWithValue("PositionY" + numString, (double)FixFloat(prim.OffsetPosition.Y));
            cmd.Parameters.AddWithValue("PositionZ" + numString, (double)FixFloat(prim.OffsetPosition.Z));
            cmd.Parameters.AddWithValue("GroupPositionX" + numString, (double)FixFloat(prim.GroupPosition.X));
            cmd.Parameters.AddWithValue("GroupPositionY" + numString, (double)FixFloat(prim.GroupPosition.Y));
            cmd.Parameters.AddWithValue("GroupPositionZ" + numString, (double)FixFloat(prim.GroupPosition.Z));
            cmd.Parameters.AddWithValue("VelocityX" + numString, (double)FixFloat(prim.SerializedVelocity.X));
            cmd.Parameters.AddWithValue("VelocityY" + numString, (double)FixFloat(prim.SerializedVelocity.Y));
            cmd.Parameters.AddWithValue("VelocityZ" + numString, (double)FixFloat(prim.SerializedVelocity.Z));
            cmd.Parameters.AddWithValue("AngularVelocityX" + numString, (double)FixFloat(prim.PhysicalAngularVelocity.X));
            cmd.Parameters.AddWithValue("AngularVelocityY" + numString, (double)FixFloat(prim.PhysicalAngularVelocity.Y));
            cmd.Parameters.AddWithValue("AngularVelocityZ" + numString, (double)FixFloat(prim.PhysicalAngularVelocity.Z));
            cmd.Parameters.AddWithValue("AccelerationX" + numString, (double)FixFloat(prim.Acceleration.X));
            cmd.Parameters.AddWithValue("AccelerationY" + numString, (double)FixFloat(prim.Acceleration.Y));
            cmd.Parameters.AddWithValue("AccelerationZ" + numString, (double)FixFloat(prim.Acceleration.Z));
            // quaternions
            cmd.Parameters.AddWithValue("RotationX" + numString, (double)FixFloat(prim.RotationOffset.X));
            cmd.Parameters.AddWithValue("RotationY" + numString, (double)FixFloat(prim.RotationOffset.Y));
            cmd.Parameters.AddWithValue("RotationZ" + numString, (double)FixFloat(prim.RotationOffset.Z));
            cmd.Parameters.AddWithValue("RotationW" + numString, (double)FixFloat(prim.RotationOffset.W));

            // Sit target
            SitTargetInfo sitInfo = prim.ParentGroup.SitTargetForPart(prim.UUID);
            Vector3 sitTargetPos = sitInfo.Offset;
            cmd.Parameters.AddWithValue("SitTargetOffsetX" + numString, (double)FixFloat(sitTargetPos.X));
            cmd.Parameters.AddWithValue("SitTargetOffsetY" + numString, (double)FixFloat(sitTargetPos.Y));
            cmd.Parameters.AddWithValue("SitTargetOffsetZ" + numString, (double)FixFloat(sitTargetPos.Z));

            Quaternion sitTargetOrient = sitInfo.Rotation;
            cmd.Parameters.AddWithValue("SitTargetOrientW" + numString, (double)FixFloat(sitTargetOrient.W));
            cmd.Parameters.AddWithValue("SitTargetOrientX" + numString, (double)FixFloat(sitTargetOrient.X));
            cmd.Parameters.AddWithValue("SitTargetOrientY" + numString, (double)FixFloat(sitTargetOrient.Y));
            cmd.Parameters.AddWithValue("SitTargetOrientZ" + numString, (double)FixFloat(sitTargetOrient.Z));

            cmd.Parameters.AddWithValue("PayPrice" + numString, prim.PayPrice[0]);
            cmd.Parameters.AddWithValue("PayButton1" + numString, prim.PayPrice[1]);
            cmd.Parameters.AddWithValue("PayButton2" + numString, prim.PayPrice[2]);
            cmd.Parameters.AddWithValue("PayButton3" + numString, prim.PayPrice[3]);
            cmd.Parameters.AddWithValue("PayButton4" + numString, prim.PayPrice[4]);

            if ((prim.SoundOptions & (byte)SoundFlags.Loop) == (byte)SoundFlags.Loop)
            {
                cmd.Parameters.AddWithValue("LoopedSound" + numString, prim.Sound.ToString());
                cmd.Parameters.AddWithValue("LoopedSoundGain" + numString, prim.SoundGain);
            }
            else
            {
                cmd.Parameters.AddWithValue("LoopedSound" + numString, UUID.Zero);
                cmd.Parameters.AddWithValue("LoopedSoundGain" + numString, 0.0f);
            }

            cmd.Parameters.AddWithValue("TextureAnimation" + numString, prim.TextureAnimation);
            cmd.Parameters.AddWithValue("ParticleSystem" + numString, prim.ParticleSystem);

            cmd.Parameters.AddWithValue("OmegaX" + numString, (double)FixFloat(prim.AngularVelocity.X));
            cmd.Parameters.AddWithValue("OmegaY" + numString, (double)FixFloat(prim.AngularVelocity.Y));
            cmd.Parameters.AddWithValue("OmegaZ" + numString, (double)FixFloat(prim.AngularVelocity.Z));

            cmd.Parameters.AddWithValue("CameraEyeOffsetX" + numString, (double)FixFloat(prim.GetCameraEyeOffset().X));
            cmd.Parameters.AddWithValue("CameraEyeOffsetY" + numString, (double)FixFloat(prim.GetCameraEyeOffset().Y));
            cmd.Parameters.AddWithValue("CameraEyeOffsetZ" + numString, (double)FixFloat(prim.GetCameraEyeOffset().Z));

            cmd.Parameters.AddWithValue("CameraAtOffsetX" + numString, (double)FixFloat(prim.GetCameraAtOffset().X));
            cmd.Parameters.AddWithValue("CameraAtOffsetY" + numString, (double)FixFloat(prim.GetCameraAtOffset().Y));
            cmd.Parameters.AddWithValue("CameraAtOffsetZ" + numString, (double)FixFloat(prim.GetCameraAtOffset().Z));

            if (prim.GetForceMouselook())
                cmd.Parameters.AddWithValue("ForceMouselook" + numString, 1);
            else
                cmd.Parameters.AddWithValue("ForceMouselook" + numString, 0);

            cmd.Parameters.AddWithValue("ScriptAccessPin" + numString, prim.ScriptAccessPin);

            if (prim.AllowedDrop)
                cmd.Parameters.AddWithValue("AllowedDrop" + numString, 1);
            else
                cmd.Parameters.AddWithValue("AllowedDrop" + numString, 0);

            if (prim.DIE_AT_EDGE)
                cmd.Parameters.AddWithValue("DieAtEdge" + numString, 1);
            else
                cmd.Parameters.AddWithValue("DieAtEdge" + numString, 0);

            cmd.Parameters.AddWithValue("SalePrice" + numString, prim.SalePrice);
            cmd.Parameters.AddWithValue("SaleType" + numString, Convert.ToInt16(prim.ObjectSaleType));

            byte clickAction = prim.ClickAction;
            cmd.Parameters.AddWithValue("ClickAction" + numString, clickAction);

            cmd.Parameters.AddWithValue("Material" + numString, prim.Material);

            cmd.Parameters.AddWithValue("CollisionSound" + numString, prim.CollisionSound.ToString());
            cmd.Parameters.AddWithValue("CollisionSoundVolume" + numString, prim.CollisionSoundVolume);
            cmd.Parameters.AddWithValue("LinkNumber" + numString, prim.LinkNum);

            byte passTouches = prim.PassTouches ? (byte)1 : (byte)0;
            cmd.Parameters.AddWithValue("PassTouches" + numString, passTouches);

            cmd.Parameters.AddWithValue("ServerWeight" + numString, prim.ServerWeight);
            cmd.Parameters.AddWithValue("StreamingCost" + numString, prim.StreamingCost);

            ISerializationEngine engine;
            ProviderRegistry.Instance.TryGet<ISerializationEngine>(out engine);
            cmd.Parameters.AddWithValue("KeyframeAnimation" + numString, 
                engine.MiscObjectSerializer.SerializeKeyframeAnimationToBytes(prim.KeyframeAnimation));

            // Server-use flags (per-prim persistence storage). Currently just enabled TRUE/FALSE for sit target.
            cmd.Parameters.AddWithValue("ServerFlags" + numString, prim.ServerFlags);
        }

        private StringBuilder GenerateShapeValuesBlock(int number)
        {
            const string template = " (?UUID, " +
                                "?Shape, ?ScaleX, ?ScaleY, ?ScaleZ, " +
                                "?PCode, ?PathBegin, ?PathEnd, " +
                                "?PathScaleX, ?PathScaleY, " +
                                "?PathShearX, ?PathShearY, " +
                                "?PathSkew, ?PathCurve, ?PathRadiusOffset, " +
                                "?PathRevolutions, ?PathTaperX, " +
                                "?PathTaperY, ?PathTwist, " +
                                "?PathTwistBegin, ?ProfileBegin, " +
                                "?ProfileEnd, ?ProfileCurve, " +
                                "?ProfileHollow, ?Texture, ?ExtraParams, " +
                                "?Media, ?Materials, ?State, ?PhysicsData, ?PreferredPhysicsShape, ?VertexCount, " +
                                "?HighLODBytes, ?MidLODBytes, ?LowLODBytes, ?LowestLODBytes)";

            const int SZ_PAD = 100;
            StringBuilder values = new StringBuilder(template, template.Length + SZ_PAD);

            values.Replace(",", Convert.ToString(number) + ",").Replace(")", Convert.ToString(number) + ")");

            if (number != 0) values[0] = ',';

            return values;
        }

        private StringBuilder GeneratePrimValuesBlock(int number)
        {

            const string VALUES_INIT = 
                            " (?UUID, " +
                             "?CreationDate, ?Name, ?Text, " +
                             "?Description, ?SitName, ?TouchName, " +
                             "?ObjectFlags, ?OwnerMask, ?NextOwnerMask, " +
                             "?GroupMask, ?EveryoneMask, ?BaseMask, " +
                             "?PositionX, ?PositionY, ?PositionZ, " +
                             "?GroupPositionX, ?GroupPositionY, " +
                             "?GroupPositionZ, ?VelocityX, " +
                             "?VelocityY, ?VelocityZ, ?AngularVelocityX, " +
                             "?AngularVelocityY, ?AngularVelocityZ, " +
                             "?AccelerationX, ?AccelerationY, " +
                             "?AccelerationZ, ?RotationX, " +
                             "?RotationY, ?RotationZ, " +
                             "?RotationW, ?SitTargetOffsetX, " +
                             "?SitTargetOffsetY, ?SitTargetOffsetZ, " +
                             "?SitTargetOrientW, ?SitTargetOrientX, " +
                             "?SitTargetOrientY, ?SitTargetOrientZ, " +
                             "?RegionUUID, ?CreatorID, ?OwnerID, " +
                             "?GroupID, ?LastOwnerID, ?SceneGroupID, " +
                             "?PayPrice, ?PayButton1, ?PayButton2, " +
                             "?PayButton3, ?PayButton4, ?LoopedSound, " +
                             "?LoopedSoundGain, ?TextureAnimation, " +
                             "?OmegaX, ?OmegaY, ?OmegaZ, " +
                             "?CameraEyeOffsetX, ?CameraEyeOffsetY, " +
                             "?CameraEyeOffsetZ, ?CameraAtOffsetX, " +
                             "?CameraAtOffsetY, ?CameraAtOffsetZ, " +
                             "?ForceMouselook, ?ScriptAccessPin, " +
                             "?AllowedDrop, ?DieAtEdge, ?SalePrice, " +
                             "?SaleType, ?ColorR, ?ColorG, " +
                             "?ColorB, ?ColorA, ?ParticleSystem, " +
                             "?ClickAction, ?Material, ?CollisionSound, " +
                             "?CollisionSoundVolume, ?LinkNumber, ?PassTouches, " +
                             "?ServerWeight, ?StreamingCost, ?KeyframeAnimation)";

            const int SZ_PAD = 250;
            StringBuilder values = new StringBuilder(VALUES_INIT, VALUES_INIT.Length + SZ_PAD);

            values.Replace(",", Convert.ToString(number) + ",").Replace(")", Convert.ToString(number) + ")");

            if (number != 0) values[0] = ',';

            return values;
        }

        public void RemoveObject(UUID obj, UUID regionUUID)
        {
            // Formerly, this used to check the region UUID.
            // That makes no sense, as we remove the contents of a prim
            // unconditionally, but the prim dependent on the region ID.
            // So, we would destroy an object and cause hard to detect
            // issues if we delete the contents only. Deleting it all may
            // cause the loss of a prim, but is cleaner.
            // It's also faster because it uses the primary key.
            //
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    List<UUID> uuids = new List<UUID>();

                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "select UUID from prims where " +
                                "SceneGroupID= ?UUID";
                        cmd.Parameters.AddWithValue("UUID", obj.ToString());

                        IDataReader reader = ExecuteReader(cmd);
                        try
                        {
                            while (reader.Read())
                            {
                                uuids.Add(new UUID(reader["UUID"].ToString()));
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }

                        // delete the main prims
                        cmd.CommandText = "delete from prims where SceneGroupID= ?UUID";
                        ExecuteNonQuery(cmd);
                    }

                    // there is no way this should be < 1 unless there is
                    // a very corrupt database, but in that case be extra
                    // safe anyway.
                if (uuids.Count > 0) 
                    {
                        RemoveShapes(uuids);
                        RemoveItems(uuids);
                    }
                }
            }
        }

        public void BulkRemoveObjects(IEnumerable<SceneObjectGroup> groups)
        {
            // Formerly, this used to check the region UUID.
            // That makes no sense, as we remove the contents of a prim
            // unconditionally, but the prim dependent on the region ID.
            // So, we would destroy an object and cause hard to detect
            // issues if we delete the contents only. Deleting it all may
            // cause the loss of a prim, but is cleaner.
            // It's also faster because it uses the primary key.
            //
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    List<UUID> uuids = new List<UUID>();

                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        string getPrimIdQuery = "SELECT UUID FROM prims WHERE SceneGroupID IN ";

                        StringBuilder groupIdList = new StringBuilder("(");
                        bool first = true;
                        foreach (SceneObjectGroup group in groups)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                groupIdList.Append(",");
                            }

                            groupIdList.Append("'" + group.UUID.ToString() + "'");
                        }


                        groupIdList.Append(")");

                        cmd.CommandText = getPrimIdQuery + groupIdList.ToString();
                        IDataReader reader = ExecuteReader(cmd);

                        try
                        {
                            while (reader.Read())
                            {
                                uuids.Add(new UUID(reader["UUID"].ToString()));
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }

                        // delete the main prims
                        cmd.CommandText = "DELETE FROM prims WHERE SceneGroupID IN " + groupIdList.ToString();
                        ExecuteNonQuery(cmd);
                    }

                    // there is no way this should be < 1 unless there is
                    // a very corrupt database, but in that case be extra
                    // safe anyway.
                    if (uuids.Count > 0)
                    {
                        RemoveShapes(uuids);
                        RemoveItems(uuids);
                    }
                }
            }
        }


        /// <summary>
        /// Remove all persisted shapes for a list of prims
        /// The caller must acquire the necessrary synchronization locks
        /// </summary>
        /// <param name="uuids">the list of UUIDs</param>
        private void RemoveShapes(List<UUID> uuids)
        {
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    string sql = "delete from primshapes where ";
                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        for (int i = 0; i < uuids.Count; i++)
                        {
                            if ((i + 1) == uuids.Count)
                            {// end of the list
                                sql += "(UUID = ?UUID" + i + ")";
                            }
                            else
                            {
                                sql += "(UUID = ?UUID" + i + ") or ";
                            }
                        }
                        cmd.CommandText = sql;

                        for (int i = 0; i < uuids.Count; i++)
                        {
                            cmd.Parameters.AddWithValue("UUID" + i, uuids[i].ToString());
                        }

                        ExecuteNonQuery(cmd);
                    }
                }
            }
        }

        /// <summary>
        /// Remove all persisted items for a list of prims
        /// The caller must acquire the necessrary synchronization locks
        /// </summary>
        /// <param name="uuids">the list of UUIDs</param>
        private void RemoveItems(List<UUID> uuids)
        {
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    string sql = "delete from primitems where ";
                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        for (int i = 0; i < uuids.Count; i++)
                        {
                            if ((i + 1) == uuids.Count)
                            {// end of the list
                                sql += "(PrimID = ?PrimID" + i + ")";
                            }
                            else
                            {
                                sql += "(PrimID = ?PrimID" + i + ") or ";
                            }
                        }
                        cmd.CommandText = sql;

                        for (int i = 0; i < uuids.Count; i++)
                        {
                            cmd.Parameters.AddWithValue("PrimID" + i, uuids[i].ToString());
                        }

                        ExecuteNonQuery(cmd);
                    }
                }
            }
        }

        private class PartLoadingComparer : IComparer<TaggedPart>
        {
            #region IComparer<SceneObjectPart> Members

            public int Compare(TaggedPart x, TaggedPart y)
            {
                const int CHILD_PART_BURDEN = 65535;

                //the first thing we use to compare is whether or not the UUID
                //of the part matches the group. If it does we do not add the
                //burden to the calculated comparison value this insures
                //that the root part is sorted at the top

                int xCompareVal = 0;
                int yCompareVal = 0;

                if (! x.IsConfirmedRoot)
                {
                    xCompareVal += CHILD_PART_BURDEN;
                }

                if (! y.IsConfirmedRoot)
                {
                    yCompareVal += CHILD_PART_BURDEN;
                }

                xCompareVal += x.Part.LinkNum;
                yCompareVal += y.Part.LinkNum;

                return xCompareVal.CompareTo(yCompareVal);
            }

            #endregion
        }

        private class TaggedPart
        {
            public SceneObjectPart Part;
            public UUID GroupId;
            public bool IsConfirmedRoot;
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            //SceneObjectGroup grp = null;

            Dictionary<UUID, List<TaggedPart>> rawGroups = new Dictionary<UUID, List<TaggedPart>>();
            Dictionary<UUID, SceneObjectPart> allParts = new Dictionary<UUID, SceneObjectPart>();

            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    //Clean dropped attachments
                    using (MySqlCommand attachmentDeleteCmd = conn.CreateCommand())
                    {
                        attachmentDeleteCmd.CommandText =
                            "delete from prims, primshapes using prims " +
                            "left join primshapes on prims.uuid = primshapes.uuid " +
                            "where PCode = 9 and State <> 0 AND prims.regionUUID = '" + regionUUID.ToString() + "'";

                        attachmentDeleteCmd.ExecuteNonQuery();
                        attachmentDeleteCmd.Dispose();

                        for (int i = 0; ; i += 500)
                        {
                            using (MySqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandTimeout = 45;
                                cmd.CommandText = "select *, " +
                                        "case when prims.UUID = SceneGroupID " +
                                        "then 0 else 1 end as sort from prims " +
                                        "left join primshapes on prims.UUID = primshapes.UUID " +
                                        "where RegionUUID = ?RegionUUID " +
                                        "LIMIT " + Convert.ToString(i) + ", 500";

                                cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());

                                using (IDataReader reader = ExecuteReader(cmd))
                                {
                                    bool hadRecord = false;
                                    while (reader.Read())
                                    {
                                        hadRecord = true;

                                        SceneObjectPart prim = BuildPrim(reader);
                                        if (reader["Shape"] is DBNull)
                                        {
                                            prim.Shape = PrimitiveBaseShape.Default;
                                        }
                                        else
                                        {
                                            prim.Shape = BuildShape(reader);
                                        }

                                        UUID SceneGroupID = new UUID(Convert.ToString(reader["SceneGroupID"]));

                                        List<TaggedPart> partList;
                                        if (!rawGroups.TryGetValue(SceneGroupID, out partList))
                                        {
                                            partList = new List<TaggedPart>();
                                            rawGroups[SceneGroupID] = partList;
                                        }

                                        partList.Add(new TaggedPart { GroupId = SceneGroupID, IsConfirmedRoot = (prim.UUID == SceneGroupID), Part = prim });
                                        allParts.Add(prim.UUID, prim);
                                    }

                                    if (!hadRecord)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //sort prims in each group
            PartLoadingComparer compare = new PartLoadingComparer();
            foreach (List<TaggedPart> partList in rawGroups.Values)
            {
                partList.Sort(compare);
            }

            List<SceneObjectGroup> finalGroups = new List<SceneObjectGroup>();
            foreach (KeyValuePair<UUID, List<TaggedPart>> rawParts in rawGroups)
            {
                List<TaggedPart> partList = rawParts.Value;

                // There sometimes exist OpenSim bugs that 'orphan groups' so that none of the prims are
                // recorded as the root prim (for which the UUID must equal the persisted group UUID).  In
                // this case, force the UUID to be the same as the group UUID so that at least these can be
                // deleted (we need to change the UUID so that any other prims in the linkset can also be 
                // deleted).
                if (partList[0].Part.UUID != rawParts.Key && rawParts.Key != UUID.Zero)
                {
                    m_log.WarnFormat(
                        "[REGION DB]: Found root prim {0} {1} at {2} where group was actually {3}.  Forcing UUID to group UUID",
                        partList[0].Part.Name, partList[0].Part.UUID, partList[0].Part.GroupPosition, rawParts.Key);

                    partList[0].Part.UUID = rawParts.Key;
                }

                //create the group and add the remaining parts
                SceneObjectGroup grp = new SceneObjectGroup(partList[0].Part);
                for (int i = 1; i < partList.Count; i++)
                {
                    // Black magic to preserve link numbers
                    // (this is an opensim artifact Im not even sure it's needed)
                    SceneObjectPart prim = partList[i].Part;
                    int link = prim.LinkNum;

                    grp.AddPart(prim);

                    if (link != 0)
                    {
                        prim.LinkNum = link;
                    }
                }

                finalGroups.Add(grp);
            }


            m_log.DebugFormat("[REGION DB]: Loading inventory items for region", finalGroups.Count, allParts.Count);
            List<TaskInventoryItem> items = LoadItemsForRegion(regionUUID);

            foreach (TaskInventoryItem item in items)
            {
                SceneObjectPart prim;
                if (allParts.TryGetValue(item.ParentPartID, out prim))
                {
                    prim.Inventory.RestoreSingleInventoryItem(item);
                }
            }

            m_log.DebugFormat("[REGION DB]: Loaded {0} objects using {1} prims, {2} items", finalGroups.Count, allParts.Count, items.Count);

            return finalGroups;
        }

        private List<TaskInventoryItem> LoadItemsForRegion(UUID regionUUID)
        {
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    List<TaskInventoryItem> inventory = new List<TaskInventoryItem>();

                    for (int i = 0; ; i += 100)
                    {
                        using (MySqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandTimeout = 45;
                            cmd.CommandText = "SELECT primitems.* FROM primitems " +
                                                "INNER JOIN prims ON primitems.primID = prims.UUID " +
                                                    "AND prims.RegionUUID = ?RegionID " +
                                                    "LIMIT " + Convert.ToString(i) + ", 100";

                            cmd.Parameters.AddWithValue("RegionID", regionUUID.ToString());

                            using (IDataReader reader = ExecuteReader(cmd))
                            {
                                bool hadRow = false;
                                while (reader.Read())
                                {
                                    hadRow = true;
                                    TaskInventoryItem item = BuildItem(reader);
                                    inventory.Add(item);
                                }

                                if (!hadRow)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    return inventory;
                }
            }
        }

        /// <summary>
        /// Load in a prim's persisted inventory.
        /// </summary>
        /// <param name="prim">The prim</param>        
        private void LoadItems(SceneObjectPart prim)
        {
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    List<TaskInventoryItem> inventory = new List<TaskInventoryItem>();

                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "select * from primitems where PrimID = ?PrimID";
                        cmd.Parameters.AddWithValue("PrimID", prim.UUID.ToString());

                        IDataReader reader = ExecuteReader(cmd);
                        try
                        {
                            while (reader.Read())
                            {
                                TaskInventoryItem item = BuildItem(reader);

                                item.ParentID = prim.UUID; // Values in database are
                                // often wrong
                                inventory.Add(item);
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }

                    prim.Inventory.RestoreInventoryItems(inventory);
                }
            }
        }

        public void StoreTerrain(double[,] ter, UUID regionID, int revision)
        {
            m_log.Info("[REGION DB]: Storing terrain");

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "delete from terrain where RegionUUID = ?RegionUUID";
                    cmd.Parameters.AddWithValue("RegionUUID", regionID.ToString());

                    ExecuteNonQuery(cmd);
                }

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "insert into terrain (RegionUUID, Revision, Heightfield) values (?RegionUUID, " +
                            "?Revision, ?Heightfield)";
                    cmd.Parameters.AddWithValue("RegionUUID", regionID.ToString());
                    cmd.Parameters.AddWithValue("Revision", revision);
                    cmd.Parameters.AddWithValue("Heightfield", SerializeTerrain(ter));

                    ExecuteNonQuery(cmd);
                }
            }
        }

        public Tuple<double[,], int> LoadTerrain(UUID regionID)
        {
            double[,] terrain = null;
            int rev = 0;

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select RegionUUID, Revision, Heightfield " +
                            "from terrain where RegionUUID = ?RegionUUID " +
                            "order by Revision desc limit 1";
                    cmd.Parameters.AddWithValue("RegionUUID", regionID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        try
                        {
                            while (reader.Read())
                            {
                                terrain = new double[256, 256];
                                terrain.Initialize();

                                MemoryStream mstr = new MemoryStream((byte[])reader["Heightfield"]);

                                BinaryReader br = new BinaryReader(mstr);
                                for (int x = 0; x < 256; x++)
                                {
                                    for (int y = 0; y < 256; y++)
                                    {
                                        terrain[x, y] = br.ReadDouble();
                                    }
                                    rev = Convert.ToInt32(reader["Revision"]);
                                }
                                m_log.InfoFormat("[REGION DB]: Loaded terrain revision r{0}", rev);
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }
                }
            }

            return new Tuple<double[,], int>(terrain, rev);
        }

        public void RemoveLandObject(UUID globalID)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "delete from land where UUID = ?UUID";
                    cmd.Parameters.AddWithValue("UUID", globalID.ToString());
                    ExecuteNonQuery(cmd);
                }
            }
        }

        public void StoreLandObject(ILandObject parcel)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "replace into land (UUID, RegionUUID, " +
                            "LocalLandID, Bitmap, Name, Description, " +
                            "OwnerUUID, IsGroupOwned, Area, AuctionID, " +
                            "Category, ClaimDate, ClaimPrice, GroupUUID, " +
                            "SalePrice, LandStatus, LandFlags, LandingType, " +
                            "MediaAutoScale, MediaTextureUUID, MediaURL, " +
                            "MusicURL, PassHours, PassPrice, SnapshotUUID, " +
                            "UserLocationX, UserLocationY, UserLocationZ, " +
                            "UserLookAtX, UserLookAtY, UserLookAtZ, " +
                            "AuthbuyerID, OtherCleanTime, Dwell) values (" +
                            "?UUID, ?RegionUUID, " +
                            "?LocalLandID, ?Bitmap, ?Name, ?Description, " +
                            "?OwnerUUID, ?IsGroupOwned, ?Area, ?AuctionID, " +
                            "?Category, ?ClaimDate, ?ClaimPrice, ?GroupUUID, " +
                            "?SalePrice, ?LandStatus, ?LandFlags, ?LandingType, " +
                            "?MediaAutoScale, ?MediaTextureUUID, ?MediaURL, " +
                            "?MusicURL, ?PassHours, ?PassPrice, ?SnapshotUUID, " +
                            "?UserLocationX, ?UserLocationY, ?UserLocationZ, " +
                            "?UserLookAtX, ?UserLookAtY, ?UserLookAtZ, " +
                            "?AuthbuyerID, ?OtherCleanTime, ?Dwell)";

                    FillLandCommand(cmd, parcel.landData, parcel.regionUUID);

                    ExecuteNonQuery(cmd);

                    cmd.CommandText = "delete from landaccesslist where " +
                            "LandUUID = ?UUID";

                    ExecuteNonQuery(cmd);

                    cmd.Parameters.Clear();
                    cmd.CommandText = "insert into landaccesslist (LandUUID, " +
                            "AccessUUID, Flags) values (?LandUUID, ?AccessUUID, " +
                            "?Flags)";

                    foreach (ParcelManager.ParcelAccessEntry entry in
                            parcel.landData.ParcelAccessList)
                    {
                        FillLandAccessCommand(cmd, entry, parcel.landData.GlobalID);
                        ExecuteNonQuery(cmd);
                        cmd.Parameters.Clear();
                    }
                }
            }
        }

        public string LoadRegionEnvironmentString(UUID regionUUID)
        {
            string result = null;

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    string sql = "SELECT regionUUID,llsd_text FROM regionenvironment where regionUUID = ?regionUUID";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("regionUUID", regionUUID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        try
                        {
                            if (reader.Read())
                            {
                                result = Convert.ToString(reader["llsd_text"]);
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }
                }
            }

            return result;  // caller handles null and string.empty
        }

        public void StoreRegionEnvironmentString(UUID regionID, string llsd_text)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    string sql = "INSERT INTO regionenvironment (regionUUID,llsd_text) VALUES (?regionUUID,?llsd_text)" +
                                    "ON DUPLICATE KEY UPDATE llsd_text=?llsd_text";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("regionUUID", regionID.ToString());
                    cmd.Parameters.AddWithValue("llsd_text", llsd_text);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RemoveRegionEnvironment(UUID regionID)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    string sql = "DELETE FROM regionenvironment WHERE regionUUID = ?regionUUID";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("regionUUID", regionID.ToString());

                    ExecuteNonQuery(cmd);
                }
            }
        }

#if USE_REGION_ENVIRONMENT_DATA
        private static RegionEnvironmentData BuildRegionEnvironment(IDataReader row)
        {
            RegionEnvironmentData newEnv = new RegionEnvironmentData();

            newEnv.regionID = new UUID(Convert.ToString(row["regionUUID"]));

            // Sky
            newEnv.ambient.X = Convert.ToSingle(row["ambient_r"]);
            newEnv.ambient.Y = Convert.ToSingle(row["ambient_g"]);
            newEnv.ambient.Z = Convert.ToSingle(row["ambient_b"]);
            newEnv.ambient.W = Convert.ToSingle(row["ambient_i"]);

            newEnv.blue_density.X = Convert.ToSingle(row["blue_density_r"]);
            newEnv.blue_density.Y = Convert.ToSingle(row["blue_density_g"]);
            newEnv.blue_density.Z = Convert.ToSingle(row["blue_density_b"]);
            newEnv.blue_density.W = Convert.ToSingle(row["blue_density_i"]);

            newEnv.blue_horizon.X = Convert.ToSingle(row["blue_horizon_r"]);
            newEnv.blue_horizon.Y = Convert.ToSingle(row["blue_horizon_g"]);
            newEnv.blue_horizon.Z = Convert.ToSingle(row["blue_horizon_b"]);
            newEnv.blue_horizon.W = Convert.ToSingle(row["blue_horizon_i"]);

            newEnv.cloud_color.X = Convert.ToSingle(row["cloud_color_r"]);
            newEnv.cloud_color.Y = Convert.ToSingle(row["cloud_color_g"]);
            newEnv.cloud_color.Z = Convert.ToSingle(row["cloud_color_b"]);
            newEnv.cloud_color.W = Convert.ToSingle(row["cloud_color_i"]);

            newEnv.cloud_pos_density1.X = Convert.ToSingle(row["cloud_pos_density1_x"]);
            newEnv.cloud_pos_density1.Y = Convert.ToSingle(row["cloud_pos_density1_y"]);
            newEnv.cloud_pos_density1.Z = Convert.ToSingle(row["cloud_pos_density1_x"]);
            newEnv.cloud_pos_density1.W = Convert.ToSingle(row["cloud_pos_density1_w"]);

            newEnv.cloud_pos_density2.X = Convert.ToSingle(row["cloud_pos_density2_x"]);
            newEnv.cloud_pos_density2.Y = Convert.ToSingle(row["cloud_pos_density2_y"]);
            newEnv.cloud_pos_density2.Z = Convert.ToSingle(row["cloud_pos_density2_x"]);
            newEnv.cloud_pos_density2.W = Convert.ToSingle(row["cloud_pos_density2_w"]);

            newEnv.cloud_scale.X = Convert.ToSingle(row["cloud_scale_x"]);
            newEnv.cloud_scale.Y = Convert.ToSingle(row["cloud_scale_y"]);
            newEnv.cloud_scale.Z = Convert.ToSingle(row["cloud_scale_z"]);
            newEnv.cloud_scale.W = Convert.ToSingle(row["cloud_scale_w"]);

            newEnv.cloud_scroll_rate.X = Convert.ToSingle(row["cloud_scroll_rate_x"]);
            newEnv.cloud_scroll_rate.Y = Convert.ToSingle(row["cloud_scroll_rate_y"]);

            newEnv.cloud_shadow.X = Convert.ToSingle(row["cloud_shadow_x"]);
            newEnv.cloud_shadow.Y = Convert.ToSingle(row["cloud_shadow_y"]);
            newEnv.cloud_shadow.Z = Convert.ToSingle(row["cloud_shadow_z"]);
            newEnv.cloud_shadow.W = Convert.ToSingle(row["cloud_shadow_w"]);

            newEnv.density_multiplier.X = Convert.ToSingle(row["density_multiplier_x"]);
            newEnv.density_multiplier.Y = Convert.ToSingle(row["density_multiplier_y"]);
            newEnv.density_multiplier.Z = Convert.ToSingle(row["density_multiplier_z"]);
            newEnv.density_multiplier.W = Convert.ToSingle(row["density_multiplier_w"]);

            newEnv.distance_multiplier.X = Convert.ToSingle(row["distance_multiplier_x"]);
            newEnv.distance_multiplier.Y = Convert.ToSingle(row["distance_multiplier_y"]);
            newEnv.distance_multiplier.Z = Convert.ToSingle(row["distance_multiplier_z"]);
            newEnv.distance_multiplier.W = Convert.ToSingle(row["distance_multiplier_w"]);

            newEnv.east_angle = Convert.ToSingle(row["east_angle"]);

            newEnv.enable_cloud_scroll_x = Convert.ToBoolean(row["enable_cloud_scroll_x"]);
            newEnv.enable_cloud_scroll_y = Convert.ToBoolean(row["enable_cloud_scroll_y"]);

            newEnv.gamma.X = Convert.ToSingle(row["gamma_x"]);
            newEnv.gamma.Y = Convert.ToSingle(row["gamma_y"]);
            newEnv.gamma.Z = Convert.ToSingle(row["gamma_z"]);
            newEnv.gamma.W = Convert.ToSingle(row["gamma_w"]);

            newEnv.glow.X = Convert.ToSingle(row["glow_x"]);
            newEnv.glow.Y = Convert.ToSingle(row["glow_y"]);
            newEnv.glow.Z = Convert.ToSingle(row["glow_z"]);
            newEnv.glow.W = Convert.ToSingle(row["glow_w"]);

            newEnv.haze_density.X = Convert.ToSingle(row["haze_density_x"]);
            newEnv.haze_density.Y = Convert.ToSingle(row["haze_density_y"]);
            newEnv.haze_density.Z = Convert.ToSingle(row["haze_density_z"]);
            newEnv.haze_density.W = Convert.ToSingle(row["haze_density_w"]);

            newEnv.haze_horizon.X = Convert.ToSingle(row["haze_horizon_x"]);
            newEnv.haze_horizon.Y = Convert.ToSingle(row["haze_horizon_y"]);
            newEnv.haze_horizon.Z = Convert.ToSingle(row["haze_horizon_z"]);
            newEnv.haze_horizon.W = Convert.ToSingle(row["haze_horizon_w"]);

            newEnv.lightnorm.X = Convert.ToSingle(row["lightnorm_x"]);
            newEnv.lightnorm.Y = Convert.ToSingle(row["lightnorm_y"]);
            newEnv.lightnorm.Z = Convert.ToSingle(row["lightnorm_z"]);
            newEnv.lightnorm.W = Convert.ToSingle(row["lightnorm_w"]);

            newEnv.max_y.X = Convert.ToSingle(row["max_y_x"]);
            newEnv.max_y.Y = Convert.ToSingle(row["max_y_y"]);
            newEnv.max_y.Z = Convert.ToSingle(row["max_y_z"]);
            newEnv.max_y.W = Convert.ToSingle(row["max_y_w"]);

            newEnv.preset_num = Convert.ToInt32(row["preset_num"]);

            newEnv.star_brightness = Convert.ToSingle(row["star_brightness"]);

            newEnv.sun_angle = Convert.ToSingle(row["sun_angle"]);

            newEnv.sunlight_color.X = Convert.ToSingle(row["sunlight_color_x"]);
            newEnv.sunlight_color.Y = Convert.ToSingle(row["sunlight_color_y"]);
            newEnv.sunlight_color.Z = Convert.ToSingle(row["sunlight_color_z"]);
            newEnv.sunlight_color.W = Convert.ToSingle(row["sunlight_color_w"]);

            // Water
            newEnv.blurMultiplier = Convert.ToSingle(row["blurMultiplier"]);
            newEnv.fresnelOffset = Convert.ToSingle(row["fresnelOffset"]);
            newEnv.fresnelScale = Convert.ToSingle(row["fresnelScale"]);
            newEnv.normScale.X = Convert.ToSingle(row["normScale_x"]);
            newEnv.normScale.Y = Convert.ToSingle(row["normScale_y"]);
            newEnv.normScale.Z = Convert.ToSingle(row["normScale_z"]);
            UUID.TryParse(row["normal_map"].ToString(), out newEnv.normalMap);
            newEnv.scaleAbove = Convert.ToSingle(row["scaleAbove"]);
            newEnv.scaleBelow = Convert.ToSingle(row["scaleBelow"]);
            newEnv.underWaterFogMod = Convert.ToSingle(row["underWaterFogMod"]);
            newEnv.waterFogColor.X = Convert.ToSingle(row["waterFogColor_r"]);
            newEnv.waterFogColor.Y = Convert.ToSingle(row["waterFogColor_g"]);
            newEnv.waterFogColor.Z = Convert.ToSingle(row["waterFogColor_b"]);
            newEnv.waterFogColor.W = Convert.ToSingle(row["waterFogColor_i"]);
            newEnv.waterFogDensity = Convert.ToSingle(row["waterFogDensity"]);
            newEnv.wave1Dir.X = Convert.ToSingle(row["wave1Dir_x"]);
            newEnv.wave1Dir.Y = Convert.ToSingle(row["wave1Dir_y"]);
            newEnv.wave2Dir.X = Convert.ToSingle(row["wave2Dir_x"]);
            newEnv.wave2Dir.Y = Convert.ToSingle(row["wave2Dir_y"]);

            newEnv.valid = true;    

            return newEnv;
        }

        public RegionEnvironmentData LoadRegionEnvironmentData(UUID regionUUID)
        {
            RegionEnvironmentData newEnv = new RegionEnvironmentData();
            bool found = false;
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    string sql = "SELECT * FROM regionenvironment where regionUUID = ?regionUUID";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("regionUUID", regionUUID.ToString());

                    newEnv.OnSave += StoreRegionEnvironmentData;

                    IDataReader reader = ExecuteReader(cmd);
                    try
                    {
                        if (reader.Read())
                        {
                            newEnv = BuildRegionEnvironment(reader);
                            found = true;
                        }
                    }
                    finally
                    {
                        reader.Close();
                    }
                }
            }

            if (!found)
            {
                //No result, so store our default windlight profile and return it
                newEnv.regionID = regionUUID;
                StoreRegionEnvironmentData(newEnv);
            }
            return newEnv;
        }

        private void AddQueryParam(MySqlCommand cmd, ref string varlist, ref string values, string name, object value)
        {
            // "INSERT INTO room(person,address) VALUES(?person, ?address)"

            if (String.IsNullOrEmpty(varlist))
            {
                varlist += ",";
                values += ",";
            }
            varlist += name;
            values += "?" + name;
            cmd.Parameters.AddWithValue("region_id", value);
        }

        public void StoreRegionEnvironmentData(RegionEnvironmentData env)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    string sql = "SELECT COUNT (regionUUID) FROM regionenvironment WHERE regionUUID = ?regionUUID";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("regionUUID", env.regionID.ToString());
                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        bool exists = false;
                        try
                        {
                            if (reader.Read())
                            {
                                exists = (int)cmd.ExecuteScalar() > 0;
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }
                        if (exists)
                        {
                            RemoveRegionEnvironment(env.regionID);
                        }
                    }

                    // start with two empty strings
                    string varlist = String.Empty;
                    string values = String.Empty;

                    // Sky
                    AddQueryParam(cmd, ref varlist, ref values, "region_id", env.regionID);
                    AddQueryParam(cmd, ref varlist, ref values, "ambient_r", env.ambient.X);
                    AddQueryParam(cmd, ref varlist, ref values, "ambient_g", env.ambient.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "ambient_b", env.ambient.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "ambient_i", env.ambient.W);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_density_r", env.blue_density.X);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_density_g", env.blue_density.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_density_b", env.blue_density.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_density_i", env.blue_density.W);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_horizon_r", env.blue_horizon.X);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_horizon_g", env.blue_horizon.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_horizon_b", env.blue_horizon.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "blue_horizon_i", env.blue_horizon.W);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_color_r", env.cloud_color.X);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_color_g", env.cloud_color.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_color_b", env.cloud_color.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_color_i", env.cloud_color.W);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density1_x", env.cloud_pos_density1.X);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density1_y", env.cloud_pos_density1.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density1_z", env.cloud_pos_density1.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density1_w", env.cloud_pos_density1.W);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density2_x", env.cloud_pos_density2.X);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density2_y", env.cloud_pos_density2.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density2_z", env.cloud_pos_density2.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_pos_density2_w", env.cloud_pos_density2.W);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_shadow_x", env.cloud_shadow.X);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_shadow_y", env.cloud_shadow.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_shadow_z", env.cloud_shadow.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "cloud_shadow_w", env.cloud_shadow.W);
                    AddQueryParam(cmd, ref varlist, ref values, "distance_multiplier_x", env.distance_multiplier.X);
                    AddQueryParam(cmd, ref varlist, ref values, "distance_multiplier_y", env.distance_multiplier.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "distance_multiplier_z", env.distance_multiplier.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "distance_multiplier_w", env.distance_multiplier.W);
                    AddQueryParam(cmd, ref varlist, ref values, "east_angle", env.east_angle);
                    AddQueryParam(cmd, ref varlist, ref values, "enable_cloud_scroll_x", env.enable_cloud_scroll_x);
                    AddQueryParam(cmd, ref varlist, ref values, "enable_cloud_scroll_x", env.enable_cloud_scroll_y);
                    AddQueryParam(cmd, ref varlist, ref values, "gamma_x", env.gamma.X);
                    AddQueryParam(cmd, ref varlist, ref values, "gamma_y", env.gamma.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "gamma_z", env.gamma.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "gamma_w", env.gamma.W);
                    AddQueryParam(cmd, ref varlist, ref values, "glow_x", env.glow.X);
                    AddQueryParam(cmd, ref varlist, ref values, "glow_y", env.glow.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "glow_z", env.glow.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "glow_w", env.glow.W);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_density_x", env.haze_density.X);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_density_y", env.haze_density.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_density_z", env.haze_density.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_density_w", env.haze_density.W);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_horizon_x", env.haze_horizon.X);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_horizon_y", env.haze_horizon.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_horizon_z", env.haze_horizon.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "haze_horizon_w", env.haze_horizon.W);
                    AddQueryParam(cmd, ref varlist, ref values, "lightnorm_x", env.lightnorm.X);
                    AddQueryParam(cmd, ref varlist, ref values, "lightnorm_y", env.lightnorm.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "lightnorm_z", env.lightnorm.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "lightnorm_w", env.lightnorm.W);
                    AddQueryParam(cmd, ref varlist, ref values, "max_y_x", env.max_y.X);
                    AddQueryParam(cmd, ref varlist, ref values, "max_y_y", env.max_y.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "max_y_z", env.max_y.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "max_y_w", env.max_y.W);
                    AddQueryParam(cmd, ref varlist, ref values, "preset_num", env.preset_num);
                    AddQueryParam(cmd, ref varlist, ref values, "star_brightness", env.star_brightness);
                    AddQueryParam(cmd, ref varlist, ref values, "sun_angle", env.sun_angle);
                    AddQueryParam(cmd, ref varlist, ref values, "sunlight_color_x", env.sunlight_color.X);
                    AddQueryParam(cmd, ref varlist, ref values, "sunlight_color_y", env.sunlight_color.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "sunlight_color_z", env.sunlight_color.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "sunlight_color_w", env.sunlight_color.W);

                    // Water
                    AddQueryParam(cmd, ref varlist, ref values, "blurMultiplier", env.blurMultiplier);
                    AddQueryParam(cmd, ref varlist, ref values, "fresnelOffset", env.fresnelOffset);
                    AddQueryParam(cmd, ref varlist, ref values, "fresnelScale", env.fresnelScale);
                    AddQueryParam(cmd, ref varlist, ref values, "normScale_x", env.normScale.X);
                    AddQueryParam(cmd, ref varlist, ref values, "normScale_y", env.normScale.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "normScale_z", env.normScale.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "normal_map", env.normalMap);
                    AddQueryParam(cmd, ref varlist, ref values, "scaleAbove", env.scaleAbove);
                    AddQueryParam(cmd, ref varlist, ref values, "scaleBelow", env.scaleBelow);
                    AddQueryParam(cmd, ref varlist, ref values, "underWaterFogMod", env.underWaterFogMod);
                    AddQueryParam(cmd, ref varlist, ref values, "waterFogColor_r", env.waterFogColor.X);
                    AddQueryParam(cmd, ref varlist, ref values, "waterFogColor_g", env.waterFogColor.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "waterFogColor_b", env.waterFogColor.Z);
                    AddQueryParam(cmd, ref varlist, ref values, "waterFogColor_i", env.waterFogColor.W);
                    AddQueryParam(cmd, ref varlist, ref values, "waterFogDensity", env.waterFogDensity);
                    AddQueryParam(cmd, ref varlist, ref values, "wave1Dir_x", env.wave1Dir.X);
                    AddQueryParam(cmd, ref varlist, ref values, "wave1Dir_y", env.wave1Dir.Y);
                    AddQueryParam(cmd, ref varlist, ref values, "wave2Dir_x", env.wave2Dir.X);
                    AddQueryParam(cmd, ref varlist, ref values, "wave2Dir_y", env.wave2Dir.Y);

                    sql = "INSERT INTO regionenvironment (" + varlist + ") VALUES (" + values + ")";
                    cmd.ExecuteNonQuery();
                }
            }
        }
#endif

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            RegionSettings rs = null;

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select * from regionsettings where regionUUID = ?RegionUUID";
                    cmd.Parameters.AddWithValue("regionUUID", regionUUID);

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        if (reader.Read())
                        {
                            rs = BuildRegionSettings(reader);
                            rs.OnSave += StoreRegionSettings;

                            reader.Close();
                        }
                        else
                        {
                            reader.Close();

                            rs = new RegionSettings();
                            rs.RegionUUID = regionUUID;
                            rs.OnSave += StoreRegionSettings;

                            StoreRegionSettings(rs);
                        }
                    }
                }
            }

            return rs;
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "replace into regionsettings (regionUUID, " +
                            "block_terraform, block_fly, allow_damage, " +
                            "restrict_pushing, allow_land_resell, " +
                            "allow_land_join_divide, block_show_in_search, " +
                            "agent_limit, object_bonus, maturity, " +
                            "disable_scripts, disable_collisions, " +
                            "disable_physics, terrain_texture_1, " +
                            "terrain_texture_2, terrain_texture_3, " +
                            "terrain_texture_4, elevation_1_nw, " +
                            "elevation_2_nw, elevation_1_ne, " +
                            "elevation_2_ne, elevation_1_se, " +
                            "elevation_2_se, elevation_1_sw, " +
                            "elevation_2_sw, water_height, " +
                            "terrain_raise_limit, terrain_lower_limit, " +
                            "use_estate_sun, fixed_sun, sun_position, " +
                            "covenant, Sandbox, sunvectorx, sunvectory, " +
                            "sunvectorz, covenantTimeStamp) values ( ?RegionUUID, ?BlockTerraform, " +
                            "?BlockFly, ?AllowDamage, ?RestrictPushing, " +
                            "?AllowLandResell, ?AllowLandJoinDivide, " +
                            "?BlockShowInSearch, ?AgentLimit, ?ObjectBonus, " +
                            "?Maturity, ?DisableScripts, ?DisableCollisions, " +
                            "?DisablePhysics, ?TerrainTexture1, " +
                            "?TerrainTexture2, ?TerrainTexture3, " +
                            "?TerrainTexture4, ?Elevation1NW, ?Elevation2NW, " +
                            "?Elevation1NE, ?Elevation2NE, ?Elevation1SE, " +
                            "?Elevation2SE, ?Elevation1SW, ?Elevation2SW, " +
                            "?WaterHeight, ?TerrainRaiseLimit, " +
                            "?TerrainLowerLimit, ?UseEstateSun, ?FixedSun, " +
                            "?SunPosition, ?Covenant, ?Sandbox, " +
                            "?SunVectorX, ?SunVectorY, ?SunVectorZ, ?CovenantTimeStamp)";

                    FillRegionSettingsCommand(cmd, rs);

                    ExecuteNonQuery(cmd);
                }
            }
        }

        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            List<LandData> landData = new List<LandData>();

            using (MySqlConnection conn = GetConnection())
            {
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select * from land where RegionUUID = ?RegionUUID";

                    cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());

                    using (IDataReader reader = ExecuteReader(cmd))
                    {
                        try
                        {
                            while (reader.Read())
                            {
                                LandData newLand = BuildLandData(reader);
                                landData.Add(newLand);
                            }
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }

                    foreach (LandData land in landData)
                    {
                        cmd.Parameters.Clear();

                        cmd.CommandText = "select * from landaccesslist " +
                                "where LandUUID = ?LandUUID";

                        cmd.Parameters.AddWithValue("LandUUID", land.GlobalID.ToString());

                        using (IDataReader reader = ExecuteReader(cmd))
                        {
                            try
                            {
                                while (reader.Read())
                                {
                                    land.ParcelAccessList.Add(BuildLandAccessData(reader));
                                }
                            }
                            finally
                            {
                                reader.Close();
                            }
                        }
                    }
                }
            }

            return landData;
        }

        public void Shutdown()
        {
        }

        private SceneObjectPart BuildPrim(IDataReader row)
        {
            SceneObjectPart prim = new SceneObjectPart();
            prim.UUID = new UUID(Convert.ToString(row["UUID"]));
            // explicit conversion of integers is required, which sort
            // of sucks.  No idea if there is a shortcut here or not.
            prim.CreationDate = Convert.ToInt32(row["CreationDate"]);
            prim.Name = (String) row["Name"];
            // various text fields
            prim.Text = (String) row["Text"];
            prim.TextColor = Color.FromArgb(Convert.ToInt32(row["ColorA"]),
                                        Convert.ToInt32(row["ColorR"]),
                                        Convert.ToInt32(row["ColorG"]),
                                        Convert.ToInt32(row["ColorB"]));
            prim.Description = (String) row["Description"];
            prim.SitName = (String) row["SitName"];
            prim.TouchName = (String) row["TouchName"];
            // permissions
            prim.ObjectFlags = Convert.ToUInt32(row["ObjectFlags"]);
            prim.CreatorID = new UUID(Convert.ToString(row["CreatorID"]));
            prim.OwnerID = new UUID(Convert.ToString(row["OwnerID"]));
            prim.GroupID = new UUID(Convert.ToString(row["GroupID"]));
            prim.LastOwnerID = new UUID(Convert.ToString(row["LastOwnerID"]));
            prim.OwnerMask = Convert.ToUInt32(row["OwnerMask"]);
            prim.NextOwnerMask = Convert.ToUInt32(row["NextOwnerMask"]);
            prim.GroupMask = Convert.ToUInt32(row["GroupMask"]);
            prim.EveryoneMask = Convert.ToUInt32(row["EveryoneMask"]);
            prim.BaseMask = Convert.ToUInt32(row["BaseMask"]);
            // vectors
            prim.OffsetPosition = new Vector3(
                Convert.ToSingle(row["PositionX"]),
                Convert.ToSingle(row["PositionY"]),
                Convert.ToSingle(row["PositionZ"])
                );
            prim.GroupPosition = new Vector3(
                Convert.ToSingle(row["GroupPositionX"]),
                Convert.ToSingle(row["GroupPositionY"]),
                Convert.ToSingle(row["GroupPositionZ"])
                );
            prim.Velocity = new Vector3(
                Convert.ToSingle(row["VelocityX"]),
                Convert.ToSingle(row["VelocityY"]),
                Convert.ToSingle(row["VelocityZ"])
                );
            prim.PhysicalAngularVelocity = new Vector3(
                Convert.ToSingle(row["AngularVelocityX"]),
                Convert.ToSingle(row["AngularVelocityY"]),
                Convert.ToSingle(row["AngularVelocityZ"])
                );
            // quaternions
            prim.RotationOffset = new Quaternion(
                Convert.ToSingle(row["RotationX"]),
                Convert.ToSingle(row["RotationY"]),
                Convert.ToSingle(row["RotationZ"]),
                Convert.ToSingle(row["RotationW"])
                );

            // We need ServerFlags first, in order to set all the SitTarget data.
            prim.ServerFlags = Convert.ToUInt32(row["ServerFlags"]);

            Vector3 sitTargetPos = new Vector3(
                Convert.ToSingle(row["SitTargetOffsetX"]),
                Convert.ToSingle(row["SitTargetOffsetY"]),
                Convert.ToSingle(row["SitTargetOffsetZ"])
            );
            Quaternion sitTargetRot = new Quaternion(
                Convert.ToSingle(row["SitTargetOrientX"]),
                Convert.ToSingle(row["SitTargetOrientY"]),
                Convert.ToSingle(row["SitTargetOrientZ"]),
                Convert.ToSingle(row["SitTargetOrientW"])
            );

            // Now the sit target info itself.

            // This function must be called on asset load (inventory rez) or database load (rezzed)
            // with SOP.ServerFlags initialized, which may be updated before return.
            bool sitTargetActive = prim.PrepSitTargetFromStorage(sitTargetPos, sitTargetRot);
            // Even though the prim is set, we need to call this to update the SceneObjectGroup.
            prim.SetSitTarget(sitTargetActive, sitTargetPos, sitTargetRot, false);

            prim.PayPrice[0] = Convert.ToInt32(row["PayPrice"]);
            prim.PayPrice[1] = Convert.ToInt32(row["PayButton1"]);
            prim.PayPrice[2] = Convert.ToInt32(row["PayButton2"]);
            prim.PayPrice[3] = Convert.ToInt32(row["PayButton3"]);
            prim.PayPrice[4] = Convert.ToInt32(row["PayButton4"]);

            prim.Sound = new UUID(row["LoopedSound"].ToString());
            if (prim.Sound == UUID.Zero)
            {
                prim.SoundGain = 0.0f;
                prim.SoundOptions = (byte)SoundFlags.None;
            }
            else
            {
                prim.SoundGain = Convert.ToSingle(row["LoopedSoundGain"]);
                // There is no SoundFlags persistence in the MySQL data. Fabricate one. (Another Thoosa improvement.)
                prim.SoundOptions = (byte)SoundFlags.Loop; // If it's persisted at all, it's looped
            }

            if (!(row["TextureAnimation"] is DBNull))
                prim.TextureAnimation = (Byte[])row["TextureAnimation"];
            if (!(row["ParticleSystem"] is DBNull))
                prim.ParticleSystem = (Byte[])row["ParticleSystem"];

            prim.AngularVelocity = new Vector3(
                Convert.ToSingle(row["OmegaX"]),
                Convert.ToSingle(row["OmegaY"]),
                Convert.ToSingle(row["OmegaZ"])
                );

            prim.SetCameraEyeOffset(new Vector3(
                Convert.ToSingle(row["CameraEyeOffsetX"]),
                Convert.ToSingle(row["CameraEyeOffsetY"]),
                Convert.ToSingle(row["CameraEyeOffsetZ"])
                ));

            prim.SetCameraAtOffset(new Vector3(
                Convert.ToSingle(row["CameraAtOffsetX"]),
                Convert.ToSingle(row["CameraAtOffsetY"]),
                Convert.ToSingle(row["CameraAtOffsetZ"])
                ));

            if (Convert.ToInt16(row["ForceMouselook"]) != 0)
                prim.SetForceMouselook(true);

            prim.ScriptAccessPin = Convert.ToInt32(row["ScriptAccessPin"]);

            if (Convert.ToInt16(row["AllowedDrop"]) != 0)
                prim.AllowedDrop = true;

            if (Convert.ToInt16(row["DieAtEdge"]) != 0)
                prim.DIE_AT_EDGE = true;

            prim.SalePrice = Convert.ToInt32(row["SalePrice"]);
            prim.ObjectSaleType = Convert.ToByte(row["SaleType"]);

            prim.Material = Convert.ToByte(row["Material"]);

            if (!(row["ClickAction"] is DBNull))
                prim.ClickAction = (byte)Convert.ToByte(row["ClickAction"]);

            prim.CollisionSound = new UUID(row["CollisionSound"].ToString());
            prim.CollisionSoundVolume = Convert.ToSingle(row["CollisionSoundVolume"]);
            prim.LinkNum = Convert.ToInt32(row["LinkNumber"]);

            prim.PassTouches = Convert.ToInt32(row["PassTouches"]) != 0;

            prim.SerializedPhysicsData = null;
            
            if (!(row["PhysicsData"] is System.DBNull))
            {
                byte[] phyData = (byte[])row["PhysicsData"];
                prim.SerializedPhysicsData = phyData;
            }

            prim.ServerWeight = Convert.ToSingle(row["ServerWeight"]);
            prim.StreamingCost = Convert.ToSingle(row["StreamingCost"]);

            if (!(row["KeyframeAnimation"] is DBNull))
            {
                ISerializationEngine engine;
                ProviderRegistry.Instance.TryGet<ISerializationEngine>(out engine);
                prim.KeyframeAnimation = engine.MiscObjectSerializer.DeserializeKeyframeAnimationFromBytes((Byte[])row["KeyframeAnimation"]);
                
                //prim.KeyframAnimation could be null if the deserialization failed
                if (prim.KeyframeAnimation != null)
                {
                    // Avoid reloading exception seen on Eagle Aerie
                    if (prim.KeyframeAnimation.TimeList == null)
                    {
                        m_log.ErrorFormat("[KeyframeAnimation]: Invalid time list for '{0}' at {1}", prim.Name, prim.AbsolutePosition);
                        prim.KeyframeAnimation.TimeList = new TimeSpan[0]; // repair it
                        prim.KeyframeAnimation.CurrentAnimationPosition = 0;
                    }
                    else // Avoid reloading persisted data that has out-of-range errors (seen on IB5).
                    if (prim.KeyframeAnimation.CurrentAnimationPosition >= prim.KeyframeAnimation.TimeList.Length)
                    {
                        m_log.ErrorFormat("[KeyframeAnimation]: Invalid current anim position {0} for '{1}' at {2}", prim.KeyframeAnimation.CurrentAnimationPosition, prim.Name, prim.AbsolutePosition);
                        prim.KeyframeAnimation.CurrentAnimationPosition = 0;    // repair by starting over since it wrapped anyway
                    }
                }
            }

            return prim;
        }


        /// <summary>
        /// Build a prim inventory item from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static TaskInventoryItem BuildItem(IDataReader row)
        {
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ItemID = new UUID(Convert.ToString(row["itemID"]));
            taskItem.ParentPartID = new UUID(Convert.ToString(row["primID"]));
            taskItem.AssetID = new UUID(Convert.ToString(row["assetID"]));
            taskItem.ParentID = new UUID(Convert.ToString(row["parentFolderID"]));

            taskItem.InvType       = Convert.ToInt32(row["invType"]);
            taskItem.Type          = Convert.ToInt32(row["assetType"]);

            taskItem.Name          = (String)row["name"];
            taskItem.Description   = (String)row["description"];
            taskItem.CreationDate  = Convert.ToUInt32(row["creationDate"]);
            taskItem.CreatorID = new UUID(Convert.ToString(row["creatorID"]));
            taskItem.OwnerID = new UUID(Convert.ToString(row["ownerID"]));
            taskItem.LastOwnerID = new UUID(Convert.ToString(row["lastOwnerID"]));
            taskItem.GroupID = new UUID(Convert.ToString(row["groupID"]));

            taskItem.NextPermissions = Convert.ToUInt32(row["nextPermissions"]);
            taskItem.CurrentPermissions     = Convert.ToUInt32(row["currentPermissions"]);
            taskItem.BasePermissions      = Convert.ToUInt32(row["basePermissions"]);
            taskItem.EveryonePermissions  = Convert.ToUInt32(row["everyonePermissions"]);
            taskItem.GroupPermissions     = Convert.ToUInt32(row["groupPermissions"]);
            taskItem.Flags         = Convert.ToUInt32(row["flags"]);

            if (Convert.ToInt32(row["canDebitOwner"]) == 1)
            {
                taskItem.PermsGranter = taskItem.OwnerID;
                taskItem.PermsMask |= PERMISSION_DEBIT;
            }

            return taskItem;
        }

        private static RegionSettings BuildRegionSettings(IDataReader row)
        {
            RegionSettings newSettings = new RegionSettings();

            newSettings.RegionUUID = new UUID(Convert.ToString(row["regionUUID"]));
            newSettings.BlockTerraform = Convert.ToBoolean(row["block_terraform"]);
            newSettings.AllowDamage = Convert.ToBoolean(row["allow_damage"]);
            newSettings.BlockFly = Convert.ToBoolean(row["block_fly"]);
            newSettings.RestrictPushing = Convert.ToBoolean(row["restrict_pushing"]);
            newSettings.AllowLandResell = Convert.ToBoolean(row["allow_land_resell"]);
            newSettings.AllowLandJoinDivide = Convert.ToBoolean(row["allow_land_join_divide"]);
            newSettings.BlockShowInSearch = Convert.ToBoolean(row["block_show_in_search"]);
            newSettings.AgentLimit = Convert.ToInt32(row["agent_limit"]);
            newSettings.ObjectBonus = Convert.ToDouble(row["object_bonus"]);
            newSettings.Maturity = Convert.ToInt32(row["maturity"]);
            newSettings.DisableScripts = Convert.ToBoolean(row["disable_scripts"]);
            newSettings.DisableCollisions = Convert.ToBoolean(row["disable_collisions"]);
            newSettings.DisablePhysics = Convert.ToBoolean(row["disable_physics"]);
            newSettings.TerrainTexture1 = new UUID(Convert.ToString(row["terrain_texture_1"]));
            newSettings.TerrainTexture2 = new UUID(Convert.ToString(row["terrain_texture_2"]));
            newSettings.TerrainTexture3 = new UUID(Convert.ToString(row["terrain_texture_3"]));
            newSettings.TerrainTexture4 = new UUID(Convert.ToString(row["terrain_texture_4"]));
            newSettings.Elevation1NW = Convert.ToDouble(row["elevation_1_nw"]);
            newSettings.Elevation2NW = Convert.ToDouble(row["elevation_2_nw"]);
            newSettings.Elevation1NE = Convert.ToDouble(row["elevation_1_ne"]);
            newSettings.Elevation2NE = Convert.ToDouble(row["elevation_2_ne"]);
            newSettings.Elevation1SE = Convert.ToDouble(row["elevation_1_se"]);
            newSettings.Elevation2SE = Convert.ToDouble(row["elevation_2_se"]);
            newSettings.Elevation1SW = Convert.ToDouble(row["elevation_1_sw"]);
            newSettings.Elevation2SW = Convert.ToDouble(row["elevation_2_sw"]);
            newSettings.WaterHeight = Convert.ToDouble(row["water_height"]);
            newSettings.TerrainRaiseLimit = Convert.ToDouble(row["terrain_raise_limit"]);
            newSettings.TerrainLowerLimit = Convert.ToDouble(row["terrain_lower_limit"]);
            newSettings.UseEstateSun = Convert.ToBoolean(row["use_estate_sun"]);
            newSettings.Sandbox = Convert.ToBoolean(row["sandbox"]);
            newSettings.SunVector = new Vector3 (
                                                 Convert.ToSingle(row["sunvectorx"]),
                                                 Convert.ToSingle(row["sunvectory"]),
                                                 Convert.ToSingle(row["sunvectorz"])
                                                 );
            newSettings.FixedSun = Convert.ToBoolean(row["fixed_sun"]);
            newSettings.SunPosition = Convert.ToDouble(row["sun_position"]);
            newSettings.Covenant = new UUID(Convert.ToString(row["covenant"]));
            newSettings.CovenantLastUpdated = uint.Parse(Convert.ToString(row["covenantTimeStamp"]));

            return newSettings;
        }

        // region land parcel bitmap array with all bits on
        private static byte[] DefaultRegionBitmap()
        {
            byte[] tempBitmap = new byte[512];
            tempBitmap.Initialize();
            for (int x = 0; x < 512; x++)
                tempBitmap[x] = (byte)0xFF;
            return tempBitmap;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static LandData BuildLandData(IDataReader row)
        {
            LandData newData = new LandData();

            newData.GlobalID = new UUID(Convert.ToString(row["UUID"]));
            newData.LocalID = Convert.ToInt32(row["LocalLandID"]);

            // Bitmap is a byte[512]
            newData.Bitmap = (row["Bitmap"] is DBNull) ? DefaultRegionBitmap() : (Byte[])row["Bitmap"];
            newData.Name = (String) row["Name"];
            newData.Description = (String) row["Description"];
            newData.OwnerID = new UUID(Convert.ToString(row["OwnerUUID"]));
            newData.IsGroupOwned = Convert.ToBoolean(row["IsGroupOwned"]);
            newData.Area = Convert.ToInt32(row["Area"]);
            newData.AuctionID = Convert.ToUInt32(row["AuctionID"]); //Unimplemented
            newData.Category = (ParcelCategory) Convert.ToInt32(row["Category"]);
                //Enum libsecondlife.Parcel.ParcelCategory
            newData.ClaimDate = Convert.ToInt32(row["ClaimDate"]);
            newData.ClaimPrice = Convert.ToInt32(row["ClaimPrice"]);
            newData.GroupID = new UUID(Convert.ToString(row["GroupUUID"]));
            newData.SalePrice = Convert.ToInt32(row["SalePrice"]);
            newData.Status = (ParcelStatus) Convert.ToInt32(row["LandStatus"]);
                //Enum. libsecondlife.Parcel.ParcelStatus
            newData.Flags = Convert.ToUInt32(row["LandFlags"]);

            newData.LandingType = Convert.ToByte(row["LandingType"]);
            newData.MediaAutoScale = Convert.ToByte(row["MediaAutoScale"]);
            newData.MediaID = new UUID(Convert.ToString(row["MediaTextureUUID"]));
            newData.MediaURL = (String) row["MediaURL"];
            newData.MusicURL = (String) row["MusicURL"];
            newData.PassHours = Convert.ToSingle(row["PassHours"]);
            newData.PassPrice = Convert.ToInt32(row["PassPrice"]);
            UUID authedbuyer = UUID.Zero;
            UUID snapshotID = UUID.Zero;

            UUID.TryParse(Convert.ToString(row["AuthBuyerID"]), out authedbuyer);
            UUID.TryParse(Convert.ToString(row["SnapshotUUID"]), out snapshotID);
            newData.OtherCleanTime = Convert.ToInt32(row["OtherCleanTime"]);
            newData.Dwell = Convert.ToInt32(row["Dwell"]);

            newData.AuthBuyerID = authedbuyer;
            newData.SnapshotID = snapshotID;
            try
            {
                newData.UserLocation =
                    new Vector3(Convert.ToSingle(row["UserLocationX"]), Convert.ToSingle(row["UserLocationY"]),
                                  Convert.ToSingle(row["UserLocationZ"]));
                newData.UserLookAt =
                    new Vector3(Convert.ToSingle(row["UserLookAtX"]), Convert.ToSingle(row["UserLookAtY"]),
                                  Convert.ToSingle(row["UserLookAtZ"]));
            }
            catch (InvalidCastException)
            {
                newData.UserLocation = Vector3.Zero;
                newData.UserLookAt = Vector3.Zero;
                m_log.ErrorFormat("[PARCEL]: unable to get parcel telehub settings for {1}", newData.Name);
            }

            newData.ParcelAccessList = new List<ParcelManager.ParcelAccessEntry>();

            return newData;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static ParcelManager.ParcelAccessEntry BuildLandAccessData(IDataReader row)
        {
            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = new UUID(Convert.ToString(row["AccessUUID"]));
            entry.Flags = (AccessList) Convert.ToInt32(row["Flags"]);
            entry.Time = new DateTime();
            return entry;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static Array SerializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(65536*sizeof(double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < 256; x++)
                for (int y = 0; y < 256; y++)
                {
                    double height = val[x, y];
                    if (height == 0.0)
                        height = double.Epsilon;

                    bw.Write(height);
                }

            return str.ToArray();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="taskItem"></param>
        private static void FillItemCommand(MySqlCommand cmd, TaskInventoryItem taskItem)
        {
            cmd.Parameters.AddWithValue("itemID", taskItem.ItemID);
            cmd.Parameters.AddWithValue("primID", taskItem.ParentPartID);
            cmd.Parameters.AddWithValue("assetID", taskItem.AssetID);
            cmd.Parameters.AddWithValue("parentFolderID", taskItem.ParentID);

            cmd.Parameters.AddWithValue("invType", taskItem.InvType);
            cmd.Parameters.AddWithValue("assetType", taskItem.Type);

            cmd.Parameters.AddWithValue("name", taskItem.Name);
            cmd.Parameters.AddWithValue("description", taskItem.Description);
            cmd.Parameters.AddWithValue("creationDate", taskItem.CreationDate);
            cmd.Parameters.AddWithValue("creatorID", taskItem.CreatorID);
            cmd.Parameters.AddWithValue("ownerID", taskItem.OwnerID);
            cmd.Parameters.AddWithValue("lastOwnerID", taskItem.LastOwnerID);
            cmd.Parameters.AddWithValue("groupID", taskItem.GroupID);
            cmd.Parameters.AddWithValue("nextPermissions", taskItem.NextPermissions);
            cmd.Parameters.AddWithValue("currentPermissions", taskItem.CurrentPermissions);
            cmd.Parameters.AddWithValue("basePermissions", taskItem.BasePermissions);
            cmd.Parameters.AddWithValue("everyonePermissions", taskItem.EveryonePermissions);
            cmd.Parameters.AddWithValue("groupPermissions", taskItem.GroupPermissions);
            cmd.Parameters.AddWithValue("flags", taskItem.Flags);

            int debitOwner = 0;
            if ((taskItem.PermsMask & PERMISSION_DEBIT) != 0)
            {
                debitOwner = 1;
            }

            cmd.Parameters.AddWithValue("canDebitOwner", debitOwner);
        }

        /// <summary>
        ///
        /// </summary>
        private static void FillRegionSettingsCommand(MySqlCommand cmd, RegionSettings settings)
        {
            cmd.Parameters.AddWithValue("RegionUUID", settings.RegionUUID.ToString());
            cmd.Parameters.AddWithValue("BlockTerraform", settings.BlockTerraform);
            cmd.Parameters.AddWithValue("BlockFly", settings.BlockFly);
            cmd.Parameters.AddWithValue("AllowDamage", settings.AllowDamage);
            cmd.Parameters.AddWithValue("RestrictPushing", settings.RestrictPushing);
            cmd.Parameters.AddWithValue("AllowLandResell", settings.AllowLandResell);
            cmd.Parameters.AddWithValue("AllowLandJoinDivide", settings.AllowLandJoinDivide);
            cmd.Parameters.AddWithValue("BlockShowInSearch", settings.BlockShowInSearch);
            cmd.Parameters.AddWithValue("AgentLimit", settings.AgentLimit);
            cmd.Parameters.AddWithValue("ObjectBonus", settings.ObjectBonus);
            cmd.Parameters.AddWithValue("Maturity", settings.Maturity);
            cmd.Parameters.AddWithValue("DisableScripts", settings.DisableScripts);
            cmd.Parameters.AddWithValue("DisableCollisions", settings.DisableCollisions);
            cmd.Parameters.AddWithValue("DisablePhysics", settings.DisablePhysics);
            cmd.Parameters.AddWithValue("TerrainTexture1", settings.TerrainTexture1.ToString());
            cmd.Parameters.AddWithValue("TerrainTexture2", settings.TerrainTexture2.ToString());
            cmd.Parameters.AddWithValue("TerrainTexture3", settings.TerrainTexture3.ToString());
            cmd.Parameters.AddWithValue("TerrainTexture4", settings.TerrainTexture4.ToString());
            cmd.Parameters.AddWithValue("Elevation1NW", settings.Elevation1NW);
            cmd.Parameters.AddWithValue("Elevation2NW", settings.Elevation2NW);
            cmd.Parameters.AddWithValue("Elevation1NE", settings.Elevation1NE);
            cmd.Parameters.AddWithValue("Elevation2NE", settings.Elevation2NE);
            cmd.Parameters.AddWithValue("Elevation1SE", settings.Elevation1SE);
            cmd.Parameters.AddWithValue("Elevation2SE", settings.Elevation2SE);
            cmd.Parameters.AddWithValue("Elevation1SW", settings.Elevation1SW);
            cmd.Parameters.AddWithValue("Elevation2SW", settings.Elevation2SW);
            cmd.Parameters.AddWithValue("WaterHeight", settings.WaterHeight);
            cmd.Parameters.AddWithValue("TerrainRaiseLimit", settings.TerrainRaiseLimit);
            cmd.Parameters.AddWithValue("TerrainLowerLimit", settings.TerrainLowerLimit);
            cmd.Parameters.AddWithValue("UseEstateSun", settings.UseEstateSun);
            cmd.Parameters.AddWithValue("Sandbox", settings.Sandbox);
            cmd.Parameters.AddWithValue("SunVectorX", settings.SunVector.X);
            cmd.Parameters.AddWithValue("SunVectorY", settings.SunVector.Y);
            cmd.Parameters.AddWithValue("SunVectorZ", settings.SunVector.Z);
            cmd.Parameters.AddWithValue("FixedSun", settings.FixedSun);
            cmd.Parameters.AddWithValue("SunPosition", settings.SunPosition);
            cmd.Parameters.AddWithValue("Covenant", settings.Covenant.ToString());
            cmd.Parameters.AddWithValue("CovenantTimeStamp", settings.CovenantLastUpdated.ToString());
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="land"></param>
        /// <param name="regionUUID"></param>
        private static void FillLandCommand(MySqlCommand cmd, LandData land, UUID regionUUID)
        {
            cmd.Parameters.AddWithValue("UUID", land.GlobalID.ToString());
            cmd.Parameters.AddWithValue("RegionUUID", regionUUID.ToString());
            cmd.Parameters.AddWithValue("LocalLandID", land.LocalID);

            // Bitmap is a byte[512]
            cmd.Parameters.AddWithValue("Bitmap", land.Bitmap);

            cmd.Parameters.AddWithValue("Name", land.Name);
            cmd.Parameters.AddWithValue("Description", land.Description);
            cmd.Parameters.AddWithValue("OwnerUUID", land.OwnerID.ToString());
            cmd.Parameters.AddWithValue("IsGroupOwned", land.IsGroupOwned);
            cmd.Parameters.AddWithValue("Area", land.Area);
            cmd.Parameters.AddWithValue("AuctionID", land.AuctionID); //Unemplemented
            cmd.Parameters.AddWithValue("Category", land.Category); //Enum libsecondlife.Parcel.ParcelCategory
            cmd.Parameters.AddWithValue("ClaimDate", land.ClaimDate);
            cmd.Parameters.AddWithValue("ClaimPrice", land.ClaimPrice);
            cmd.Parameters.AddWithValue("GroupUUID", land.GroupID.ToString());
            cmd.Parameters.AddWithValue("SalePrice", land.SalePrice);
            cmd.Parameters.AddWithValue("LandStatus", land.Status); //Enum. libsecondlife.Parcel.ParcelStatus
            cmd.Parameters.AddWithValue("LandFlags", land.Flags);
            cmd.Parameters.AddWithValue("LandingType", land.LandingType);
            cmd.Parameters.AddWithValue("MediaAutoScale", land.MediaAutoScale);
            cmd.Parameters.AddWithValue("MediaTextureUUID", land.MediaID.ToString());
            cmd.Parameters.AddWithValue("MediaURL", land.MediaURL);
            cmd.Parameters.AddWithValue("MusicURL", land.MusicURL);
            cmd.Parameters.AddWithValue("PassHours", land.PassHours);
            cmd.Parameters.AddWithValue("PassPrice", land.PassPrice);
            cmd.Parameters.AddWithValue("SnapshotUUID", land.SnapshotID.ToString());
            cmd.Parameters.AddWithValue("UserLocationX", land.UserLocation.X);
            cmd.Parameters.AddWithValue("UserLocationY", land.UserLocation.Y);
            cmd.Parameters.AddWithValue("UserLocationZ", land.UserLocation.Z);
            cmd.Parameters.AddWithValue("UserLookAtX", land.UserLookAt.X);
            cmd.Parameters.AddWithValue("UserLookAtY", land.UserLookAt.Y);
            cmd.Parameters.AddWithValue("UserLookAtZ", land.UserLookAt.Z);
            cmd.Parameters.AddWithValue("AuthBuyerID", land.AuthBuyerID);
            cmd.Parameters.AddWithValue("OtherCleanTime", land.OtherCleanTime);
            cmd.Parameters.AddWithValue("Dwell", land.Dwell);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="entry"></param>
        /// <param name="parcelID"></param>
        private static void FillLandAccessCommand(MySqlCommand cmd, ParcelManager.ParcelAccessEntry entry, UUID parcelID)
        {
            cmd.Parameters.AddWithValue("LandUUID", parcelID.ToString());
            cmd.Parameters.AddWithValue("AccessUUID", entry.AgentID.ToString());
            cmd.Parameters.AddWithValue("Flags", entry.Flags);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private PrimitiveBaseShape BuildShape(IDataReader row)
        {
            PrimitiveBaseShape s = new PrimitiveBaseShape();
            s.Scale = new Vector3(
                Convert.ToSingle(row["ScaleX"]),
                Convert.ToSingle(row["ScaleY"]),
                Convert.ToSingle(row["ScaleZ"])
                );
            // paths
            s.PCode = Convert.ToByte(row["PCode"]);
            s.PathBegin = Convert.ToUInt16(row["PathBegin"]);
            s.PathEnd = Convert.ToUInt16(row["PathEnd"]);
            s.PathScaleX = Convert.ToByte(row["PathScaleX"]);
            s.PathScaleY = Convert.ToByte(row["PathScaleY"]);
            s.PathShearX = Convert.ToByte(row["PathShearX"]);
            s.PathShearY = Convert.ToByte(row["PathShearY"]);
            s.PathSkew = Convert.ToSByte(row["PathSkew"]);
            s.PathCurve = Convert.ToByte(row["PathCurve"]);
            s.PathRadiusOffset = Convert.ToSByte(row["PathRadiusOffset"]);
            s.PathRevolutions = Convert.ToByte(row["PathRevolutions"]);
            s.PathTaperX = Convert.ToSByte(row["PathTaperX"]);
            s.PathTaperY = Convert.ToSByte(row["PathTaperY"]);
            s.PathTwist = Convert.ToSByte(row["PathTwist"]);
            s.PathTwistBegin = Convert.ToSByte(row["PathTwistBegin"]);
            // profile
            s.ProfileBegin = Convert.ToUInt16(row["ProfileBegin"]);
            s.ProfileEnd = Convert.ToUInt16(row["ProfileEnd"]);
            s.ProfileCurve = Convert.ToByte(row["ProfileCurve"]);
            s.ProfileHollow = Convert.ToUInt16(row["ProfileHollow"]);

            s.TextureEntryBytes = (byte[])row["Texture"];   // also updates Textures
            s.ExtraParams = (byte[]) row["ExtraParams"];

            s.Media = null;
            if (!(row["Media"] is System.DBNull))
            {
                byte[] media = (byte[])row["Media"];
                s.Media = PrimitiveBaseShape.PrimMedia.FromXml(UTF8Encoding.UTF8.GetString(media, 0, media.Length));
            }

            if (!(row["Materials"] is System.DBNull))
            {
                byte[] materials = (byte[])row["Materials"];
                s.RenderMaterials = RenderMaterials.FromBytes(materials, 0);
            }

            s.State = Convert.ToByte(row["State"]);

            s.PreferredPhysicsShape = (OpenMetaverse.PhysicsShapeType) Convert.ToByte(row["PreferredPhysicsShape"]);

            s.VertexCount = (row["VertexCount"] is System.DBNull) ? 0 : Convert.ToInt32(row["VertexCount"]);
            s.HighLODBytes = (row["HighLODBytes"] is System.DBNull) ? 0 : Convert.ToInt32(row["HighLODBytes"]);
            s.MidLODBytes = (row["MidLODBytes"] is System.DBNull) ? 0 : Convert.ToInt32(row["MidLODBytes"]);
            s.LowLODBytes = (row["LowLODBytes"] is System.DBNull) ? 0 : Convert.ToInt32(row["LowLODBytes"]);
            s.LowestLODBytes = (row["LowestLODBytes"] is System.DBNull) ? 0 : Convert.ToInt32(row["LowestLODBytes"]);

            return s;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        private void FillShapeCommand(MySqlCommand cmd, SceneObjectPart prim)
        {
            PrimitiveBaseShape s = prim.Shape;
            cmd.Parameters.AddWithValue("UUID", prim.UUID.ToString());
            // shape is an enum
            cmd.Parameters.AddWithValue("Shape", 0);
            // vectors
            cmd.Parameters.AddWithValue("ScaleX", (double)FixFloat(s.Scale.X));
            cmd.Parameters.AddWithValue("ScaleY", (double)FixFloat(s.Scale.Y));
            cmd.Parameters.AddWithValue("ScaleZ", (double)FixFloat(s.Scale.Z));
            // paths
            cmd.Parameters.AddWithValue("PCode", s.PCode);
            cmd.Parameters.AddWithValue("PathBegin", s.PathBegin);
            cmd.Parameters.AddWithValue("PathEnd", s.PathEnd);
            cmd.Parameters.AddWithValue("PathScaleX", s.PathScaleX);
            cmd.Parameters.AddWithValue("PathScaleY", s.PathScaleY);
            cmd.Parameters.AddWithValue("PathShearX", s.PathShearX);
            cmd.Parameters.AddWithValue("PathShearY", s.PathShearY);
            cmd.Parameters.AddWithValue("PathSkew", s.PathSkew);
            cmd.Parameters.AddWithValue("PathCurve", s.PathCurve);
            cmd.Parameters.AddWithValue("PathRadiusOffset", s.PathRadiusOffset);
            cmd.Parameters.AddWithValue("PathRevolutions", s.PathRevolutions);
            cmd.Parameters.AddWithValue("PathTaperX", s.PathTaperX);
            cmd.Parameters.AddWithValue("PathTaperY", s.PathTaperY);
            cmd.Parameters.AddWithValue("PathTwist", s.PathTwist);
            cmd.Parameters.AddWithValue("PathTwistBegin", s.PathTwistBegin);
            // profile
            cmd.Parameters.AddWithValue("ProfileBegin", s.ProfileBegin);
            cmd.Parameters.AddWithValue("ProfileEnd", s.ProfileEnd);
            cmd.Parameters.AddWithValue("ProfileCurve", s.ProfileCurve);
            cmd.Parameters.AddWithValue("ProfileHollow", s.ProfileHollow);
            cmd.Parameters.AddWithValue("Texture", s.TextureEntryBytes);
            cmd.Parameters.AddWithValue("ExtraParams", s.ExtraParams);
            cmd.Parameters.AddWithValue("Media", s.Media == null ? null : s.Media.ToXml());
            cmd.Parameters.AddWithValue("Materials", s.RenderMaterials == null ? null : s.RenderMaterials.ToBytes());
            cmd.Parameters.AddWithValue("State", s.State);
        }

        private string GenerateInList(IEnumerable<UUID> ids)
        {
            StringBuilder groupIdList = new StringBuilder("(");
            bool first = true;
            foreach (UUID id in ids)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    groupIdList.Append(",");
                }

                groupIdList.Append("'" + id.ToString() + "'");
            }


            groupIdList.Append(")");

            return groupIdList.ToString();
        }

        public void CleanObjectInventories(List<UUID> primIds, List<UUID> deletedItems)
        {
            if (deletedItems.Count > 0)
            {
                string query = "DELETE FROM primitems WHERE primID IN ";
                query += this.GenerateInList(primIds);
                query += " AND itemID IN ";
                query += this.GenerateInList(deletedItems);

                lock (m_PrimDBLock)
                {
                    using (MySqlConnection conn = GetConnection())
                    {
                        using (MySqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = query;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private string GenerateNumberedInventoryValues(int number)
        {
            string numberString = Convert.ToString(number);

            string valueSet = 
                "(?invType, " +
                "?assetType, ?name, ?description, " +
                "?creationDate, ?nextPermissions, " +
                "?currentPermissions, ?basePermissions, " +
                "?everyonePermissions, ?groupPermissions, " +
                "?flags, ?itemID, ?primID, ?assetID, " +
                "?parentFolderID, ?creatorID, ?ownerID, " +
                "?groupID, ?lastOwnerID, ?canDebitOwner)";

            valueSet = valueSet.Replace(",", numberString + ",").Replace(")", numberString + ")");
            if (number != 0) valueSet = "," + valueSet;

            return valueSet;
        }

        private static void FillItemCommandNumbered(MySqlCommand cmd, TaskInventoryItem taskItem, int number)
        {
            string numString = Convert.ToString(number);

            cmd.Parameters.AddWithValue("itemID" + numString, taskItem.ItemID);
            cmd.Parameters.AddWithValue("primID" + numString, taskItem.ParentPartID);
            cmd.Parameters.AddWithValue("assetID" + numString, taskItem.AssetID);
            cmd.Parameters.AddWithValue("parentFolderID" + numString, taskItem.ParentID);

            cmd.Parameters.AddWithValue("invType" + numString, taskItem.InvType);
            cmd.Parameters.AddWithValue("assetType" + numString, taskItem.Type);

            cmd.Parameters.AddWithValue("name" + numString, taskItem.Name);
            cmd.Parameters.AddWithValue("description" + numString, taskItem.Description);
            cmd.Parameters.AddWithValue("creationDate" + numString, taskItem.CreationDate);
            cmd.Parameters.AddWithValue("creatorID" + numString, taskItem.CreatorID);
            cmd.Parameters.AddWithValue("ownerID" + numString, taskItem.OwnerID);
            cmd.Parameters.AddWithValue("lastOwnerID" + numString, taskItem.LastOwnerID);
            cmd.Parameters.AddWithValue("groupID" + numString, taskItem.GroupID);
            cmd.Parameters.AddWithValue("nextPermissions" + numString, taskItem.NextPermissions);
            cmd.Parameters.AddWithValue("currentPermissions" + numString, taskItem.CurrentPermissions);
            cmd.Parameters.AddWithValue("basePermissions" + numString, taskItem.BasePermissions);
            cmd.Parameters.AddWithValue("everyonePermissions" + numString, taskItem.EveryonePermissions);
            cmd.Parameters.AddWithValue("groupPermissions" + numString, taskItem.GroupPermissions);
            cmd.Parameters.AddWithValue("flags" + numString, taskItem.Flags);

            int debitOwner = 0;
            if ((taskItem.PermsMask & PERMISSION_DEBIT) != 0)
            {
                debitOwner = 1;
            }

            cmd.Parameters.AddWithValue("canDebitOwner" + numString, debitOwner);
        }

        public void BulkStoreObjectInventories(IEnumerable<KeyValuePair<UUID, IEnumerable<TaskInventoryItem>>> items,
            IEnumerable<KeyValuePair<UUID, IEnumerable<UUID>>> deletedItems)
        {
            lock (m_PrimDBLock)
            {
                using (MySqlConnection conn = GetConnection())
                {
                    //collect prim ids and item ids
                    //BOTH deleted items AND items will have all the prim IDs, so it's safe to use either for 
                    //a list of prim ids
                    List<UUID> primIds = new List<UUID>();
                    List<UUID> deletedItemIds = new List<UUID>();
                    List<UUID> existingItems = new List<UUID>();

                    foreach (KeyValuePair<UUID, IEnumerable<UUID>> prims in deletedItems)
                    {
                        primIds.Add(prims.Key);

                        foreach (UUID item in prims.Value)
                        {
                            deletedItemIds.Add(item);
                        }
                    }

                    foreach (KeyValuePair<UUID, IEnumerable<TaskInventoryItem>> prims in items)
                    {
                        foreach (TaskInventoryItem item in prims.Value)
                        {
                            existingItems.Add(item.ItemID);
                        }
                    }

                    if (primIds.Count == 0) return;

                    this.CleanObjectInventories(primIds, deletedItemIds);

                    if (existingItems.Count == 0) return;


                string primItemsQueryHeader = 
                    "INSERT INTO primitems ("+
                        "invType, assetType, name, "+
                        "description, creationDate, nextPermissions, "+
                        "currentPermissions, basePermissions, "+
                        "everyonePermissions, groupPermissions, "+
                        "flags, itemID, primID, assetID, "+
                        "parentFolderID, creatorID, ownerID, "+
                            "groupID, lastOwnerID, canDebitOwner) values ";

                    string primItemsQueryEpilogue =
                        " ON DUPLICATE KEY UPDATE " +
                        "invType=VALUES(invType), assetType=VALUES(assetType), " +
                        "name=VALUES(name), description=VALUES(description), " +
                        "creationDate=VALUES(creationDate), nextPermissions=VALUES(nextPermissions), " +
                        "currentPermissions=VALUES(currentPermissions), basePermissions=VALUES(basePermissions), " +
                        "everyonePermissions=VALUES(everyonePermissions), groupPermissions=VALUES(groupPermissions), " +
                        "flags=VALUES(flags), primID=VALUES(primID), assetID=VALUES(assetID), " +
                        "parentFolderID=VALUES(parentFolderID), creatorID=VALUES(creatorID), ownerID=VALUES(ownerID), " +
                        "groupID=VALUES(groupID), lastOwnerID=VALUES(lastOwnerID), canDebitOwner=VALUES(canDebitOwner) ";

                    int MAX_ITEMS_PER_QUERY = 128;

                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        StringBuilder primItemsQuery = new StringBuilder(primItemsQueryHeader);

                        int i = 0;
                        foreach (KeyValuePair<UUID, IEnumerable<TaskInventoryItem>> prims in items)
                        {
                            foreach (TaskInventoryItem item in prims.Value)
                            {
                                primItemsQuery.Append(GenerateNumberedInventoryValues(i));
                                FillItemCommandNumbered(cmd, item, i);

                                if (i == MAX_ITEMS_PER_QUERY)
                                {
                                    cmd.CommandText = primItemsQuery.ToString() + primItemsQueryEpilogue;
                                    cmd.ExecuteNonQuery();

                                    cmd.Parameters.Clear();
                                    primItemsQuery = new StringBuilder(primItemsQueryHeader);

                                    i = 0;
                                }
                                else
                                {
                                    i++;
                                }
                            }
                        }

                        if (i != 0)
                        {
                            cmd.CommandText = primItemsQuery.ToString() + primItemsQueryEpilogue;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }
}
