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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.IO;
using System.Runtime.InteropServices;
using OpenSim.Region.Physics.Manager;
using PrimMesher;
using OpenMetaverse;
using OpenSim.Region.Physics.ConvexDecompositionDotNet;

namespace OpenSim.Region.Physics.Meshing
{
    public class Mesh : IMesh
    {
        private static MeshDataCache meshCache = new MeshDataCache();

        private Dictionary<Vertex, int> m_vertices;

        private MeshData _meshData;

        public float[] m_normals;

        private ulong _meshHash;

        private C5.HashSet<IndexedTriangle> _triIndex;

        public int VertCount
        {
            get { return m_vertices.Count; }
        }

        public Mesh(ulong hashCode, int numVerticies, int numTriangles)
        {
            _meshHash = hashCode;

            m_vertices = new Dictionary<Vertex, int>();
            _triIndex = new C5.HashSet<IndexedTriangle>();

            _meshData = meshCache.Lease(numTriangles);

            if (_meshData == null || _meshData.VertexCapacity < numVerticies || _meshData.TriangleCapacity < numTriangles)
            {
                _meshData = new MeshData(numVerticies, numTriangles);
            }
        }

        public Mesh Clone()
        {
            Mesh result = new Mesh(_meshHash, _meshData.VertexCount, _meshData.TriangleCount);

            foreach (Triangle t in _meshData.Triangles)
            {
                result.Add(t.v1.Clone(), t.v2.Clone(), t.v3.Clone());
            }

            return result;
        }

        /// <summary>
        /// Adds a triangle to the mesh using existing vertex indexes 
        /// </summary>
        /// <param name="v1index"></param>
        /// <param name="v2Index"></param>
        /// <param name="v3index"></param>
        public void Add(int v1index, int v2index, int v3index)
        {
            this.Add(_meshData.VertexAt(v1index), _meshData.VertexAt(v2index), _meshData.VertexAt(v3index));
        }

        /// <summary>
        /// Adds a triangle to the mesh using absolute vertices
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        public void Add(Vertex v1, Vertex v2, Vertex v3)
        {
            //do not include any triangles that have been smashed into lines
            if (v1.Equals(v2) || v2.Equals(v3) || v1.Equals(v3))
                return;

            // If a vertex of the triangle is not yet in the vertices list,
            // add it and set its index to the current index count
            if (!m_vertices.ContainsKey(v1))
                m_vertices[v1] = m_vertices.Count;
            if (!m_vertices.ContainsKey(v2))
                m_vertices[v2] = m_vertices.Count;
            if (!m_vertices.ContainsKey(v3))
                m_vertices[v3] = m_vertices.Count;

            //do not include any triangles we already have
            if (!_triIndex.Add(new IndexedTriangle(m_vertices[v1], m_vertices[v2], m_vertices[v3])))
                return;

            _meshData.AddTriangle(v1, v2, v3);
        }

        public void CalcNormals()
        {
            int iTriangles = _meshData.TriangleCount;

            this.m_normals = new float[iTriangles * 3];

            int i = 0;
            foreach (Triangle t in _meshData.Triangles)
            {
                float ux, uy, uz;
                float vx, vy, vz;
                float wx, wy, wz;

                ux = t.v1.X;
                uy = t.v1.Y;
                uz = t.v1.Z;

                vx = t.v2.X;
                vy = t.v2.Y;
                vz = t.v2.Z;

                wx = t.v3.X;
                wy = t.v3.Y;
                wz = t.v3.Z;


                // Vectors for edges
                float e1x, e1y, e1z;
                float e2x, e2y, e2z;

                e1x = ux - vx;
                e1y = uy - vy;
                e1z = uz - vz;

                e2x = ux - wx;
                e2y = uy - wy;
                e2z = uz - wz;


                // Cross product for normal
                float nx, ny, nz;
                nx = e1y * e2z - e1z * e2y;
                ny = e1z * e2x - e1x * e2z;
                nz = e1x * e2y - e1y * e2x;

                // Length
                float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                float lReciprocal = 1.0f / l;

                // Normalized "normal"
                //nx /= l;
                //ny /= l;
                //nz /= l;

                m_normals[i] = nx * lReciprocal;
                m_normals[i + 1] = ny * lReciprocal;
                m_normals[i + 2] = nz * lReciprocal;

                i += 3;
            }
        }

