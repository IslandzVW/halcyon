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
using System.Drawing;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Interfaces;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace OpenSim.Region.CoreModules.World.WorldMap
{
    public enum DrawRoutine
    {
        Rectangle,
        Polygon,
        Ellipse
    }

    public struct face
    {
        public Point[] pts;
    }

    public struct DrawStruct
    {
        public DrawRoutine dr;
        public Rectangle rect;
        public SolidBrush brush;
        public face[] trns;
    }

    /// <summary>
    /// A very fast, but special purpose, tool for doing lots of drawing operations.
    /// </summary>
    public class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }

    public class MapImageModule : IMapImageGenerator, IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IConfigSource m_config;
        private IMapTileTerrainRenderer terrainRenderer;

        private bool drawPrimVolume = true;
        private bool textureTerrain = false;

        private byte[] imageData = null;

        private DirectBitmap mapbmp = new DirectBitmap(256, 256);

        #region IMapImageGenerator Members

        public byte[] WriteJpeg2000Image()
        {
            if (terrainRenderer != null)
            {
            //long t = System.Environment.TickCount;
            //for (int i = 0; i < 10; ++i) {
                terrainRenderer.TerrainToBitmap(mapbmp);
            //}
            //t = System.Environment.TickCount - t;
            //m_log.InfoFormat("[MAPTILE] generation of 10 maptiles needed {0} ms", t);
            }

            if (drawPrimVolume)
            {
                DrawObjectVolume(m_scene, mapbmp);
            }

            try
            {
                imageData = OpenJPEG.EncodeFromImage(mapbmp.Bitmap, true);
            }
            catch (Exception e) // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                m_log.Error("Failed generating terrain map: " + e);
            }

            return imageData;
        }

        #endregion

        #region IRegionModule Members

        public void Initialize(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_config = source;

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"]; // Location supported for legacy INI files.
                IConfig worldmapConfig = m_config.Configs["WorldMap"];

                if (startupConfig.GetString("MapImageModule", "MapImageModule") !=
                    "MapImageModule")
                    return;
                
                // Go find the parameters in the new location and if not found go looking in the old.
                drawPrimVolume = (worldmapConfig != null && worldmapConfig.Contains("DrawPrimOnMapTile")) ?
                    worldmapConfig.GetBoolean("DrawPrimOnMapTile", drawPrimVolume) :
                    startupConfig.GetBoolean("DrawPrimOnMapTile", drawPrimVolume);

                textureTerrain = (worldmapConfig != null && worldmapConfig.Contains("TextureOnMapTile")) ?
                    worldmapConfig.GetBoolean("TextureOnMapTile", textureTerrain) :
                    startupConfig.GetBoolean("TextureOnMapTile", textureTerrain);
            }
            catch
            {
                m_log.Warn("[MAPTILE]: Failed to load StartupConfig");
            }

            if (textureTerrain)
            {
                terrainRenderer = new TexturedMapTileRenderer();
            }
            else
            {
                terrainRenderer = new ShadedMapTileRenderer();
            }
            terrainRenderer.Initialize(m_scene, m_config);

            m_scene.RegisterModuleInterface<IMapImageGenerator>(this);
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "MapImageModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

