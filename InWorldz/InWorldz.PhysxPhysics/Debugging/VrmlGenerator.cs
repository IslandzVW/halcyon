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

namespace InWorldz.PhysxPhysics.Debugging
{
    /// <summary>
    /// Outputs a VRML file for the a mesh
    /// </summary>
    internal class VrmlGenerator
    {
        class Tabs
        {
            int num = 0;

            public override string ToString()
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < num; i++)
                {
                    builder.Append("    ");
                }

                return builder.ToString();
            }

            public static Tabs operator++(Tabs me)
            {
                me.num++;
                return me;
            }

            public static Tabs operator --(Tabs me)
            {
                me.num--;
                if (me.num < 0) me.num = 0;
                return me;
            }
        }

        public static void SaveToVrmlFile(string filePath, IList<OpenMetaverse.Vector3> points, List<Tuple<int, int, int>> triangles)
        {
            string vrml = GenerateVrml(points, triangles);
            vrml = vrml.Replace("\r\n", "\n");

            System.IO.File.Delete(filePath);
            System.IO.File.AppendAllText(filePath, vrml, Encoding.UTF8);
        }

        public static string GenerateVrml(IList<OpenMetaverse.Vector3> points, List<Tuple<int, int, int>> triangles)
        {
            StringBuilder vrmlOut = new StringBuilder();

            Tabs tabs = new Tabs();

            vrmlOut.AppendLine(tabs + "#VRML V2.0 utf8");
            vrmlOut.AppendLine();
            vrmlOut.AppendLine(tabs + String.Format("# Vertices: {0}", points.Count));
            vrmlOut.AppendLine(tabs + String.Format("# Triangles: {0}", triangles.Count));

            vrmlOut.AppendLine();

            vrmlOut.AppendLine(tabs + "Group {"); tabs++;
            vrmlOut.AppendLine(tabs + "children ["); tabs++;
            vrmlOut.AppendLine(tabs + "Shape {"); tabs++;

            vrmlOut.AppendLine(tabs + "appearance Appearance {"); tabs++;
            vrmlOut.AppendLine(tabs + "material Material {"); tabs++;
            vrmlOut.AppendLine(tabs + "diffuseColor 0.5 0.5 0.5");
            vrmlOut.AppendLine(tabs + "ambientIntensity 0.4");
            vrmlOut.AppendLine(tabs + "specularColor 0.5 0.5 0.5");
            vrmlOut.AppendLine(tabs + "emissiveColor 0 0 0");
            vrmlOut.AppendLine(tabs + "shininess 0.4");
            vrmlOut.AppendLine(tabs + "transparency 0");
            tabs--; vrmlOut.AppendLine(tabs + "}");
            tabs--; vrmlOut.AppendLine(tabs + "}");

            vrmlOut.AppendLine(tabs + "geometry IndexedFaceSet {"); tabs++;
            vrmlOut.AppendLine(tabs + "ccw TRUE");
            vrmlOut.AppendLine(tabs + "solid TRUE");
            vrmlOut.AppendLine(tabs + "convex TRUE");

            vrmlOut.AppendLine(tabs + "coord DEF co Coordinate {"); tabs++;
            vrmlOut.AppendLine(tabs + "point ["); tabs++;

            foreach (OpenMetaverse.Vector3 v in points)
            {
                vrmlOut.AppendLine(String.Format(tabs + "{0} {1} {2},", v.X, v.Y, v.Z));
            }

            tabs--; vrmlOut.AppendLine(tabs + "]");
            tabs--; vrmlOut.AppendLine(tabs + "}");

            vrmlOut.AppendLine(tabs + "coordIndex ["); tabs++;

            foreach (Tuple<int, int, int> t in triangles)
            {
                vrmlOut.AppendLine(String.Format(tabs + "{0}, {1}, {2}, -1,", t.Item1, t.Item2, t.Item3));
            }

            tabs--; vrmlOut.AppendLine(tabs + "]");
            tabs--; vrmlOut.AppendLine(tabs + "}");
            tabs--; vrmlOut.AppendLine(tabs + "}");
            tabs--; vrmlOut.AppendLine(tabs + "]");
            tabs--; vrmlOut.AppendLine(tabs + "}");

            return vrmlOut.ToString();
        }
    }
}
