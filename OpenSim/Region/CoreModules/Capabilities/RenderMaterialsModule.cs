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

#if false
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using zlib;

using Nini.Config;
using log4net;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using CapsUtil = OpenSim.Framework.CapsUtil;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

namespace OpenSim.Region.CoreModules.Capabilities
{
    public class RenderMaterialsModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private bool m_enabled;
        public Dictionary<UUID, OSDMap> m_knownMaterials = new Dictionary<UUID, OSDMap>();

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            m_enabled = (source.Configs["MaterialsModule"] != null &&
            source.Configs["MaterialsModule"].GetBoolean("Enabled", true));
            if (!m_enabled)
                return;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_scene = scene;

            m_scene = scene;
            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "RenderMaterialsModule"; }
        }

        #endregion INonSharedRegionModule Members

        private void OnRegisterCaps(UUID agentID, OpenSim.Framework.Communications.Capabilities.Caps caps)
        {
            string renderCap = CapsUtil.CreateCAPS("RenderMaterials", "");

            IRequestHandler renderMaterialsPostHandler
            = new GenericStreamHandler("POST", renderCap, (path, request, httpRequest, httpResponse) =>
                RenderMaterialsPostCap(path, request, httpRequest, httpResponse, agentID));
            MainServer.Instance.AddStreamHandler(renderMaterialsPostHandler);
            caps.RegisterHandler("RenderMaterials", renderMaterialsPostHandler);

            // OpenSimulator CAPs infrastructure seems to be somewhat hostile towards any CAP that requires both GET
            // and POST handlers, (at least at the time this was originally written), so we first set up a POST
            // handler normally and then add a GET handler via MainServer

            IRequestHandler renderMaterialsGetHandler =
                new GenericStreamHandler("GET", renderCap, RenderMaterialsGetCap);
            MainServer.Instance.AddStreamHandler(renderMaterialsGetHandler);

            IRequestHandler renderMaterialsPutHandler =
                new GenericStreamHandler("PUT", renderCap, (path, request, httpRequest, httpResponse) =>
                    RenderMaterialsPostCap(path, request, httpRequest, httpResponse, agentID));
            MainServer.Instance.AddStreamHandler(renderMaterialsPutHandler);
        }
        /*
        public OSDMap RegisterCaps(UUID agentID, IHttpServer server)
        {
            OSDMap retVal = new OSDMap();
            retVal["RenderMaterials"] = CapsUtil.CreateCAPS("RenderMaterials", "");
            server.AddStreamHandler(new GenericStreamHandler("POST", retVal["RenderMaterials"],
                                                             RenderMaterialsPostCap));
            server.AddStreamHandler(new GenericStreamHandler("GET", retVal["RenderMaterials"],
                                                             RenderMaterialsGetCap));
            server.AddStreamHandler(new GenericStreamHandler("PUT", retVal["RenderMaterials"],
                                                             RenderMaterialsPostCap));
            return retVal;
        }
*/

        public byte[] RenderMaterialsPostCap(
            string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse, UUID agentID)
        {
            m_log.Debug("[MaterialsDemoModule]: POST cap handler");

            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap resp = new OSDMap();

            OSDMap materialsFromViewer = null;

            OSDArray respArr = new OSDArray();

            if (req.ContainsKey("Zipped"))
            {
                OSD osd = null;

                byte[] inBytes = req["Zipped"].AsBinary();

                try
                {
                    osd = ZDecompressBytesToOsd(inBytes);

                    if (osd != null)
                    {
                        if (osd is OSDArray) // assume array of MaterialIDs designating requested material entries
                        {
                            foreach (OSD elem in (OSDArray)osd)
                            {
                                try
                                {
                                    UUID id = new UUID(elem.AsBinary(), 0);
                                    AssetBase materialAsset = null;
                                    if (m_knownMaterials.ContainsKey(id))
                                    {
                                        m_log.Info("[MaterialsDemoModule]: request for known material ID: " + id.ToString());
                                        OSDMap matMap = new OSDMap();
                                        matMap["ID"] = elem.AsBinary();

                                        matMap["Material"] = m_knownMaterials[id];
                                        respArr.Add(matMap);
                                    }
                                    else if ((materialAsset = m_scene.AssetService.Get(id.ToString())) != null)
                                    {
                                        m_log.Info("[MaterialsDemoModule]: request for stored material ID: " + id.ToString());
                                        OSDMap matMap = new OSDMap();
                                        matMap["ID"] = elem.AsBinary();

                                        matMap["Material"] = (OSDMap)OSDParser.DeserializeJson(
                                            Encoding.UTF8.GetString(materialAsset.Data));
                                        respArr.Add(matMap);
                                    }
                                    else
                                        m_log.Info("[MaterialsDemoModule]: request for UNKNOWN material ID: " + id.ToString());
                                }
                                catch (Exception)
                                {
                                    // report something here?
                                    continue;
                                }
                            }
                        }
                        else if (osd is OSDMap) // request to assign a material
                        {
                            materialsFromViewer = osd as OSDMap;

                            if (materialsFromViewer.ContainsKey("FullMaterialsPerFace"))
                            {
                                OSD matsOsd = materialsFromViewer["FullMaterialsPerFace"];
                                if (matsOsd is OSDArray)
                                {
                                    OSDArray matsArr = matsOsd as OSDArray;

                                    try
                                    {
                                        foreach (OSDMap matsMap in matsArr)
                                        {
                                            m_log.Debug("[MaterialsDemoModule]: processing matsMap: " + OSDParser.SerializeJsonString(matsMap));

                                            uint matLocalID = 0;
                                            try
                                            {
                                                matLocalID = matsMap["ID"].AsUInteger();
                                            }
                                            catch (Exception e)
                                            {
                                                m_log.Warn("[MaterialsDemoModule]: cannot decode \"ID\" from matsMap: " + e.Message);
                                            }
                                            m_log.Debug("[MaterialsDemoModule]: matLocalId: " + matLocalID.ToString());

                                            OSDMap mat = null;
                                            try
                                            {
                                                mat = matsMap["Material"] as OSDMap;
                                            }
                                            catch (Exception e)
                                            {
                                                m_log.Warn("[MaterialsDemoModule]: cannot decode \"Material\" from matsMap: " + e.Message);
                                            }
                                            m_log.Debug("[MaterialsDemoModule]: mat: " + OSDParser.SerializeJsonString(mat));

                                            UUID id = HashOsd(mat);
                                            m_knownMaterials[id] = mat;

                                            var sop = m_scene.GetSceneObjectPart(matLocalID);
                                            if (sop == null)
                                                m_log.Debug("[MaterialsDemoModule]: null SOP for localId: " + matLocalID.ToString());
                                            else
                                            {
                                                //var te = sop.Shape.Textures;
                                                var te = new Primitive.TextureEntry(sop.Shape.TextureEntry, 0, sop.Shape.TextureEntry.Length);

                                                if (te == null)
                                                {
                                                    m_log.Debug("[MaterialsDemoModule]: null TextureEntry for localId: " + matLocalID.ToString());
                                                }
                                                else
                                                {
                                                    int face = -1;

                                                    if (matsMap.ContainsKey("Face"))
                                                    {
                                                        face = matsMap["Face"].AsInteger();
                                                        if (te.FaceTextures == null) // && face == 0)
                                                        {
                                                            if (te.DefaultTexture == null)
                                                                m_log.Debug("[MaterialsDemoModule]: te.DefaultTexture is null");
                                                            else
                                                            {
                                                                //## FixMe ##
                                                                // comparison always results in 'False'                                   if (te.DefaultTexture.MaterialID == null)
                                                                //                                                                     m_log.Debug("[MaterialsDemoModule]: te.DefaultTexture.MaterialID is null");
                                                                //                                                                else
                                                                //                                                                {
                                                                te.DefaultTexture.MaterialID = id;
                                                                //                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (te.FaceTextures.Length >= face - 1)
                                                            {
                                                                if (te.FaceTextures[face] == null)
                                                                    te.DefaultTexture.MaterialID = id;
                                                                else
                                                                    te.FaceTextures[face].MaterialID = id;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (te.DefaultTexture != null)
                                                            te.DefaultTexture.MaterialID = id;
                                                    }

                                                    m_log.Debug("[MaterialsDemoModule]: setting material ID for face " + face.ToString() + " to " + id.ToString());

                                                    //we cant use sop.UpdateTextureEntry(te); because it filters so do it manually

                                                    if (sop.ParentGroup != null)
                                                    {
                                                        sop.Shape.TextureEntry = te.GetBytes();
                                                        sop.TriggerScriptChangedEvent(Changed.TEXTURE);
                                                        sop.ParentGroup.HasGroupChanged = true;

                                                        sop.ScheduleFullUpdate();

                                                        AssetBase asset = new AssetBase(id, "RenderMaterial",
                                                                              (sbyte)AssetType.Texture, sop.OwnerID.ToString())
                                                        {
                                                            Data = Encoding.UTF8.GetBytes(
                                                                OSDParser.SerializeJsonString(mat))
                                                        };
                                                        m_scene.AssetService.Store(asset);

                                                        StoreMaterialsForPart(sop);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        m_log.Warn("[MaterialsDemoModule]: exception processing received material: " + e.ToString());
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[MaterialsDemoModule]: exception decoding zipped CAP payload: " + e.ToString());
                    //return "";
                }
                m_log.Debug("[MaterialsDemoModule]: knownMaterials.Count: " + m_knownMaterials.Count.ToString());
            }

            resp["Zipped"] = ZCompressOSD(respArr, false);
            string response = OSDParser.SerializeLLSDXmlString(resp);

            // m_log.Debug("[MaterialsDemoModule]: cap request: " + request);
            m_log.Debug("[MaterialsDemoModule]: cap request (zipped portion): " + ZippedOsdBytesToString(req["Zipped"].AsBinary()));
            m_log.Debug("[MaterialsDemoModule]: cap response: " + response);
            return OSDParser.SerializeLLSDBinary(resp);
        }

        private void StoreMaterialsForPart(SceneObjectPart part)
        {
            try
            {
                if (part == null || part.Shape == null)
                    return;

                Dictionary<UUID, OSDMap> mats = new Dictionary<UUID, OSDMap>();

                Primitive.TextureEntry te = part.Shape.Textures;

                if (te.DefaultTexture != null)
                {
                    if (m_knownMaterials.ContainsKey(te.DefaultTexture.MaterialID))
                        mats[te.DefaultTexture.MaterialID] = m_knownMaterials[te.DefaultTexture.MaterialID];
                }

                if (te.FaceTextures != null)
                {
                    foreach (var face in te.FaceTextures)
                    {
                        if (face != null)
                        {
                            if (m_knownMaterials.ContainsKey(face.MaterialID))
                                mats[face.MaterialID] = m_knownMaterials[face.MaterialID];
                        }
                    }
                }
                if (mats.Count == 0)
                    return;

                OSDArray matsArr = new OSDArray();
                foreach (KeyValuePair<UUID, OSDMap> kvp in mats)
                {
                    OSDMap matOsd = new OSDMap();
                    matOsd["ID"] = OSD.FromUUID(kvp.Key);
                    matOsd["Material"] = kvp.Value;
                    matsArr.Add(matOsd);
                }

                lock (part.Shape.RenderMaterials)
                    part.Shape.RenderMaterials = RenderMaterials.FromOSD(matsArr);
            }
            catch (Exception e)
            {
                m_log.Warn("[MaterialsDemoModule]: exception in StoreMaterialsForPart(): " + e.ToString());
            }
        }

        public byte[] RenderMaterialsGetCap(string path, Stream request,
                                            OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            m_log.Debug("[MaterialsDemoModule]: GET cap handler");

            OSDMap resp = new OSDMap();

            int matsCount = 0;

            OSDArray allOsd = new OSDArray();

            foreach (KeyValuePair<UUID, OSDMap> kvp in m_knownMaterials)
            {
                OSDMap matMap = new OSDMap();

                matMap["ID"] = OSD.FromBinary(kvp.Key.GetBytes());

                matMap["Material"] = kvp.Value;
                allOsd.Add(matMap);
                matsCount++;
            }

            resp["Zipped"] = ZCompressOSD(allOsd, false);
            m_log.Debug("[MaterialsDemoModule]: matsCount: " + matsCount.ToString());

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

        /// <summary>
        /// computes a UUID by hashing a OSD object
        /// </summary>
        /// <param name="osd"></param>
        /// <returns></returns>
        private static UUID HashOsd(OSD osd)
        {
            using (var md5 = MD5.Create())
            using (MemoryStream ms = new MemoryStream(OSDParser.SerializeLLSDBinary(osd, false)))
                return new UUID(md5.ComputeHash(ms), 0);
        }

        public static OSD ZCompressOSD(OSD inOsd, bool useHeader)
        {
            OSD osd = null;

            using (MemoryStream msSinkCompressed = new MemoryStream())
            {
                using (ZOutputStream zOut = new ZOutputStream(msSinkCompressed))
                {
                    CopyStream(new MemoryStream(OSDParser.SerializeLLSDBinary(inOsd, useHeader)), zOut);
                    msSinkCompressed.Seek(0L, SeekOrigin.Begin);
                    osd = OSD.FromBinary(msSinkCompressed.ToArray());
                    zOut.Close();
                }
            }

            return osd;
        }

        public static OSD ZDecompressBytesToOsd(byte[] input)
        {
            OSD osd = null;

            using (MemoryStream msSinkUnCompressed = new MemoryStream())
            {
                using (ZInputStream zOut = new ZInputStream(msSinkUnCompressed))
                {
                    zOut.Read(input, 0, input.Length);
                    msSinkUnCompressed.Seek(0L, SeekOrigin.Begin);
                    osd = OSDParser.DeserializeLLSDBinary(msSinkUnCompressed.ToArray());
                    zOut.Close();
                }
            }

            return osd;
        }

        private static void CopyStream(System.IO.Stream input, System.IO.Stream output)
        {
            input.CopyTo(output);
            output.Flush();
        }
    }
}
#endif 