// TODO: unused:
//         private void ShadeBuildings(Bitmap map)
//         {
//             lock (map)
//             {
//                 lock (m_scene.Entities)
//                 {
//                     foreach (EntityBase entity in m_scene.Entities.Values)
//                     {
//                         if (entity is SceneObjectGroup)
//                         {
//                             SceneObjectGroup sog = (SceneObjectGroup) entity;
//
//                             foreach (SceneObjectPart primitive in sog.Children.Values)
//                             {
//                                 int x = (int) (primitive.AbsolutePosition.X - (primitive.Scale.X / 2));
//                                 int y = (int) (primitive.AbsolutePosition.Y - (primitive.Scale.Y / 2));
//                                 int w = (int) primitive.Scale.X;
//                                 int h = (int) primitive.Scale.Y;
//
//                                 int dx;
//                                 for (dx = x; dx < x + w; dx++)
//                                 {
//                                     int dy;
//                                     for (dy = y; dy < y + h; dy++)
//                                     {
//                                         if (x < 0 || y < 0)
//                                             continue;
//                                         if (x >= map.Width || y >= map.Height)
//                                             continue;
//
//                                         map.SetPixel(dx, dy, Color.DarkGray);
//                                     }
//                                 }
//                             }
//                         }
//                     }
//                 }
//             }
//         }

        private DirectBitmap DrawObjectVolume(Scene whichScene, DirectBitmap mapbmp)
        {
            int tc = 0;
            var hm = whichScene.Heightmap.GetDoubles();
            tc = Environment.TickCount;
            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Object Volume Profile");
            var objs = whichScene.GetEntities();
            var z_sort = new Dictionary<uint, DrawStruct>();
            //SortedList<float, RectangleDrawStruct> z_sort = new SortedList<float, RectangleDrawStruct>();
            var z_sortheights = new List<float>();
            var z_localIDs = new List<uint>();

            //useless: lock (objs)
            {
                SceneObjectGroup mapdot;
                Color mapdotspot;
                Color4 texcolor;
                Vector3 pos;
                Quaternion rot;
                Vector3 scale;
                Vector3 tScale;

                var vertexes = new Vector3[8];
                // float[] distance = new float[6];
                var FaceA = new Vector3[6]; // vertex A for Facei
                var FaceB = new Vector3[6]; // vertex B for Facei
                var FaceC = new Vector3[6]; // vertex C for Facei
                var FaceD = new Vector3[6]; // vertex D for Facei

                // Love the copy-on-assignment for structs...
                var ds = new DrawStruct();
                face workingface;// = new face();
                workingface.pts = new Point[5];

                foreach (EntityBase obj in objs)
                {
                    // Only draw the contents of SceneObjectGroup
                    mapdot = obj as SceneObjectGroup;
                    if (mapdot != null)
                    {
                        mapdotspot = Color.Gray; // Default color when prim color is white
                        // Loop over prim in group
                        foreach (SceneObjectPart part in mapdot.GetParts())
                        {
                            /* * * * * * * * * * * * * * * * * * */
                            // FILTERING PASS
                            /* * * * * * * * * * * * * * * * * * */

                            if (
                                // get the null checks out of the way
                                part == null ||
                                part.Shape == null ||
                                part.Shape.Textures == null ||
                                part.Shape.Textures.DefaultTexture == null ||

                                // Make sure the object isn't temp or phys
                                (part.Flags & (PrimFlags.Physics | PrimFlags.Temporary | PrimFlags.TemporaryOnRez)) != 0 ||

                                // Draw only if the object is at least 1 meter wide in all directions
                                part.Scale.X <= 1f || part.Scale.Y <= 1f || part.Scale.Z <= 1f ||

                                // Eliminate trees from this since we don't really have a good tree representation
                                part.Shape.PCode == (byte)PCode.Tree || part.Shape.PCode == (byte)PCode.NewTree || part.Shape.PCode == (byte)PCode.Grass ||

                                false
                            )
                                continue;

                            pos = part.GetWorldPosition();

                            // skip prim outside of region
                            if (pos.X < 0.0f || pos.X >= 256.0f || pos.Y < 0.0f || pos.Y >= 256.0f)
                                continue;

                            // skip prim in non-finite position
                            if (Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) ||
                                Single.IsInfinity(pos.X) || Single.IsInfinity(pos.Y))
                                continue;

                            bool isBelow256AboveTerrain = false;

                            try
                            {
                                isBelow256AboveTerrain = (pos.Z < ((float)hm[(int)pos.X, (int)pos.Y] + 256f));
                            }
                            catch (Exception)
                            {
                            }

                            if (!isBelow256AboveTerrain)
                                continue;

                            // Translate scale by rotation so scale is represented properly when object is rotated
                            rot = part.GetWorldRotation();
                            // Convert from LL's XYZW format to the WXYZ format the following math needs.
                            float temp = rot.X;
                            rot.X = rot.W; // WYZW
                            rot.W = rot.Z; // WYZZ
                            rot.Z = rot.Y; // WYYZ
                            rot.Y = temp; // WXYZ

                            scale = part.Shape.Scale * rot;

                            // negative scales don't work in this situation
                            scale.X = Math.Abs(scale.X);
                            scale.Y = Math.Abs(scale.Y);
                            //scale.Z = Math.Abs(scale.Z); // Z unused.

                            // This scaling isn't very accurate and doesn't take into account the face rotation :P
                            int mapdrawstartX = (int)(pos.X - scale.X);
                            int mapdrawstartY = (int)(pos.Y - scale.Y);
                            int mapdrawendX = (int)(pos.X + scale.X);
                            int mapdrawendY = (int)(pos.Y + scale.Y);

                            // If object is beyond the edge of the map, don't draw it to avoid errors
                            if (mapdrawstartX < 0 || mapdrawstartX > 255 || mapdrawendX < 0 || mapdrawendX > 255
                                                  || mapdrawstartY < 0 || mapdrawstartY > 255 || mapdrawendY < 0
                                                  || mapdrawendY > 255)
                                continue;


                            /* * * * * * * * * * * * * * * * * * */
                            // OBB DRAWING PREPARATION PASS
                            /* * * * * * * * * * * * * * * * * * */

                            // Try to get the RGBA of the default texture entry..
                            try
                            {
                                texcolor = part.Shape.Textures.DefaultTexture.RGBA;

                                // Not sure why some of these are null, oh well.

                                int colorr = 255 - (int)(texcolor.R * 255f);
                                int colorg = 255 - (int)(texcolor.G * 255f);
                                int colorb = 255 - (int)(texcolor.B * 255f);

                                if (!(colorr == 255 && colorg == 255 && colorb == 255))
                                {
                                    //Try to set the map spot color
                                    // If the color gets goofy somehow, skip it *shakes fist at Color4
                                    mapdotspot = Color.FromArgb(colorr, colorg, colorb);
                                }
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Windows Array
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                // Mono Array
                            }
                            catch (ArgumentException)
                            {
                                // Color4 fail
                            }

#region obb face reconstruction part duex
                            // Do these in the order that leave the least amount of changes.
                            tScale = part.Shape.Scale;

                            scale = tScale * rot;
                            //vertexes[1] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[1].X = pos.X + scale.X;
                            vertexes[1].Y = pos.Y + scale.Y;
                            vertexes[1].Z = pos.Z + scale.Z;
                            FaceB[0] = vertexes[1];
                            FaceA[1] = vertexes[1];
                            FaceC[4] = vertexes[1];
                            //+X +Y +Z

                            //tScale = new Vector3(part.Shape.Scale.X, -part.Shape.Scale.Y, part.Shape.Scale.Z);
                            tScale.Y = -tScale.Y; // instead of allocating a whole new copy.
                            scale = tScale * rot;
                            vertexes[0].X = pos.X + scale.X;
                            vertexes[0].Y = pos.Y + scale.Y;
                            vertexes[0].Z = pos.Z + scale.Z;
                            FaceA[0] = vertexes[0];
                            FaceB[3] = vertexes[0];
                            FaceA[4] = vertexes[0];
                            // And reverse the above for the next operation.
                            //tScale.Y = -tScale.Y; // or not.
                            //+X -Y +Z

                            //tScale = new Vector3(part.Shape.Scale.X, -part.Shape.Scale.Y, -part.Shape.Scale.Z);
                            //tScale.Y = -tScale.Y;
                            tScale.Z = -tScale.Z;
                            scale = tScale * rot;
                           // vertexes[2] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[2].X = pos.X + scale.X;
                            vertexes[2].Y = pos.Y + scale.Y;
                            vertexes[2].Z = pos.Z + scale.Z;
                            FaceC[0] = vertexes[2];
                            FaceD[3] = vertexes[2];
                            FaceC[5] = vertexes[2];
                            // And reverse the above for the next operation.
                            tScale.Y = -tScale.Y;
                            //tScale.Z = -tScale.Z;
                            //+X +Y -Z

                            //tScale = new Vector3(part.Shape.Scale.X, part.Shape.Scale.Y, -part.Shape.Scale.Z);
                            //tScale.Z = -tScale.Z;
                            scale = tScale * rot;
                            //vertexes[3] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[3].X = pos.X + scale.X;
                            vertexes[3].Y = pos.Y + scale.Y;
                            vertexes[3].Z = pos.Z + scale.Z;
                            FaceD[0] = vertexes[3];
                            FaceC[1] = vertexes[3];
                            FaceA[5] = vertexes[3];
                            // And reverse the above for the next operation.
                            tScale.Z = -tScale.Z;
                            //+X +Y +Z

                            //tScale = new Vector3(-part.Shape.Scale.X, part.Shape.Scale.Y, part.Shape.Scale.Z);
                            tScale.X = -tScale.X;
                            scale = tScale * rot;
                            //vertexes[4] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[4].X = pos.X + scale.X;
                            vertexes[4].Y = pos.Y + scale.Y;
                            vertexes[4].Z = pos.Z + scale.Z;
                            FaceB[1] = vertexes[4];
                            FaceA[2] = vertexes[4];
                            FaceD[4] = vertexes[4];
                            // And reverse the above for the next operation.
                            //tScale.X = -tScale.X;
                            //-X +Y +Z

                            //tScale = new Vector3(-part.Shape.Scale.X, part.Shape.Scale.Y, -part.Shape.Scale.Z);
                            //tScale.X = -tScale.X;
                            tScale.Z = -tScale.Z;
                            scale = tScale * rot;
                            //vertexes[5] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[5].X = pos.X + scale.X;
                            vertexes[5].Y = pos.Y + scale.Y;
                            vertexes[5].Z = pos.Z + scale.Z;
                            FaceD[1] = vertexes[5];
                            FaceC[2] = vertexes[5];
                            FaceB[5] = vertexes[5];
                            // And reverse the above for the next operation.
                            //tScale.X = -tScale.X;
                            tScale.Z = -tScale.Z;
                            //-X +Y +Z

                            //tScale = new Vector3(-part.Shape.Scale.X, -part.Shape.Scale.Y, part.Shape.Scale.Z);
                            //tScale.X = -tScale.X;
                            tScale.Y = -tScale.Y;
                            scale = tScale * rot;
                            //vertexes[6] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[6].X = pos.X + scale.X;
                            vertexes[6].Y = pos.Y + scale.Y;
                            vertexes[6].Z = pos.Z + scale.Z;
                            FaceB[2] = vertexes[6];
                            FaceA[3] = vertexes[6];
                            FaceB[4] = vertexes[6];
                            // And reverse the above for the next operation.
                            //tScale.X = -tScale.X;
                            //tScale.Y = -tScale.Y;
                            //-X -Y +Z

                            //tScale = new Vector3(-part.Shape.Scale.X, -part.Shape.Scale.Y, -part.Shape.Scale.Z);
                            //tScale.X = -tScale.X;
                            //tScale.Y = -tScale.Y;
                            tScale.Z = -tScale.Z;
                            scale = tScale * rot;
                            //vertexes[7] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                            vertexes[7].X = pos.X + scale.X;
                            vertexes[7].Y = pos.Y + scale.Y;
                            vertexes[7].Z = pos.Z + scale.Z;
                            FaceD[2] = vertexes[7];
                            FaceC[3] = vertexes[7];
                            FaceD[5] = vertexes[7];
                            //-X -Y -Z
#endregion

                            ds.brush = new SolidBrush(mapdotspot);

                            ds.trns = new face[FaceA.Length];

                            for (int i = 0; i < FaceA.Length; i++)
                            {
                                project(ref FaceA[i], /*pos,*/ ref workingface.pts[0]);
                                project(ref FaceB[i], /*pos,*/ ref workingface.pts[1]);
                                project(ref FaceD[i], /*pos,*/ ref workingface.pts[2]);
                                project(ref FaceC[i], /*pos,*/ ref workingface.pts[3]);
                                project(ref FaceA[i], /*pos,*/ ref workingface.pts[4]);

                                ds.trns[i] = workingface;
                            }

                            z_sort.Add(part.LocalId, ds);
                            z_localIDs.Add(part.LocalId);
                            z_sortheights.Add(pos.Z);

                        } // loop over group children
                    } // entitybase is sceneobject group
                } // foreach loop over entities

                float[] sortedZHeights = z_sortheights.ToArray();
                uint[] sortedlocalIds = z_localIDs.ToArray();

                // Sort prim by Z position
                Array.Sort(sortedZHeights, sortedlocalIds);

                Graphics g = Graphics.FromImage(mapbmp.Bitmap);

                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                DrawStruct rectDrawStruct;
                for (int s = 0; s < sortedZHeights.Length; s++)
                {
                    if (z_sort.TryGetValue(sortedlocalIds[s], out rectDrawStruct))
                    {
                        for (int r = 0; r < rectDrawStruct.trns.Length; r++ )
                        {
                            g.FillPolygon(rectDrawStruct.brush, rectDrawStruct.trns[r].pts);
                        }
                        //g.FillRectangle(rectDrawStruct.brush , rectDrawStruct.rect);
                    }
                }

                g.Dispose();
            } // lock entities objs

            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Done in " + (Environment.TickCount - tc) + " ms");
            return mapbmp;
        }

        private static Point project(ref Vector3 point3d, /*Vector3 originpos, */ref Point returnpt)
        {
            //Point returnpt;// = new Point();
            //originpos = point3d;
            //int d = (int)(256f / 1.5f);

            //Vector3 topos = new Vector3(0, 0, 0);
           // float z = -point3d.z - topos.z;

            returnpt.X = (int)point3d.X;//(int)((topos.x - point3d.x) / z * d);
            returnpt.Y = 255 - (int)point3d.Y;//(int)(255 - (((topos.y - point3d.y) / z * d)));

            return returnpt;
        }

