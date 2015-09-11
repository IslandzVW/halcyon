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
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using System.Threading;
using System.IO;
using log4net;
using System.Reflection;
using OpenSim.Region.Physics.ConvexDecompositionDotNet;

namespace InWorldz.PhysxPhysics.Meshing
{
    /// <summary>
    /// Manages the meshing of physics objects. Actual meshing is done in a separate
    /// thread to not stop/hinder the main loop or the physical simulation
    /// </summary>
    internal partial class MeshingStage : IDisposable
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void ShapeNeedsFreeingDelegate(PhysicsShape shape);
        public delegate void TerrainMeshingCompleteDelegate(Tuple<PhysX.TriangleMesh, MemoryStream> terrainData);

        /// <summary>
        /// This event should be hooked to enable the proper freeing of physx geometries in 
        /// the simulation thread to prevent threading problems
        /// </summary>
        public event ShapeNeedsFreeingDelegate OnShapeNeedsFreeing;

        /// <summary>
        /// Ripped from ODE. Don't know why "32" is the magic number here
        /// </summary>
        public const float SCULPT_MESH_LOD = (float)LevelOfDetail.High;

        public delegate void MeshingCompleteDelegate(PhysicsShape shape);

        internal interface IMeshingQueueItem
        {
            void Execute(MeshingStage meshingStage);
        }

        private PhysX.Scene _scene;
        private OpenSim.Region.Physics.Manager.IMesher _mesher;
        private TerrainMesher _terrainMesher;

        private Queue<IMeshingQueueItem> _meshWaitingQueue = new Queue<IMeshingQueueItem>();

        private Thread _runThread;

        private ShapeCache _singleConvexHullCache = new ShapeCache();
        private ShapeCache _acdHullCache = new ShapeCache();
        private ShapeCache _trimeshCache = new ShapeCache();

        private volatile bool _stop = false;

        private PhysX.Cooking _cooking;

        public static HacdHullCache HullCache = new HacdHullCache();

        public MeshingStage(PhysX.Scene scene, OpenSim.Region.Physics.Manager.IMesher mesher, TerrainMesher terrainMesher)
        {
            _scene = scene;
            _mesher = mesher;
            _terrainMesher = terrainMesher;

            PhysX.CookingParams cookParms = new PhysX.CookingParams
            {
                SkinWidth = 0.025f,
                SuppressTriangleMeshRemapTable = true,
                TargetPlatform = PhysX.Platform.PC
            };

            _cooking = _scene.Physics.CreateCooking(cookParms);

            _singleConvexHullCache.OnShapeNeedsFreeing += new ShapeCache.ShapeNeedsFreeingDelegate(_shapeCache_OnShapeNeedsFreeing);
            _acdHullCache.OnShapeNeedsFreeing += new ShapeCache.ShapeNeedsFreeingDelegate(_shapeCache_OnShapeNeedsFreeing);
            _trimeshCache.OnShapeNeedsFreeing += new ShapeCache.ShapeNeedsFreeingDelegate(_shapeCache_OnShapeNeedsFreeing);

            _runThread = new Thread(new ThreadStart(this.QueueProcessorLoop));
            _runThread.Name = "MeshingStage";
            _runThread.Start();
        }

        void _shapeCache_OnShapeNeedsFreeing(PhysicsShape shape)
        {
            if (OnShapeNeedsFreeing != null)
            {
                OnShapeNeedsFreeing(shape);
            }
        }

        private void QueueProcessorLoop()
        {
            while (!_stop)
            {
                ProcessNextQueueItem();
            }
        }

        internal void QueueForMeshing(string primName, OpenSim.Framework.PrimitiveBaseShape pbs, OpenMetaverse.Vector3 size, float lod,
            bool isDynamic, byte[] serializedShapes, bool fromCrossing, MeshingCompleteDelegate completedDelegate)
        {
            lock (_meshWaitingQueue)
            {
                _meshWaitingQueue.Enqueue(
                    new MeshingQueueItem
                    {
                        PrimName = primName,
                        Shape = pbs,
                        Size = size,
                        LOD = lod,
                        IsDynamic = isDynamic,
                        SerializedShapes = serializedShapes,
                        CompletedDelegate = completedDelegate,
                        FromCrossing = fromCrossing
                    });

                Monitor.Pulse(_meshWaitingQueue);
            }
        }

