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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        private readonly bool[,] taint;
        private double[,] map;
        private int _revision;

        public TerrainChannel()
        {
            map = new double[Constants.RegionSize, Constants.RegionSize];
            taint = new bool[Constants.RegionSize / 16,Constants.RegionSize / 16];

            int x;
            for (x = 0; x < Constants.RegionSize; x++)
            {
                int y;
                for (y = 0; y < Constants.RegionSize; y++)
                {
                    map[x, y] = TerrainUtil.PerlinNoise2D(x, y, 2, 0.125) * 10;
                    double spherFacA = TerrainUtil.SphericalFactor(x, y, Constants.RegionSize / 2.0, Constants.RegionSize / 2.0, 50) * 0.01;
                    double spherFacB = TerrainUtil.SphericalFactor(x, y, Constants.RegionSize / 2.0, Constants.RegionSize / 2.0, 100) * 0.001;
                    if (map[x, y] < spherFacA)
                        map[x, y] = spherFacA;
                    if (map[x, y] < spherFacB)
                        map[x, y] = spherFacB;
                }
            }
        }

        public TerrainChannel(double[,] import)
        {
            map = import;
            taint = new bool[import.GetLength(0),import.GetLength(1)];
        }

        public TerrainChannel(double[,] import, int rev) : this(import)
        {
            _revision = rev;
        }

        public TerrainChannel(bool createMap)
        {
            if (createMap)
            {
                map = new double[Constants.RegionSize,Constants.RegionSize];
                taint = new bool[Constants.RegionSize / 16,Constants.RegionSize / 16];
            }
        }

        public TerrainChannel(int w, int h)
        {
            map = new double[w,h];
            taint = new bool[w / 16,h / 16];
        }

        #region ITerrainChannel Members

        public int Width
        {
            get { return map.GetLength(0); }
        }

        public int Height
        {
            get { return map.GetLength(1); }
        }

        public ITerrainChannel MakeCopy()
        {
            TerrainChannel copy = new TerrainChannel(false);
            copy.map = (double[,]) map.Clone();

            return copy;
        }

        public float[] GetFloatsSerialized()
        {
            // Move the member variables into local variables, calling
            // member variables 256*256 times gets expensive
            int w = Width;
            int h = Height;
            float[] heights = new float[w * h];

            int i, j; // map coordinates
            int idx = 0; // index into serialized array
            for (i = 0; i < h; i++)
            {
                for (j = 0; j < w; j++)
                {
                    heights[idx++] = (float)map[j, i];
                }
            }

            return heights;
        }

        public double[,] GetDoubles()
        {
            return map;
        }

        public double this[int x, int y]
        {
            get 
            {
                if (x < map.GetLength(0) && y < map.GetLength(1) &&
                    x >= 0 && y >= 0)
                {
                    return map[x, y];
                }
                else
                {
                    return 0.0;
                }
            }
            set
            {
                // Will "fix" terrain hole problems. Although not fantastically.
                if (Double.IsNaN(value) || Double.IsInfinity(value))
                    return;

                if (map[x, y] != value)
                {
                    taint[x / 16, y / 16] = true;
                    map[x, y] = value;
                }
            }
        }

        public int RevisionNumber
        {
            get
            {
                return _revision;
            }
        }

        public bool Tainted(int x, int y)
        {
            if (taint[x / 16, y / 16])
            {
                taint[x / 16, y / 16] = false;
                return true;
            }
            return false;
        }

        #endregion

        public TerrainChannel Copy()
        {
            TerrainChannel copy = new TerrainChannel(false);
            copy.map = (double[,]) map.Clone();

            return copy;
        }

        public string SaveToXmlString()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            using (StringWriter sw = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sw, settings))
                {
                    WriteXml(writer);
                }
                string output = sw.ToString();
                return output;
            }
        }

        private void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(String.Empty, "TerrainMap", String.Empty);
            ToXml(writer);
            writer.WriteEndElement();
        }

        public void LoadFromXmlString(string data)
        {
            StringReader sr = new StringReader(data);
            XmlTextReader reader = new XmlTextReader(sr);
            reader.Read();

            ReadXml(reader);
            reader.Close();
            sr.Close();
        }

        private void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement("TerrainMap");
            FromXml(reader);
        }

        private void ToXml(XmlWriter xmlWriter)
        {
            float[] mapData = GetFloatsSerialized();
            byte[] buffer = new byte[mapData.Length * 4];
            for (int i = 0; i < mapData.Length; i++)
            {
                byte[] value = BitConverter.GetBytes(mapData[i]);
                Array.Copy(value, 0, buffer, (i * 4), 4);
            }
            XmlSerializer serializer = new XmlSerializer(typeof(byte[]));
            serializer.Serialize(xmlWriter, buffer);
        }

        private void FromXml(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(byte[]));
            byte[] dataArray = (byte[])serializer.Deserialize(xmlReader);
            int index = 0;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float value;
                    value = BitConverter.ToSingle(dataArray, index);
                    index += 4;
                    this[x, y] = (double)value;
                }
            }
        }

        /// <summary>
        /// Get the raw height at the given x/y coordinates on 1m boundary.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns></returns>
        public float GetRawHeightAt(int x, int y)
        {
            return (float)(map[x, y]);
        }
        public float GetRawHeightAt(uint x, uint y)
        {
            return (float)(map[x, y]);
        }
        public double GetRawHeightAt(Vector3 point)
        {
            return map[(int)point.X, (int)point.Y];
        }

        /// <summary>
        /// Get the height of the terrain at given horizontal coordinates.
        /// </summary>
        /// <param name="xPos">X coordinate</param>
        /// <param name="yPos">Y coordinate</param>
        /// <returns>Height at given coordinates</returns>
        public float CalculateHeightAt(float xPos, float yPos)
        {
            if (!Util.IsValidRegionXY(xPos, yPos))
                return 0.0f;

            int x = (int)xPos;
            int y = (int)yPos;
            float xOffset = xPos - (float)x;
            float yOffset = yPos - (float)y;
            if ((xOffset == 0.0f) && (yOffset == 0.0f))
                return (float)(map[x, y]);  // optimize the exact heightmap entry case

            int xPlusOne = x + 1;
            int yPlusOne = y + 1;

            // Check for edge cases (literally)
            if ((xPlusOne > Constants.RegionSize - 1) || (yPlusOne > Constants.RegionSize - 1))
                return (float)map[x, y];
            if (xPlusOne > Constants.RegionSize - 1)
            {
                if (yPlusOne > Constants.RegionSize - 1)
                    return (float)map[x, y];    // upper right corner

                // Simpler linear interpolation between 2 points along y (right map edge)
                return (float)((map[x, yPlusOne] - map[x, y]) * yOffset + map[x, y]);
            }
            if (yPlusOne > Constants.RegionSize - 1)
            {
                // Simpler linear interpolation between 2 points along x (top map edge)
                return (float)((map[xPlusOne, y] - map[x, y]) * xOffset + map[x, y]);
            } 
            
            // LL terrain triangles are divided like [/] rather than [\] ...
            // Vertex/triangle layout is:  Z2 - Z3
            //                              | / |
            //                             Z0 - Z1
            float triZ0 = (float)(map[x, y]);
            float triZ1 = (float)(map[xPlusOne, y]);
            float triZ2 = (float)(map[x, yPlusOne]);
            float triZ3 = (float)(map[xPlusOne, yPlusOne]);

            float height = 0.0f;
            if ((xOffset + (1.0 - yOffset)) < 1.0f)
            {
                // Upper left (NW) triangle
                // So relative to Z2 corner
                height = triZ2;
                height += (triZ3 - triZ2) * xOffset;
                height += (triZ0 - triZ2) * (1.0f - yOffset);
            }
            else
            {
                // Lower right (SE) triangle
                // So relative to Z1 corner
                height = triZ1;
                height += (triZ0 - triZ1) * (1.0f - xOffset);
                height += (triZ3 - triZ1) * yOffset;
            }
            return height;
        }

        /// <summary>
        /// Get the height of the terrain at given horizontal coordinates.
        /// Uses a 4-point bilinear calculation to smooth the terrain based 
        /// on all 4 nearest terrain heightmap vertices.
        /// </summary>
        /// <param name="xPos">X coordinate</param>
        /// <param name="yPos">Y coordinate</param>
        /// <returns>Height at given coordinates</returns>
        public float Calculate4PointHeightAt(float xPos, float yPos)
        {
            if (!Util.IsValidRegionXY(xPos, yPos))
                return 0.0f;

            int x = (int)xPos;
            int y = (int)yPos;
            float xOffset = xPos - (float)x;
            float yOffset = yPos - (float)y;
            if ((xOffset == 0.0f) && (yOffset == 0.0f))
                return (float)(map[x, y]);  // optimize the exact heightmap entry case

            int xPlusOne = x + 1;
            int yPlusOne = y + 1;

            // Check for edge cases (literally)
            if ((xPlusOne > Constants.RegionSize - 1) || (yPlusOne > Constants.RegionSize - 1))
                return (float)map[x, y];
            if (xPlusOne > Constants.RegionSize - 1)
            {
                if (yPlusOne > Constants.RegionSize - 1)
                    return (float)map[x, y];    // upper right corner

                // Simpler linear interpolation between 2 points along y (right map edge)
                return (float)((map[x, yPlusOne] - map[x, y]) * yOffset + map[x, y]);
            }
            if (yPlusOne > Constants.RegionSize - 1) {
                // Simpler linear interpolation between 2 points along x (top map edge)
                return (float)((map[xPlusOne, y] - map[x, y]) * xOffset + map[x, y]);
            }

            // Inside the map square: use 4-point bilinear interpolation
            // f(x,y) = f(0,0) * (1-x)(1-y) + f(1,0) * x(1-y) + f(0,1) * (1-x)y + f(1,1) * xy
            float f00 = GetRawHeightAt(x, y);
            float f01 = GetRawHeightAt(x, yPlusOne);
            float f10 = GetRawHeightAt(xPlusOne, y);
            float f11 = GetRawHeightAt(xPlusOne, yPlusOne);
            float lowest = f00;
            lowest = Math.Min(lowest, f01);
            lowest = Math.Min(lowest, f10);
            lowest = Math.Min(lowest, f11);
            f00 -= lowest; f01 -= lowest; f10 -= lowest; f11 -= lowest;
            float z = (float)(
                        f00 * (1.0f - xOffset) * (1.0f - yOffset) 
                      + f01 *         xOffset  * (1.0f - yOffset) 
                      + f10 * (1.0f - xOffset) * yOffset 
                      + f11 *         xOffset  * yOffset
                      );
            return lowest + z;
        }

        // Pass the deltas between point1 and the corner and point2 and the corner
        private Vector3 TriangleNormal(Vector3 delta1, Vector3 delta2)
        {
            Vector3 normal;

            if (delta1 == Vector3.Zero)
                normal = new Vector3(0.0f, delta2.Y * -delta1.Z, 1.0f);
            else
            if (delta2 == Vector3.Zero)
                normal = new Vector3(delta1.X * -delta1.Z, 0.0f, 1.0f);
            else
            {
                // The cross product is the slope normal.
                normal = new Vector3(
                            (delta1.Y * delta2.Z) - (delta1.Z * delta2.Y),
                            (delta1.Z * delta2.X) - (delta1.X * delta2.Z),
                            (delta1.X * delta2.Y) - (delta1.Y * delta2.X)
                         );
            }
            return normal;
        }

        public Vector3 NormalToSlope(Vector3 normal)
        {
            Vector3 slope = new Vector3(normal);
            if (slope.Z == 0.0f)
                slope.Z = 1.0f;
            else
                slope.Z = ((normal.X * normal.X) + (normal.Y * normal.Y)) / (-1.0f * normal.Z);
            return slope;
        }

        // Pass the deltas between point1 and the corner and point2 and the corner
        private Vector3 TriangleSlope(Vector3 delta1, Vector3 delta2)
        {
            return NormalToSlope(TriangleNormal(delta1, delta2));
        }

        public Vector3 CalculateNormalAt(float xPos, float yPos) // 3-point triangle surface normal
        {
            if (xPos < 0 || yPos < 0 || xPos >= Constants.RegionSize || yPos >= Constants.RegionSize)
                return new Vector3(0.00000f, 0.00000f, 1.00000f);
            
            uint x = (uint)xPos;
            uint y = (uint)yPos;
            uint xPlusOne = x + 1;
            uint yPlusOne = y + 1;
            if (xPlusOne > Constants.RegionSize - 1)
                xPlusOne = Constants.RegionSize - 1;
            if (yPlusOne > Constants.RegionSize - 1)
                yPlusOne = Constants.RegionSize - 1;

            Vector3 P0 = new Vector3(x, y, (float)map[x, y]);
            Vector3 P1 = new Vector3(xPlusOne, y, (float)map[xPlusOne, y]);
            Vector3 P2 = new Vector3(x, yPlusOne, (float)map[x, yPlusOne]);
            Vector3 P3 = new Vector3(xPlusOne, yPlusOne, (float)map[xPlusOne, yPlusOne]);

            float xOffset = xPos - (float)x;
            float yOffset = yPos - (float)y;
            Vector3 normal;
            if ((xOffset + (1.0 - yOffset)) < 1.0f)
                normal = TriangleNormal(P2 - P3, P0 - P2);  // Upper left (NW) triangle
            else
                normal = TriangleNormal(P0 - P1, P1 - P3);  // Lower right (SE) triangle
            return normal;
        }

        public Vector3 CalculateSlopeAt(float xPos, float yPos)  // 3-point triangle slope
        {
            return NormalToSlope(CalculateNormalAt(xPos, yPos));
        }

        public Vector3 Calculate4PointNormalAt(float xPos, float yPos)
        {
            if (!Util.IsValidRegionXY(xPos, yPos))
                return new Vector3(0.00000f, 0.00000f, 1.00000f);
            
            uint x = (uint)xPos;
            uint y = (uint)yPos;
            uint xPlusOne = x + 1;
            uint yPlusOne = y + 1;
            if (xPlusOne > Constants.RegionSize - 1)
                xPlusOne = Constants.RegionSize - 1;
            if (yPlusOne > Constants.RegionSize - 1)
                yPlusOne = Constants.RegionSize - 1;

            Vector3 P0 = new Vector3(x,        y,        (float)map[x, y]);
            Vector3 P1 = new Vector3(xPlusOne, y,        (float)map[xPlusOne, y]);
            Vector3 P2 = new Vector3(x,        yPlusOne, (float)map[x, yPlusOne]);
            Vector3 P3 = new Vector3(xPlusOne, yPlusOne, (float)map[xPlusOne, yPlusOne]);

            // LL terrain triangles are divided like [/] rather than [\] ...
            // Vertex/triangle layout is:  P2 - P3
            //                              | / |
            //                             P0 - P1
            Vector3 normal0 = TriangleNormal(P2 - P3, P0 - P2);   // larger values first, so slope points down
            Vector3 normal1 = TriangleNormal(P1 - P0, P3 - P1);   // and counter-clockwise edge order
            Vector3 normal = (normal0 + normal1) / 2.0f;
            if ((normal.X == 0.0f) && (normal.Y == 0.0f))
            {
                // The two triangles are completely symmetric and will result in a vertical slope when averaged.
                // i.e. the location is on one side of a perfectly balanced ridge or valley.
                // So use only the triangle the location is in, so that it points into the valley or down the ridge.
                float xOffset = xPos - x;
                float yOffset = yPos - y;
                if ((xOffset + (1.0 - yOffset)) <= 1.0f)
                {   // Upper left (NW) triangle
                    normal = normal0;
                }
                else
                {   // Lower right (SE) triangle
                    normal = normal1;
                }
            }
            normal.Normalize();
            return normal;
        }

        public Vector3 Calculate4PointSlopeAt(float xPos, float yPos)  // 3-point triangle slope
        {
            return NormalToSlope(Calculate4PointNormalAt(xPos, yPos));
        }

        public int IncrementRevisionNumber()
        {
            return ++_revision;
        }

    }
}
