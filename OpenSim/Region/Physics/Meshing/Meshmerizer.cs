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
//#define SPAM

using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using PrimMesher;
using log4net;
using System.Reflection;
using System.IO;
using System.IO.Compression;

namespace OpenSim.Region.Physics.Meshing
{
    public class MeshmerizerPlugin : IMeshingPlugin
    {
        public MeshmerizerPlugin()
        {
        }

        public string GetName()
        {
            return "Meshmerizer";
        }

        public IMesher GetMesher()
        {
            return new Meshmerizer();
        }
    }

    public class Meshmerizer : IMesher
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done
#if SPAM
        const string baseDir = "rawFiles";
#else
        private const string baseDir = null; //"rawFiles";
#endif

        IAssetCache _assetCache;

        public ulong TotalProcessedPrims { get; set; }
        public ulong TotalProcessedVerts { get; set; }

        public Meshmerizer()
        {
            _assetCache = ProviderRegistry.Instance.Get<IAssetCache>();
        }

        /// <summary>
        /// creates a simple box mesh of the specified size. This mesh is of very low vertex count and may
        /// be useful as a backup proxy when level of detail is not needed or when more complex meshes fail
        /// for some reason
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <param name="minZ"></param>
        /// <param name="maxZ"></param>
        /// <returns></returns>
        private Mesh CreateSimpleBoxMesh(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            Mesh box = new Mesh(0, 8, 12);

            // bottom
            box.AddMeshVertex(minX, maxY, minZ);
            box.AddMeshVertex(maxX, maxY, minZ);
            box.AddMeshVertex(maxX, minY, minZ);
            box.AddMeshVertex(minX, minY, minZ);

            box.Add(0, 1, 2);
            box.Add(0, 2, 3);

            // top

            box.AddMeshVertex(maxX, maxY, maxZ);
            box.AddMeshVertex(minX, maxY, maxZ);
            box.AddMeshVertex(minX, minY, maxZ);
            box.AddMeshVertex(maxX, minY, maxZ);

            box.Add(4, 5, 6);
            box.Add(4, 6, 7);

            // sides
            box.Add(5, 0, 3);
            box.Add(5, 3, 6);

            box.Add(1, 0, 5);
            box.Add(1, 5, 4);

            box.Add(7, 1, 4);
            box.Add(7, 2, 1);

            box.Add(3, 2, 7);
            box.Add(3, 7, 6);

            return box;
        }


