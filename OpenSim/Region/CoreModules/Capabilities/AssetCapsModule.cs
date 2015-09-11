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

        public void Initialise(IConfigSource source)
        {
            m_useAperture = false;
            IConfig startupConfig = source.Configs["Startup"];
            if (startupConfig == null)
                return;

            if (startupConfig.GetString("use_aperture_server", "no") == "yes")
            {
                m_useAperture = true;
                m_apPort = startupConfig.GetString("aperture_server_port", "8000");
                m_apToken = startupConfig.GetString("aperture_server_caps_token", "");
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
            bool getTextureCapRegistered = false;

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
                    getTextureCapRegistered = true;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[APERTURE] Could not contact the aperture texture server to register caps on region {0}. Server returned error {1}", 
                    caps.RegionName, e.Message);
            }

            if (getTextureCapRegistered == false)
            {
#if false
            // If we get here aperture is either disabled or we failed to contact it
            IRequestHandler handler = new GetTextureHandler("/CAPS/" + capID + "/", m_assetService, "GetTexture", agentID.ToString());
            caps.RegisterHandler("GetTexture", handler);
            // m_log.DebugFormat("[GETTEXTURE]: /CAPS/{0} in region {1}", capID, m_scene.RegionInfo.RegionName);  
#endif
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
                        string addCapURL = externalBaseURL + ADD_CAPS_TOKEN_URL + m_apToken + "/" + capID.ToString();

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
            string externalURL = GetApertureHttUrl(caps, capID);
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
            string externalURL = GetApertureHttUrl(caps, capID);
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
            string externalURL = GetApertureHttUrl(caps, capID);
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
                string capuuid = externalURL.Replace(externalBaseURL + "/CAPS/HTT/", "");
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

#if false
		protected class GetTextureHandler : BaseStreamHandler
        {
            private static readonly ILog m_log =
                LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            private IAssetService m_assetService;

            public const string DefaultFormat = "x-j2c";

            // TODO: Change this to a config option
            const string REDIRECT_URL = null;

            public GetTextureHandler(string path, IAssetService assService, string name, string description)
                : base("GET", path, name, description)
            {
                m_assetService = assService;
            }

            public override byte[] Handle(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                // Try to parse the texture ID from the request URL
                NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
                string textureStr = query.GetOne("texture_id");
                string format = query.GetOne("format");

                //m_log.DebugFormat("[GETTEXTURE]: called {0}", textureStr);

                if (m_assetService == null)
                {
                    m_log.Error("[GETTEXTURE]: Cannot fetch texture " + textureStr + " without an asset service");
                    httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                }

                UUID textureID;
                if (!String.IsNullOrEmpty(textureStr) && UUID.TryParse(textureStr, out textureID))
                {
                    //                m_log.DebugFormat("[GETTEXTURE]: Received request for texture id {0}", textureID);

                    string[] formats;
                    if (format != null && format != string.Empty)
                    {
                        formats = new string[1] { format.ToLower() };
                    }
                    else
                    {
                        formats = WebUtil.GetPreferredImageTypes(httpRequest.Headers.Get("Accept"));
                        if (formats.Length == 0)
                            formats = new string[1] { DefaultFormat }; // default

                    }
                    // OK, we have an array with preferred formats, possibly with only one entry

                    httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                    foreach (string f in formats)
                    {
                        if (FetchTexture(httpRequest, httpResponse, textureID, f))
                            break;
                    }
                }
                else
                {
                    m_log.Warn("[GETTEXTURE]: Failed to parse a texture_id from GetTexture request: " + httpRequest.Url);
                }

                //            m_log.DebugFormat(
                //                "[GETTEXTURE]: For texture {0} sending back response {1}, data length {2}",
                //                textureID, httpResponse.StatusCode, httpResponse.ContentLength);

                return null;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="httpRequest"></param>
            /// <param name="httpResponse"></param>
            /// <param name="textureID"></param>
            /// <param name="format"></param>
            /// <returns>False for "caller try another codec"; true otherwise</returns>
            private bool FetchTexture(OSHttpRequest httpRequest, OSHttpResponse httpResponse, UUID textureID, string format)
            {
                //            m_log.DebugFormat("[GETTEXTURE]: {0} with requested format {1}", textureID, format);
                AssetBase texture;

                string fullID = textureID.ToString();
                if (format != DefaultFormat)
                    fullID = fullID + "-" + format;

                if (!String.IsNullOrEmpty(REDIRECT_URL))
                {
                    // Only try to fetch locally cached textures. Misses are redirected
                    texture = m_assetService.GetCached(fullID);

                    if (texture != null)
                    {
                        if (texture.Type != (sbyte)AssetType.Texture)
                        {
                            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                            return true;
                        }
                        WriteTextureData(httpRequest, httpResponse, texture, format);
                    }
                    else
                    {
                        string textureUrl = REDIRECT_URL + textureID.ToString();
                        m_log.Debug("[GETTEXTURE]: Redirecting texture request to " + textureUrl);
                        httpResponse.RedirectLocation = textureUrl;
                        return true;
                    }
                }
                else // no redirect
                {
                    // try the cache
                    texture = m_assetService.GetCached(fullID);

                    if (texture == null)
                    {
                        //m_log.DebugFormat("[GETTEXTURE]: texture was not in the cache");

                        // Fetch locally or remotely. Misses return a 404
                        texture = m_assetService.Get(textureID.ToString());

                        if (texture != null)
                        {
                            if (texture.Type != (sbyte)AssetType.Texture)
                            {
                                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                                return true;
                            }
                            if (format == DefaultFormat)
                            {
                                WriteTextureData(httpRequest, httpResponse, texture, format);
                                return true;
                            }
                            else
                            {
                                AssetBase newTexture = new AssetBase(texture.ID + "-" + format, texture.Name, (sbyte)AssetType.Texture, texture.Metadata.CreatorID);
                                newTexture.Data = ConvertTextureData(texture, format);
                                if (newTexture.Data.Length == 0)
                                    return false; // !!! Caller try another codec, please!

                                newTexture.Flags = AssetFlags.Collectable;
                                newTexture.Temporary = true;
                                m_assetService.Store(newTexture);
                                WriteTextureData(httpRequest, httpResponse, newTexture, format);
                                return true;
                            }
                        }
                    }
                    else // it was on the cache
                    {
                        //m_log.DebugFormat("[GETTEXTURE]: texture was in the cache");
                        WriteTextureData(httpRequest, httpResponse, texture, format);
                        return true;
                    }
                }

                // not found
                //            m_log.Warn("[GETTEXTURE]: Texture " + textureID + " not found");
                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                return true;
            }

            private void WriteTextureData(IOSHttpRequest request, IOSHttpResponse response, AssetBase texture, string format)
            {
                string range = request.Headers.GetOne("Range");

                if (!String.IsNullOrEmpty(range)) // JP2's only
                {
                    // Range request
                    int start, end;
                    if (TryParseRange(range, out start, out end))
                    {

                        // Before clamping start make sure we can satisfy it in order to avoid
                        // sending back the last byte instead of an error status
                        if (start >= texture.Data.Length)
                        {
                            response.StatusCode = (int)System.Net.HttpStatusCode.RequestedRangeNotSatisfiable;
                        }
                        else
                        {
                            end = Utils.Clamp(end, 0, texture.Data.Length - 1);
                            start = Utils.Clamp(start, 0, end);
                            int len = end - start + 1;

                            //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);

                            // Always return PartialContent, even if the range covered the entire data length
                            // We were accidentally sending back 404 before in this situation
                            // https://issues.apache.org/bugzilla/show_bug.cgi?id=51878 supports sending 206 even if the
                            // entire range is requested, and viewer 3.2.2 (and very probably earlier) seems fine with this.
                            response.StatusCode = (int)System.Net.HttpStatusCode.PartialContent;

                            response.ContentLength = len;
                            response.ContentType = texture.Metadata.ContentType;
                            response.AddHeader("Content-Range", String.Format("bytes {0}-{1}/{2}", start, end, texture.Data.Length));

                            response.Body.Write(texture.Data, start, len);
                        }
                    }
                    else
                    {
                        m_log.Warn("[GETTEXTURE]: Malformed Range header: " + range);
                        response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                    }
                }
                else // JP2's or other formats
                {
                    // Full content request
                    response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                    response.ContentLength = texture.Data.Length;
                    if (format == DefaultFormat)
                        response.ContentType = texture.Metadata.ContentType;
                    else
                        response.ContentType = "image/" + format;
                    response.Body.Write(texture.Data, 0, texture.Data.Length);
                }

                //            if (response.StatusCode < 200 || response.StatusCode > 299)
                //                m_log.WarnFormat(
                //                    "[GETTEXTURE]: For texture {0} requested range {1} responded {2} with content length {3} (actual {4})",
                //                    texture.FullID, range, response.StatusCode, response.ContentLength, texture.Data.Length);
                //            else
                //                m_log.DebugFormat(
                //                    "[GETTEXTURE]: For texture {0} requested range {1} responded {2} with content length {3} (actual {4})",
                //                    texture.FullID, range, response.StatusCode, response.ContentLength, texture.Data.Length);
            }

            private bool TryParseRange(string header, out int start, out int end)
            {
                if (header.StartsWith("bytes="))
                {
                    string[] rangeValues = header.Substring(6).Split('-');
                    if (rangeValues.Length == 2)
                    {
                        if (Int32.TryParse(rangeValues[0], out start) && Int32.TryParse(rangeValues[1], out end))
                            return true;
                    }
                }

                start = end = 0;
                return false;
            }

            private byte[] ConvertTextureData(AssetBase texture, string format)
            {
                m_log.DebugFormat("[GETTEXTURE]: Converting texture {0} to {1}", texture.ID, format);
                byte[] data = new byte[0];

                MemoryStream imgstream = new MemoryStream();
                Bitmap mTexture = new Bitmap(1, 1);
                ManagedImage managedImage;
                Image image = (Image)mTexture;

                try
                {
                    // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular data

                    imgstream = new MemoryStream();

                    // Decode image to System.Drawing.Image
                    if (OpenJPEG.DecodeToImage(texture.Data, out managedImage, out image))
                    {
                        // Save to bitmap
                        mTexture = new Bitmap(image);

                        EncoderParameters myEncoderParameters = new EncoderParameters();
                        myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                        // Save bitmap to stream
                        ImageCodecInfo codec = GetEncoderInfo("image/" + format);
                        if (codec != null)
                        {
                            mTexture.Save(imgstream, codec, myEncoderParameters);
                            // Write the stream to a byte array for output
                            data = imgstream.ToArray();
                        }
                        else
                            m_log.WarnFormat("[GETTEXTURE]: No such codec {0}", format);

                    }
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[GETTEXTURE]: Unable to convert texture {0} to {1}: {2}", texture.ID, format, e.Message);
                }
                finally
                {
                    // Reclaim memory, these are unmanaged resources
                    // If we encountered an exception, one or more of these will be null
                    if (mTexture != null)
                        mTexture.Dispose();

                    if (image != null)
                        image.Dispose();

                    if (imgstream != null)
                    {
                        imgstream.Close();
                        imgstream.Dispose();
                    }
                }

                return data;
            }

            // From msdn
            private static ImageCodecInfo GetEncoderInfo(String mimeType)
            {
                ImageCodecInfo[] encoders;
                encoders = ImageCodecInfo.GetImageEncoders();
                for (int j = 0; j < encoders.Length; ++j)
                {
                    if (encoders[j].MimeType == mimeType)
                        return encoders[j];
                }
                return null;
            }
        }  
#endif

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
                    return LLSDHelpers.SerialiseLLSDReply(uploadResponse);
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
                        result = LLSDHelpers.SerialiseLLSDReply(badReply);
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

                        result = LLSDHelpers.SerialiseLLSDReply(uploadComplete);
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