// TODO: unused:
//         #region Deprecated Maptile Generation.  Adam may update this
//         private Bitmap TerrainToBitmap(string gradientmap)
//         {
//             Bitmap gradientmapLd = new Bitmap(gradientmap);
//
//             int pallete = gradientmapLd.Height;
//
//             Bitmap bmp = new Bitmap(m_scene.Heightmap.Width, m_scene.Heightmap.Height);
//             Color[] colours = new Color[pallete];
//
//             for (int i = 0; i < pallete; i++)
//             {
//                 colours[i] = gradientmapLd.GetPixel(0, i);
//             }
//
//             lock (m_scene.Heightmap)
//             {
//                 ITerrainChannel copy = m_scene.Heightmap;
//                 for (int y = 0; y < copy.Height; y++)
//                 {
//                     for (int x = 0; x < copy.Width; x++)
//                     {
//                         // 512 is the largest possible height before colours clamp
//                         int colorindex = (int) (Math.Max(Math.Min(1.0, copy[x, y] / 512.0), 0.0) * (pallete - 1));
//
//                         // Handle error conditions
//                         if (colorindex > pallete - 1 || colorindex < 0)
//                             bmp.SetPixel(x, copy.Height - y - 1, Color.Red);
//                         else
//                             bmp.SetPixel(x, copy.Height - y - 1, colours[colorindex]);
//                     }
//                 }
//                 ShadeBuildings(bmp);
//                 return bmp;
//             }
//         }
//         #endregion
    }
}