        public List<Vector3> getVertexList()
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Vertex v in m_vertices.Keys)
            {
                result.Add(new Vector3(v.X, v.Y, v.Z));
            }
            return result;
        }

        public List<float3> getVertexListAsFloat3()
        {
            List<float3> result = new List<float3>();
            foreach (Vertex v in m_vertices.Keys)
            {
                result.Add(new float3(v.X, v.Y, v.Z));
            }
            return result;
        }

        public float[] getVertexListAsFloat()
        {
            if (m_vertices == null)
                throw new NotSupportedException();

            float[] result = new float[m_vertices.Count * 3];
            foreach (KeyValuePair<Vertex, int> kvp in m_vertices)
            {
                Vertex v = kvp.Key;
                int i = kvp.Value;
                result[3 * i + 0] = v.X;
                result[3 * i + 1] = v.Y;
                result[3 * i + 2] = v.Z;
            }
            return result;
        }

        public int[] getIndexListAsIntFlipped()
        {
            if (_meshData == null)
                throw new NotSupportedException();

            int[] result = new int[_meshData.TriangleCount * 3];
            for (int i = 0; i < _meshData.TriangleCount; i++)
            {
                Triangle t = _meshData.TriangleAt(i);
                result[3 * i + 0] = m_vertices[t.v3];
                result[3 * i + 1] = m_vertices[t.v2];
                result[3 * i + 2] = m_vertices[t.v1];
            }
            return result;
        }

        public int[] getIndexListAsInt()
        {
            if (_meshData == null)
                throw new NotSupportedException();

            int[] result = new int[_meshData.TriangleCount * 3];
            for (int i = 0; i < _meshData.TriangleCount; i++)
            {
                Triangle t = _meshData.TriangleAt(i);
                result[3 * i + 0] = m_vertices[t.v1];
                result[3 * i + 1] = m_vertices[t.v2];
                result[3 * i + 2] = m_vertices[t.v3];
            }
            return result;
        }

        public List<int> getIndexListAsIntList()
        {
            if (_meshData == null)
                throw new NotSupportedException();

            List<int> result = new List<int>(_meshData.TriangleCount * 3);
            for (int i = 0; i < _meshData.TriangleCount; i++)
            {
                Triangle t = _meshData.TriangleAt(i);
                result.Add(m_vertices[t.v1]);
                result.Add(m_vertices[t.v2]);
                result.Add(m_vertices[t.v3]);
            }

            return result;
        }

        public List<Tuple<int, int, int>> getTriangleList()
        {
            List<Tuple<int, int, int>> triangles = new List<Tuple<int, int, int>>();

            for (int i = 0; i < _meshData.TriangleCount; i++)
            {
                Triangle t = _meshData.TriangleAt(i);
                int v1 = m_vertices[t.v1];
                int v2 = m_vertices[t.v2];
                int v3 = m_vertices[t.v3];

                triangles.Add(new Tuple<int, int, int>(v1, v2, v3));
            }

            return triangles;
        }

        public Vector3[] getVertexListAsArray()
        {
            Vector3[] vlist = new Vector3[m_vertices.Count];

            foreach (KeyValuePair<Vertex, int> kvp in m_vertices)
            {
                Vertex v = kvp.Key;
                int i = kvp.Value;
                vlist[i] = v.AsVector();
            }

            return vlist;
        }

        /// <summary>
        /// frees up the source mesh data to minimize memory - call this method after calling get*Locked() functions
        /// </summary>
        public void ReleaseSourceMeshData()
        {
            m_vertices = null;

            if (_meshData != null)
            {
                meshCache.Return(_meshData);
                _meshData = null;
            }
        }

        public void Append(IMesh newMesh)
        {
            if (!(newMesh is Mesh))
                return;

            foreach (Triangle t in ((Mesh)newMesh)._meshData.Triangles)
            {
                Add(t.v1, t.v2, t.v3);
            }
        }

        // Do a linear transformation of  mesh.
        public void TransformLinear(float[,] matrix, float[] offset)
        {
            foreach (Vertex v in m_vertices.Keys)
            {
                if (v == null)
                    continue;
                float x, y, z;
                x = v.X * matrix[0, 0] + v.Y * matrix[1, 0] + v.Z * matrix[2, 0];
                y = v.X * matrix[0, 1] + v.Y * matrix[1, 1] + v.Z * matrix[2, 1];
                z = v.X * matrix[0, 2] + v.Y * matrix[1, 2] + v.Z * matrix[2, 2];
                v.X = x + offset[0];
                v.Y = y + offset[1];
                v.Z = z + offset[2];
            }
        }

        public void DumpRaw(String path, String name, String title)
        {
            if (path == null)
                return;
            String fileName = name + "_" + title + ".raw";
            String completePath = System.IO.Path.Combine(path, fileName);
            StreamWriter sw = new StreamWriter(completePath);
            foreach (Triangle t in _meshData.Triangles)
            {
                String s = t.ToStringRaw();
                sw.WriteLine(s);
            }
            sw.Close();
        }

        public void TrimExcess()
        {

        }

        public void AddMeshVertex(float x, float y, float z)
        {
            _meshData.AddVertex(x, y, z);
        }

        public Vertex MeshVertexAt(int index)
        {
            return _meshData.VertexAt(index);
        }
    }
}
