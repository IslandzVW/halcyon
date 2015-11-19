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

        /// <summary>
        /// RenderMaterialEntry - A node in our lookup cache. Records the material and the # of references.
        /// We use the reference count to decide when and entry and material can be removed from the cache.
        /// </summary>
        private struct RenderMaterialEntry
        {
            public RenderMaterial material;
            public List<uint> partIds;

            public RenderMaterialEntry(RenderMaterial mat, uint partId)
            {
                material = mat;
                partIds = new List<uint>();
                partIds.Add(partId);
            }
        }

        private Dictionary<UUID, RenderMaterialEntry> m_knownMaterials = new Dictionary<UUID, RenderMaterialEntry>();

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
            m_scene.EventManager.OnObjectAddedToScene += OnObjectAdded;
            m_scene.EventManager.OnObjectBeingRemovedFromScene += OnObjectRemoved;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene.EventManager.OnObjectAddedToScene -= OnObjectAdded;
            m_scene.EventManager.OnObjectBeingRemovedFromScene -= OnObjectRemoved;
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
                = new RestStreamHandler(
                    "POST", renderCap, 
                    (request, path, param, httpRequest, httpResponse) =>  RenderMaterialsPostCap(request, agentID),
                    "RenderMaterials", null);
            MainServer.Instance.AddStreamHandler(renderMaterialsPostHandler);
            caps.RegisterHandler("RenderMaterials", renderMaterialsPostHandler);

            IRequestHandler renderMaterialsGetHandler
                = new RestStreamHandler("GET", renderCap,
                    (request, path, param, httpRequest, httpResponse) => RenderMaterialsGetCap(request),
                    "RenderMaterials", null);
            MainServer.Instance.AddStreamHandler(renderMaterialsGetHandler);

            // materials viewer seems to use either POST or PUT, so assign POST handler for PUT as well
            IRequestHandler renderMaterialsPutHandler
                = new RestStreamHandler("PUT", renderCap,
                    (request, path, param, httpRequest, httpResponse) => RenderMaterialsPostCap(request, agentID),
                    "RenderMaterials", null);
            MainServer.Instance.AddStreamHandler(renderMaterialsPutHandler);
        }

        private void OnObjectAdded(SceneObjectGroup obj)
        {
            lock (m_knownMaterials)
            {
                foreach (SceneObjectPart part in obj.GetParts())
                {
                    // scan through the rendermaterials of this part for any textures used as materials
                    if ((part.Shape.TextureEntry == null) || (part.Shape.RenderMaterials == null))
                        continue;

                    var te = new Primitive.TextureEntry(part.Shape.TextureEntry, 0, part.Shape.TextureEntry.Length);
                    if (te == null)
                        continue;

                    var matIds = getMaterialIDsForPartFromTextureEntry(te);

                    foreach (var key in matIds)
                    {
                        if (m_knownMaterials.ContainsKey(key))
                        {
                            var entry = m_knownMaterials[key];
                            entry.partIds.Add(part.LocalId);
                            m_log.DebugFormat("[MaterialsModule]: KNOWN Material {0} for SOP {1}", key, part.LocalId);
                        }
                        else if (part.Shape.RenderMaterials.ContainsMaterial(key))
                        {
                            RenderMaterial mat = part.Shape.RenderMaterials.GetMaterial(key);
                            m_knownMaterials.Add(key, new RenderMaterialEntry(mat, part.LocalId));
                            m_log.DebugFormat("[MaterialsModule]: NEW Material {0} for SOP {1} ", key, part.LocalId);
                        }
                        else
                        {
                            m_log.ErrorFormat("[MaterialsModule]: ORPHANED Material {0} for SOP {1}!", key, part.LocalId);
                        }
                    }
                }
            }
        }

        private List<UUID> getMaterialIDsForPartFromTextureEntry(Primitive.TextureEntry te)
        {
            List<UUID> matIds = new List<UUID>();

            if (te != null)
            { 
                if ((te.DefaultTexture != null) && (te.DefaultTexture.MaterialID != UUID.Zero))
                    matIds.Add(te.DefaultTexture.MaterialID);

                foreach (var face in te.FaceTextures)
                {
                    if ((face != null) && (face.MaterialID != UUID.Zero))
                        matIds.Add(face.MaterialID);
                }
            }

            return matIds;
        }

        private void OnObjectRemoved(SceneObjectGroup obj)
        {
            lock (m_knownMaterials)
            {
                foreach (SceneObjectPart part in obj.GetParts())
                {
                    // scan through the rendermaterials of this part for any textures used as materials
                    if ((part.Shape.TextureEntry == null) || (part.Shape.RenderMaterials == null))
                        continue;

                    var te = new Primitive.TextureEntry(part.Shape.TextureEntry, 0, part.Shape.TextureEntry.Length);
                    if (te == null)
                        continue;

                    var matIds = getMaterialIDsForPartFromTextureEntry(te);

                    m_log.DebugFormat("[MaterialsModule]: OnObjectRemoved for SOP {0}:  {1}", part.LocalId, matIds.ToString());

                    foreach (var key in matIds)
                    {
                        if (m_knownMaterials.ContainsKey(key))
                        {
                            var entry = m_knownMaterials[key];
                            entry.partIds.Remove(part.LocalId);
                            if (entry.partIds.Count <= 0)
                            {
                                m_knownMaterials.Remove(key);
                                m_log.DebugFormat("[MaterialsModule]: OnObjectRemoved Material {0} not referenced. Removed from cache.", key);
                            }
                        }
                    }
                }
            }
        }

        public string RenderMaterialsPostCap(string request, UUID agentID)
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
                        ReturnRequestedMaterials(ref respArr, osd as OSDArray);
                    }
                    else if (osd is OSDMap) // request to assign a material
                    {
                        AssignRequestedMaterials(osd as OSDMap, agentID);
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[RenderMaterials]: exception decoding zipped CAP payload: " + e.ToString());
                }
            }

            resp["Zipped"] = ZCompressOSD(respArr, false);
            return OSDParser.SerializeLLSDXmlString(resp);
        }

        /// <summary>
        /// Assign The requested materials to the specified part(s).
        /// </summary>
        /// <param name="materialsFromViewer"></param>
        private void AssignRequestedMaterials(OSDMap materialsFromViewer, UUID agentID)
        {
            if (!(materialsFromViewer.ContainsKey("FullMaterialsPerFace") &&
                  (materialsFromViewer["FullMaterialsPerFace"] is OSDArray)))
            {
                m_log.Warn("[RenderMaterials]: AssignRequestedMaterials - FullMaterialsPerFace not defined or incorrect type");
                return;
            }

            OSDArray mats = materialsFromViewer["FullMaterialsPerFace"] as OSDArray;

            foreach (OSDMap matsMap in mats)
            {
                uint matLocalID = 0;
                int face = -1;
                OSDMap matData = null;

                try
                {
                    // m_log.Debug("[RenderMaterials]: processing matsMap: " + OSDParser.SerializeJsonString(matsMap));
                    matLocalID = matsMap["ID"].AsUInteger();

                    if (matsMap.ContainsKey("Face"))
                        face = matsMap["Face"].AsInteger();

                    if (matsMap.ContainsKey("Material"))
                        matData = matsMap["Material"] as OSDMap;
                }
                catch (Exception e)
                {
                    m_log.Warn("[RenderMaterials]: cannot decode material from matsMap: " + e.Message);
                    continue;
                }

                /// Look up the scene object this material pertains to.
                var sop = m_scene.GetSceneObjectPart(matLocalID);
                if (sop == null)
                {
                    m_log.Warn("[RenderMaterials]: null SOP for localId: " + matLocalID.ToString());
                    continue;
                }

                // Make sure we can modify it
                if (m_scene.Permissions.CanEditObject(sop.UUID, agentID, (uint)PermissionMask.Modify) == false)
                {
                    m_log.WarnFormat("[RenderMaterials]: User {0} can't edit object {1} {2}", agentID, sop.Name, sop.UUID);
                    continue;
                }

                AssignSingleMaterial(sop, face, matData);
            }
        }

        /// <summary>
        /// Assign a single material value.  Based on the values passed we'll either set (or clear) the materials for a SOP.
        /// </summary>
        /// <param name="sop">The SOP being affected.</param>
        /// <param name="face">The face to assign, or -1 if the default texture is being set.</param>
        /// <param name="id">The ID assigned to this material.  Setting a Zero UUID clears it.</param>
        /// <param name="material">If not null, the material to set.  Otherwise we are clearing.</param>
        private void AssignSingleMaterial(SceneObjectPart sop, int face, OSDMap matData)
        {
            /// Get a copy of the texture entry so we can make changes.
            var te = new Primitive.TextureEntry(sop.Shape.TextureEntry, 0, sop.Shape.TextureEntry.Length);
            if (te == null)
            {
                m_log.Warn("[RenderMaterials]: null TextureEntry for localId: " + sop.LocalId.ToString());
                return;
            }

            lock (m_knownMaterials)
            {
                UUID id = UUID.Zero;

                // Record what we currently have
                var currentMaterialIDs = getMaterialIDsForPartFromTextureEntry(te);

                // If there is a material being set see if we've seen it before.
                // If not we'll add it to the region cache
                if (matData != null)
                {
                    RenderMaterial material = RenderMaterial.FromOSD(matData);
                    id = sop.Shape.RenderMaterials.AddMaterial(material);

                    if (m_knownMaterials.ContainsKey(id))
                    {
                        material = m_knownMaterials[id].material;
                        m_knownMaterials[id].partIds.Add(sop.LocalId);
                    }
                    else
                    {
                        m_knownMaterials[id] = new RenderMaterialEntry(material, sop.LocalId);
                    }
                }

                // Set the Material ID in the sop texture entry. If there's a face defined
                // use that else use the Default entry.  ID can either be a material ID
                // we set from the lookup above or Zero when we are clearing a material.
                if (face < 0)
                {
                    te.DefaultTexture.MaterialID = id;
                }
                else
                {
                    var faceEntry = te.CreateFace((uint)face);
                    faceEntry.MaterialID = id;
                }

                // Get the updated list of Materials from the TextureEntry.  We will
                // Use that to rebuild the RenderMaterials for the part. We'll also get a list
                // of entries that have been removed based on what we fetched initially so we
                // can clean that up in our cache.
                var newMaterialIDs = getMaterialIDsForPartFromTextureEntry(te);
                var materials = new List<RenderMaterial>();

                // If there was an update and the material id has changed, clean up the old value.  
                // Have to be careful here. It might still be in use in another slot.  So we build 
                // a list of keys and walk the texture entries subtracting keys in use.  Whatever
                // is left are candidates to clean up.
                foreach (var entry in newMaterialIDs)
                {
                    if (currentMaterialIDs.Contains(entry))
                        currentMaterialIDs.Remove(entry);
                    if (m_knownMaterials.ContainsKey(entry))
                        materials.Add(m_knownMaterials[entry].material);
                }

                // Set the new materials list into the part.
                sop.Shape.RenderMaterials.SetMaterials(materials);

                // currentMaterialsIDs contains orphans if any.  Remove them from the cache
                foreach (var entry in currentMaterialIDs)
                {
                    if (m_knownMaterials.ContainsKey(entry))
                    {
                        m_knownMaterials[entry].partIds.Remove(sop.LocalId);
                        if (m_knownMaterials[entry].partIds.Count <= 0)
                            m_knownMaterials.Remove(entry);
                    }
                }
            }

            // Update the texture entry which will force an update to connected clients
            sop.UpdateTextureEntry(te.GetBytes());
        }


        /// <summary>
        /// Given an array of Materials IDs return the data to the caller.
        /// </summary>
        /// <param name="respArr"></param>
        /// <param name="requestedMaterials"></param>
        private void ReturnRequestedMaterials(ref OSDArray respArr, OSDArray requestedMaterials)
        {
            lock (m_knownMaterials)
            {
                foreach (OSD elem in requestedMaterials)
                {
                    UUID id = new UUID(elem.AsBinary(), 0);
                    if (m_knownMaterials.ContainsKey(id))
                    {
                        m_log.Debug("[RenderMaterials]: request for known material ID: " + id.ToString());

                        var matEntry = m_knownMaterials[id];
                        OSDMap matMap = new OSDMap();
                        matMap["ID"] = elem.AsBinary();
                        matMap["Material"] = matEntry.material.GetOSD() as OSDMap;
                        respArr.Add(matMap);
                    }
                    else
                    {
                        m_log.Warn("[RenderMaterials]: request for UNKNOWN material ID: " + id.ToString());
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

        public string RenderMaterialsGetCap(string request)
        {
            OSDArray allOsd = new OSDArray();

            lock (m_knownMaterials)
            {
                foreach (KeyValuePair<UUID, RenderMaterialEntry> kvp in m_knownMaterials)
                {
                    OSDMap matMap = new OSDMap();
                    matMap["ID"] = OSD.FromBinary(kvp.Key.GetBytes());
                    matMap["Material"] = kvp.Value.material.GetOSD() as OSDMap;
                    allOsd.Add(matMap);
                }
            }

            m_log.Debug("[RenderMaterials]: matsCount: " + allOsd.Count.ToString());

            OSDMap resp = new OSDMap();
            resp["Zipped"] = ZCompressOSD(allOsd, false);
            return OSDParser.SerializeLLSDXmlString(resp);
        }

        public static OSD ZCompressOSD(OSD inOSD, bool useHeader)
        {
            byte[] inData = OSDParser.SerializeLLSDBinary(inOSD, useHeader);
            OSD osd = null;

            try
            {
                using (MemoryStream outMemoryStream = new MemoryStream())
                using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
                using (Stream inMemoryStream = new MemoryStream(inData))
                {
                    CopyStream(inMemoryStream, outZStream);
                    outZStream.finish();
                    osd = OSD.FromBinary(outMemoryStream.ToArray());
                }
            }
            catch (Exception e)
            {
                m_log.Debug("[RenderMaterials]: Exception in ZCompressBytesToOSD: " + e.ToString());
            }

            return osd;
        }

        public static OSD ZDecompressBytesToOsd(byte[] inData)
        {
            OSD osd = null;

            try
            {
                using (MemoryStream outMemoryStream = new MemoryStream())
                using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream))
                using (Stream inMemoryStream = new MemoryStream(inData))
                {
                    CopyStream(inMemoryStream, outZStream);
                    outZStream.finish();
                    osd = OSDParser.DeserializeLLSDBinary(outMemoryStream.ToArray());
                }
            }
            catch (Exception e)
            {
                m_log.Debug("[RenderMaterials]: Exception in ZDecompressBytesToOSD: " + e.ToString());
            }

            return osd;
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