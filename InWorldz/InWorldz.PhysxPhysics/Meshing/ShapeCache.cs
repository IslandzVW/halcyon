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

namespace InWorldz.PhysxPhysics.Meshing
{
    /// <summary>
    /// Holds PhysX geometries of both current and a limited number of past objects
    /// so that we don't have to generate new geometries for objects who have the same
    /// shape
    /// </summary>
    internal class ShapeCache : IDisposable
    {
        public delegate void ShapeNeedsFreeingDelegate(PhysicsShape shape);

        /// <summary>
        /// This event should be hooked to enable the proper freeing of physx geometries in 
        /// the simulation thread to prevent threading problems
        /// </summary>
        public event ShapeNeedsFreeingDelegate OnShapeNeedsFreeing;

        /// <summary>
        /// The maximum number of inactive shapes to cache. Unused shapes beyond this 
        /// amount will be freed in LRU order
        /// </summary>
        private const int MAX_INACTIVE_SHAPES = 256;

        /// <summary>
        /// Dynamic shapes that are currently rezzed out somewhere in a scene
        /// </summary>
        private Dictionary<ulong, PhysicsShape> _activeShapes = new Dictionary<ulong, PhysicsShape>();

        /// <summary>
        /// Dynamic shapes that were in the scene but are no longer active. This helps for shape changing objects
        /// </summary>
        private LRUCache<ulong, PhysicsShape> _inactiveShapes = new LRUCache<ulong, PhysicsShape>(MAX_INACTIVE_SHAPES);


        public ShapeCache()
        {
            _inactiveShapes.OnItemPurged += new LRUCache<ulong, PhysicsShape>.ItemPurgedDelegate(OnInactiveShapePurged);
        }

        private void OnInactiveShapePurged(PhysicsShape item)
        {
            this.CheckAndDispose(item, true);
        }

        private void CheckAndDispose(PhysicsShape item, bool dynamic)
        {
            //we want to dispose this object, but ONLY if no one else has a reference to it
            //that includes the opposite inactive list
            if (item.HasRefs)
            {
                return;
            }

            //we can dispose
            if (this.OnShapeNeedsFreeing != null)
            {
                this.OnShapeNeedsFreeing(item);
            }
            else
            {
                //this will happen during shutdown, where we dont want to defer the deletes to the physics thread
                //since it is stopped
                item.Dispose();
            }
        }

        private bool TryGetActiveShape(ulong shapeHash, out PhysicsShape shape)
        {
            return _activeShapes.TryGetValue(shapeHash, out shape);
        }

        private bool TryGetInactiveShape(ulong shapeHash, out PhysicsShape shape)
        {
            return _inactiveShapes.TryGetValue(shapeHash, out shape);
        }

        public bool TryGetShape(ulong shapeHash, out PhysicsShape shape)
        {
            if (this.TryGetActiveShape(shapeHash, out shape))
            {
                return true;
            }

            if (this.TryGetInactiveShape(shapeHash, out shape))
            {
                this.ReactivateShape(shapeHash, shape);
                return true;
            }

            shape = null;
            return false;
        }

        /// <summary>
        /// Moves the given shape to the active list
        /// </summary>
        /// <param name="item"></param>
        private void ReactivateShape(ulong shapeHash, PhysicsShape item)
        {
            _inactiveShapes.Remove(shapeHash);
            _activeShapes.Add(shapeHash, item);
        }

        /// <summary>
        /// Adds a new shape to the active list
        /// </summary>
        /// <param name="shapeHash"></param>
        /// <param name="shape"></param>
        /// <param name="dynamic"></param>
        public void AddShape(ulong shapeHash, PhysicsShape shape)
        {
            _activeShapes.Add(shapeHash, shape);
        }

        /// <summary>
        /// Remove means the reference count for this shape has reached zero
        /// </summary>
        /// <param name="shapeHash"></param>
        /// <param name="shape"></param>
        /// <param name="dynamic"></param>
        public void RemoveShape(ulong shapeHash, PhysicsShape shape)
        {
            _activeShapes.Remove(shapeHash);
            _inactiveShapes.Add(shapeHash, shape);
        }

        public void Dispose()
        {
            foreach (var shape in _activeShapes)
            {
                shape.Value.Dispose();
                _inactiveShapes.Remove(shape.Key);
            }

            foreach (var shape in _inactiveShapes)
            {
                shape.Value.Dispose();
            }
        }

        internal void BeginPerformingDirectDeletes()
        {
            this.OnShapeNeedsFreeing = null;
        }
    }
}
