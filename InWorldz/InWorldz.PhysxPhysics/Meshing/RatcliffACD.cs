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
using OpenSim.Region.Physics.ConvexDecompositionDotNet;
using log4net;
using System.Reflection;

namespace InWorldz.PhysxPhysics.Meshing
{
    internal class RatcliffACD
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The minimum number of vertices to consult the cache for. Less than this and the
        /// disk activity cost of contacting the cache may outweigh the cost/time of just rebuilding
        /// </summary>
        private const int MIN_VERTS_FOR_CACHING = 120;

        private List<ConvexResult> m_hulls;

        private static bool IsCacheCandidate(bool useCache, int vertCount)
        {
            return useCache && vertCount >= MIN_VERTS_FOR_CACHING;
        }

        public HacdConvexHull[] DecomposeToConvexHulls(ulong meshHash, bool useCache, List<float3> convVertices, List<int> convIndices)
        {
            if (convIndices.Count % 3 != 0)
                throw new InvalidOperationException("Number of indicies must be divisble by 3");

            if (IsCacheCandidate(useCache, convVertices.Count))
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
                    m_log.ErrorFormat("[InWorldz.PhysX.RatcliffACD] Failure retrieving HACD hulls from cache: {0}: {1}", e, e.Message);
                }
            }

            ConvexBuilder builder = new ConvexBuilder(HullReturn);
            m_hulls = new List<ConvexResult>();

            DecompDesc dcomp = new DecompDesc();
            dcomp.mIndices = convIndices;
            dcomp.mVertices = convVertices;

            builder.process(dcomp);

            var retHulls = new HacdConvexHull[m_hulls.Count];

            for (int i = 0; i < m_hulls.Count; i++)
            {
                ConvexResult hull = m_hulls[i];

                float[] rawVerts = null;
                if (IsCacheCandidate(useCache, convVertices.Count))
                {
                    rawVerts = new float[hull.HullVertices.Count * 3];
                }

                PhysX.Math.Vector3[] hullVerts = new PhysX.Math.Vector3[hull.HullVertices.Count];
                for (int j = 0; j < hull.HullVertices.Count; j++)
                {
                    hullVerts[j] = new PhysX.Math.Vector3(hull.HullVertices[j].x, hull.HullVertices[j].y, hull.HullVertices[j].z);

                    if (rawVerts != null)
                    {
                        rawVerts[j * 3 + 0] = hull.HullVertices[j].x;
                        rawVerts[j * 3 + 1] = hull.HullVertices[j].y;
                        rawVerts[j * 3 + 2] = hull.HullVertices[j].z;
                    }
                }

                retHulls[i] = new HacdConvexHull { 
                    Indicies = hull.HullIndices.ToArray(), 
                    Vertices = hullVerts,
                    _rawVerts = rawVerts,
                };
            }

            //store in cache for later
            if (IsCacheCandidate(useCache, convVertices.Count))
            {
                try
                {
                    MeshingStage.HullCache.CacheHulls(meshHash, retHulls);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[InWorldz.PhysX.RatcliffACD] Failure storing HACD results in cache: {0}: {1}", e, e.Message);
                }
            }

            return retHulls;
        }

        private void HullReturn(ConvexResult result)
        {
            m_hulls.Add(result);
            return;
        }
    }
}