        internal void UnrefShape(PhysicsShape shape, bool dynamic)
        {
            lock (_meshWaitingQueue)
            {
                _meshWaitingQueue.Enqueue(new UnrefShapeItem
                {
                    Shape = shape,
                    Dynamic = dynamic
                });

                Monitor.Pulse(_meshWaitingQueue);
            }
        }

        internal void MeshHeightfield(float[] heightField, TerrainMeshingCompleteDelegate callback)
        {
            lock (_meshWaitingQueue)
            {
                _meshWaitingQueue.Enqueue(new MeshHeightfieldItem
                {
                    Terrain = heightField,
                    CompleteCallback = callback
                });

                Monitor.Pulse(_meshWaitingQueue);
            }
        }

        private void ProcessNextQueueItem()
        {
            IMeshingQueueItem item;

            lock (_meshWaitingQueue)
            {
                while (!_stop && _meshWaitingQueue.Count == 0)
                {
                    Monitor.Wait(_meshWaitingQueue);
                }

                if (_stop) return;

                if (_meshWaitingQueue.Count > 0)
                {
                    item = _meshWaitingQueue.Dequeue();
                }
                else
                {
                    return;
                }
            }

            item.Execute(this);
        }

        private Tuple<PhysX.Geometry, ShapeType> GenerateBasicPhysXShape(PrimitiveBaseShape primitiveBaseShape)
        {
            if (primitiveBaseShape.ProfileShape == ProfileShape.HalfCircle)
            {
                //this is a sphere
                PhysX.Geometry geom = this.GeneratePhysXSphereShape(primitiveBaseShape);
                return new Tuple<PhysX.Geometry, ShapeType>(geom, ShapeType.PrimitiveSphere);
            }
            else
            {
                //this is a box
                PhysX.Geometry geom = this.GeneratePhysXBoxShape(primitiveBaseShape);
                return new Tuple<PhysX.Geometry, ShapeType>(geom, ShapeType.PrimitiveBox);
            }
        }

        private PhysX.Geometry GeneratePhysXBoxShape(PrimitiveBaseShape primitiveBaseShape)
        {
            //box geometry size is specified in half extents
            return new PhysX.BoxGeometry(primitiveBaseShape.Scale.X / 2.0f, primitiveBaseShape.Scale.Y / 2.0f, primitiveBaseShape.Scale.Z / 2.0f);
        }

        private PhysX.Geometry GeneratePhysXSphereShape(PrimitiveBaseShape primitiveBaseShape)
        {
            return new PhysX.SphereGeometry(primitiveBaseShape.Scale.X / 2.0f);
        }

