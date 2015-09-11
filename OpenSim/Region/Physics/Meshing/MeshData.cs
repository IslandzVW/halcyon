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
using System.Text;

namespace OpenSim.Region.Physics.Meshing
{
    /// <summary>
    /// Stores data required by the mesher for the MeshDataCache
    /// </summary>
    public class MeshData
    {
        private int _nextVertexSlot;
        private int _nextTriangleSlot;

        private List<Vertex> _verticies;
        private List<Triangle> _triangles;

        /// <summary>
        /// Returns the number of triangles that could fit into this mesh data block
        /// </summary>
        public int TriangleCapacity
        {
            get
            {
                return _triangles.Count;
            }
        }

        /// <summary>
        /// Returns the number of verticies that could fit into this mesh data block
        /// </summary>
        public int VertexCapacity
        {
            get
            {
                return _verticies.Count;
            }
        }

        /// <summary>
        /// Returns the number of vertexes currently loaded
        /// </summary>
        public int VertexCount
        {
            get
            {
                return _nextVertexSlot;
            }
        }

        /// <summary>
        /// Returns the numer of triangles currently loaded
        /// </summary>
        public int TriangleCount
        {
            get
            {
                return _nextTriangleSlot;
            }
        }

        public IEnumerable<Triangle> Triangles
        {
            get
            {
                for (int i = 0; i < TriangleCount; i++)
                {
                    yield return _triangles[i];
                }
            }
        }

        /// <summary>
        /// Create new mesh data with the given sizes
        /// </summary>
        /// <param name="numVerticies"></param>
        /// <param name="numTriangles"></param>
        public MeshData(int numVerticies, int numTriangles)
        {
            _verticies = new List<Vertex>(numVerticies);
            _triangles = new List<Triangle>(numTriangles);

            //insert blank trangles and verticies
            for (int i = 0; i < numVerticies; i++)
            {
                _verticies.Add(new Vertex());
            }

            for (int i = 0; i < numTriangles; i++)
            {
                _triangles.Add(new Triangle());
            }
        }

        /// <summary>
        /// "Clears" the existing data from this object
        /// </summary>
        public void Clear()
        {
            _nextVertexSlot = 0;
            _nextTriangleSlot = 0;
        }

        /// <summary>
        /// Adds a new vertex to this block of cached data
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void AddVertex(float x, float y, float z)
        {
            _verticies[_nextVertexSlot++].Set(x, y, z);
        }

        /// <summary>
        /// Adds a new triangle to this block of cached data
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        public void AddTriangle(Vertex v1, Vertex v2, Vertex v3)
        {
            _triangles[_nextTriangleSlot++].Set(v1, v2, v3);
        }

        /// <summary>
        /// Returns the triangle at the given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Triangle TriangleAt(int index)
        {
            return _triangles[index];
        }

        /// <summary>
        /// Returns the vertex at the given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vertex VertexAt(int index)
        {
            return _verticies[index];
        }
    }
}
