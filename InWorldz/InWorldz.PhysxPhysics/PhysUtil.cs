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
using OpenMetaverse;

namespace InWorldz.PhysxPhysics
{
    internal class PhysUtil
    {
        public static PhysX.Math.Vector3[] OmvVectorArrayToPhysx(OpenMetaverse.Vector3[] omvArray)
        {
            PhysX.Math.Vector3[] physxArray = new PhysX.Math.Vector3[omvArray.Length];

            for (int i = 0; i < omvArray.Length; ++i)
            {
                OpenMetaverse.Vector3 omvVec = omvArray[i];
                physxArray[i] = new PhysX.Math.Vector3(omvVec.X, omvVec.Y, omvVec.Z);
            }

            return physxArray;
        }

        public static PhysX.Math.Vector3[] FloatArrayToVectorArray(float[] floatArray)
        {
            if (floatArray.Length % 3 != 0)
                throw new InvalidOperationException("Float array size must be a multiple of 3 (X,Y,Z)");

            PhysX.Math.Vector3[] ret = new PhysX.Math.Vector3[floatArray.Length / 3];

            for (int i = 0, j = 0; i < floatArray.Length; i += 3, ++j)
            {
                ret[j] = new PhysX.Math.Vector3(floatArray[i + 0], floatArray[i + 1], floatArray[i + 2]);
            }

            return ret;
        }

        public static PhysX.Math.Vector3 OmvVectorToPhysx(OpenMetaverse.Vector3 vec)
        {
            MakeFinite(ref vec);
            return new PhysX.Math.Vector3(vec.X, vec.Y, vec.Z);
        }

        public static OpenMetaverse.Vector3 PhysxVectorToOmv(PhysX.Math.Vector3 vec)
        {
            return new OpenMetaverse.Vector3(vec.X, vec.Y, vec.Z);
        }

        public static PhysX.Math.Matrix PositionToMatrix(OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation)
        {
            MakeFinite(ref position);
            MakeFinite(ref rotation);

            return PhysX.Math.Matrix.RotationQuaternion(new PhysX.Math.Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)) *
                        PhysX.Math.Matrix.Translation(position.X, position.Y, position.Z);
        }

        private static void MakeFinite(ref Quaternion rotation)
        {
            if (float.IsNaN(rotation.X) || float.IsInfinity(rotation.X))
            {
                rotation = Quaternion.Identity;
            }

            if (float.IsNaN(rotation.Y) || float.IsInfinity(rotation.Y))
            {
                rotation = Quaternion.Identity;
            }

            if (float.IsNaN(rotation.Z) || float.IsInfinity(rotation.Z))
            {
                rotation = Quaternion.Identity;
            }

            if (float.IsNaN(rotation.W) || float.IsInfinity(rotation.W))
            {
                rotation = Quaternion.Identity;
            }
        }

        private static void MakeFinite(ref Vector3 position)
        {
            if (float.IsNaN(position.X) || float.IsInfinity(position.X))
            {
                position.X = 0.0f;
            }

            if (float.IsNaN(position.Y) || float.IsInfinity(position.Y))
            {
                position.Y = 0.0f;
            }

            if (float.IsNaN(position.Z) || float.IsInfinity(position.Z))
            {
                position.Z = 0.0f;
            }
        }

        public static OpenMetaverse.Vector3 DecomposeToPosition(PhysX.Math.Matrix matrix)
        {
            return new OpenMetaverse.Vector3(matrix.M41, matrix.M42, matrix.M43);
        }

        public static OpenMetaverse.Quaternion DecomposeToRotation(PhysX.Math.Matrix matrix)
        {
            PhysX.Math.Quaternion physxRot = PhysX.Math.Quaternion.RotationMatrix(matrix);
            return new OpenMetaverse.Quaternion(physxRot.X, physxRot.Y, physxRot.Z, physxRot.W);
        }

        public static OpenSim.Region.Physics.Manager.Pose MatrixToPose(PhysX.Math.Matrix matrix)
        {
            OpenSim.Region.Physics.Manager.Pose pose;
            pose.Position = DecomposeToPosition(matrix);
            pose.Rotation = DecomposeToRotation(matrix);

            return pose;
        }

        // This will make a 3rd version of the same algorithm (refactor later)
        public static float AngleBetween(OpenMetaverse.Quaternion a, OpenMetaverse.Quaternion b)
        {
            double aa   = (a.X * a.X + a.Y * a.Y + a.Z * a.Z + a.W * a.W);
            double bb   = (b.X * b.X + b.Y * b.Y + b.Z * b.Z + b.W * b.W);
            double aabb = aa * bb;
            if (aabb == 0) return 0.0f;

            double ab = (a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);
            double quotient = (ab * ab) / aabb;
            if (quotient >= 1.0) return 0.0f;

            return (float)Math.Acos(2 * quotient - 1);
        }