        /// <summary>
        /// Creates a simple bounding box mesh for a complex input mesh
        /// </summary>
        /// <param name="meshIn"></param>
        /// <returns></returns>
        private Mesh CreateBoundingBoxMesh(Mesh meshIn)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (Vector3 v in meshIn.getVertexList())
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;

                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            return CreateSimpleBoxMesh(minX, maxX, minY, maxY, minZ, maxZ);
        }

        private void ReportPrimError(string message, string primName, PrimMesh primMesh)
        {
            m_log.Error(message);
            m_log.Error("\nPrim Name: " + primName);
            m_log.Error("****** PrimMesh Parameters ******\n" + primMesh.ParamsToDisplayString());

        }

        public MeshingResult CreateMeshFromPrimMesher(string primName, PrimitiveBaseShape primShape, OpenMetaverse.Vector3 size, float lod, ShapeType desiredShape, bool preScale)
        {
            Vector3 szvec = new Vector3(size.X, size.Y, size.Z);
            ulong meshKey = primShape.GetMeshKey(szvec, lod);

            try
            {
                if (primShape.SculptEntry && ((primShape.SculptData == null) || (primShape.SculptData.Length == 0)))
                {
                    //preload the sculpt/mesh data
                    AssetBase asset = null;
                    Util.ReportIfSlow("[MESHER]: GetAsset for '"+primName+"'", 500, () =>
                        {
                            asset = _assetCache.GetAsset(primShape.SculptTexture, AssetRequestInfo.InternalRequest());
                        });

                    if (asset == null)
                    {
                        return null;
                    }

                    primShape.SculptData = asset.Data;
                }

                if (primShape.SculptEntry == false ||
                    (primShape.SculptEntry == true && (SculptType)primShape.SculptType != SculptType.Mesh))
                {
                    MeshingResult result = null;
                    Util.ReportIfSlow("[MESHER]: From prim" + primName + "'", 1000, () =>
                        {
                            result = ExtractTrimeshFromPrimOrSculpt(primName, primShape, ref size, lod, preScale, meshKey);
                        });
                    return result;
                }
                else //mesh
                {
                    MeshingResult result = null;
                    Util.ReportIfSlow("[MESHER]: From mesh" + primName + "'", 1000, () =>
                        {
                            result = ExtractMeshingResultFromMesh(primName, primShape, ref size, lod, preScale, desiredShape, meshKey);
                        });
                    return result;
                }
            }
            finally
            {
                //we dont need the sculpt data around anymore
                primShape.SculptData = null;
            }
        }

        private MeshingResult ExtractMeshingResultFromMesh(string primName, PrimitiveBaseShape primShape, ref Vector3 size, float lod, bool preScale, 
            ShapeType desiredShape, ulong meshKey)
        {
            long start = 0;
            OSDMap map = TryExtractOSDMapFromLLSDBinary(ref start, primShape.SculptData);

            if (map == null) return null;

            //m_log.DebugFormat("Mesh: {0}", map.ToString());

            IMesh singleMesh = TryExtractSingleMesh(primName, primShape, meshKey, ref size, desiredShape, start, map);
            if (singleMesh != null)
            {
                return new MeshingResult { TriMesh = singleMesh, ResultType = ShapeType.TriMesh };
            }
            else
            {
                if (desiredShape == ShapeType.DecomposedConvexHulls)
                {
                    List<List<Vector3>> convexSet = TryExtractConvexSet(primName, primShape, meshKey, ref size, desiredShape, start, map);

                    if (convexSet != null)
                    {
                        return new MeshingResult { ConvexVerts = convexSet, ResultType = ShapeType.DecomposedConvexHulls };
                    }
                }
                else
                {
                    List<Vector3> convex = TryExtractSingleConvex(primName, primShape, meshKey, ref size, desiredShape, start, map);

                    if (convex != null)
                    {
                        return new MeshingResult { SingleConvex = convex, ResultType = ShapeType.SingleConvex };
                    }
                }
            }

            return null;
        }

        private List<Vector3> TryExtractSingleConvex(string primName, PrimitiveBaseShape primShape, ulong meshKey, ref Vector3 size, ShapeType desiredShape, long start, OSDMap map)
        {
            if (!map.ContainsKey("physics_convex"))
                return null;

            OSDMap convexMap = (OSDMap)map["physics_convex"];

            //retrieve the number of hulls and number of verts in each

            int physOffset = convexMap["offset"].AsInteger() + (int)start;
            int physSize = convexMap["size"].AsInteger();

            if (physOffset < 0 || physSize == 0)
                return null; // no mesh data in asset

            OSD decodedConvexOSD = DecodeCompressedOSD(primShape.SculptData, physOffset, physSize);
            if (decodedConvexOSD == null) return null;

            OSDMap decodedConvexMap = decodedConvexOSD as OSDMap;
            if (decodedConvexMap == null) return null;

            if (!decodedConvexMap.ContainsKey("BoundingVerts"))
                return null;

            OpenMetaverse.Vector3 posMax = new Vector3(0.5f, 0.5f, 0.5f);
            OpenMetaverse.Vector3 posMin = new Vector3(-0.5f, -0.5f, -0.5f);

            if (decodedConvexMap.ContainsKey("Max"))
            {
                posMax = decodedConvexMap["Max"].AsVector3();
            }

            if (decodedConvexMap.ContainsKey("Min"))
            {
                posMin = decodedConvexMap["Min"].AsVector3();
            }

            byte[] vertBytes = decodedConvexMap["BoundingVerts"];

            List<Coord> thisHull = new List<Coord>();
            ExtractCoordsFrom16BitPositions(size, thisHull, ref posMax, ref posMin, vertBytes, 0, int.MaxValue);
            
            List<Vector3> vecList = new List<Vector3>();
            foreach (Coord coord in thisHull)
            {
                vecList.Add(new Vector3(coord.X, coord.Y, coord.Z));
            }

            return vecList;
        }

        private List<List<Vector3>> TryExtractConvexSet(string primName, PrimitiveBaseShape primShape, ulong meshKey, ref Vector3 size, ShapeType desiredShape, 
            long start, OSDMap map)
        {
            if (! map.ContainsKey("physics_convex"))
                return null;

            OSDMap convexMap = (OSDMap)map["physics_convex"];

            //retrieve the number of hulls and number of verts in each

            int physOffset = convexMap["offset"].AsInteger() + (int)start;
            int physSize = convexMap["size"].AsInteger();

            if (physOffset < 0 || physSize == 0)
                return null; // no mesh data in asset

            OSD decodedConvexOSD = DecodeCompressedOSD(primShape.SculptData, physOffset, physSize);
            if (decodedConvexOSD == null) return null;

            OSDMap decodedConvexMap = decodedConvexOSD as OSDMap;
            if (decodedConvexMap == null) return null;

            if (! decodedConvexMap.ContainsKey("HullList"))
                return null;

            byte[] hullCounts = decodedConvexMap["HullList"].AsBinary();
            OpenMetaverse.Vector3 posMax = new Vector3(0.5f, 0.5f, 0.5f);
            OpenMetaverse.Vector3 posMin = new Vector3(-0.5f, -0.5f, -0.5f);

            if (decodedConvexMap.ContainsKey("Max"))
            {
                posMax = decodedConvexMap["Max"].AsVector3();
            }

            if (decodedConvexMap.ContainsKey("Min"))
            {
                posMin = decodedConvexMap["Min"].AsVector3();
            }

            byte[] vertBytes = decodedConvexMap["Positions"];

            List<List<Vector3>> retHulls = new List<List<Vector3>>();
            int vertsExtracted = 0;
            List<Coord> thisHull = new List<Coord>();
            foreach (byte count in hullCounts)
            {
                ExtractCoordsFrom16BitPositions(size, thisHull, ref posMax, ref posMin, vertBytes, vertsExtracted, (int)count);
                vertsExtracted += count;

                List<Vector3> vecList = new List<Vector3>();
                foreach (Coord coord in thisHull)
                {
                    vecList.Add(new Vector3(coord.X, coord.Y, coord.Z));
                }

                retHulls.Add(vecList);
            }

            TotalProcessedVerts += (ulong)vertsExtracted;

            return retHulls;
        }

        private MeshingResult ExtractTrimeshFromPrimOrSculpt(string primName, PrimitiveBaseShape primShape, ref OpenMetaverse.Vector3 size, float lod, bool preScale, ulong meshKey)
        {
            List<Coord> coords;
            List<Face> faces;

            if (primShape.SculptEntry)
            {
                //standard sculpt
                if (!DecodeSculptMesh(primName, primShape, ref size, lod, preScale, out coords, out faces))
                {
                    return null;
                }
            }
            else
            {
                if (!GeneratePrimTrimesh(primName, primShape, ref size, lod, preScale, out coords, out faces))
                {
                    return null;
                }
            }

            Mesh mesh = BuildMeshFromCoordsAndFaces(meshKey, coords, faces);

            TotalProcessedVerts += (ulong)mesh.VertCount;

            MeshingResult result = new MeshingResult { ResultType = ShapeType.TriMesh, TriMesh = mesh };
            return result;
        }

        private static Mesh BuildMeshFromCoordsAndFaces(ulong meshKey, List<Coord> coords, List<Face> faces)
        {
            Mesh mesh = new Mesh(meshKey, coords.Count, faces.Count);

            for (int i = 0; i < coords.Count; i++)
            {
                Coord c = coords[i];
                mesh.AddMeshVertex(c.X, c.Y, c.Z);
            }

            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];
                mesh.Add(f.v1, f.v2, f.v3);
            }

            return mesh;
        }

        private bool GeneratePrimTrimesh(string primName, PrimitiveBaseShape primShape, ref OpenMetaverse.Vector3 size, float lod, bool preScale, 
            out List<Coord> coords, out List<Face> faces)
        {
            PrimMesh primMesh;

            float pathShearX = Primitive.UnpackPathShear((sbyte)primShape.PathShearX);
            float pathShearY = Primitive.UnpackPathShear((sbyte)primShape.PathShearY);
            float pathBegin = (float)primShape.PathBegin * 2.0e-5f;
            float pathEnd = 1.0f - (float)primShape.PathEnd * 2.0e-5f;
            float pathScaleX = (float)(primShape.PathScaleX - 100) * 0.01f;
            float pathScaleY = (float)(primShape.PathScaleY - 100) * 0.01f;

            float profileBegin = (float)primShape.ProfileBegin * 2.0e-5f;
            float profileEnd = 1.0f - (float)primShape.ProfileEnd * 2.0e-5f;
            float profileHollow = (float)primShape.ProfileHollow * 2.0e-5f;
            if (profileHollow > 0.95f)
                profileHollow = 0.95f;

            int sides = 4;
            LevelOfDetail iLOD = (LevelOfDetail)lod;
            if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
                sides = 3;
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
                switch (iLOD)
                {
                    case LevelOfDetail.High: sides = 24; break;
                    case LevelOfDetail.Medium: sides = 12; break;
                    case LevelOfDetail.Low: sides = 6; break;
                    case LevelOfDetail.VeryLow: sides = 3; break;
                    default: sides = 24; break;
                }
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            { // half circle, prim is a sphere
                switch (iLOD)
                {
                    case LevelOfDetail.High: sides = 24; break;
                    case LevelOfDetail.Medium: sides = 12; break;
                    case LevelOfDetail.Low: sides = 6; break;
                    case LevelOfDetail.VeryLow: sides = 3; break;
                    default: sides = 24; break;
                }

                profileBegin = 0.5f * profileBegin + 0.5f;
                profileEnd = 0.5f * profileEnd + 0.5f;
            }

            int hollowSides = sides;
            if (primShape.HollowShape == HollowShape.Circle)
            {
                switch (iLOD)
                {
                    case LevelOfDetail.High: hollowSides = 24; break;
                    case LevelOfDetail.Medium: hollowSides = 12; break;
                    case LevelOfDetail.Low: hollowSides = 6; break;
                    case LevelOfDetail.VeryLow: hollowSides = 3; break;
                    default: hollowSides = 24; break;
                }
            }
            else if (primShape.HollowShape == HollowShape.Square)
                hollowSides = 4;
            else if (primShape.HollowShape == HollowShape.Triangle)
                hollowSides = 3;

            primMesh = new PrimMesh(sides, profileBegin, profileEnd, profileHollow, hollowSides);

            if (primMesh.errorMessage != null)
            if (!String.IsNullOrEmpty(primMesh.errorMessage))
                    m_log.Error("[ERROR] " + primMesh.errorMessage);

            primMesh.topShearX = pathShearX;
            primMesh.topShearY = pathShearY;
            primMesh.pathCutBegin = pathBegin;
            primMesh.pathCutEnd = pathEnd;

            if (primShape.PathCurve == (byte)Extrusion.Straight || primShape.PathCurve == (byte)Extrusion.Flexible)
            {
                primMesh.twistBegin = primShape.PathTwistBegin * 18 / 10;
                primMesh.twistEnd = primShape.PathTwist * 18 / 10;
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    ReportPrimError("*** CORRUPT PRIM!! ***", primName, primMesh);
                    if (profileBegin < 0.0f) profileBegin = 0.0f;
                    if (profileEnd > 1.0f) profileEnd = 1.0f;
                }

                //m_log.Debug("****** PrimMesh Parameters (Linear) ******\n" + primMesh.ParamsToDisplayString());

                try
                {
                    primMesh.ExtrudeLinear();
                }
                catch (Exception ex)
                {
                    ReportPrimError("Extrusion failure: exception: " + ex.ToString(), primName, primMesh);
                    coords = null;
                    faces = null;
                    return false;
                }
            }
            else
            {
                primMesh.holeSizeX = (200 - primShape.PathScaleX) * 0.01f;
                primMesh.holeSizeY = (200 - primShape.PathScaleY) * 0.01f;
                primMesh.radius = 0.01f * primShape.PathRadiusOffset;
                primMesh.revolutions = 1.0f + 0.015f * primShape.PathRevolutions;
                primMesh.skew = 0.01f * primShape.PathSkew;
                primMesh.twistBegin = primShape.PathTwistBegin * 36 / 10;
                primMesh.twistEnd = primShape.PathTwist * 36 / 10;
                primMesh.taperX = primShape.PathTaperX * 0.01f;
                primMesh.taperY = primShape.PathTaperY * 0.01f;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    ReportPrimError("*** CORRUPT PRIM!! ***", primName, primMesh);
                    if (profileBegin < 0.0f) profileBegin = 0.0f;
                    if (profileEnd > 1.0f) profileEnd = 1.0f;
                }

                //m_log.Debug("****** PrimMesh Parameters (Circular) ******\n" + primMesh.ParamsToDisplayString());

                try
                {
                    primMesh.ExtrudeCircular();
                }
                catch (Exception ex)
                {
                    ReportPrimError("Extrusion failure: exception: " + ex.ToString(), primName, primMesh);
                    coords = null;
                    faces = null;
                    return false;
                }
            }

            primMesh.DumpRaw(baseDir, primName, "primMesh");

            if (preScale) primMesh.Scale(size.X, size.Y, size.Z);

            coords = primMesh.coords;
            faces = primMesh.faces;

            return true;
        }

        private static OSDMap TryExtractOSDMapFromLLSDBinary(ref long start, byte[] bytes)
        {
            using (MemoryStream data = new MemoryStream(bytes))
            {
                try
                {
                    OSD osd = OSDParser.DeserializeLLSDBinary(data);
                    if (osd is OSDMap)
                    {
                        start = data.Position;
                        return (OSDMap)osd;
                    }
                    else
                    {
                        m_log.Warn("[MESH]: unable to cast mesh asset to OSDMap");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[MESH]: Exception deserializing mesh asset header: {0}", e);
                    return null;
                }   
            }
        }

        private IMesh TryExtractSingleMesh(string primName, PrimitiveBaseShape primShape, ulong meshKey, ref Vector3 size, ShapeType desiredShape, long start, OSDMap map)
        {
            OSDMap physicsParms = null;

            //if the caller has supplied a mesh, we will use that
            if (map.ContainsKey("physics_shape"))
            {
                physicsParms = (OSDMap)map["physics_shape"]; // old asset format
            }
            else if (map.ContainsKey("physics_mesh"))
            {
                physicsParms = (OSDMap)map["physics_mesh"]; // new asset format
            }
            else if (desiredShape == ShapeType.TriMesh)
            {
                //elif the caller wants a trimesh, we use the high LOD visible mesh directly
                //and send that physics
                if (map.ContainsKey("high_lod"))
                    physicsParms = (OSDMap)map["high_lod"];
            }

            //nothing available?
            if (physicsParms == null)
            {
                return null;
            }

            return (ExtractSingleMeshFromMap(physicsParms, primShape, meshKey, ref size, start));
        }

        private IMesh ExtractSingleMeshFromMap(OSDMap map, PrimitiveBaseShape primShape, ulong meshKey, ref Vector3 size, long start)
        {
            OSDArray decodedMeshOsdArray = DecodeMeshLODArray(primShape, start, map);

            if (decodedMeshOsdArray == null)
                return null;

            //Console.WriteLine("decodedMeshOsd for {0} - {1}", primName, Util.GetFormattedXml((OSD)decodedMeshOsdArray));

            List<Coord> coords = new List<Coord>();
            List<Face> faces = new List<Face>();
            foreach (OSD subMeshOsd in decodedMeshOsdArray)
            {
                if (subMeshOsd is OSDMap)
                    AddSubMesh(subMeshOsd as OSDMap, size, coords, faces);
            }

            return BuildMeshFromCoordsAndFaces(meshKey, coords, faces);
        }

        private static OSDArray DecodeMeshLODArray(PrimitiveBaseShape primShape, long start, OSDMap physicsParms)
        {
            int physOffset = physicsParms["offset"].AsInteger() + (int)start;
            int physSize = physicsParms["size"].AsInteger();

            if (physOffset < 0 || physSize == 0)
                return null; // no mesh data in asset

            OSD decodedMeshOsd = DecodeCompressedOSD(primShape.SculptData, physOffset, physSize);
            if (decodedMeshOsd == null) return null;

            return decodedMeshOsd as OSDArray;
        }

        private static OSD DecodeCompressedOSD(byte[] data, int offset, int size)
        {
            try
            {
                using (MemoryStream inMs = new MemoryStream(data, offset, size))
                {
                    /*
                        * Skipping past the first two bytes, which are part of the zlib specification 
                        * (RFC 1950), not the deflate specification (RFC 1951). Those bytes contain 
                        * information about the compression method and flags.
                        */
                    int streamType = inMs.ReadByte();
                    int streamFlags = inMs.ReadByte();

                    using (MemoryStream outMs = new MemoryStream())
                    {
                        using (DeflateStream zOut = new DeflateStream(inMs, CompressionMode.Decompress))
                        {
                            zOut.CopyTo(outMs);
                            byte[] decompressedBuf = outMs.ToArray();
                            return OSDParser.DeserializeLLSDBinary(decompressedBuf) as OSDArray;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MESH]: exception decoding OSD: {0}", e);
                return null;
            }
        }

        /// <summary>
        /// Add a submesh to an existing list of coords and faces.
        /// </summary>
        /// <param name="subMeshData"></param>
        /// <param name="size">Size of entire object</param>
        /// <param name="coords"></param>
        /// <param name="faces"></param>
        private void AddSubMesh(OSDMap subMeshData, Vector3 size, List<Coord> coords, List<Face> faces)
        {
            //                                    Console.WriteLine("subMeshMap for {0} - {1}", primName, Util.GetFormattedXml((OSD)subMeshMap));

            // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
            // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
            // geometry for this submesh.
            if (subMeshData.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshData["NoGeometry"]))
                return;

            OpenMetaverse.Vector3 posMax = ((OSDMap)subMeshData["PositionDomain"])["Max"].AsVector3();
            OpenMetaverse.Vector3 posMin = ((OSDMap)subMeshData["PositionDomain"])["Min"].AsVector3();
            ushort faceIndexOffset = (ushort)coords.Count;

            byte[] posBytes = subMeshData["Position"].AsBinary();
            ExtractCoordsFrom16BitPositions(size, coords, ref posMax, ref posMin, posBytes, 0, int.MaxValue);

            byte[] triangleBytes = subMeshData["TriangleList"].AsBinary();
            for (int i = 0; i < triangleBytes.Length; i += 6)
            {
                ushort v1 = (ushort)(Utils.BytesToUInt16(triangleBytes, i) + faceIndexOffset);
                ushort v2 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 2) + faceIndexOffset);
                ushort v3 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 4) + faceIndexOffset);
                Face f = new Face(v1, v2, v3);
                faces.Add(f);
            }
        }

        private static void ExtractCoordsFrom16BitPositions(Vector3 size, List<Coord> coords, ref OpenMetaverse.Vector3 posMax, 
            ref OpenMetaverse.Vector3 posMin, byte[] posBytes, int startAt, int extractCount)
        {
            startAt *= 6;
            for (int i = startAt, j = 0; i < posBytes.Length && j < extractCount; i += 6, j++)
            {
                ushort uX = Utils.BytesToUInt16(posBytes, i);
                ushort uY = Utils.BytesToUInt16(posBytes, i + 2);
                ushort uZ = Utils.BytesToUInt16(posBytes, i + 4);

                Coord c = new Coord(
                Utils.UInt16ToFloat(uX, posMin.X, posMax.X) * size.X,
                Utils.UInt16ToFloat(uY, posMin.Y, posMax.Y) * size.Y,
                Utils.UInt16ToFloat(uZ, posMin.Z, posMax.Z) * size.Z,
                false);

                coords.Add(c);
            }
        }

        private bool DecodeSculptMesh(string primName, PrimitiveBaseShape primShape, ref OpenMetaverse.Vector3 size, float lod, bool preScale, 
            out List<Coord> coords, out List<Face> faces)
        {
            Image idata = null;
            PrimMesher.SculptMesh sculptMesh;

            coords = null;
            faces = null;

            try
            {
                idata = CSJ2K.J2kImage.FromBytes(primShape.SculptData);
            }
            catch (DllNotFoundException)
            {
                m_log.Error("[PHYSICS]: OpenJpeg is not installed correctly on this system. Physics Proxy generation failed.  Often times this is because of an old version of GLIBC.  You must have version 2.4 or above!");
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                m_log.Error("[PHYSICS]: OpenJpeg was unable to decode this.   Physics Proxy generation failed");
                return false;
            }
            catch (Exception)
            {
                m_log.Error("[PHYSICS]: Unable to generate a Sculpty physics proxy.  Sculpty texture decode failed!");
                return false;
            }

            if (idata == null)
            {
                m_log.Error("[PHYSICS]: Unable to generate a Sculpty physics proxy.  Sculpty texture decode failed!");
                return false;
            }

            bool mirror = ((primShape.SculptType & 128) != 0);
            bool invert = ((primShape.SculptType & 64) != 0);

            sculptMesh = new PrimMesher.SculptMesh((Bitmap)idata, (SculptMesh.SculptType)(primShape.SculptType & 0xF), (int)lod, false, mirror, invert);

            idata.Dispose();

            sculptMesh.DumpRaw(baseDir, primName, "primMesh");

            if (preScale) sculptMesh.Scale(size.X, size.Y, size.Z);

            coords = sculptMesh.coords;
            faces = sculptMesh.faces;

            return true;
        }

        public MeshingResult CreateMesh(String primName, PrimitiveBaseShape primShape, OpenMetaverse.Vector3 size, float lod, ShapeType desiredType, bool preScale)
        {
            if (preScale == false)
            {
                size = new Vector3(1f, 1f, 1f);
            }
            else
            {
                if (size.X < 0.01f) size.X = 0.01f;
                if (size.Y < 0.01f) size.Y = 0.01f;
                if (size.Z < 0.01f) size.Z = 0.01f;
            }

            TotalProcessedPrims++;

            return CreateMeshFromPrimMesher(primName, primShape, size, lod, desiredType, preScale);
        }
    }
}
