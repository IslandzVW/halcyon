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

// #define PLUS_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using Nini.Config;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.IO;

namespace OpenSim.Region.CoreModules.Plus
{

        /// <summary>
        /// Sets up a remote HTTP POST url (on "http://regionIP:regionPort/plus")
        /// to take Plus parcel synchronizatiuon messages
        /// 
        /// Command [ app -> regionhost ] (HTTP POST)
        ///
        /// POST to "http://regionIP:regionPort/plus/claim_parcel"
        /// {
        ///      “user_id”:”[user_uuid]”
        ///      “parcel_id:”[parcel_uuid]”
        ///    }
        ///    
        /// POST to "http://regionIP:regionPort/plus/abandon_parcel"
        /// {
        ///      “user_id”:”[user_uuid]”
        ///      “parcel_id:”[parcel_uuid]”
        ///    }
        ///    
        /// The response to both requests will be in the following format:
        /// 
        /// CommandReply [ regionhost -> app ] (REPLY TO HTTP POST)
        ///
        /// {
        ///      "status":"OK"|"UNAUTHORIZED"
        ///      “parcel_id:”[actual parcel ID]”
        ///      “user_id”:”[actual parcel ownerID]”
        /// }

    public class PlusParcelModule : ISharedRegionModule
    {
        #region Declares

        private List<Scene> _scenes = new List<Scene>();
        private string _gridSendKey;
        private IMessageTransferModule m_TransferModule = null;

        #endregion

        #region Enums and Classes

        /// <summary>
        /// The response that will be returned from a command
        /// </summary>
        public enum PlusModuleResponse
        {
            // The command worked properly
            OK = 1,
            // The region or parcel does not exist
            NOTFOUND = 2,
            // The parcel owner does not match the specified new owner
            UNAUTHORIZED = 3
        }

        #endregion

        #region INonSharedRegionModule members

        public void Initialize(IConfigSource source)
        {
        }

        public void PostInitialize()
        {
        }

        public readonly String CLAIM_URL = "/plus/claim_parcel";
        public readonly String ABANDON_URL = "/plus/abandon_parcel";

        public void AddRegion(Scene scene)
        {
            _gridSendKey = scene.GridSendKey;
            _scenes.Add(scene);

            IHttpServer server = MainServer.GetHttpServer(scene.RegionInfo.HttpPort);
            server.AddStreamHandler(new BinaryStreamHandler("POST", CLAIM_URL, HandlePlusParcelClaim));
            server.AddStreamHandler(new BinaryStreamHandler("POST", ABANDON_URL, HandlePlusParcelAbandon));
#if PLUS_DEBUG
            server.AddStreamHandler(new RestStreamHandler("GET", CLAIM_URL, HandlePlusParcelClaimGET));
            server.AddStreamHandler(new RestStreamHandler("GET", ABANDON_URL, HandlePlusParcelAbandonGET));
#endif
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            _scenes.Remove(scene);

            IHttpServer server = MainServer.GetHttpServer(scene.RegionInfo.HttpPort);
            server.RemoveStreamHandler("POST", CLAIM_URL);
            server.RemoveStreamHandler("POST", ABANDON_URL);
#if PLUS_DEBUG
            server.RemoveStreamHandler("GET", CLAIM_URL);
            server.RemoveStreamHandler("GET", ABANDON_URL);
#endif
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "PlusParcelModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region Utility methods

        private void SendIM(UUID agentId, Vector3 pos, UUID fromID, string fromName, string message)
        {
            if (_scenes.Count < 1)
                return;     // not ready to send the IM

            Scene scene = _scenes[0];
            if (m_TransferModule == null)    // ready to send IM?
            {
                m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule>();
                if (m_TransferModule == null)
                    return;     // not ready to send the IM
            }

            GridInstantMessage msg = new GridInstantMessage();
            msg.fromAgentID = fromID.Guid;
            msg.toAgentID = agentId.Guid;
            msg.imSessionID = fromID.Guid;  // put all of these is the same IM "session" from the fromID
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch();// timestamp;
            msg.fromAgentName = fromName;
            // Cap the message length at 1024.
            if (message != null && message.Length > 1024)
                msg.message = message.Substring(0, 1024);
            else
                msg.message = message;
            msg.dialog = (byte)InstantMessageDialog.MessageFromAgent;
            msg.fromGroup = false;// fromGroup;
            msg.offline = (byte)1; //yes, store for fetching missed IMs on login
            msg.ParentEstateID = 0; //ParentEstateID;
            msg.Position = pos;
            msg.RegionID = scene.RegionInfo.RegionID.Guid;//RegionID.Guid;
            // binaryBucket is the SL URL without the prefix, e.g. "Region/x/y/z"
            string url = Util.LocationShortCode(scene.RegionInfo.RegionName, msg.Position, "/");
            byte[] bucket = Utils.StringToBytes(url);
            msg.binaryBucket = new byte[bucket.Length];// binaryBucket;
            bucket.CopyTo(msg.binaryBucket, 0);

            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(msg, delegate(bool success) { });
            }
        }