        public static OpenMetaverse.Quaternion RotBetween(OpenMetaverse.Vector3 a, OpenMetaverse.Vector3 b)
        {
            //A and B should both be normalized
            
            double dotProduct = OpenMetaverse.Vector3.Dot(a, b);
            OpenMetaverse.Vector3 crossProduct = OpenMetaverse.Vector3.Cross(a, b);
            double magProduct = OpenMetaverse.Vector3.Mag(a) * OpenMetaverse.Vector3.Mag(b);
            double angle = Math.Acos(dotProduct / magProduct);
            OpenMetaverse.Vector3 axis = OpenMetaverse.Vector3.Normalize(crossProduct);
            double s = Math.Sin(angle / 2);

            double x = axis.X * s;
            double y = axis.Y * s;
            double z = axis.Z * s;
            double w = Math.Cos(angle / 2);

            if (Double.IsNaN(x) || Double.IsNaN(y) || Double.IsNaN(z) || Double.IsNaN(w))
                return new OpenMetaverse.Quaternion(0.0f, 0.0f, 0.0f, 1.0f);

            return OpenMetaverse.Quaternion.Normalize(new OpenMetaverse.Quaternion((float)x, (float)y, (float)z, (float)w));
        }

        // Returns the angle of a quaternion (see llRot2Axis for the axis)
        public static float Rot2Angle(OpenMetaverse.Quaternion rot)
        {
            if (rot.W > 1) // normalization needed
            {
                float length = (float)Math.Sqrt(rot.X * rot.X + rot.Y * rot.Y +
                        rot.Z * rot.Z + rot.W * rot.W);

                rot.X /= length;
                rot.Y /= length;
                rot.Z /= length;
                rot.W /= length;
            }

            float angle = (float)(2 * Math.Acos(rot.W));

            return angle;
        }

        public static OpenMetaverse.Quaternion AxisAngle2Rot(OpenMetaverse.Vector3 axis, float angle)
        {
            double x, y, z, s, t;

            s = Math.Cos(angle / 2);
            t = Math.Sin(angle / 2); // temp value to avoid 2 more sin() calcs
            x = axis.X * t;
            y = axis.Y * t;
            z = axis.Z * t;

            return new OpenMetaverse.Quaternion((float)x, (float)y, (float)z, (float)s);
        }

        public static OpenMetaverse.Vector3 Rot2Axis(OpenMetaverse.Quaternion rot)
        {
            double x, y, z;

            if (rot.W < 0f)
            {
                //negate to prevent NaN in sqrt after normalization is applied
                rot = Quaternion.Negate(rot);
            }

            if (rot.W > 1) // normalization needed
            {
                float length = (float)Math.Sqrt(rot.X * rot.X + rot.Y * rot.Y +
                        rot.Z * rot.Z + rot.W * rot.W);

                rot.X /= length;
                rot.Y /= length;
                rot.Z /= length;
                rot.W /= length;

            }

            // double angle = 2 * Math.Acos(rot.s);
            double s = Math.Sqrt(1 - rot.W * rot.W);
            if (s < 0.001)
            {
                x = 1;
                y = z = 0;
            }
            else
            {
                x = rot.X / s; // normalize axis
                y = rot.Y / s;
                z = rot.Z / s;
            }

            return new OpenMetaverse.Vector3((float)x, (float)y, (float)z);
        }

        public static OpenMetaverse.Vector3 Rot2Euler(OpenMetaverse.Quaternion r)
        {
            OpenMetaverse.Vector3 v = new OpenMetaverse.Vector3(0.0f, 0.0f, 1.0f) * r;   // Z axis unit vector unaffected by Z rotation component of r.
            double m = OpenMetaverse.Vector3.Mag(v);                       // Just in case v isn't normalized, need magnitude for Asin() operation later.
            if (m == 0.0) return new OpenMetaverse.Vector3();
            double x = Math.Atan2(-v.Y, v.Z);
            double sin = v.X / m;
            if (sin < -0.999999 || sin > 0.999999) x = 0.0;     // Force X rotation to 0 at the singularities.
            double y = Math.Asin(sin);
            // Rotate X axis unit vector by r and unwind the X and Y rotations leaving only the Z rotation
            v = new OpenMetaverse.Vector3(1.0f, 0.0f, 0.0f) * ((r * new OpenMetaverse.Quaternion((float)Math.Sin(-x / 2.0), 0.0f, 0.0f, (float)Math.Cos(-x / 2.0))) * new OpenMetaverse.Quaternion(0.0f, (float)Math.Sin(-y / 2.0), 0.0f, (float)Math.Cos(-y / 2.0)));
            double z = Math.Atan2(v.Y, v.X);

            return new OpenMetaverse.Vector3((float)x, (float)y, (float)z);
        }

        public static OpenMetaverse.Quaternion Euler2Rot(OpenMetaverse.Vector3 v)
        {
            float x, y, z, s;
            double c1 = Math.Cos(v.X / 2.0);
            double c2 = Math.Cos(v.Y / 2.0);
            double c3 = Math.Cos(v.Z / 2.0);
            double s1 = Math.Sin(v.X / 2.0);
            double s2 = Math.Sin(v.Y / 2.0);
            double s3 = Math.Sin(v.Z / 2.0);

            x = (float)(s1 * c2 * c3 + c1 * s2 * s3);
            y = (float)(c1 * s2 * c3 - s1 * c2 * s3);
            z = (float)(s1 * s2 * c3 + c1 * c2 * s3);
            s = (float)(c1 * c2 * c3 - s1 * s2 * s3);

            // Normalize the results to LL "lindle" precision format 
            // (matching visual/viewer precision of 5 decimal places)
            OpenMetaverse.Quaternion rot = OpenMetaverse.Quaternion.Normalize(new OpenMetaverse.Quaternion(x, y, z, s));
            return rot;
        }
    }
}
