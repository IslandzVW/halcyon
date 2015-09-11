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
    /// Provides a centralized repository for mesh data and prevents unneeded garbage
    /// collections during meshing
    /// </summary>
    class MeshDataCache
    {
        /// <summary>
        /// Number of slots for mesh data
        /// </summary>
        private const int MESH_DATA_SLOTS = 50;
        
        /// <summary>
        /// Divisor to split up mesh data on 
        /// </summary>
        private const int MESH_DATA_DIVISOR = 100;

        /// <summary>
        /// set up an array of 50 slots for mesh data
        /// each of these slots will be divided up for the number of faces/100
        /// so the first slot would hold data for 1 to 100 faces. etc.
        /// </summary>
        private MeshData[] _cache = new MeshData[MESH_DATA_SLOTS];

        /// <summary>
        /// Construct a new cache
        /// </summary>
        public MeshDataCache()
        {
        }

        private int FindPosition(int numFaces)
        {
            return numFaces / MESH_DATA_DIVISOR;
        }

        /// <summary>
        /// Leases mesh data from the cache if it exists
        /// </summary>
        /// <param name="numFaces">Minimum number of faces the mesh data must support</param>
        /// <returns></returns>
        public MeshData Lease(int numTriangles)
        {
            int position = this.FindPosition(numTriangles);
            if (position >= MESH_DATA_SLOTS)
            {
               //mesh is too big and won't be stored here
                return null;
            }

            lock (this)
            {
                MeshData retData = _cache[position];
                _cache[position] = null;

                if (retData != null) retData.Clear();

                return retData;
            }
        }

        /// <summary>
        /// Returns mesh data to the cache for later use
        /// </summary>
        /// <param name="data">The data to add into the pool</param>
        public void Return(MeshData data)
        {
            int position = this.FindPosition(data.TriangleCapacity);
            if (position >= MESH_DATA_SLOTS)
            {
                //mesh is too big and won't be stored here
                return;
            }

            lock (this)
            {
                _cache[position] = data;
            }
        }
    }
}
