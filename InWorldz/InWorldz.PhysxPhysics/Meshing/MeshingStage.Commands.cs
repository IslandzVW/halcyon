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
using System.IO;
using OpenSim.Region.Physics.Manager;

namespace InWorldz.PhysxPhysics.Meshing
{
    internal partial class MeshingStage
    {
        /// <summary>
        /// Command to retrieve a mesh from the meshing stage
        /// </summary>
        internal struct MeshingQueueItem : IMeshingQueueItem
        {
            public string PrimName;
            public OpenSim.Framework.PrimitiveBaseShape Shape;
            public OpenMetaverse.Vector3 Size;
            public float LOD;
            public MeshingCompleteDelegate CompletedDelegate;
            public bool IsDynamic;
            public byte[] SerializedShapes;
            public bool FromCrossing;

            public void Execute(MeshingStage meshingStage)
            {
                //short circuit null shapes
                if (Shape.PreferredPhysicsShape == OpenMetaverse.PhysicsShapeType.None)
                {
                    this.CompletedDelegate(PhysicsShape.Null);
                    return;
                }

                ulong meshHash = Shape.GetMeshKey(Size, MeshingStage.SCULPT_MESH_LOD);

                //check to see if we have this shape in the cache
                PhysicsShape phyShape;
                if (meshingStage.TryGetCachedShape(meshHash, Shape, IsDynamic, out phyShape))
                {
                    phyShape.AddRef();

                    //we are done here, call back to caller
                    this.CompletedDelegate(phyShape);

                    return;
                }

                int meshingBegan = Environment.TickCount;

                var bestShape = ShapeDeterminer.FindBestShape(Shape, IsDynamic);

                //first try to extract serialized shapes 
                if (!TryExtractSerializedShapes(meshingStage, meshHash, bestShape))
                {
                    //failure, generate
                    GenerateShapes(meshingStage, meshHash, bestShape);
                }

                if (Settings.Instance.InstrumentMeshing)
                {
                    m_log.InfoFormat("[STATS]: PHYSX_MESHING_TIME,{0},{1}", bestShape, Environment.TickCount - meshingBegan);
                }
            }

            private bool TryExtractSerializedShapes(MeshingStage meshingStage, ulong meshHash, ShapeType bestShape)
            {
                if (SerializedShapes == null) return false;
                if (bestShape != ShapeType.TriMesh && bestShape != ShapeType.DecomposedConvexHulls && bestShape != ShapeType.SingleConvex) return false; //we only handle these types

                using (PhysX.Collection coll = meshingStage._scene.Physics.CreateCollection())
                {
                    using (MemoryStream ms = new MemoryStream(SerializedShapes))
                    {
                        if (! coll.Deserialize(ms))
                        {
                            m_log.Warn("[InWorldz.PhysxPhysics]: Geometry deserialization failed");
                            return false;
                        }
                    }

                    if (coll.DeserializedObjects.Count > 0)
                    {
                        PhysicsShape phyShape;

                        if (coll.DeserializedObjects[0] is PhysX.ConvexMesh)
                        {
                            List<PhysX.ConvexMesh> target = new List<PhysX.ConvexMesh>(coll.DeserializedObjects.Cast<PhysX.ConvexMesh>());
                            var geoms = new List<PhysX.ConvexMeshGeometry>(target.Select(item => new PhysX.ConvexMeshGeometry(item)));

                            if (bestShape == ShapeType.DecomposedConvexHulls)
                            {
                                phyShape = CreatePhysicsShapeFromConvexSetAndCache(meshingStage, meshHash, geoms);
                            }
                            else if (bestShape == ShapeType.SingleConvex)
                            {
                                phyShape = CreatePhysicsShapeFromSingleConvexAndCache(meshingStage, meshHash, geoms[0]);
                            }
                            else
                            {
                                m_log.Warn("[InWorldz.PhysxPhysics]: Serialized geoms were convex, but best shape doesnt match");
                                return false;
                            }
                        }
                        else if (coll.DeserializedObjects[0] is PhysX.TriangleMesh)
                        {
                            List<PhysX.TriangleMesh> target = new List<PhysX.TriangleMesh>(coll.DeserializedObjects.Cast<PhysX.TriangleMesh>());
                            var geoms = new List<PhysX.TriangleMeshGeometry>(target.Select(item => new PhysX.TriangleMeshGeometry(item)));

                            if (target.Count == 1)
                            {
                                phyShape = CreatePhysicsShapeFromTrimeshAndCache(meshingStage, meshHash, geoms[0]);
                            }
                            else
                            {
                                m_log.Warn("[InWorldz.PhysxPhysics]: Oddity. Got more than one serialized trimesh back");
                                return false;
                            }
                        }
                        else
                        {
                            m_log.Warn("[InWorldz.PhysxPhysics]: Oddity. Serialized geoms weren't convex or trimesh");
                            return false;
                        }

                        this.CompletedDelegate(phyShape);
                        return true;
                    }
                    else
                    {
                        //no geom?
                        m_log.Warn("[InWorldz.PhysxPhysics]: Oddity. Didnt find any geom even though we were supplied with a serialized collection");
                        return false;
                    }
                }
            }

