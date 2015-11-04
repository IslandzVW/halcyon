/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
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
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using zlib;

using Nini.Config;
using Mono.Addins;
using log4net;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using CapsUtil = OpenSim.Framework.CapsUtil;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RenderMaterialsModule")]
    public class RenderMaterialsModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        public Dictionary<UUID, RenderMaterial> m_knownMaterials = new Dictionary<UUID, RenderMaterial>();

        /// <summary>
        /// Is this module enabled?
        /// </summary>
        public bool Enabled { get; private set; }

        #region INonSharedRegionModule Members

        public RenderMaterialsModule()
        {
            Enabled = true;
        }

        public void Initialize(IConfigSource source)
        {
            IConfig config = source.Configs["RenderMaterialsModule"];
            if (config != null)
                Enabled = config.GetBoolean("Enabled", true);
        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            m_scene = scene;
            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialize()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "RenderMaterialsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion INonSharedRegionModule Members

        private void OnRegisterCaps(UUID agentID, OpenSim.Framework.Communications.Capabilities.Caps caps)
        {
            string renderCap = CapsUtil.CreateCAPS("RenderMaterials", "");

            // OpenSimulator CAPs infrastructure seems to be somewhat hostile towards any CAP that requires both GET
            // and POST handlers, so we first set up a POST handler normally and then add a GET/PUT handler via MainServer

            IRequestHandler renderMaterialsPostHandler
            = new GenericStreamHandler("POST", renderCap, (path, request, httpRequest, httpResponse) =>
                RenderMaterialsPostCap(path, request, httpRequest, httpResponse, agentID));
            MainServer.Instance.AddStreamHandler(renderMaterialsPostHandler);
            caps.RegisterHandler("RenderMaterials", renderMaterialsPostHandler);

            IRequestHandler renderMaterialsGetHandler =
                new GenericStreamHandler("GET", renderCap, RenderMaterialsGetCap);
            MainServer.Instance.AddStreamHandler(renderMaterialsGetHandler);

            IRequestHandler renderMaterialsPutHandler =
                new GenericStreamHandler("PUT", renderCap, (path, request, httpRequest, httpResponse) =>
                    RenderMaterialsPostCap(path, request, httpRequest, httpResponse, agentID));
            MainServer.Instance.AddStreamHandler(renderMaterialsPutHandler);
        }


        public byte[] RenderMaterialsPostCap(
            string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse, UUID agentID)
        {
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap resp = new OSDMap();
            OSDArray respArr = new OSDArray();

            if (req.ContainsKey("Zipped"))
            {
                byte[] inBytes = req["Zipped"].AsBinary();

                try
                {
                    OSD osd = ZDecompressBytesToOsd(inBytes);
                    if (osd == null)
                    {
                        m_log.Warn("[MaterialsModule]: Decoded Materials request is empty");
                    }
                    else if (osd is OSDArray) // assume array of MaterialIDs designating requested material entries
                    {
                        ReturnRequestedMaterials(respArr, osd as OSDArray);
                    }
                    else if (osd is OSDMap) // request to assign a material
                    {
                        AssignRequestedMaterials(osd as OSDMap);
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[MaterialsDemoModule]: exception decoding zipped CAP payload: " + e.ToString());
                }
            }

            resp["Zipped"] = ZCompressOSD(respArr, false);
            string response = OSDParser.SerializeLLSDXmlString(resp);

            m_log.Debug("[MaterialsDemoModule]: cap request: " + request);
            m_log.Debug("[MaterialsDemoModule]: cap request (zipped portion): " + ZippedOsdBytesToString(req["Zipped"].AsBinary()));
            m_log.Debug("[MaterialsDemoModule]: cap response: " + response);

            return OSDParser.SerializeLLSDBinary(resp);
        }

        /// <summary>
        /// Assign The requested materials to the specified part(s).
        /// </summary>
        /// <param name="materialsFromViewer"></param>
        private void AssignRequestedMaterials(OSDMap materialsFromViewer)
        {
            if (!(materialsFromViewer.ContainsKey("FullMaterialsPerFace") &&
                  (materialsFromViewer["FullMaterialsPerFace"] is OSDArray)))
            {
                m_log.Warn("[MaterialsDemoModule]: AssignRequestedMaterials - FullMaterialsPerFace not defined or incorrect type");
                return;
            }

            OSDArray mats = materialsFromViewer["FullMaterialsPerFace"] as OSDArray;

            foreach (OSDMap matsMap in mats)
            {
                uint matLocalID = 0;
                int face = -1;
                UUID id = UUID.Zero;
                RenderMaterial material = null;

                try
                {
                    // m_log.Debug("[MaterialsDemoModule]: processing matsMap: " + OSDParser.SerializeJsonString(matsMap));
                    matLocalID = matsMap["ID"].AsUInteger();

                    if (matsMap.ContainsKey("Face"))
                        face = matsMap["Face"].AsInteger();

                    if (matsMap.ContainsKey("Material"))
                    {
                        material = RenderMaterial.FromOSD(matsMap["Material"] as OSDMap);
                        id = UUID.Random();
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[MaterialsDemoModule]: cannot decode material from matsMap: " + e.Message);
                    continue;
                }

                /// Look up the scene object this material pertains to.
                var sop = m_scene.GetSceneObjectPart(matLocalID);
                if (sop == null)
                {
                    m_log.Warn("[MaterialsDemoModule]: null SOP for localId: " + matLocalID.ToString());
                    continue;
                }

                AssignSingleMaterial(sop, face, id, material);
            }
        }

        /// <summary>
        /// Assign a single material value.  Based on the values passed we'll either set (or clear) the materials for a SOP.
        /// </summary>
        /// <param name="sop">The SOP being affected.</param>
        /// <param name="face">The face to assign, or -1 if the default texture is being set.</param>
        /// <param name="id">The ID assigned to this material.  Setting a Zero UUID clears it.</param>
        /// <param name="material">If not null, the material to set.  Otherwise we are clearing.</param>
        private void AssignSingleMaterial(SceneObjectPart sop, int face, UUID id, RenderMaterial material)
        {
            /// Get a copy of the texture entry so we can make changes.
            var te = new Primitive.TextureEntry(sop.Shape.TextureEntry, 0, sop.Shape.TextureEntry.Length);
            if (te == null)
            {
                m_log.Warn("[MaterialsDemoModule]: null TextureEntry for localId: " + sop.LocalId.ToString());
                return;
            }

            // If we are doing a replace this will get set with the old valie.
            UUID currentMatId = UUID.Zero;

            if (face < 0)
            {
                if (te.DefaultTexture != null)
                {
                    currentMatId = te.DefaultTexture.MaterialID;
                    te.DefaultTexture.MaterialID = id;
                }
            }
            else
            {
                if (te.FaceTextures.Length >= face - 1)
                {
                    if (te.FaceTextures[face] == null)
                    {
                        currentMatId = te.DefaultTexture.MaterialID;
                        te.DefaultTexture.MaterialID = id;
                    }
                    else
                    {
                        currentMatId = te.FaceTextures[face].MaterialID;
                        te.FaceTextures[face].MaterialID = id;
                    }
                }
            }

            lock (m_knownMaterials)
            {
                if (currentMatId != UUID.Zero)
                {
                    m_knownMaterials.Remove(currentMatId);
                    sop.Shape.RenderMaterials.Materials.Remove(currentMatId.ToString());
                }

                // If material is null we're just clearing nothing to set. 
                // Otherwise set the new value in the Shape and in known materials.
                if (material != null)
                {
                    m_knownMaterials[id] = material;
                    sop.Shape.RenderMaterials.Materials[id.ToString()] = material;
                }
            }

            // U[pdate the texture entry which will force an update to connected clients
            sop.UpdateTextureEntry(te.GetBytes());
        }


        /// <summary>
        /// Given an array of Materials IDs return the data to the caller.
        /// </summary>
        /// <param name="respArr"></param>
        /// <param name="requestedMaterials"></param>
        private void ReturnRequestedMaterials(OSDArray respArr, OSDArray requestedMaterials)
        {
            lock (m_knownMaterials)
            {
                foreach (OSD elem in requestedMaterials)
                {
                    UUID id = new UUID(elem.AsBinary(), 0);
                    if (m_knownMaterials.ContainsKey(id))
                    {
                        m_log.Debug("[MaterialsDemoModule]: request for known material ID: " + id.ToString());

                        OSDMap matMap = new OSDMap();
                        matMap["ID"] = elem.AsBinary();
                        matMap["Material"] = m_knownMaterials[id].GetOSD();
                        respArr.Add(matMap);
                    }
                    else
                    {
                        m_log.Warn("[MaterialsDemoModule]: request for UNKNOWN material ID: " + id.ToString());
                    }
                }
            }
        }


        /// <summary>
        /// GET request. Return all the materials we know about.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        public byte[] RenderMaterialsGetCap(
            string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse
            )
        {
            OSDMap resp = new OSDMap();
            int matsCount = 0;
            OSDArray allOsd = new OSDArray();

            lock (m_knownMaterials)
            {
                foreach (KeyValuePair<UUID, RenderMaterial> kvp in m_knownMaterials)
                {
                    OSDMap matMap = new OSDMap();
                    matMap["ID"] = OSD.FromBinary(kvp.Key.GetBytes());
                    matMap["Material"] = kvp.Value.GetOSD();
                    allOsd.Add(matMap);
                    matsCount++;
                }
            }

            m_log.Debug("[MaterialsDemoModule]: matsCount: " + matsCount.ToString());

            resp["Zipped"] = ZCompressOSD(allOsd, false);
            return OSDParser.SerializeLLSDBinary(resp);
        }


        private static string ZippedOsdBytesToString(byte[] bytes)
        {
            try
            {
                return OSDParser.SerializeJsonString(ZDecompressBytesToOsd(bytes));
            }
            catch (Exception e)
            {
                return "ZippedOsdBytesToString caught an exception: " + e.ToString();
            }
        }

        public static OSD ZCompressOSD(OSD inOSD, bool useHeader)
        {
            byte[] inData = OSDParser.SerializeLLSDBinary(inOSD, useHeader);

            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
            using (Stream inMemoryStream = new MemoryStream(inData))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.finish();
                return OSD.FromBinary(outMemoryStream.ToArray());
            }
        }

        public static OSD ZDecompressBytesToOsd(byte[] inData)
        {
            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream))
            using (Stream inMemoryStream = new MemoryStream(inData))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.finish();
                return OSDParser.DeserializeLLSDBinary(outMemoryStream.ToArray());
            }
        }

        public static void CopyStream(System.IO.Stream input, System.IO.Stream output)
        {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }
            output.Flush();
        }
    }
}