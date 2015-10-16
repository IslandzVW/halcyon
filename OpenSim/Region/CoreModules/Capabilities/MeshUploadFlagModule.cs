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
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Capabilities
{
    /// <summary>
    /// MeshUploadFlag capability. This is required for uploading Mesh.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class MeshUploadFlagModule : INonSharedRegionModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Is this module enabled?
        /// </summary>
        public bool Enabled { get; private set; }

        private Scene m_scene;

        #region ISharedRegionModule Members

        public MeshUploadFlagModule()
        {
            Enabled = false;
        }

        public void Initialize(IConfigSource source)
        {
            IConfig config = source.Configs["Mesh"];
            if (config == null)
                return;
            
            Enabled = config.GetBoolean("AllowMeshUpload", Enabled);
        }

        public void AddRegion(Scene s)
        {
            if (!Enabled)
                return;

            m_scene = s;
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void RemoveRegion(Scene s)
        {
            if (!Enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
        }

        public void RegionLoaded(Scene s)
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
            get { return "MeshUploadFlagModule"; } 
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        #endregion

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            MeshUploadHandler handler = new MeshUploadHandler(m_scene, agentID, caps);
            IRequestHandler reqHandler = new RestHTTPHandler("GET", "/CAPS/" + UUID.Random(), handler.MeshUploadFlag);
            caps.RegisterHandler("MeshUploadFlag", reqHandler);
        }

        /// <summary>
        /// Look up and return wether this client can upload mesh objects.
        /// </summary>
        private class MeshUploadHandler
        {
            private UUID m_agentID;
            private Caps m_Caps;
            private Scene m_Scene;

            public MeshUploadHandler(Scene scene, UUID agentID, Caps caps)
            {
                m_agentID = agentID;
                m_Caps = caps;
                m_Scene = scene;
            }

            public Hashtable MeshUploadFlag(Hashtable mDhttpMethod)
            {
                //            m_log.DebugFormat("[MESH UPLOAD FLAG MODULE]: MeshUploadFlag request");

                OSDMap data = new OSDMap();
                ScenePresence sp = m_Scene.GetScenePresence(m_agentID);
                data["username"] = sp.Firstname + "." + sp.Lastname;
                data["display_name_next_update"] = new OSDDate(DateTime.Now);
                data["legacy_first_name"] = sp.Firstname;
                data["mesh_upload_status"] = "valid";
                data["display_name"] = sp.Firstname + " " + sp.Lastname;
                data["legacy_last_name"] = sp.Lastname;
                data["id"] = m_agentID;
                data["is_display_name_default"] = true;

                //Send back data
                Hashtable responsedata = new Hashtable();
                responsedata["int_response_code"] = 200;
                responsedata["content_type"] = "text/plain";
                responsedata["str_response_string"] = OSDParser.SerializeLLSDXmlString(data);
                return responsedata;
            }
        }
    }
}