        #endregion

        #region HTTP processing

        private readonly UUID INWORLDZ_MAINLAND = new UUID("75b480f8-50e4-493d-911f-5159f1f3a696");

        public string BuildPlusResponse(PlusModuleResponse rep, UUID parcelID, UUID userID)
        {
            OSDMap map = new OSDMap();
            map["status"] = (int)rep;
            map["parcel_id"] = parcelID.ToString();
            map["user_id"] = userID.ToString();

            return OSDParser.SerializeJsonString(map);

        }

        private string HandlePlusParcelChange(bool isClaim, UUID userID, UUID parcelID)
        {
            Scene scene = _scenes[0];
            if (scene == null)  //No scene found...
                return BuildPlusResponse(PlusModuleResponse.NOTFOUND, parcelID, userID);

            //Process the command
            LandData parcel;
            if (isClaim)
                parcel = scene.LandChannel.ClaimPlusParcel(parcelID, userID);
            else
                parcel = scene.LandChannel.AbandonPlusParcel(parcelID);
            if (parcel == null)
                return BuildPlusResponse(PlusModuleResponse.NOTFOUND, parcelID, userID);

            UserProfileData userProfile = scene.CommsManager.UserService.GetUserProfile(userID);
            string name = (userProfile != null) ? userProfile.Name : "Unknown " + userID.ToString();
            string verb = isClaim ? "claimed" : "abandoned";
            string url = Util.LocationURL(scene.RegionInfo.RegionName, parcel.UserLocation);

            foreach (UUID manager in scene.RegionInfo.EstateSettings.EstateManagers)
            {
                SendIM(manager, parcel.UserLocation, INWORLDZ_MAINLAND, scene.RegionInfo.RegionName, name + " has " + verb + " parcel '" + parcel.Name + "' at " + url);
            }
            return BuildPlusResponse(PlusModuleResponse.OK, parcelID, userID);
        }

        private string HandlePlusParcelRequest(bool isClaim, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            if (!Util.CheckHttpAuthorization(_gridSendKey, httpRequest.Headers))
            {
                httpResponse.StatusCode = 401;
                return "Unauthorized";
            }

            OSDMap map = (OSDMap)OSDParser.DeserializeJson(httpRequest.InputStream);

            UUID parcelID = map["parcel_id"].AsUUID();
            UUID userID = map["user_id"].AsUUID();

            return HandlePlusParcelChange(isClaim, userID, parcelID);
        }

        private string HandlePlusParcelClaim(Stream data, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            return HandlePlusParcelRequest(true, httpRequest, httpResponse);
        }

        private string HandlePlusParcelAbandon(Stream data, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            return HandlePlusParcelRequest(false, httpRequest, httpResponse);
        }

#if PLUS_DEBUG
        private string HandlePlusParcelClaimGET(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // UUID userID = new UUID("edb4f996-95e7-4754-8f0c-611a4b29b566"); // Test User
            UUID userID = new UUID("fc4fd042-93d0-4056-a9da-698f35d0eafd"); // Jim Tarber
            UUID parcelID = new UUID("90b32329-a2e7-45a9-9fab-a255aae7b3dd"); // Media Parcel in Test1
            return HandlePlusParcelChange(true, userID, parcelID);
        }
        private string HandlePlusParcelAbandonGET(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // UUID userID = new UUID("edb4f996-95e7-4754-8f0c-611a4b29b566"); // Test User
            UUID userID = new UUID("fc4fd042-93d0-4056-a9da-698f35d0eafd"); // Jim Tarber
            UUID parcelID = new UUID("90b32329-a2e7-45a9-9fab-a255aae7b3dd"); // Media Parcel in Test1
            return HandlePlusParcelChange(false, userID, parcelID);
        }
#endif
        #endregion
    }
}