            private void GenerateShapes(MeshingStage meshingStage, ulong meshHash, ShapeType bestShape)
            {
                //if we've gotten here, and this is from a crossing, we must have the 
                //serialized physics shape data. if for whatever reason we don't, we
                //need to fall back to a basic shape to prevent the crossing from 
                //trying to pull assets while we're trying to move an object to the
                //new region
                if (FromCrossing)
                {
                    bestShape = ShapeType.PrimitiveBox;
                }

                switch (bestShape)
                {
                    case ShapeType.PrimitiveBox:
                    case ShapeType.PrimitiveSphere:
                        this.GenerateBasicShapeAndComplete(meshingStage, meshHash);
                        break;

                    case ShapeType.DecomposedConvexHulls:
                        this.GenerateConvexSetAndComplete(meshingStage, meshHash);
                        break;

                    case ShapeType.TriMesh:
                        this.GenerateTrimeshAndComplete(meshingStage, meshHash);
                        break;

                    case ShapeType.SingleConvex:
                        this.GenerateSingleConvexAndComplete(meshingStage, meshHash);
                        break;

                    case ShapeType.Null:
                        this.CompletedDelegate(PhysicsShape.Null);
                        break;

                    default:
                        throw new ArgumentException("Unable to generate an appropriate PhysX shape for the given parameters");
                }
            }

            private void GenerateSingleConvexAndComplete(MeshingStage meshingStage, ulong meshHash)
            {
                PhysicsShape phyShape;

                //we need to mesh this object into convex hulls appropriate for dynamic objects
                PhysX.ConvexMeshGeometry convex = meshingStage.GenerateBasicConvexHull(PrimName, Shape, Size, MeshingStage.SCULPT_MESH_LOD, IsDynamic);

                if (convex == null)
                {
                    //meshing  or one of its prereq steps failed, generate a bounding box
                    PhysX.Geometry geom = meshingStage.GeneratePhysXBoxShape(Shape);
                    //basic shapes are not cached
                    phyShape = new PhysicsShape(geom, ShapeType.PrimitiveBox, meshHash);
                }
                else
                {
                    phyShape = CreatePhysicsShapeFromSingleConvexAndCache(meshingStage, meshHash, convex);
                }

                //we are done here, call back to caller
                this.CompletedDelegate(phyShape);
            }

            private static PhysicsShape CreatePhysicsShapeFromSingleConvexAndCache(MeshingStage meshingStage, ulong meshHash, PhysX.ConvexMeshGeometry convex)
            {
                PhysicsShape phyShape;
                phyShape = new PhysicsShape(new List<PhysX.ConvexMeshGeometry> { convex }, meshHash, true);
                phyShape.AddRef();

                //complex shapes are cached
                meshingStage.CacheShape(meshHash, phyShape, ShapeType.SingleConvex);
                return phyShape;
            }

            private void GenerateTrimeshAndComplete(MeshingStage meshingStage, ulong meshHash)
            {
                PhysicsShape phyShape;
                PhysX.TriangleMeshGeometry triMesh = meshingStage.GeneratePhysXTrimeshShape(PrimName, Shape, Size, MeshingStage.SCULPT_MESH_LOD, IsDynamic);

                if (triMesh == null)
                {
                    //meshing  or one of its prereq steps failed, generate a bounding box
                    PhysX.Geometry geom = meshingStage.GeneratePhysXBoxShape(Shape);
                    //basic shapes are not cached
                    phyShape = new PhysicsShape(geom, ShapeType.PrimitiveBox, meshHash);
                }
                else
                {
                    phyShape = CreatePhysicsShapeFromTrimeshAndCache(meshingStage, meshHash, triMesh);
                }

                //we are done here, call back to caller
                this.CompletedDelegate(phyShape);
            }

