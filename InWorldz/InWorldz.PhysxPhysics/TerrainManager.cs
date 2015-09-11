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
using OpenMetaverse;

namespace InWorldz.PhysxPhysics
{
    internal class TerrainManager : IDisposable
    {
        const string TERRAIN_CACHE_FILE = "{0}.r{1}.terrain.nxs";

        private PhysX.Scene _scene;
        private Meshing.TerrainMesher _terrainMesher;
        private UUID _regionId;

        private PhysX.Shape _groundShape;
        private PhysX.RigidActor _groundActor;
        private PhysX.TriangleMesh _mesh;

        public TerrainManager(PhysX.Scene scene, Meshing.TerrainMesher mesher, UUID regionId)
        {
            _scene = scene;
            _terrainMesher = mesher;
            _regionId = regionId;
        }

        public void SetTerrainSync(float[] heightMap, bool canLoadFromCache, int revision)
        {
            Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> groundActor = null;

            if (canLoadFromCache)
            {
                groundActor = LoadTerrainFromCache(revision);
            }

            if (groundActor == null)
            {
                //cache load failed/not valid, calculate
                Tuple<PhysX.Math.Vector3[], int[]> indexedTris = _terrainMesher.GenerateTrimeshDataFromHeightmap(heightMap);
                groundActor = GenerateGroundActorFromIndexedTriangles(indexedTris.Item1, indexedTris.Item2, revision);
            }

            this.SwapGroundActor(groundActor);
        }

        public void SetTerrainPremeshedSync(Tuple<PhysX.TriangleMesh, MemoryStream> premeshedTerrainData, int revision)
        {
            Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> groundActor
                = this.GenerateGroundActorFromTrimeshAndMemoryStream(premeshedTerrainData, revision);

            this.SwapGroundActor(groundActor);
        }

        private Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> LoadTerrainFromCache(int revision)
        {
            Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> groundActor = null;

            Stream cacheStream = TryOpenTerrainCache(revision);
            if (cacheStream != null)
            {
                using (cacheStream)
                {
                    PhysX.TriangleMesh triMesh;
                    PhysX.RigidActor hfActor;
                    PhysX.Shape shape;
                    this.GenerateActorsFromMeshStream(cacheStream, out triMesh, out hfActor, out shape);
                    groundActor = new Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh>(hfActor, shape, triMesh);
                }
            }

            return groundActor;
        }

        private void SwapGroundActor(Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> groundActor)
        {
            if (_groundActor != null)
            {
                _scene.RemoveActor(_groundActor);
                _groundActor.Dispose();

                if (!_groundShape.Disposed)
                {
                    _groundShape.Dispose();
                }

                _mesh.Dispose();
            }

            _groundActor = groundActor.Item1;
            _groundShape = groundActor.Item2;
            _mesh = groundActor.Item3;

            _groundActor.UserData = this;

            CollisionGroup.SetCollisionGroup(CollisionGroupFlag.Ground, _groundShape);

            _scene.AddActor(_groundActor);
        }

        

        private Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> GenerateGroundActorFromIndexedTriangles(PhysX.Math.Vector3[] points, int[] triangles,
            int revision)
        {
            Tuple<PhysX.TriangleMesh, MemoryStream> trimeshData = _terrainMesher.GenerateTrimeshFromIndexedTriangles(points, triangles);

            return GenerateGroundActorFromTrimeshAndMemoryStream(trimeshData, revision);
        }

        private Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh> GenerateGroundActorFromTrimeshAndMemoryStream(Tuple<PhysX.TriangleMesh, MemoryStream> trimeshData,
            int revision)
        {
            using (trimeshData.Item2) //free up the stream that gets passed in after its cached
            {
                PhysX.TriangleMeshGeometry triangleMeshShapeDesc = new PhysX.TriangleMeshGeometry(trimeshData.Item1);

                PhysX.RigidActor hfActor;
                PhysX.Shape shape;

                GenerateActorsFromTrimesh(triangleMeshShapeDesc, out hfActor, out shape);
                CacheTerrainMesh(trimeshData.Item2, revision);

                return new Tuple<PhysX.RigidActor, PhysX.Shape, PhysX.TriangleMesh>(hfActor, shape, trimeshData.Item1);
            }
        }

        private void GenerateActorsFromMeshStream(Stream ms, out PhysX.TriangleMesh triangleMesh, out PhysX.RigidActor hfActor, out PhysX.Shape shape)
        {
            triangleMesh = _scene.Physics.CreateTriangleMesh(ms);
            PhysX.TriangleMeshGeometry triangleMeshShapeDesc = new PhysX.TriangleMeshGeometry(triangleMesh);

            GenerateActorsFromTrimesh(triangleMeshShapeDesc, out hfActor, out shape);
        }

        private void GenerateActorsFromTrimesh(PhysX.TriangleMeshGeometry triangleMeshShapeDesc, out PhysX.RigidActor hfActor, out PhysX.Shape shape)
        {
            //PhysX.Math.Matrix.RotationYawPitchRoll(0f, (float)Math.PI / 2, 0f) * PhysX.Math.Matrix.Translation(0f, 0f, 0f)

            //PhysX.RigidDynamic dynActor = _scene.Physics.CreateRigidDynamic();
            //dynActor.Flags |= PhysX.RigidDynamicFlags.Kinematic;
            //hfActor = dynActor;

            hfActor = _scene.Physics.CreateRigidStatic();

            shape = hfActor.CreateShape(triangleMeshShapeDesc, Material.GROUND.PhyMaterial);
        }

        private void CacheTerrainMesh(MemoryStream ms, int revision)
        {
            //delete all previous cached meshes
            try
            {
                foreach (FileInfo f in new DirectoryInfo(".").GetFiles("*.terrain.nxs"))
                {
                    f.Delete();
                }
            }
            catch
            {
                
            }

            ms.Position = 0;
            using (var fileStream = File.Create(GetRevisionCacheFileName(revision)))
            {
                ms.CopyTo(fileStream);
                fileStream.Close();
            }
        }

        private string GetRevisionCacheFileName(int revision)
        {
            return String.Format(TERRAIN_CACHE_FILE, _regionId, revision);
        }

        private Stream TryOpenTerrainCache(int revision)
        {
            string cacheFile = GetRevisionCacheFileName(revision);
            if (!File.Exists(cacheFile)) return null;
            return File.OpenRead(cacheFile);
        }

        public void Dispose()
        {
            if (_groundActor != null)
            {
                _groundActor.Dispose();
                _groundActor = null;
            }

            if (_mesh != null)
            {
                _mesh.Dispose();
                _mesh = null;
            }
        }
    }
}
