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
using System.Threading;
using OpenSim.Region.Physics.Manager;

namespace InWorldz.PhysxPhysics
{
    /// <summary>
    /// Represents a shape that can be applied to a prim. Shapes can be broken down as a set
    /// of convex hulls, a single triangle mesh, or a single PhysX primitive type
    /// </summary>
    internal class PhysicsShape : IDisposable
    {
        private const float REST_OFFSET = 0.0005f;
        
        private List<PhysX.ConvexMeshGeometry> _convexHulls = new List<PhysX.ConvexMeshGeometry>();
        private PhysX.TriangleMeshGeometry _triMesh;
        private PhysX.Geometry _primitiveGeom;
        
        private ShapeType _shapeType;

        private ulong _shapeHash;

        private int _refCount;

        public ulong Hash
        {
            get { return _shapeHash; }
        }

        public ShapeType Type
        {
            get
            {
                return _shapeType;
            }
        }

        public bool HasRefs
        {
            get
            {
                return _refCount > 0;
            }
        }

        /// <summary>
        /// The physics complexity of this shape in convex hull count
        /// </summary>
        public int Complexity { get; set; }

        public static readonly PhysicsShape Null = new PhysicsShape();

        public PhysicsShape()
        {
            _shapeType = ShapeType.Null;
        }

        public PhysicsShape(PhysX.Geometry primitive, ShapeType shapeType, ulong meshHash)
        {
            this.AssignGeom(primitive, shapeType);

            _shapeType = shapeType;
            _shapeHash = meshHash;
        }

        public PhysicsShape(List<PhysX.ConvexMeshGeometry> convexes, ulong meshHash)
        {
            this.AssignGeom(convexes);

            _shapeHash = meshHash;
        }

        public PhysicsShape(List<PhysX.ConvexMeshGeometry> convexes, ulong meshHash, bool singleConvex)
        {
            this.AssignGeom(convexes);

            _shapeHash = meshHash;

            if (singleConvex)
            {
                _shapeType = ShapeType.SingleConvex;
            }
        }

        private void AssignGeom(List<PhysX.ConvexMeshGeometry> convexes)
        {
            _convexHulls = convexes;
            _shapeType = ShapeType.DecomposedConvexHulls;
            Complexity = convexes.Count;
        }

        private void AssignGeom(PhysX.Geometry primitive, ShapeType shapeType)
        {
            switch (shapeType)
            {
                case ShapeType.PrimitiveBox:
                case ShapeType.PrimitiveSphere:
                    _primitiveGeom = primitive;
                    break;

                case ShapeType.TriMesh:
                    _triMesh = (PhysX.TriangleMeshGeometry)primitive;
                    break;
            }

            Complexity = 1;
        }

        internal List<PhysX.Shape> AssignToActor(PhysX.RigidActor actor, PhysX.Material material, bool physical)
        {
            return AssignToActor(actor, material, PhysX.Math.Matrix.Identity, physical);
        }

        internal List<PhysX.Shape> AssignToActor(PhysX.RigidActor actor, PhysX.Material material, PhysX.Math.Matrix localPose, bool physical)
        {
            switch (_shapeType)
            {
                case ShapeType.PrimitiveBox:
                case ShapeType.PrimitiveSphere:
                    return new List<PhysX.Shape> { CreatePrimitiveShape(actor, material, ref localPose, physical) };

                case ShapeType.TriMesh:
                    return new List<PhysX.Shape> { CreateTrimeshShape(actor, material, ref localPose) };

                case ShapeType.DecomposedConvexHulls:
                case ShapeType.SingleConvex:
                    return this.AssignHullsToActor(actor, material, localPose, physical);

                case ShapeType.Null:
                    return new List<PhysX.Shape>(0);

                default:
                    throw new InvalidOperationException("Can not assign shape to actor: shapeType is missing or invalid");
            }
        }

        private PhysX.Shape CreateTrimeshShape(PhysX.RigidActor actor, PhysX.Material material, ref PhysX.Math.Matrix localPose)
        {
            PhysX.Shape shape = actor.CreateShape(_triMesh, material, localPose);
            shape.RestOffset = REST_OFFSET;

            return shape;
        }

        private PhysX.Shape CreatePrimitiveShape(PhysX.RigidActor actor, PhysX.Material material, ref PhysX.Math.Matrix localPose,
            bool physical)
        {
            PhysX.Shape shape = actor.CreateShape(_primitiveGeom, material, localPose);
            shape.RestOffset = REST_OFFSET;

            if (physical && Settings.Instance.UseCCD)
            {
                //enable CCD
                shape.Flags |= PhysX.ShapeFlag.UseSweptBounds;
            }

            return shape;
        }

        private List<PhysX.Shape> AssignHullsToActor(PhysX.RigidActor actor, PhysX.Material material, PhysX.Math.Matrix localPose,
            bool physical)
        {
            List<PhysX.Shape> hulls = new List<PhysX.Shape>();
            foreach (PhysX.ConvexMeshGeometry geom in _convexHulls)
            {
                PhysX.Shape shape = actor.CreateShape(geom, material, localPose);
                shape.RestOffset = REST_OFFSET;

                if (physical && Settings.Instance.UseCCD)
                {
                    //enable CCD
                    shape.Flags |= PhysX.ShapeFlag.UseSweptBounds;
                }

                hulls.Add(shape);
            }

            return hulls;
        }


        /// <summary>
        /// Not thread safe, must be called from the meshing thread
        /// </summary>
        public int DecRef()
        {
            return --_refCount;
        }

        /// <summary>
        /// Not thread safe, must be called from the meshing thread
        /// </summary>
        public void AddRef()
        {
            ++_refCount;
        }

        public void Dispose()
        {
            foreach (PhysX.ConvexMeshGeometry geom in _convexHulls)
            {
                geom.ConvexMesh.Dispose();
            }

            if (_triMesh != null)
            {
                _triMesh.TriangleMesh.Dispose();
            }
        }
    }
}