            private static PhysicsShape CreatePhysicsShapeFromTrimeshAndCache(MeshingStage meshingStage, ulong meshHash, PhysX.TriangleMeshGeometry triMesh)
            {
                PhysicsShape phyShape;
                phyShape = new PhysicsShape(triMesh, ShapeType.TriMesh, meshHash);
                phyShape.AddRef();

                //complex shapes are cached
                meshingStage.CacheShape(meshHash, phyShape, ShapeType.TriMesh);
                return phyShape;
            }

            private void GenerateConvexSetAndComplete(MeshingStage meshingStage, ulong meshHash)
            {
                PhysicsShape phyShape;

                //we need to mesh this object into convex hulls appropriate for dynamic objects
                List<PhysX.ConvexMeshGeometry> convexes = meshingStage.GenerateComplexPhysXShape(meshHash, PrimName, Shape, Size, MeshingStage.SCULPT_MESH_LOD, IsDynamic);

                if (convexes == null)
                {
                    //meshing  or one of its prereq steps failed, generate a bounding box
                    PhysX.Geometry geom = meshingStage.GeneratePhysXBoxShape(Shape);
                    //basic shapes are not cached
                    phyShape = new PhysicsShape(geom, ShapeType.PrimitiveBox, meshHash);
                }
                else
                {
                    phyShape = CreatePhysicsShapeFromConvexSetAndCache(meshingStage, meshHash, convexes);
                }

                //we are done here, call back to caller
                this.CompletedDelegate(phyShape);
            }

            private static PhysicsShape CreatePhysicsShapeFromConvexSetAndCache(MeshingStage meshingStage, ulong meshHash, List<PhysX.ConvexMeshGeometry> convexes)
            {
                PhysicsShape phyShape;
                phyShape = new PhysicsShape(convexes, meshHash);
                phyShape.Complexity = convexes.Count;
                phyShape.AddRef();

                //complex shapes are cached
                meshingStage.CacheShape(meshHash, phyShape, ShapeType.DecomposedConvexHulls);
                return phyShape;
            }

            private void GenerateBasicShapeAndComplete(MeshingStage meshingStage, ulong meshHash)
            {
                Tuple<PhysX.Geometry, ShapeType> result = meshingStage.GenerateBasicPhysXShape(Shape);
                PhysicsShape phyShape = new PhysicsShape(result.Item1, result.Item2, meshHash);

                //basic shapes are not cached

                //we are done here, call back to caller
                this.CompletedDelegate(phyShape);
            }
        }



        /// <summary>
        /// Command to indicate to the meshing stage that the given shape is no longer referenced
        /// by a prim
        /// </summary>
        internal struct UnrefShapeItem : IMeshingQueueItem
        {
            public PhysicsShape Shape;
            public bool Dynamic;

            public void Execute(MeshingStage meshingStage)
            {
                if (Shape != null && Shape.DecRef() == 0)
                {
                    if (Shape.Type == ShapeType.PrimitiveBox || Shape.Type == ShapeType.PrimitiveSphere
                        || Shape.Type == ShapeType.Null)
                    {
                        //primitive shapes are not cached
                        Shape.Dispose();
                    }
                    else
                    {
                        meshingStage.UncacheShape(Shape.Hash, Shape, Shape.Type);
                    }
                }
            }
        }

        /// <summary>
        /// Command to mesh the given heightfield to a triangle mesh
        /// </summary>
        internal struct MeshHeightfieldItem : IMeshingQueueItem
        {
            public float[] Terrain;
            public TerrainMeshingCompleteDelegate CompleteCallback;

            public void Execute(MeshingStage meshingStage)
            {
                Tuple<PhysX.Math.Vector3[], int[]> data = meshingStage._terrainMesher.GenerateTrimeshDataFromHeightmap(Terrain);
                Tuple<PhysX.TriangleMesh, MemoryStream> trimeshData = meshingStage._terrainMesher.GenerateTrimeshFromIndexedTriangles(data.Item1, data.Item2);

                CompleteCallback(trimeshData);
            }
        }
    }
}
