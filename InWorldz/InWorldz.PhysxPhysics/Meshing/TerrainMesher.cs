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

namespace InWorldz.PhysxPhysics.Meshing
{
    internal class TerrainMesher : IDisposable
    {
        private PhysX.Scene _scene;
        private PhysX.Cooking _cooking;

        public TerrainMesher(PhysX.Scene parentScene)
        {
            _scene = parentScene;
            _cooking = _scene.Physics.CreateCooking();
        }

        public Tuple<PhysX.Math.Vector3[], int[]> GenerateTrimeshDataFromHeightmap(float[] heightMap)
        {
            const int EDGE_SIZE = 2;
            const int EDGE_COLS_ROWS_ADDED = EDGE_SIZE * 2;

            const int MESH_SIZE = (int)OpenSim.Framework.Constants.RegionSize + EDGE_COLS_ROWS_ADDED;

            //build the trimesh points by row and column
            PhysX.Math.Vector3[] points = new PhysX.Math.Vector3[MESH_SIZE * MESH_SIZE];
            for (int y = 0; y < MESH_SIZE; y++)
            {
                for (int x = 0; x < MESH_SIZE; x++)
                {
                    points[y * MESH_SIZE + x] = new PhysX.Math.Vector3(x - EDGE_SIZE, y - EDGE_SIZE, GetHeightAtPosition(heightMap, x - EDGE_SIZE, y - EDGE_SIZE));
                }
            }


            //build triangle index
            int regionMinusOne = (int)MESH_SIZE - 1;
            int[] triangles = new int[regionMinusOne * regionMinusOne * 6];
            int arrayPos = 0;
            for (int y = 0; y < regionMinusOne; y++)
            {
                int yColStartCurrent = y * (int)MESH_SIZE;
                int yColStartNext = (y + 1) * (int)MESH_SIZE;

                for (int x = 0; x < regionMinusOne; x++)
                {
                    //we build both of the triangles of the heightmap square at once

                    //first triangle
                    triangles[arrayPos++] = yColStartCurrent + x;
                    triangles[arrayPos++] = yColStartNext + x + 1;
                    triangles[arrayPos++] = yColStartNext + x;

                    //second triangle
                    triangles[arrayPos++] = yColStartCurrent + x;
                    triangles[arrayPos++] = yColStartCurrent + x + 1;
                    triangles[arrayPos++] = yColStartNext + x + 1;
                }
            }

            return new Tuple<PhysX.Math.Vector3[], int[]>(points, triangles);
        }

        private static float GetHeightAtPosition(float[] heightMap, int x, int y)
        {
            //handle the case where we are building the outside edge
            if (x < 0)
            {
                x = 0;
            }
            if (y < 0)
            {
                y = 0;
            }
            if (x >= OpenSim.Framework.Constants.RegionSize)
            {
                x = (int)OpenSim.Framework.Constants.RegionSize - 1;
            }
            if (y >= OpenSim.Framework.Constants.RegionSize)
            {
                y = (int)OpenSim.Framework.Constants.RegionSize - 1;
            }

            //heightmap is X as the major axis, then multiply by Y to go to the next row
            return heightMap[y * OpenSim.Framework.Constants.RegionSize + x];
        }

        public Tuple<PhysX.TriangleMesh, MemoryStream> GenerateTrimeshFromIndexedTriangles(PhysX.Math.Vector3[] points, int[] triangles)
        {
            PhysX.TriangleMeshDesc triangleMeshDesc = new PhysX.TriangleMeshDesc()
            {
                Triangles = triangles,
                Points = points,
            };

            MemoryStream ms = new MemoryStream();
            if (!_cooking.CookTriangleMesh(triangleMeshDesc, ms))
            {
                throw new PhysxSdkException("TerrainMesher: GenerateTrimeshFromIndexedTriangles(): CookTriangleMesh() failed");
            }


            ms.Position = 0;
            PhysX.TriangleMesh triangleMesh = _scene.Physics.CreateTriangleMesh(ms);

            return new Tuple<PhysX.TriangleMesh, MemoryStream>(triangleMesh, ms);
        }

        public void Dispose()
        {
            _cooking.Dispose();
        }
    }
}
