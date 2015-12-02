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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;
using System.Text.RegularExpressions;

namespace OpenSim.Grid.UserServer.Modules
{
    public delegate void logOffUser(UUID AgentID);

    public class UserManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event logOffUser OnLogOffUser;
        private logOffUser handlerLogOffUser;

        private UserDataBaseService m_userDataBaseService;
        private BaseHttpServer m_httpServer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userDataBaseService"></param>
        public UserManager(UserDataBaseService userDataBaseService)
        {
            m_userDataBaseService = userDataBaseService;
        }

        public void Initialize(IGridServiceCore core)
        {

        }

        public void PostInitialize()
        {

        }

        private string RESTGetUserProfile(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(httpRequest.RemoteIPEndPoint))
            {
                httpResponse.StatusCode = 403;
                return ("Access is denied for this IP Endpoint");
            }

            UUID id;
            UserProfileData userProfile;

            try
            {
                id = new UUID(param);
            }
            catch (Exception)
            {
                httpResponse.StatusCode = 500;
                return "Malformed Param [" + param + "]";
            }

            userProfile = m_userDataBaseService.GetUserProfile(id);

            if (userProfile == null)
            {
                httpResponse.StatusCode = 404;
                return "Not Found.";
            }

            return ProfileToXmlRPCResponse(userProfile).ToString();
        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;

            // Rest
            m_httpServer.AddStreamHandler(new RestStreamHandler("DELETE", "/usersessions/", RestDeleteUserSessionMethod)); 
            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/users/", RESTGetUserProfile));
            //m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/users/", RESTGetUserInterests));

            // XmlRpc
            m_httpServer.AddXmlRPCHandler("get_user_by_name", XmlRPCGetUserMethodName);
            m_httpServer.AddXmlRPCHandler("get_user_by_uuid", XmlRPCGetUserMethodUUID);
            m_httpServer.AddXmlRPCHandler("get_avatar_picker_avatar", XmlRPCGetAvatarPickerAvatar);
            m_httpServer.AddXmlRPCHandler("update_user_current_region", XmlRPCAtRegion);
            m_httpServer.AddXmlRPCHandler("logout_of_simulator", XmlRPCLogOffUserMethodUUID);
            m_httpServer.AddXmlRPCHandler("get_agent_by_uuid", XmlRPCGetAgentMethodUUID);
            m_httpServer.AddXmlRPCHandler("update_user_profile", XmlRpcResponseXmlRPCUpdateUserProfile);
            //m_httpServer.AddXmlRPCHandler("update_user_interests", XmlRpcResponseXmlRPCUpdateUserInterests);
            m_httpServer.AddXmlRPCHandler("get_user_preferences", XmlRpcGetUserPreferences);
            m_httpServer.AddXmlRPCHandler("save_user_preferences", XmlRpcSaveUserPreferences);