        private PhysX.TriangleMeshGeometry GeneratePhysXTrimeshShape(string primName, PrimitiveBaseShape shape, OpenMetaverse.Vector3 size, float LOD, bool isDynamic)
        {
            MeshingResult result = _mesher.CreateMesh(primName, shape, size, LOD, ShapeType.TriMesh, true);
            if (result == null) return null;

            IMesh mesh = result.TriMesh;
            if (mesh == null) return null;


            int[] indexes = mesh.getIndexListAsInt();
            PhysX.Math.Vector3[] verts = PhysUtil.OmvVectorArrayToPhysx(mesh.getVertexListAsArray());

            mesh.ReleaseSourceMeshData();

            PhysX.TriangleMeshDesc desc = new PhysX.TriangleMeshDesc
            {
                Points = verts,
                Triangles = indexes,
            };

            if (!desc.IsValid())
            {
                m_log.Warn("[InWorldz.PhysxPhysics] Unable to create trimesh for shape. Invalid description.");
                return null;
            }

            using (MemoryStream ms = new MemoryStream())
            {
                try
                {
                    if (!_cooking.CookTriangleMesh(desc, ms))
                    {
                        m_log.Warn("[InWorldz.PhysxPhysics] Unable to create trimesh for shape.");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[InWorldz.PhysxPhysics] Unable to create trimesh for shape: {0}", e);
                    return null;
                }

                ms.Position = 0;

                try
                {
                    PhysX.TriangleMesh triMesh = _scene.Physics.CreateTriangleMesh(ms);

                    //m_log.DebugFormat("Trimesh Created: {0} {1}", triMesh.GetHashCode(), primName);

                    PhysX.TriangleMeshGeometry triGeom = new PhysX.TriangleMeshGeometry(triMesh);

                    return triGeom;
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[InWorldz.PhysxPhysics] Unable to create trimesh for shape: {0}", e);
                    return null;
                }
            }
        }

        private List<PhysX.ConvexMeshGeometry> GenerateComplexPhysXShape(ulong meshHash, string primName, PrimitiveBaseShape shape, OpenMetaverse.Vector3 size, 
            float LOD, bool isDynamic)
        {
            //create the mesh and do not prescale it. the ACD algorithm can then cache the output hulls and 
            //scale as appropriate
            MeshingResult result = _mesher.CreateMesh(primName, shape, size, LOD, ShapeType.DecomposedConvexHulls, false);
            if (result == null) return null;

            HacdConvexHull[] hulls = null;

            if (result.ResultType == ShapeType.TriMesh)
            {
                IMesh mesh = result.TriMesh;
                if (mesh == null) return null;

                //Debugging.VrmlGenerator.SaveToVrmlFile("lastMesh.wrl", mesh.getVertexListAsArray(), mesh.getTriangleList());

                switch (ShapeDeterminer.FindBestAcdAlgorithm(shape))
                {
                    case ShapeDeterminer.AcdAlgorithm.HACD:
                        hulls = DecomposeWithHACD(shape, LOD, mesh);
                        break;

                    case ShapeDeterminer.AcdAlgorithm.RATCLIFF:
                        hulls = DecomposeWithRatcliff(shape, LOD, mesh);
                        break;

                    default:
                        throw new PhysxSdkException("GenerateComplexPhysXShape(): Specified ACD algorithm does not exist");
                }

                if (hulls == null)
                {
                    return null;
                }
            }
            else if (result.ResultType == ShapeType.DecomposedConvexHulls)
            {
                hulls = new HacdConvexHull[result.ConvexVerts.Count];

                for (int i = 0; i < result.ConvexVerts.Count; i++)
                {
                    hulls[i] = new HacdConvexHull { Vertices = new PhysX.Math.Vector3[result.ConvexVerts[i].Count] };
                    for (int j = 0; j < result.ConvexVerts[i].Count; j++)
                    {
                        var vert = result.ConvexVerts[i][j];
                        hulls[i].Vertices[j] = new PhysX.Math.Vector3(vert.X, vert.Y, vert.Z);
                    }
                }
            }
            else
            {
                return null;
            }

            HacdConvexHull.Scale(size, hulls);

            List<PhysX.ConvexMeshGeometry> ret = new List<PhysX.ConvexMeshGeometry>();

            try
            {
                foreach (HacdConvexHull hull in hulls)
                {
                    PhysX.ConvexMeshDesc convexMeshDesc = new PhysX.ConvexMeshDesc()
                    {
                        Flags = PhysX.ConvexFlag.InflateConvex
                    };

                    if (hull.Indicies == null) convexMeshDesc.Flags |= PhysX.ConvexFlag.ComputeConvex;

                    convexMeshDesc.SetPositions(hull.Vertices);
                    if (hull.Indicies != null) convexMeshDesc.SetTriangles(hull.Indicies);

                    if (!convexMeshDesc.IsValid())
                    {
                        throw new PhysxSdkException("GenerateComplexPhysXShape: Convex mesh description is invalid");
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (!_cooking.CookConvexMesh(convexMeshDesc, ms))
                        {
                            throw new PhysxSdkException("GenerateComplexPhysXShape: CookConvexMesh() failed");
                        }

                        ms.Position = 0;

                        PhysX.ConvexMesh convexMesh = _scene.Physics.CreateConvexMesh(ms);
                        PhysX.ConvexMeshGeometry convexShapeGeom = new PhysX.ConvexMeshGeometry(convexMesh);
                        ret.Add(convexShapeGeom);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[InWorldz.PhysxPhysics] Unable to create convex hullset for shape: {0}", e);

                result = _mesher.CreateMesh(primName, shape, size, LOD, ShapeType.SingleConvex, true);
                if (result == null)
                {
                    m_log.WarnFormat("[InWorldz.PhysxPhysics] Fallback hull generation failed, giving up", e);
                    return null;
                }

                //direct convex available?
                if (result.ResultType == ShapeType.SingleConvex)
                {
                    if (!TryGenerateFallbackHullFromHullData(ret, e, result.SingleConvex))
                    {
                        return null;
                    }
                }
                else if (result.ResultType == ShapeType.TriMesh)
                {
                    IMesh mesh = result.TriMesh;
                    if (mesh == null)
                    {
                        m_log.WarnFormat("[InWorldz.PhysxPhysics] Fallback hull generation failed, giving up", e);
                        return null;
                    }

                    if (!TryGenerateFallbackHullFromTrimesh(ret, e, mesh))
                    {
                        return null;
                    }
                }
            }

            return ret;
        }

        private bool TryGenerateFallbackHullFromHullData(List<PhysX.ConvexMeshGeometry> ret, Exception e, List<OpenMetaverse.Vector3> vertList)
        {
            PhysX.Math.Vector3[] verts = new PhysX.Math.Vector3[vertList.Count];
            for (int i = 0; i < vertList.Count; i++)
            {
                verts[i] = PhysUtil.OmvVectorToPhysx(vertList[i]);
            }

            // fall back to basic convex hull
            PhysX.ConvexMeshGeometry fallbackHull = this.GenerateBasicConvexHull(null, verts);
            if (fallbackHull != null)
            {
                ret.Add(fallbackHull);
            }
            else
            {
                m_log.WarnFormat("[InWorldz.PhysxPhysics] Fallback hull generation failed, giving up", e);
                return false;
            }

            return true;
        }

        private bool TryGenerateFallbackHullFromTrimesh(List<PhysX.ConvexMeshGeometry> ret, Exception e, IMesh mesh)
        {
            int[] indexes = mesh.getIndexListAsInt();
            float[] verts = mesh.getVertexListAsFloat();

            // fall back to basic convex hull
            PhysX.ConvexMeshGeometry fallbackHull = this.GenerateBasicConvexHull(indexes, verts);
            if (fallbackHull != null)
            {
                ret.Add(fallbackHull);
            }
            else
            {
                m_log.WarnFormat("[InWorldz.PhysxPhysics] Fallback hull generation failed, giving up", e);
                return false;
            }

            return true;
        }

        private HacdConvexHull[] DecomposeWithRatcliff(PrimitiveBaseShape shape, float LOD, IMesh mesh)
        {
            List<int> indexes = mesh.getIndexListAsIntList();
            List<float3> verts = mesh.getVertexListAsFloat3();

            RatcliffACD rcAcd = new RatcliffACD();
            HacdConvexHull[] hulls = rcAcd.DecomposeToConvexHulls(shape.GetMeshKey(OpenMetaverse.Vector3.One, LOD), true, verts, indexes);

            return hulls;
        }

        private static HacdConvexHull[] DecomposeWithHACD(PrimitiveBaseShape shape, float LOD, IMesh mesh)
        {
            int[] indexes = mesh.getIndexListAsInt();
            float[] verts = mesh.getVertexListAsFloat();

            mesh.ReleaseSourceMeshData();

            HacdPreset preset;
            if (shape.SculptEntry)
            {
                preset = Hacd.SculptDefaultPreset;
            }
            else
            {
                preset = Hacd.PrimDefaultPreset;
            }

            //we cant use the hash we have here, as Hacd needs the mesh hash prescaled
            HacdConvexHull[] hulls = Hacd.DecomposeToConvexHulls(shape.GetMeshKey(OpenMetaverse.Vector3.One, LOD), shape.SculptEntry == false, preset, verts, indexes);
            return hulls;
        }

        private PhysX.ConvexMeshGeometry GenerateBasicConvexHull(string primName, PrimitiveBaseShape shape, OpenMetaverse.Vector3 size, float LOD, bool isDynamic)
        {
            MeshingResult result = _mesher.CreateMesh(primName, shape, size, LOD, ShapeType.SingleConvex, true);
            if (result == null) return null;

            IMesh mesh = result.TriMesh;
            if (mesh == null) return null;
            
            int[] indexes = mesh.getIndexListAsInt();
            float[] verts = mesh.getVertexListAsFloat();

            if (indexes.Length == 0 || verts.Length == 0)
            {
                return null;
            }

            return GenerateBasicConvexHull(indexes, verts);
        }

        private PhysX.ConvexMeshGeometry GenerateBasicConvexHull(int[] indexes, float[] verts)
        {
            try
            {
                PhysX.ConvexMeshDesc convexMeshDesc = new PhysX.ConvexMeshDesc()
                {
                    Flags = PhysX.ConvexFlag.InflateConvex | PhysX.ConvexFlag.ComputeConvex
                };

                convexMeshDesc.SetPositions(PhysUtil.FloatArrayToVectorArray(verts));
                if (indexes != null) convexMeshDesc.SetTriangles(indexes);

                using (MemoryStream ms = new MemoryStream())
                {
                    if (!_cooking.CookConvexMesh(convexMeshDesc, ms))
                    {
                        throw new PhysxSdkException("GenerateBasicConvexHull: CookConvexMesh() failed");
                    }

                    ms.Position = 0;

                    PhysX.ConvexMesh convexMesh = _scene.Physics.CreateConvexMesh(ms);
                    PhysX.ConvexMeshGeometry convexShapeGeom = new PhysX.ConvexMeshGeometry(convexMesh);
                    return convexShapeGeom;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[InWorldz.PhysxPhysics] Unable to fallback to convex hull for shape: {0}", e);
            }

            return null;
        }

        private PhysX.ConvexMeshGeometry GenerateBasicConvexHull(int[] indexes, PhysX.Math.Vector3[] verts)
        {
            try
            {
                PhysX.ConvexMeshDesc convexMeshDesc = new PhysX.ConvexMeshDesc()
                {
                    Flags = PhysX.ConvexFlag.InflateConvex | PhysX.ConvexFlag.ComputeConvex
                };

                convexMeshDesc.SetPositions(verts);
                if (indexes != null) convexMeshDesc.SetTriangles(indexes);

                using (MemoryStream ms = new MemoryStream())
                {
                    if (!_cooking.CookConvexMesh(convexMeshDesc, ms))
                    {
                        throw new PhysxSdkException("GenerateBasicConvexHull: CookConvexMesh() failed");
                    }

                    ms.Position = 0;

                    PhysX.ConvexMesh convexMesh = _scene.Physics.CreateConvexMesh(ms);
                    PhysX.ConvexMeshGeometry convexShapeGeom = new PhysX.ConvexMeshGeometry(convexMesh);
                    return convexShapeGeom;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[InWorldz.PhysxPhysics] Unable to fallback to convex hull for shape: {0}", e);
            }

            return null;
        }


        internal void Stop()
        {
            _stop = true;
            lock (_meshWaitingQueue)
            {
                Monitor.Pulse(_meshWaitingQueue);
            }

            _runThread.Join();
        }

        public void Dispose()
        {
            _singleConvexHullCache.Dispose();
            _acdHullCache.Dispose();
            _trimeshCache.Dispose();
            _cooking.Dispose();
            _terrainMesher.Dispose();
        }

        internal void InformCachesToPerformDirectDeletes()
        {
            _singleConvexHullCache.BeginPerformingDirectDeletes();
            _acdHullCache.BeginPerformingDirectDeletes();
            _trimeshCache.BeginPerformingDirectDeletes();
        }

        private ShapeCache FindCorrespondingCache(ShapeType shapeType)
        {
            switch (shapeType)
            {
                case ShapeType.SingleConvex:
                    return _singleConvexHullCache;

                case ShapeType.DecomposedConvexHulls:
                    return _acdHullCache;

                case ShapeType.TriMesh:
                    return _trimeshCache;
            }

            return null;
        }

        internal bool TryGetCachedShape(ulong meshHash, OpenSim.Framework.PrimitiveBaseShape shape, bool isDynamic, out PhysicsShape phyShape)
        {
            ShapeCache cache = FindCorrespondingCache(ShapeDeterminer.FindBestShape(shape, isDynamic));
            if (cache != null)
            {
                return cache.TryGetShape(meshHash, out phyShape);
            }

            phyShape = null;
            return false;
        }

        internal void CacheShape(ulong meshHash, PhysicsShape phyShape, ShapeType bestFitShape)
        {
            ShapeCache cache = FindCorrespondingCache(bestFitShape);
            cache.AddShape(meshHash, phyShape);
        }

        internal void UncacheShape(ulong meshHash, PhysicsShape shape, ShapeType shapeType)
        {
            ShapeCache cache = FindCorrespondingCache(shapeType);
            cache.RemoveShape(meshHash, shape);
        }
    }
}
