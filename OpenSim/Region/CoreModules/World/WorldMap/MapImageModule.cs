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
        public SolidBrush brush;
        public face[] trns;
    }

    public struct DrawStruct2
    {
        public SolidBrush brush;
        public Point[] vertices;
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

        private static readonly SolidBrush DefaultBrush = new SolidBrush(Color.Black);
        private static SolidBrush GetFaceBrush(SceneObjectPart part, uint face)
        {
            if (face >= part.Shape.Textures.FaceTextures.Length)
                return DefaultBrush;

            try
            {
                var facetexture = part.Shape.Textures.GetFace(face);

                // TODO: compute a better color from the texture data AND the color applied.

                //Try to set the map spot color
                // If the color gets goofy somehow, skip it *shakes fist at Color4
                return new SolidBrush(Color.FromArgb((int)(facetexture.RGBA.R * 255f), (int)(facetexture.RGBA.G * 255f), (int)(facetexture.RGBA.B * 255f)));
            }
            catch (IndexOutOfRangeException)
            {
                // Windows Array fail
                return DefaultBrush;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Mono Array fail
                return DefaultBrush;
            }
            catch (ArgumentException)
            {
                // Color4 fail
                return DefaultBrush;
            }
        }

        private static DirectBitmap DrawObjectVolume(Scene whichScene, DirectBitmap mapbmp)
        {
            int time_start_temp = Environment.TickCount, time_start = time_start_temp, time_prep = 0, time_filtering = 0, time_vertex_calcs = 0, time_sort_height_calc = 0, time_obb_prep = 0, time_sorting = 0, time_drawing = 0;
            int sop_count = 0, sop_count_filtered = 0;

            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Object Volume Profile");

            float scale_factor = (float)mapbmp.Height / OpenSim.Framework.Constants.RegionSize;

            SceneObjectGroup sog;
            Vector3 pos;
            Quaternion rot;

            Vector3 rotated_radial_scale;
            Vector3 radial_scale;

            DrawStruct2 drawdata;

            var sortheights = new List<float>();
            var drawdata_for_sorting = new List<DrawStruct2>();

            var vertices = new Vector3[8];

            float sort_height;

            var entities = whichScene.GetEntities(); // GetEntities returns a new list of entities, so no threading issues.
            time_prep += Environment.TickCount - time_start_temp;
            foreach (EntityBase obj in entities)
            {
                // Only SOGs till have the needed parts.
                sog = obj as SceneObjectGroup;
                if (sog == null)
                    continue;

                foreach (var part in sog.GetParts())
                {
                    ++sop_count;
                    /* * * * * * * * * * * * * * * * * * */
                    // FILTERING PASS
                    /* * * * * * * * * * * * * * * * * * */
                    time_start_temp = Environment.TickCount;

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

                    if (
                        // skip prim in non-finite position
                        Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) ||
                        Single.IsInfinity(pos.X) || Single.IsInfinity(pos.Y) ||

                        // skip prim outside of region (REVISIT: prims can be outside of region and still overlap into the region.)
                        pos.X < 0f || pos.X >= 256f || pos.Y < 0f || pos.Y >= 256f ||

                        // skip prim Z at or above 256m above the terrain at that position.
                        pos.Z >= (whichScene.Heightmap.GetRawHeightAt((int)pos.X, (int)pos.Y) + 256f)
                    )
                        continue;

                    rot = part.GetWorldRotation();

                    radial_scale = Vector3.Multiply(part.Shape.Scale, 0.5f);

                    time_filtering += Environment.TickCount - time_start_temp;

                    ++sop_count_filtered;

                    /* * * * * * * * * * * * * * * * * * */
                    // OBB VERTEX COMPUTATION
                    /* * * * * * * * * * * * * * * * * * */
                    time_start_temp = Environment.TickCount;
                    /*
                    Vertex pattern:
                    # XYZ
                    0 --+
                    1 +-+
                    2 +++
                    3 -++
                    4 ---
                    5 +--
                    6 ++-
                    7 -+-
                    */
                    rotated_radial_scale.X = -radial_scale.X;
                    rotated_radial_scale.Y = -radial_scale.Y;
                    rotated_radial_scale.Z = radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[0].X = pos.X + rotated_radial_scale.X;
                    vertices[0].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[0].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = radial_scale.X;
                    rotated_radial_scale.Y = -radial_scale.Y;
                    rotated_radial_scale.Z = radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[1].X = pos.X + rotated_radial_scale.X;
                    vertices[1].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[1].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = radial_scale.X;
                    rotated_radial_scale.Y = radial_scale.Y;
                    rotated_radial_scale.Z = radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[2].X = pos.X + rotated_radial_scale.X;
                    vertices[2].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[2].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = -radial_scale.X;
                    rotated_radial_scale.Y = radial_scale.Y;
                    rotated_radial_scale.Z = radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[3].X = pos.X + rotated_radial_scale.X;
                    vertices[3].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[3].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = -radial_scale.X;
                    rotated_radial_scale.Y = -radial_scale.Y;
                    rotated_radial_scale.Z = -radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[4].X = pos.X + rotated_radial_scale.X;
                    vertices[4].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[4].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = radial_scale.X;
                    rotated_radial_scale.Y = -radial_scale.Y;
                    rotated_radial_scale.Z = -radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[5].X = pos.X + rotated_radial_scale.X;
                    vertices[5].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[5].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = radial_scale.X;
                    rotated_radial_scale.Y = radial_scale.Y;
                    rotated_radial_scale.Z = -radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[6].X = pos.X + rotated_radial_scale.X;
                    vertices[6].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[6].Z = pos.Z + rotated_radial_scale.Z;

                    rotated_radial_scale.X = -radial_scale.X;
                    rotated_radial_scale.Y = radial_scale.Y;
                    rotated_radial_scale.Z = -radial_scale.Z;
                    rotated_radial_scale *= rot;
                    vertices[7].X = pos.X + rotated_radial_scale.X;
                    vertices[7].Y = pos.Y + rotated_radial_scale.Y;
                    vertices[7].Z = pos.Z + rotated_radial_scale.Z;

                    time_vertex_calcs += Environment.TickCount - time_start_temp;


                    /* * * * * * * * * * * * * * * * * * */
                    // SORT HEIGHT CALC
                    /* * * * * * * * * * * * * * * * * * */
                    time_start_temp = Environment.TickCount;

                    // Sort faces by AABB top height, which by nature will always be the maximum Z value of all upwards facing face vertices, but is also simply the highest vertex.
                    sort_height =
                        Math.Max(vertices[0].Z,
                            Math.Max(vertices[1].Z,
                                Math.Max(vertices[2].Z,
                                    Math.Max(vertices[3].Z,
                                        Math.Max(vertices[4].Z,
                                            Math.Max(vertices[5].Z,
                                                Math.Max(vertices[6].Z,
                                                    vertices[7].Z
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ;

                    time_sort_height_calc += Environment.TickCount - time_start_temp;


                    /* * * * * * * * * * * * * * * * * * */
                    // OBB DRAWING PREPARATION PASS
                    /* * * * * * * * * * * * * * * * * * */
                    time_start_temp = Environment.TickCount;

                    // Compute face 0 of OBB and add if facing up.
                    if (Vector3.Cross(Vector3.Subtract(vertices[1], vertices[0]), Vector3.Subtract(vertices[3], vertices[0])).Z > 0)
                    {
                        drawdata.brush = GetFaceBrush(part, 0);

                        drawdata.vertices = new Point[4];
                        drawdata.vertices[0].X = (int)(vertices[0].X * scale_factor);
                        drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

                        drawdata.vertices[1].X = (int)(vertices[1].X * scale_factor);
                        drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

                        drawdata.vertices[2].X = (int)(vertices[2].X * scale_factor);
                        drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

                        drawdata.vertices[3].X = (int)(vertices[3].X * scale_factor);
                        drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

                        drawdata_for_sorting.Add(drawdata);
                        sortheights.Add(sort_height);
                    }

                    // Compute face 1 of OBB and add if facing up.
                    if (Vector3.Cross(Vector3.Subtract(vertices[5], vertices[4]), Vector3.Subtract(vertices[0], vertices[4])).Z > 0)
                    {
                        drawdata.brush = GetFaceBrush(part, 1);

                        drawdata.vertices = new Point[4];
                        drawdata.vertices[0].X = (int)(vertices[4].X * scale_factor);
                        drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

                        drawdata.vertices[1].X = (int)(vertices[5].X * scale_factor);
                        drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

                        drawdata.vertices[2].X = (int)(vertices[1].X * scale_factor);
                        drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

                        drawdata.vertices[3].X = (int)(vertices[0].X * scale_factor);
                        drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

                        drawdata_for_sorting.Add(drawdata);
                        sortheights.Add(sort_height);
                    }

                    // Compute face 2 of OBB and add if facing up.
                    if (Vector3.Cross(Vector3.Subtract(vertices[6], vertices[5]), Vector3.Subtract(vertices[1], vertices[5])).Z > 0)
                    {
                        drawdata.brush = GetFaceBrush(part, 2);

                        drawdata.vertices = new Point[4];
                        drawdata.vertices[0].X = (int)(vertices[5].X * scale_factor);
                        drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

                        drawdata.vertices[1].X = (int)(vertices[6].X * scale_factor);
                        drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

                        drawdata.vertices[2].X = (int)(vertices[2].X * scale_factor);
                        drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

                        drawdata.vertices[3].X = (int)(vertices[1].X * scale_factor);
                        drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

                        drawdata_for_sorting.Add(drawdata);
                        sortheights.Add(sort_height);
                    }

                    // Compute face 3 of OBB and add if facing up.
                    if (Vector3.Cross(Vector3.Subtract(vertices[7], vertices[6]), Vector3.Subtract(vertices[2], vertices[6])).Z > 0)
                    {
                        drawdata.brush = GetFaceBrush(part, 3);

                        drawdata.vertices = new Point[4];
                        drawdata.vertices[0].X = (int)(vertices[6].X * scale_factor);
                        drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

                        drawdata.vertices[1].X = (int)(vertices[7].X * scale_factor);
                        drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

                        drawdata.vertices[2].X = (int)(vertices[3].X * scale_factor);
                        drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

                        drawdata.vertices[3].X = (int)(vertices[2].X * scale_factor);
                        drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

                        drawdata_for_sorting.Add(drawdata);
                        sortheights.Add(sort_height);
                    }

                    // Compute face 4 of OBB and add if facing up.
                    if (Vector3.Cross(Vector3.Subtract(vertices[4], vertices[7]), Vector3.Subtract(vertices[3], vertices[7])).Z > 0)
                    {
                        drawdata.brush = GetFaceBrush(part, 4);

                        drawdata.vertices = new Point[4];
                        drawdata.vertices[0].X = (int)(vertices[7].X * scale_factor);
                        drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

                        drawdata.vertices[1].X = (int)(vertices[4].X * scale_factor);
                        drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

                        drawdata.vertices[2].X = (int)(vertices[0].X * scale_factor);
                        drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

                        drawdata.vertices[3].X = (int)(vertices[3].X * scale_factor);
                        drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

                        drawdata_for_sorting.Add(drawdata);
                        sortheights.Add(sort_height);
                    }

                    // Compute face 5 of OBB and add if facing up.
                    if (Vector3.Cross(Vector3.Subtract(vertices[6], vertices[7]), Vector3.Subtract(vertices[4], vertices[7])).Z > 0)
                    {
                        drawdata.brush = GetFaceBrush(part, 5);

                        drawdata.vertices = new Point[4];
                        drawdata.vertices[0].X = (int)(vertices[7].X * scale_factor);
                        drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

                        drawdata.vertices[1].X = (int)(vertices[6].X * scale_factor);
                        drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

                        drawdata.vertices[2].X = (int)(vertices[5].X * scale_factor);
                        drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

                        drawdata.vertices[3].X = (int)(vertices[4].X * scale_factor);
                        drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

                        drawdata_for_sorting.Add(drawdata);
                        sortheights.Add(sort_height);
                    }

                    time_obb_prep += Environment.TickCount - time_start_temp;
                }
            }

            time_start_temp = Environment.TickCount;
            // TODO: keep all this in a combined list and sort there
            float[] sorted_z_heights = sortheights.ToArray();
            DrawStruct2[] sorted_drawdata = drawdata_for_sorting.ToArray();

            // Sort prim by Z position
            Array.Sort(sorted_z_heights, sorted_drawdata);

            time_sorting = Environment.TickCount - time_start_temp;

            time_start_temp = Environment.TickCount;
            using (Graphics g = Graphics.FromImage(mapbmp.Bitmap))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                for (int s = 0; s < sorted_z_heights.Length; s++)
                {
                    g.FillPolygon(sorted_drawdata[s].brush, sorted_drawdata[s].vertices);
                }
            }
            time_drawing = Environment.TickCount - time_start_temp;

            m_log.InfoFormat("[MAPTILE]: Generating Maptile Step 2 (Objects): Processed {0} entities, {1} prims, {2} used for map drawing, resulting in {3} faces to draw.",
                entities.Count, sop_count, sop_count_filtered, sorted_z_heights.Length
            );
            m_log.InfoFormat("[MAPTILE]: Generating Maptile Step 2 (Objects): Timing: " +
                "prepping took {0}ms, " +
                "filtering prims took {1}ms, " +
                "calculating vertices took {2}ms, " +
                "computing sorting height took {3}ms, " +
                "calculating OBBs took {4}ms, " +
                "sorting took {5}ms, " +
                "drawing took {6}ms, " +
                "total time: {7}ms",
                time_prep, time_filtering, time_vertex_calcs, time_sort_height_calc, time_obb_prep, time_sorting, time_drawing,
                Environment.TickCount - time_start
            );

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