            // New Style
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_user_by_name"), XmlRPCGetUserMethodName));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_user_by_uuid"), XmlRPCGetUserMethodUUID));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_avatar_picker_avatar"), XmlRPCGetAvatarPickerAvatar));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("update_user_current_region"), XmlRPCAtRegion));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("logout_of_simulator"), XmlRPCLogOffUserMethodUUID));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_agent_by_uuid"), XmlRPCGetAgentMethodUUID));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("update_user_profile"), XmlRpcResponseXmlRPCUpdateUserProfile));
            //m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("update_user_interests"), XmlRpcResponseXmlRPCUpdateUserInterests));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("get_user_preferences"), XmlRpcGetUserPreferences));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("save_user_preferences"), XmlRpcSaveUserPreferences));
        }

        /// <summary>
        /// Deletes an active agent session
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="path">The path (eg /bork/narf/test)</param>
        /// <param name="param">Parameters sent</param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns>Success "OK" else error</returns>
        public string RestDeleteUserSessionMethod(string request, string path, string param,
                                                  OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(httpRequest.RemoteIPEndPoint))
            {
                httpResponse.StatusCode = 403;
                return ("Access is denied for this IP Endpoint");
            }

            // TODO! Important!

            return "OK";
        }

        public XmlRpcResponse AvatarPickerListtoXmlRPCResponse(UUID queryID, List<AvatarPickerAvatar> returnUsers)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            // Query Result Information
            responseData["queryid"] = queryID.ToString();
            responseData["avcount"] = returnUsers.Count.ToString();

            for (int i = 0; i < returnUsers.Count; i++)
            {
                responseData["avatarid" + i] = returnUsers[i].AvatarID.ToString();
                responseData["firstname" + i] = returnUsers[i].firstName;
                responseData["lastname" + i] = returnUsers[i].lastName;
            }
            response.Value = responseData;

            return response;
        }

        /// <summary>
        /// This removes characters that are invalid for xml encoding
        /// </summary>
        /// <param name="text">Text to be encoded.</param>
        /// <returns>Text with invalid xml characters removed.</returns>
        public string CleanInvalidXmlChars(string text)
        {
            // From xml spec valid chars:
            // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]    
            // any Unicode character, excluding the surrogate blocks, FFFE, and FFFF.
            // string re = @"[^\x09\x0A\x0D\x20-\xD7FF\xE000-\xFFFD\x10000-x10FFFF]";
            string re = @"[^a-zA-Z0-9\s\p{P}]";
            return Regex.Replace(text, re, String.Empty);
        }

        /// <summary>
        /// Converts a user profile to an XML element which can be returned
        /// </summary>
        /// <param name="profile">The user profile</param>
        /// <returns>A string containing an XML Document of the user profile</returns>
        public XmlRpcResponse ProfileToXmlRPCResponse(UserProfileData profile)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            // Account information
            responseData["firstname"] = profile.FirstName;
            responseData["lastname"] = profile.SurName;
            responseData["uuid"] = profile.ID.ToString();
            // Server Information
            responseData["server_inventory"] = profile.UserInventoryURI;
            responseData["server_asset"] = profile.UserAssetURI;
            // Profile Information
            responseData["profile_about"] = CleanInvalidXmlChars(profile.AboutText);
            responseData["profile_firstlife_about"] = CleanInvalidXmlChars(profile.FirstLifeAboutText);
            responseData["profile_webloginkey"] = profile.WebLoginKey;
            responseData["profile_firstlife_image"] = profile.FirstLifeImage.ToString();
            responseData["profile_image"] = profile.Image.ToString();
            responseData["profile_created"] = profile.Created.ToString();
            responseData["profile_lastlogin"] = profile.LastLogin.ToString();
            // Home region information
            responseData["home_coordinates_x"] = profile.HomeLocation.X.ToString();
            responseData["home_coordinates_y"] = profile.HomeLocation.Y.ToString();
            responseData["home_coordinates_z"] = profile.HomeLocation.Z.ToString();

            responseData["home_region"] = profile.HomeRegion.ToString();
            responseData["home_region_id"] = profile.HomeRegionID.ToString();

            responseData["home_look_x"] = profile.HomeLookAt.X.ToString();
            responseData["home_look_y"] = profile.HomeLookAt.Y.ToString();
            responseData["home_look_z"] = profile.HomeLookAt.Z.ToString();

            responseData["user_flags"] = profile.UserFlags.ToString();
            responseData["god_level"] = profile.GodLevel.ToString();
            responseData["custom_type"] = profile.CustomType;
            responseData["partner"] = profile.Partner.ToString();
            responseData["profileURL"] = profile.ProfileURL;


            response.Value = responseData;

            return response;
        }

        #region XMLRPC User Methods
        
        public XmlRpcResponse XmlRpcSaveUserPreferences(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            UUID userId = UUID.Zero;
            UUID.TryParse((string)requestData["userId"], out userId);
            if (userId == UUID.Zero)
            {
                return this.GenerateXmlRpcError("InvalidArgument", "UserId given is invalid");
            }
            
            bool imsViaEmail = ((string)requestData["recvIMsViaEmail"]) == "1" ? true : false ;
            bool inDirectory = ((string)requestData["listedInDirectory"]) == "1" ? true : false;

            m_userDataBaseService.SaveUserPreferences(new UserPreferencesData(userId, imsViaEmail, inDirectory));

            return this.GenerateSucessResponse();
        }

        private XmlRpcResponse GenerateXmlRpcError(string type, string desc)
        {
            Hashtable responseData = new Hashtable();
            responseData.Add("error_type", type);
            responseData.Add("error_desc", desc);

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;

            return response;
        }

        private XmlRpcResponse GenerateSucessResponse()
        {
            Hashtable responseData = new Hashtable();
            responseData.Add("returnString", "OK");

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;

            return response;
        }

        public XmlRpcResponse XmlRpcGetUserPreferences(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());
            
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            UUID userId = UUID.Zero;
            UUID.TryParse((string)requestData["userId"], out userId);

            if (userId == UUID.Zero)
            {
                return this.GenerateXmlRpcError("InvalidArgument", "UserId given is invalid");
            }

            UserPreferencesData prefs = m_userDataBaseService.RetrieveUserPreferences(userId);
            if (prefs == null)
            {
                return this.GenerateXmlRpcError("DatabaseError", "Database error retrieving user preferences");
            }

            //return the prefs
            Hashtable responseData = new Hashtable();
            responseData["userId"] = userId.ToString();
            responseData["recvIMsViaEmail"] = prefs.ReceiveIMsViaEmail ? "1" : "0";
            responseData["listedInDirectory"] = prefs.ListedInDirectory ? "1" : "0";

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;

            return response;
        }

        public XmlRpcResponse XmlRPCGetAvatarPickerAvatar(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());
            
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            List<AvatarPickerAvatar> returnAvatar = new List<AvatarPickerAvatar>();
            UUID queryID = new UUID(UUID.Zero.ToString());

            if (requestData.Contains("avquery") && requestData.Contains("queryid"))
            {
                queryID = new UUID((string)requestData["queryid"]);
                returnAvatar = m_userDataBaseService.GenerateAgentPickerRequestResponse(queryID, (string)requestData["avquery"]);
            }

            m_log.InfoFormat("[AVATARINFO]: Servicing Avatar Query: " + (string)requestData["avquery"]);
            return AvatarPickerListtoXmlRPCResponse(queryID, returnAvatar);
        }

        public XmlRpcResponse XmlRPCAtRegion(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnstring = "FALSE";

            if (requestData.Contains("avatar_id") && requestData.Contains("region_handle") &&
                requestData.Contains("region_uuid"))
            {
                // ulong cregionhandle = 0;
                UUID regionUUID;
                UUID avatarUUID;

                UUID.TryParse((string)requestData["avatar_id"], out avatarUUID);
                UUID.TryParse((string)requestData["region_uuid"], out regionUUID);

                if (avatarUUID != UUID.Zero)
                {
                    // Force a refresh for this due to Commit below.
                    UserProfileData userProfile = m_userDataBaseService.GetUserProfile(avatarUUID,true);
                    userProfile.CurrentAgent.Region = regionUUID;
                    userProfile.CurrentAgent.Handle = (ulong)Convert.ToInt64((string)requestData["region_handle"]);
                    //userProfile.CurrentAgent.
                    m_userDataBaseService.CommitAgent(ref userProfile);
                    //setUserProfile(userProfile);


                    returnstring = "TRUE";
                }
            }
            responseData.Add("returnString", returnstring);
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCGetUserMethodName(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());

            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            if (requestData.Contains("avatar_name"))
            {
                string query = (string)requestData["avatar_name"];

                if (null == query)
                    return Util.CreateUnknownUserErrorResponse();

                // Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9]");

                string[] querysplit = query.Split(' ');

                if (querysplit.Length == 2)
                {
                    userProfile = m_userDataBaseService.GetUserProfile(querysplit[0], querysplit[1]);
                    if (userProfile == null)
                    {
                        return Util.CreateUnknownUserErrorResponse();
                    }
                }
                else
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCGetUserMethodUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse());
            
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            //CFK: this clogs the UserServer log and is not necessary at this time.
            //CFK: m_log.Debug("METHOD BY UUID CALLED");
            if (requestData.Contains("avatar_uuid"))
            {
                try
                {
                    UUID guess = new UUID((string)requestData["avatar_uuid"]);

                    userProfile = m_userDataBaseService.GetUserProfile(guess);
                }
                catch (FormatException)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }

                if (userProfile == null)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCGetAgentMethodUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.Contains("avatar_uuid"))
            {
                UUID guess;

                UUID.TryParse((string)requestData["avatar_uuid"], out guess);

                if (guess == UUID.Zero)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }

                UserAgentData agentData = m_userDataBaseService.GetUserAgent(guess);

                if (agentData == null)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }

                Hashtable responseData = new Hashtable();

                responseData["handle"] = agentData.Handle.ToString();
                responseData["session"] = agentData.SessionID.ToString();
                if (agentData.AgentOnline)
                    responseData["agent_online"] = "TRUE";
                else
                    responseData["agent_online"] = "FALSE";

                response.Value = responseData;
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCUpdateUserProfile(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            m_log.Debug("[UserManager]: Got request to update user profile.");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (!requestData.Contains("avatar_uuid"))
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            UUID UserUUID = new UUID((string)requestData["avatar_uuid"]);
            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(UserUUID);
            if (null == userProfile)
            {
                return Util.CreateUnknownUserErrorResponse();
            }
            // don't know how yet.
            if (requestData.Contains("AllowPublish"))
            {
            }
            if (requestData.Contains("FLImageID"))
            {
                userProfile.FirstLifeImage = new UUID((string)requestData["FLImageID"]);
            }
            if (requestData.Contains("ImageID"))
            {
                userProfile.Image = new UUID((string)requestData["ImageID"]);
            }
            // dont' know how yet
            if (requestData.Contains("MaturePublish"))
            {
            }
            if (requestData.Contains("AboutText"))
            {
                userProfile.AboutText = (string)requestData["AboutText"];
            }
            if (requestData.Contains("FLAboutText"))
            {
                userProfile.FirstLifeAboutText = (string)requestData["FLAboutText"];
            }

            if (requestData.Contains("profileURL"))
            {
                userProfile.ProfileURL = (string)requestData["profileURL"];
            }

            if (requestData.Contains("home_region"))
            {
                try
                {
                    userProfile.HomeRegion = Convert.ToUInt64((string)requestData["home_region"]);
                }
                catch (ArgumentException)
                {
                    m_log.Error("[PROFILE]:Failed to set home region, Invalid Argument");
                }
                catch (FormatException)
                {
                    m_log.Error("[PROFILE]:Failed to set home region, Invalid Format");
                }
                catch (OverflowException)
                {
                    m_log.Error("[PROFILE]:Failed to set home region, Value was too large");
                }
            }
            if (requestData.Contains("home_region_id"))
            {
                UUID regionID;
                UUID.TryParse((string)requestData["home_region_id"], out regionID);
                userProfile.HomeRegionID = regionID;
            }
            if (requestData.Contains("home_pos_x"))
            {
                try
                {
                    userProfile.HomeLocationX = (float)Convert.ToDouble((string)requestData["home_pos_x"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home postion x");
                }
            }
            if (requestData.Contains("home_pos_y"))
            {
                try
                {
                    userProfile.HomeLocationY = (float)Convert.ToDouble((string)requestData["home_pos_y"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home postion y");
                }
            }
            if (requestData.Contains("home_pos_z"))
            {
                try
                {
                    userProfile.HomeLocationZ = (float)Convert.ToDouble((string)requestData["home_pos_z"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home postion z");
                }
            }
            if (requestData.Contains("home_look_x"))
            {
                try
                {
                    userProfile.HomeLookAtX = (float)Convert.ToDouble((string)requestData["home_look_x"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home lookat x");
                }
            }
            if (requestData.Contains("home_look_y"))
            {
                try
                {
                    userProfile.HomeLookAtY = (float)Convert.ToDouble((string)requestData["home_look_y"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home lookat y");
                }
            }
            if (requestData.Contains("home_look_z"))
            {
                try
                {
                    userProfile.HomeLookAtZ = (float)Convert.ToDouble((string)requestData["home_look_z"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home lookat z");
                }
            }
            if (requestData.Contains("user_flags"))
            {
                try
                {
                    userProfile.UserFlags = Convert.ToInt32((string)requestData["user_flags"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set user flags");
                }
            }
            if (requestData.Contains("god_level"))
            {
                try
                {
                    userProfile.GodLevel = Convert.ToInt32((string)requestData["god_level"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set god level");
                }
            }
            if (requestData.Contains("custom_type"))
            {
                try
                {
                    userProfile.CustomType = (string)requestData["custom_type"];
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set custom type");
                }
            }
            if (requestData.Contains("partner"))
            {
                try
                {
                    userProfile.Partner = new UUID((string)requestData["partner"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set partner");
                }
            }
            else
            {
                userProfile.Partner = UUID.Zero;
            }

            // call plugin!
            bool ret = m_userDataBaseService.UpdateUserProfile(userProfile);
            responseData["returnString"] = ret.ToString();
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCLogOffUserMethodUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Check IP Endpoint Access
            if (!TrustManager.Instance.IsTrustedPeer(remoteClient))
                return (Util.CreateTrustManagerAccessDeniedResponse()); 
            
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.Contains("avatar_uuid"))
            {
                try
                {
                    UUID userUUID = new UUID((string)requestData["avatar_uuid"]);
                    UUID RegionID = new UUID((string)requestData["region_uuid"]);
                    ulong regionhandle = (ulong)Convert.ToInt64((string)requestData["region_handle"]);
                    Vector3 position = new Vector3(
                        (float)Convert.ToDouble((string)requestData["region_pos_x"]),
                        (float)Convert.ToDouble((string)requestData["region_pos_y"]),
                        (float)Convert.ToDouble((string)requestData["region_pos_z"]));
                    Vector3 lookat = new Vector3(
                        (float)Convert.ToDouble((string)requestData["lookat_x"]),
                        (float)Convert.ToDouble((string)requestData["lookat_y"]),
                        (float)Convert.ToDouble((string)requestData["lookat_z"]));

                    handlerLogOffUser = OnLogOffUser;
                    if (handlerLogOffUser != null)
                        handlerLogOffUser(userUUID);

                    m_userDataBaseService.LogOffUser(userUUID, RegionID, regionhandle, position, lookat);
                }
                catch (FormatException)
                {
                    m_log.Warn("[LOGOUT]: Error in Logout XMLRPC Params");
                    return response;
                }
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return response;
        }

        #endregion


        public void HandleAgentLocation(UUID agentID, UUID regionID, ulong regionHandle)
        {
            // Force a refresh for this due to Commit below.
            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(agentID, true);
            if (userProfile != null)
            {
                userProfile.CurrentAgent.AgentOnline = true;
                userProfile.CurrentAgent.Region = regionID;
                userProfile.CurrentAgent.Handle = regionHandle;
                m_userDataBaseService.CommitAgent(ref userProfile);
            }
        }

        public void HandleAgentLeaving(UUID agentID, UUID regionID, ulong regionHandle)
        {
            // force a refresh due to Commit below
            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(agentID, true);
            if (userProfile != null)
            {
                if (userProfile.CurrentAgent.Region == regionID)
                {
                    UserAgentData userAgent = userProfile.CurrentAgent;
                    if (userAgent != null && userAgent.AgentOnline)
                    {
                        userAgent.AgentOnline = false;
                        userAgent.LogoutTime = Util.UnixTimeSinceEpoch();
                        if (regionID != UUID.Zero)
                        {
                            userAgent.Region = regionID;
                        }
                        userAgent.Handle = regionHandle;
                        userProfile.LastLogin = userAgent.LogoutTime;

                        m_userDataBaseService.CommitAgent(ref userProfile);

                        handlerLogOffUser = OnLogOffUser;
                        if (handlerLogOffUser != null)
                            handlerLogOffUser(agentID);
                    }
                }
            }
        }

        public void HandleRegionStartup(UUID regionID)
        {
            m_userDataBaseService.LogoutUsers(regionID);
        }

        public void HandleRegionShutdown(UUID regionID)
        {
            m_userDataBaseService.LogoutUsers(regionID);
        }
    }
}
