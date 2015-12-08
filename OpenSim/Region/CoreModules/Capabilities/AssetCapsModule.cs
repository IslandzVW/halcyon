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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Collections;
using System.Reflection;
using System.Net;

using log4net;
using Nini.Config;
using Mono.Addins;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Framework.Communications.Capabilities;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

namespace OpenSim.Region.CoreModules.Capabilities
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class AssetCapsModule : INonSharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;
        private IAssetCache m_AssetCache;

        private bool m_useAperture;
        private string m_apPort;
        private string m_apToken;

        private const string ADD_CAPS_TOKEN_URL = "/CAPS/HTT/ADDCAP/";
        private const string REM_CAPS_TOKEN_URL = "/CAPS/HTT/REMCAP/";
        private const string PAUSE_TOKEN_URL = "/CAPS/HTT/PAUSE/";
        private const string RESUME_TOKEN_URL = "/CAPS/HTT/RESUME/";
        private const string LIMIT_TOKEN_URL = "/CAPS/HTT/LIMIT/";

        private const string m_uploadBakedTexturePath = "0010/";

#region IRegionModuleBase Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialize(IConfigSource source)
        {
            m_useAperture = false;
            IConfig startupConfig = source.Configs["Startup"];
            if (startupConfig == null)
                return;

            if (startupConfig.GetString("use_aperture_server", "no") == "yes")
            {
                m_useAperture = true;
                m_apPort = startupConfig.GetString("aperture_server_port", "8000");
                m_apToken = startupConfig.GetString("aperture_server_caps_token", String.Empty);
            }
            else
            {
                m_useAperture = false;
                m_log.InfoFormat("[APERTURE] Not contacting server, configuration for use_aperture_server={0}", m_useAperture);
            }
        }

        public void AddRegion(Scene pScene)
        {
            m_Scene = pScene;
            m_AssetCache = m_Scene.CommsManager.AssetCache;
        }

        public void RemoveRegion(Scene scene)
        {
            m_Scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_Scene.EventManager.OnDeregisterCaps -= DeregisterCaps;
            m_Scene = null;
            m_AssetCache = null;
        }

        public void RegionLoaded(Scene scene)
        {
            m_Scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_Scene.EventManager.OnDeregisterCaps += DeregisterCaps;
        }

        public void Close() 
        { 
        }

        public string Name 
        { 
            get { return "AssetCapsModule"; } 
        }

        #endregion

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capID = UUID.Random();

            try
            {
                if (m_useAperture == true)
                {
                    string externalBaseURL = GetApertureBaseURL(caps);
                    string externalURL = GetApertureHttUrl(caps, capID);
                    string addCapURL = externalBaseURL + ADD_CAPS_TOKEN_URL + m_apToken + "/" + capID.ToString();

                    WebRequest req = WebRequest.Create(addCapURL);
                    HttpWebResponse response = (HttpWebResponse)req.GetResponse();

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Got response '" + response.StatusDescription + "' while trying to register CAPS with HTT");
                    }

                    //register this cap url with the server
                    caps.RegisterHandler("GetTexture", externalURL, 
                        () => this.PauseAperture(caps, capID), 
                        () => this.ResumeAperture(caps, capID),
                        (int bwMax) => this.SetApertureBandwidth(caps, capID, bwMax));
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[APERTURE] Could not contact the aperture texture server to register caps on region {0}. Server returned error {1}", 
                    caps.RegionName, e.Message);
            }

            IRequestHandler requestHandler;

            ISimulatorFeaturesModule SimulatorFeatures = m_Scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if ((SimulatorFeatures != null) && (SimulatorFeatures.MeshEnabled == true))
            {
                //use the same cap ID for mesh and HTT. That way the token bucket in aperture will balance the 
                //available bandwidth between mesh and http textures
                //capID = UUID.Random();
                
                bool getMeshCapRegistered = false;

                try
                {
                    if (m_useAperture == true)
                    {
                        string externalBaseURL = GetApertureBaseURL(caps);
                        string externalURL = GetApertureHttUrl(caps, capID);

                        //register this cap url with the server
                        caps.RegisterHandler("GetMesh", externalURL); //caps control for the texture server will apply to pause mesh as well
                        getMeshCapRegistered = true;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[APERTURE] Could not contact the aperture texture server to register caps on region {0}. Server returned error {1}",
                        caps.RegionName, e.Message);
                }

                if (getMeshCapRegistered == false)
                {
                    // m_log.DebugFormat("[GETMESH]: /CAPS/{0} in region {1}", capID, m_scene.RegionInfo.RegionName);
                    GetMeshHandler gmeshHandler = new GetMeshHandler(m_Scene, agentID, caps);
                    requestHandler = new RestHTTPHandler(
                            "GET", "/CAPS/" + UUID.Random(),
                            httpMethod => gmeshHandler.ProcessGetMesh(httpMethod, UUID.Zero, null));
                    caps.RegisterHandler("GetMesh", requestHandler);
                }
            }

            // Upload Baked Texture
            UploadBakedTextureHandler uploadHandler = new UploadBakedTextureHandler(m_Scene, caps);
            requestHandler = new RestStreamHandler("POST", "/CAPS/" + caps.CapsObjectPath + m_uploadBakedTexturePath, uploadHandler.UploadBakedTexture);
            caps.RegisterHandler("UploadBakedTexture", requestHandler);

            requestHandler = new RestStreamHandler("POST", caps.CapsBase + "/" + UUID.Random(), GetObjectCostHandler);
            caps.RegisterHandler("GetObjectCost", requestHandler);

            requestHandler = new RestStreamHandler("POST", caps.CapsBase + "/" + UUID.Random(), ResourceCostsSelected);
            caps.RegisterHandler("ResourceCostSelected", requestHandler);

            requestHandler = new RestStreamHandler("POST", caps.CapsBase + "/" + UUID.Random(), GetObjectPhysicsDataHandler);
            caps.RegisterHandler("GetObjectPhysicsData", requestHandler);
        }

        private void SetApertureBandwidth(Caps caps, UUID capID, int bwMax)
        {
            string externalBaseURL = GetApertureBaseURL(caps);
            string addCapURL = externalBaseURL + LIMIT_TOKEN_URL + m_apToken + "/" + capID.ToString() + "/" + bwMax.ToString();

            WebRequest req = WebRequest.Create(addCapURL);
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Got response '" + response.StatusDescription + "' while trying to limit aperture services");
            }
        }

        private void ResumeAperture(Caps caps, UUID capID)
        {
            string externalBaseURL = GetApertureBaseURL(caps);
            string addCapURL = externalBaseURL + RESUME_TOKEN_URL + m_apToken + "/" + capID.ToString();

            WebRequest req = WebRequest.Create(addCapURL);
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Got response '" + response.StatusDescription + "' while trying to resume aperture services");
            }
        }

        private void PauseAperture(Caps caps, UUID capID)
        {
            string externalBaseURL = GetApertureBaseURL(caps);
            string addCapURL = externalBaseURL + PAUSE_TOKEN_URL + m_apToken + "/" + capID.ToString();

            WebRequest req = WebRequest.Create(addCapURL);
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Got response '" + response.StatusDescription + "' while trying to pause aperture services");
            }
        }

        private string GetApertureHttUrl(Caps caps, UUID capID)
        {
            string externalURL = GetApertureBaseURL(caps) + "/CAPS/HTT/" + capID.ToString();
            return externalURL;
        }

        private string GetApertureBaseURL(Caps caps)
        {
            string externalBaseURL = caps.HostName + ":" + m_apPort;
            return externalBaseURL;
        }

        public void DeregisterCaps(UUID agentID, Caps caps)
        {
            if (m_useAperture == true)
            {
                string[] deregister = {"GetTexture", "GetMesh"};

                foreach (string which in deregister)
                {
                    DoDeregisterSingleApertureCap(caps, which);
                }
            }
        }

        private void DoDeregisterSingleApertureCap(Caps caps, string which)
        {
            try
            {
                string externalBaseURL = caps.HostName + ":" + m_apPort;
                string externalURL = caps.CapsHandlers[which].ExternalHandlerURL;
                string capuuid = externalURL.Replace(externalBaseURL + "/CAPS/HTT/", String.Empty);
                UUID capID = UUID.Zero;

                // parse the path and search for the avatar with it registered
                if (UUID.TryParse(capuuid, out capID))
                {
                    string remCapURL = externalBaseURL + REM_CAPS_TOKEN_URL + m_apToken + "/" + capID.ToString();
                    WebRequest req = WebRequest.Create(remCapURL);
                    HttpWebResponse response = (HttpWebResponse)req.GetResponse();

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Got response '" + response.StatusDescription + "' while trying to deregister CAPS with HTT");
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[APERTURE] Could not contact the aperture texture server to deregister caps on region {0}. Server returned error {1}",
                    caps.RegionName, e.Message);
            }
        }

        protected class UploadBakedTextureHandler
        {
//            private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            private Scene m_Scene;
            private Caps m_Caps;

            public UploadBakedTextureHandler(Scene scene, Caps caps)
            {
                m_Scene = scene;
                m_Caps = caps;
            }

            /// <summary>
            /// Handle a request from the client for a Uri to upload a baked texture.
            /// </summary>
            /// <param name="request"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <param name="httpRequest"></param>
            /// <param name="httpResponse"></param>
            /// <returns>The upload response if the request is successful, null otherwise.</returns>
            public string UploadBakedTexture(
                string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                try
                {
                    IAssetCache assetCache = m_Scene.CommsManager.AssetCache;
                    IJ2KDecoder layerDecoder = m_Scene.RequestModuleInterface<IJ2KDecoder>();
                    String uploaderPath = m_Caps.CapsBase + "/" + UUID.Random();
                    BakedTextureUploader uploader = new BakedTextureUploader(m_Caps, uploaderPath, assetCache, layerDecoder);
                    m_Caps.HttpListener.AddStreamHandler(new BinaryStreamHandler("POST", uploaderPath, uploader.BakedTextureUploaded));

                    string uploaderURL = m_Caps.HttpListener.ServerURI + uploaderPath;
                    LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                    uploadResponse.uploader = uploaderURL;
                    uploadResponse.state = "upload";
                    return LLSDHelpers.SerializeLLSDReply(uploadResponse);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[UPLOAD BAKED TEXTURE HANDLER]: {0}{1}", e.Message, e.StackTrace);
                }

                return null;
            }

            protected class BakedTextureUploader
            {
                private Caps m_Caps;
                private string m_uploaderPath;
                private IAssetCache m_assetCache;
                private IJ2KDecoder m_layerDecoder;

                public BakedTextureUploader(Caps caps, string path, IAssetCache assetCache, IJ2KDecoder layerDecoder)
                {
                    m_uploaderPath = path;
                    m_Caps = caps;
                    m_assetCache = assetCache;
                    m_layerDecoder = layerDecoder;
                }

                /// <summary>
                /// Handle raw uploaded baked texture data.
                /// </summary>
                /// <param name="data"></param>
                /// <param name="path"></param>
                /// <param name="param"></param>
                /// <returns></returns>
                public string BakedTextureUploaded(byte[] data, string path, string param)
                {
                    String result;
                    bool decodeFailed = false;
                    UUID newAssetID = UUID.Random();

                    if (data.Length <= 0)
                    {
                        m_log.ErrorFormat("[CAPS]: Invalid length {0} on UploadBakeRequestPut for {1}", data.Length, path);
                        decodeFailed = true;
                    }
                    else if (m_layerDecoder != null)
                    {
                        decodeFailed = (m_layerDecoder.Decode(newAssetID, data) == false);
                    }

                    if (decodeFailed)
                    {
                        Hashtable badReply = new Hashtable();
                        badReply["state"] = "error";
                        badReply["new_asset"] = UUID.Zero;
                        result = LLSDHelpers.SerializeLLSDReply(badReply);
                    }
                    else
                    {
                        AssetBase asset = new AssetBase(newAssetID, "Baked Texture", (sbyte)AssetType.Texture, m_Caps.AgentID.ToString());
                        asset.Data = data;
                        //Persist baked textures as we will use them in the baked texture cache
                        //asset.Temporary = true;
                        asset.Local = true;
                        m_assetCache.AddAsset(asset, AssetRequestInfo.GenericNetRequest());

                        LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                        uploadComplete.new_asset = newAssetID.ToString();
                        uploadComplete.new_inventory_item = UUID.Zero;
                        uploadComplete.state = "complete";

                        result = LLSDHelpers.SerializeLLSDReply(uploadComplete);
                        // m_log.DebugFormat("[BAKED TEXTURE UPLOADER]: baked texture upload completed for {0}", newAssetID);
                    }

                    m_Caps.HttpListener.RemoveStreamHandler("POST", m_uploaderPath);
                    return (result);
                }
            }
        }

        protected string GetObjectCostHandler(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // m_log.DebugFormat("[ASSETCAPS] Got a GetObjectCost Packet {0}.", request);

            OSDMap resp = new OSDMap();
            OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml(Utils.StringToBytes(request));
            OSDArray itemsRequested = (OSDArray)requestmap["object_ids"];

            foreach (OSDUUID osdItemId in itemsRequested)
            {
                UUID itemId = osdItemId.AsUUID();
                SceneObjectPart item = m_Scene.GetSceneObjectPart(itemId);
                PhysicsActor physActor;

                if (item != null)
                {
                    SceneObjectGroup parent = item.ParentGroup;
                    OSDMap object_data = new OSDMap();

                    object_data["linked_set_resource_cost"] = parent.LandImpact;
                    object_data["resource_cost"] = item.ServerWeight;

                    physActor = item.PhysActor;
                    if (physActor != null)
                        object_data["physics_cost"] = (float)physActor.TotalComplexity;
                    else
                        object_data["physics_cost"] = 0.0; 
                    
                    physActor = parent.RootPart.PhysActor;
                    if (physActor != null)
                        object_data["linked_set_physics_cost"] = (float)physActor.TotalComplexity;
                    else
                        object_data["linked_set_physics_cost"] = 0.0;

                    resp[itemId.ToString()] = object_data;
                }
            }

            string response = OSDParser.SerializeLLSDXmlString(resp);
            // m_log.DebugFormat("[ASSETCAPS] Sending a GetObjectCost Response {0}.", response);
            return response;
        }

        protected string ResourceCostsSelected(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            //m_log.DebugFormat("[ASSETCAPS] Got a ResourceCostsSelected Packet {0}.", request);
            OSDMap resp = new OSDMap();
            OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml(Utils.StringToBytes(request));

            float streaming_cost = 0.0f;
            float simulation_cost = 0.0f;
            float physics_cost = 0.0f;

            // I dont see selected_prims ever sent but we cover our ass just in case
            string[] tags = { "selected_roots", "selected_prims" };
            foreach (string value in tags)
            {
                if (requestmap.ContainsKey(value) == false)
                    continue;

                OSDArray itemsRequested = (OSDArray)requestmap[value];
                foreach (OSDUUID osdItemId in itemsRequested)
                {
                    UUID itemId = osdItemId.AsUUID();
                    SceneObjectPart item = m_Scene.GetSceneObjectPart(itemId);
                    PhysicsActor physActor;

                    if (item != null)
                    {
                        SceneObjectGroup parent = item.ParentGroup;

                        physActor = parent.RootPart.PhysActor;
                        if (physActor != null)
                            physics_cost += (float)physActor.TotalComplexity;
                        streaming_cost += parent.StreamingCost;
                        simulation_cost += parent.ServerWeight;
                    }
                }
            }

            OSDMap object_data = new OSDMap();
            object_data["physics"] = physics_cost;
            object_data["streaming"] = streaming_cost;
            object_data["simulation"] = simulation_cost;
            resp["selected"] = object_data;

            string response = OSDParser.SerializeLLSDXmlString(resp);
            //m_log.DebugFormat("[ASSETCAPS] Sending a ResourceCostsSelected Response {0}.", response);
            return response;
        } 


        protected string GetObjectPhysicsDataHandler(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // m_log.DebugFormat("[ASSETCAPS] Got a GetObjectPhysicsData Packet {0}.", request);

            OSDMap resp = new OSDMap();
            OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml(Utils.StringToBytes(request));
            OSDArray itemsRequested = (OSDArray)requestmap["object_ids"];

            foreach (OSDUUID osdItemId in itemsRequested)
            {
                UUID itemId = osdItemId.AsUUID();
                SceneObjectPart item = m_Scene.GetSceneObjectPart(itemId);

                if ((item != null) && (item.PhysActor != null))
                {
                    Physics.Manager.IMaterial material = item.PhysActor.Properties.Material;
                    OSDMap object_data = new OSDMap();

                    object_data["PhysicsShapeType"] = (byte)item.Shape.PreferredPhysicsShape;       // obj.PhysicsShapeType;
                    object_data["Density"] = material.Density;                                      // obj.Density;
                    object_data["Friction"] = material.StaticFriction;                              // obj.Friction;
                    object_data["Restitution"] = material.Restitution;                              // obj.Restitution;
                    object_data["GravityMultiplier"] = 1.0f;                                        // material.obj.GravityModifier;

                    resp[itemId.ToString()] = object_data;
                }
            }

            string response = OSDParser.SerializeLLSDXmlString(resp);
            // m_log.DebugFormat("[ASSETCAPS] Sending a GetObjectPhysicsData Response {0}.", response);
            return response;
        }

        protected class GetMeshHandler
        {
            //        private static readonly ILog m_log =
            //            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            private Scene m_Scene;
            private UUID m_AgentID;
            private Caps m_Caps;
            private IAssetCache m_AssetCache;

            public GetMeshHandler(Scene scene, UUID agentID, Caps caps)
            {
                m_Scene = scene;
                m_AgentID = agentID;
                m_Caps = caps;
                m_AssetCache = m_Scene.CommsManager.AssetCache;
            }

            public Hashtable ProcessGetMesh(Hashtable request, UUID AgentId, Caps cap)
            {
                Hashtable responsedata = new Hashtable();
                responsedata["int_response_code"] = 400; //501; //410; //404;
                responsedata["content_type"] = "text/plain";
                responsedata["keepalive"] = false;
                responsedata["str_response_string"] = "Request wasn't what was expected";

                UUID meshID = UUID.Zero;

                if ((request.ContainsKey("mesh_id")) && 
                    (UUID.TryParse(request["mesh_id"].ToString(), out meshID)))
                {
                    if (m_AssetCache == null)
                    {
                        responsedata["int_response_code"] = 404; //501; //410; //404;
                        responsedata["str_response_string"] = "The asset service is unavailable.  So is your mesh.";
                        return responsedata;
                    }

                    AssetBase mesh = m_AssetCache.GetAsset(meshID, AssetRequestInfo.GenericNetRequest());
                    if (mesh != null)
                    {
                        if (mesh.Type == (SByte)AssetType.Mesh)
                        {
                            responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                            responsedata["content_type"] = "application/vnd.ll.mesh";
                            responsedata["int_response_code"] = 200;
                        }
                        // Optionally add additional mesh types here
                        else
                        {
                            responsedata["int_response_code"] = 404; //501; //410; //404;
                            responsedata["str_response_string"] = "Unfortunately, this asset isn't a mesh.";
                            return responsedata;
                        }
                    }
                    else
                    {
                        responsedata["int_response_code"] = 404; //501; //410; //404;
                        responsedata["str_response_string"] = "Your Mesh wasn't found.  Sorry!";
                        return responsedata;
                    }
                }

                return responsedata;
            }
        }

    }
}
