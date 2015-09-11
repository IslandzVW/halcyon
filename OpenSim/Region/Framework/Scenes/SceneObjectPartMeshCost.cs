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
using System.Reflection;
using System.IO;
using System.IO.Compression;

using log4net;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

using OpenSim.Framework;


namespace OpenSim.Region.Framework.Scenes
{
    public struct SceneObjectPartMeshCost
    {

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static OSDMap TryExtractOSDMapFromLLSDBinary(ref long start, byte[] bytes)
        {
            using (MemoryStream data = new MemoryStream(bytes))
            {
                try
                {
                    OSD osd = OSDParser.DeserializeLLSDBinary(data);
                    if (osd is OSDMap)
                    {
                        start = data.Position;
                        return (OSDMap)osd;
                    }
                    else
                    {
                        m_log.Warn("[MESH]: unable to cast mesh asset to OSDMap");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[MESH]: Exception deserializing mesh asset header: {0}", e);
                    return null;
                }
            }
        }

        private static OSDArray DecodeMeshLODArray(byte[] data, long start, OSDMap meshMap)
        {
            int offset = meshMap["offset"].AsInteger() + (int)start;
            int size = meshMap["size"].AsInteger();

            if (offset < 0 || size == 0)
                return null; // no mesh data in asset

            try
            {
                using (MemoryStream inMs = new MemoryStream(data, offset, size))
                {
                    /*
                     * Skipping past the first two bytes, which are part of the zlib specification 
                     * (RFC 1950), not the deflate specification (RFC 1951). Those bytes contain 
                     * information about the compression method and flags.
                     */
                    int streamType = inMs.ReadByte();
                    int streamFlags = inMs.ReadByte();

                    using (MemoryStream outMs = new MemoryStream())
                    {
                        using (DeflateStream zOut = new DeflateStream(inMs, CompressionMode.Decompress))
                        {
                            zOut.CopyTo(outMs);
                            byte[] decompressedBuf = outMs.ToArray();
                            return OSDParser.DeserializeLLSDBinary(decompressedBuf) as OSDArray;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MESH]: exception decoding OSD: {0}", e);
            }

            return null;
        }

        /// <summary>
        /// Add a submesh to an existing list of coords and faces.
        /// </summary>
        /// <param name="subMeshData"></param>
        /// <param name="size">Size of entire object</param>
        /// <param name="coords"></param>
        /// <param name="faces"></param>
        private static int DecodeMeshFace(OSDMap subMeshData)
        {
            // Console.WriteLine("subMeshMap for {0} - {1}", primName, Util.GetFormattedXml((OSD)subMeshMap));

            // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
            // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
            // geometry for this submesh.
            if (subMeshData.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshData["NoGeometry"]))
                return 0;

            int vertex_count = 0;
            if (subMeshData.ContainsKey("Position"))
            {
                byte[] posBytes = subMeshData["Position"].AsBinary();
                vertex_count = posBytes.Length / 6;
            }

            return vertex_count;
        }

        public static void GetMeshVertexCount(byte[] data, out int vertex_count)
        {
            vertex_count = 0;

            try
            {
                long start = 0;
                OSDMap map = TryExtractOSDMapFromLLSDBinary(ref start, data);

                if (map.ContainsKey("high_lod"))
                {
                    map = (OSDMap)map["high_lod"];
                    OSDArray lod_data = DecodeMeshLODArray(data, start, map);
                    foreach (OSD subMeshOsd in lod_data)
                    {
                        // We get a list of faces.  Sum each "face"
                        if (subMeshOsd is OSDMap)
                            vertex_count += DecodeMeshFace(subMeshOsd as OSDMap);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MESH]: exception meshing object: {0}", e);
                vertex_count = 0;
            }
        }

        public static void GetMeshLODByteCounts(byte[] mesh_data, out int hibytes, out int midbytes, out int lowbytes, out int lowestbytes)
        {
            hibytes = 0;
            midbytes = 0;
            lowbytes = 0;
            lowestbytes = 0; 
            
            try
            {
                long start = 0;
                OSDMap lodData = SceneObjectPartMeshCost.TryExtractOSDMapFromLLSDBinary(ref start, mesh_data);

                // Stash the data we need to recalculate streaming cost on scale changes.
                if (lodData.ContainsKey("high_lod"))
                    hibytes = ((OSDMap)lodData["high_lod"])["size"].AsInteger();
                if (lodData.ContainsKey("medium_lod"))
                    midbytes = ((OSDMap)lodData["medium_lod"])["size"].AsInteger();
                if (lodData.ContainsKey("low_lod"))
                    lowbytes = ((OSDMap)lodData["low_lod"])["size"].AsInteger();
                if (lodData.ContainsKey("lowest_lod"))
                    lowestbytes = ((OSDMap)lodData["lowest_lod"])["size"].AsInteger();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MESH]: exception extracting LOD ByteCounts: {0}", e);
            }
        }

    }
}
