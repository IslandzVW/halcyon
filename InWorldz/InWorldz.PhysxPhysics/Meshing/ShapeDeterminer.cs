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

namespace InWorldz.PhysxPhysics.Meshing
{
    /// <summary>
    /// Makes decisions regarding the proper meshing of shapes as discovered by experimentation
    /// and PhysX limits and recommendations
    /// </summary>
    internal class ShapeDeterminer
    {
        /// <summary>
        /// The approximate decomposition algorithms we support
        /// </summary>
        public enum AcdAlgorithm
        {
            HACD,
            RATCLIFF
        }

        /// <summary>
        /// The smallest scale for a static object to get a complex physics mesh
        /// </summary>
        private const float MIN_SIZE_FOR_COMPLEX_STATIC_MESH = 0.05f;

        public static ShapeType FindBestShape(PrimitiveBaseShape baseShape, bool isDynamic)
        {
            if (baseShape.PreferredPhysicsShape == OpenMetaverse.PhysicsShapeType.None)
            {
                return ShapeType.Null;
            }

            if (CanUseLowDetailShape(baseShape, isDynamic))
            {
                return ShapeType.PrimitiveBox;
            }

            if (CanUsePhysXPrimitive(baseShape))
            {
                if (baseShape.ProfileShape == ProfileShape.Square)
                {
                    //best fix is a box
                    return ShapeType.PrimitiveBox;
                }
                else
                {
                    //best fit is a sphere
                    return ShapeType.PrimitiveSphere;
                }
            }

            if (baseShape.PreferredPhysicsShape == OpenMetaverse.PhysicsShapeType.Prim)
            {
                if (isDynamic)
                {
                    if (NeedsPlainConvexWorkaround(baseShape, isDynamic))
                    {
                        return ShapeType.SingleConvex;
                    }
                    else
                    {
                        //for dynamics, the best fit for a mesh shape is a collection of hulls
                        return ShapeType.DecomposedConvexHulls;
                    }
                }
                else
                {
                    return ShapeType.TriMesh;
                }
            }
            else if (baseShape.PreferredPhysicsShape == OpenMetaverse.PhysicsShapeType.ConvexHull)
            {
                return ShapeType.SingleConvex;
            }
            else
            {
                // throw new InvalidOperationException(String.Format("Preferred physics shape {0} is not meshable", baseShape.PreferredPhysicsShape));
                return ShapeType.Null;
            }
        }

        private static bool CanUseLowDetailShape(PrimitiveBaseShape baseShape, bool isDynamic)
        {
            if (!isDynamic && baseShape.Scale.X < MIN_SIZE_FOR_COMPLEX_STATIC_MESH
                           && baseShape.Scale.Y < MIN_SIZE_FOR_COMPLEX_STATIC_MESH
                           && baseShape.Scale.Z < MIN_SIZE_FOR_COMPLEX_STATIC_MESH)
            {
                return true;
            }

            return false;
        }

        private static bool NeedsPlainConvexWorkaround(PrimitiveBaseShape baseShape, bool isDynamic)
        {
            //HACD is unpredictable on sculpts. So for now we're disabling it
            if (baseShape.SculptEntry && baseShape.SculptType != (byte)OpenMetaverse.SculptType.Mesh)
            {
                return true;
            }

            return false;
        }

        public static AcdAlgorithm FindBestAcdAlgorithm(OpenSim.Framework.PrimitiveBaseShape baseShape)
        {
            //spheres and torii use ratcliff
            if ((baseShape.ProfileShape == ProfileShape.Circle ||
                baseShape.ProfileShape == ProfileShape.HalfCircle)
                && baseShape.PathCurve == (byte)Extrusion.Curve1)
            {
                return AcdAlgorithm.RATCLIFF;
            }


            return AcdAlgorithm.HACD;
        }

        private static bool CanUsePhysXPrimitive(OpenSim.Framework.PrimitiveBaseShape pbs)
        {
            //mesh nor sculpts should ever go through here.
            if (pbs.SculptEntry) return false;

            //physx handles sphere and box
            if ((pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)

                    || (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1
                        && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z)
                )
            {

                if (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                    && pbs.ProfileHollow == 0
                    && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                    && pbs.PathBegin == 0 && pbs.PathEnd == 0
                    && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                    && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
                    && pbs.PathShearX == 0 && pbs.PathShearY == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
