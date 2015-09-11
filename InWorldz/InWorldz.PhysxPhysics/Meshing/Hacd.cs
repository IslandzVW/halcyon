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
using System.Runtime.InteropServices;
using ProtoBuf;
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics.Meshing
{
    [ProtoContract]
    internal class HacdConvexHull
    {
        [ProtoMember(1, IsRequired = true)]
        public float[] _rawVerts;

        public PhysX.Math.Vector3[] Vertices;

        [ProtoMember(2, IsRequired=true)]
        public int[] Indicies;

        public static void Scale(OpenMetaverse.Vector3 scale, HacdConvexHull[] hulls)
        {
            foreach (HacdConvexHull hull in hulls)
            {
                PhysX.Math.Vector3[] vertices = hull.Vertices;
                for (int i = 0; i < vertices.Length; ++i)
                {
                    PhysX.Math.Vector3 vert = vertices[i];
                    vert.X *= scale.X;
                    vert.Y *= scale.Y;
                    vert.Z *= scale.Z;

                    vertices[i] = vert;
                }
            }
        }

        public void FillVerticesFromRaw()
        {
            Vertices = PhysUtil.FloatArrayToVectorArray(_rawVerts);
            _rawVerts = null;
        }

        public static HacdConvexHull[] CloneHullArray(HacdConvexHull[] hulls)
        {
            HacdConvexHull[] retHulls = new HacdConvexHull[hulls.Length];
            for (int i = 0; i < hulls.Length; ++i)
            {
                HacdConvexHull nextHull = hulls[i];

                HacdConvexHull newHull = new HacdConvexHull();
                newHull.Indicies = (int[])nextHull.Indicies.Clone();

                newHull.Vertices = new PhysX.Math.Vector3[nextHull.Vertices.Length];
                for (int j = 0; j < nextHull.Vertices.Length; j++)
                {
                    PhysX.Math.Vector3 vec = nextHull.Vertices[j];
                    newHull.Vertices[j] = new PhysX.Math.Vector3(vec.X, vec.Y, vec.Z);
                }

                retHulls[i] = newHull;
            }

            return retHulls;
        }
    }

    internal class HacdPreset
    {
        public float DEFAULT_CC_CONNECT_DIST;
        public int MIN_HULL_COUNT;
        public float CONCAVITY;
        public int TARGET_TRIANGLES_IN_FULL_MESH;
        public int MAX_VERTS_PER_HULL;
        public float VOLUME_WEIGHT;
        public float SMALL_CLUSTER_THRESHOLD;
    }

    /// <summary>
    /// Computes a collection of convex hulls that approximates a given trimesh description
    /// </summary>
    internal class Hacd
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The minimum number of vertices to consult the HACD cache for. Less than this and the
        /// disk activity cost of contacting the cache may outweigh the cost/time of just rebuilding
        /// </summary>
        private const int MIN_VERTS_FOR_CACHING = 360;


        public static readonly HacdPreset PrimDefaultPreset = new HacdPreset
        {
            CONCAVITY = 10.0f,
            DEFAULT_CC_CONNECT_DIST = 30.0f, //30.0f
            MAX_VERTS_PER_HULL = 100, //100
            MIN_HULL_COUNT = 1,
            SMALL_CLUSTER_THRESHOLD = 0.16f,
            TARGET_TRIANGLES_IN_FULL_MESH = 1200,
            VOLUME_WEIGHT = 300.0f //3f
        };

        public static readonly HacdPreset SculptDefaultPreset = new HacdPreset
        {
            CONCAVITY = 100.0f,
            DEFAULT_CC_CONNECT_DIST = 30.0f,
            MAX_VERTS_PER_HULL = 100,
            MIN_HULL_COUNT = 1,
            SMALL_CLUSTER_THRESHOLD = 0.16f,
            TARGET_TRIANGLES_IN_FULL_MESH = 500,
            VOLUME_WEIGHT = 0.0f
        };

        [DllImport("FlatHACD.dll")]
        static extern IntPtr Decompose(
            float[] verts,
            int[] indicies,
            int vertCount,
            int indexCount,
            float ccConnectDist,
            int nClusters,
            float concavity,
            int targetNTrianglesDecimatedMesh,
            int maxVertsPerCH,
            bool addExtraDistPoints,
            bool addFacesPoints,
            float volumeWeight,
            float smallClusterThreshold);

        [DllImport("FlatHACD.dll")]
        static extern int GetConvexHullCount(IntPtr session);

        [DllImport("FlatHACD.dll")]
        static extern int GetVertexCount(IntPtr session, int convexIndex);

        [DllImport("FlatHACD.dll")]
        static extern int GetIndexCount(IntPtr session, int convexIndex);

        [DllImport("FlatHACD.dll")]
        static extern bool FreeSession(IntPtr session);

        [DllImport("FlatHACD.dll")]
        static extern bool GetConvexVertsAndIndexes(IntPtr session, int convexIndex, float[] verts, int[] indexes);


        public static HacdConvexHull[] DecomposeToConvexHulls(ulong meshHash, bool useCache, HacdPreset preset, float[] verts, int[] indicies)
        {
            if (verts.Length % 3 != 0)
                throw new InvalidOperationException("Number of verticies must be divisble by 3");

            if (indicies.Length % 3 != 0)
                throw new InvalidOperationException("Number of indicies must be divisble by 3");


            if (IsCacheCandidate(useCache, verts.Length))
            {
                //try cache
                try
                {
                    HacdConvexHull[] cachedHulls;
                    if (MeshingStage.HullCache.TryGetHulls(meshHash, out cachedHulls))
                    {
                        return cachedHulls;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[InWorldz.PhysX.HACD] Failure retrieving HACD hulls from cache: {0}: {1}", e, e.Message);
                }
            }

            IntPtr session = Decompose(verts, indicies, verts.Length, indicies.Length, preset.DEFAULT_CC_CONNECT_DIST,
                preset.MIN_HULL_COUNT, preset.CONCAVITY, preset.TARGET_TRIANGLES_IN_FULL_MESH, preset.MAX_VERTS_PER_HULL,
                true, true, preset.VOLUME_WEIGHT, preset.SMALL_CLUSTER_THRESHOLD);

            if (session == IntPtr.Zero)
            {
                return null;
            }

            HacdConvexHull[] retHulls;

            try
            {
                int hullCount = GetConvexHullCount(session);

                retHulls = new HacdConvexHull[hullCount];

                for (int hullNum = 0; hullNum < hullCount; ++hullNum)
                {
                    int vertexCount = GetVertexCount(session, hullNum);
                    int indexCount = GetIndexCount(session, hullNum);

                    float[] hullVerts = new float[vertexCount];
                    int[] hullIndexes = new int[indexCount];

                    if (!GetConvexVertsAndIndexes(session, hullNum, hullVerts, hullIndexes))
                    {
                        return null;
                    }

                    HacdConvexHull hull = new HacdConvexHull
                    {
                        Vertices = PhysUtil.FloatArrayToVectorArray(hullVerts),
                        _rawVerts = IsCacheCandidate(useCache, verts.Length) ? hullVerts : null,
                        Indicies = hullIndexes
                    };

                    retHulls[hullNum] = hull;
                }

                //store in cache for later
                if (IsCacheCandidate(useCache, verts.Length))
                {
                    try
                    {
                        MeshingStage.HullCache.CacheHulls(meshHash, retHulls);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[InWorldz.PhysX.HACD] Failure storing HACD results in cache: {0}: {1}", e, e.Message);
                    }
                }

                return retHulls;
            }
            finally
            {
                FreeSession(session);
            }
        }

        private static bool IsCacheCandidate(bool useCache, int vertCount)
        {
            return useCache && vertCount >= MIN_VERTS_FOR_CACHING;
        }
    }
}
