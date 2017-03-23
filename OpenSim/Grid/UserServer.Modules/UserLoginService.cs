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
using System.Text.RegularExpressions;
using System.Net;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Grid.UserServer.Modules
{
    public delegate void UserLoggedInAtLocation(UUID agentID, UUID sessionID, UUID RegionID,
                                                ulong regionhandle, float positionX, float positionY, float positionZ,
                                                string firstname, string lastname);

    public class RegionLoginFailure
    {
        private const int FAIL_LIMIT = 10;
        private const int FAIL_EXPIRY = 5 * 60; // in seconds (5 minutes)

        private uint _lastFail;
        private int _failures;
        public int Count
        {
            get { return _failures; }
        }

        // Allocating one of these implies the first failure.
        public RegionLoginFailure()
        {
            _lastFail = 0;
            _failures = 0;
            AddFailure();
        }
        // Increment the failure count and update the timestamp.
        public void AddFailure()
        {
            _failures++;
            _lastFail = (uint)Environment.TickCount;
        }

        // Returns true if fail count exceeds threshold
        public bool IsFailed()
        {
            return (_failures >= FAIL_LIMIT);
        }
        // Returns true if more than N minutes has passed since the last failure.
        public bool IsExpired()
        {
            return (((uint)Environment.TickCount - _lastFail) > (FAIL_EXPIRY*1000));
        }
        // Returns true if the count is exceeded and not expired.
        public bool IsActive()
        {
            return IsFailed() && !IsExpired();
        }

    }

    /// <summary>
    /// Login service used in grid mode.
    /// </summary>     
    public class UserLoginService : LoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event UserLoggedInAtLocation OnUserLoggedInAtLocation;

        private UserLoggedInAtLocation handlerUserLoggedInAtLocation;

        public UserConfig m_config;
        private readonly IRegionProfileRouter m_regionProfileService;

        protected BaseHttpServer m_httpServer;

        private Dictionary<string, RegionLoginFailure> _LastRegionFailure = new Dictionary<string, RegionLoginFailure>();

        public UserLoginService(
            OpenSim.Framework.Communications.UserProfileManager userManager,
            LibraryRootFolder libraryRootFolder, string mapServerURI, string profileServerURI,
            UserConfig config, string welcomeMess, IRegionProfileRouter regionProfileService)
            : base(userManager, libraryRootFolder, welcomeMess, mapServerURI, profileServerURI)
        {
            m_config = config;
            m_defaultHomeX = m_config.DefaultX;
            m_defaultHomeY = m_config.DefaultY;
            m_regionProfileService = regionProfileService;
        }

        public void RegisterHandlers(BaseHttpServer httpServer, bool registerOpenIDHandlers)
        {
            m_currencySymbol = m_config.CurrencySymbol;

            m_httpServer = httpServer;

            m_httpServer.AddHTTPHandler("login", ProcessHTMLLogin); 
            
            m_httpServer.AddXmlRPCHandler("login_to_simulator", XmlRpcLoginMethod);
            m_httpServer.AddXmlRPCHandler("set_login_params", XmlRPCSetLoginParams);
            m_httpServer.AddXmlRPCHandler("check_auth_session", XmlRPCCheckAuthSession, false);

            // New Style
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("login_to_simulator"), XmlRpcLoginMethod));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("set_login_params"), XmlRPCSetLoginParams));
            m_httpServer.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("check_auth_session"), XmlRPCCheckAuthSession));

            if (registerOpenIDHandlers)
            {
                // Handler for OpenID avatar identity pages
                m_httpServer.AddStreamHandler(new OpenIdStreamHandler("GET", "/users/", this));
                // Handlers for the OpenID endpoint server
                m_httpServer.AddStreamHandler(new OpenIdStreamHandler("POST", "/openid/server/", this));
                m_httpServer.AddStreamHandler(new OpenIdStreamHandler("GET", "/openid/server/", this));
            }
        }

        public void setloginlevel(int level)
        {
            m_minLoginLevel = level;
            m_log.InfoFormat("[GRID]: Login Level set to {0} ", level);
        }
        public void setwelcometext(string text)
        {
            m_welcomeMessage = text;
            m_log.InfoFormat("[GRID]: Login text  set to {0} ", text);
        }

        public override void LogOffUser(UserProfileData theUser, string message)
        {
            RegionProfileData SimInfo;
            try
            {
                SimInfo = m_regionProfileService.RequestSimProfileData(
                    theUser.CurrentAgent.Handle, m_config.GridServerURL,
                    m_config.GridSendKey, m_config.GridRecvKey);

                if (SimInfo == null)
                {
                    m_log.Error("[GRID]: Region user was in isn't currently logged in");
                    return;
                }
            }
            catch (Exception)
            {
                m_log.Error("[GRID]: Unable to look up region to log user off");
                return;
            }

            // Prepare notification
            Hashtable SimParams = new Hashtable();
            SimParams["agent_id"] = theUser.ID.ToString();
            SimParams["region_secret"] = theUser.CurrentAgent.SecureSessionID.ToString();
            SimParams["region_secret2"] = SimInfo.regionSecret;
            //m_log.Info(SimInfo.regionSecret);
            SimParams["regionhandle"] = theUser.CurrentAgent.Handle.ToString();
            SimParams["message"] = message;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            m_log.InfoFormat(
                "[ASSUMED CRASH]: Telling region {0} @ {1},{2} ({3}) that their agent is dead: {4}",
                SimInfo.regionName, SimInfo.regionLocX, SimInfo.regionLocY, SimInfo.httpServerURI,
                theUser.FirstName + " " + theUser.SurName);

            try
            {
                string methodName = "logoff_user";
                XmlRpcRequest GridReq = new XmlRpcRequest(methodName, SendParams);
                XmlRpcResponse GridResp = GridReq.Send(Util.XmlRpcRequestURI(SimInfo.httpServerURI, methodName), 6000);

                if (GridResp.IsFault)
                {
                    m_log.ErrorFormat(
                        "[LOGIN]: XMLRPC request for {0} failed, fault code: {1}, reason: {2}, This is likely an old region revision.",
                        SimInfo.httpServerURI, GridResp.FaultCode, GridResp.FaultString);
                }
            }
            catch (Exception)
            {
                m_log.Error("[LOGIN]: Error telling region to logout user!");
            }

            // Prepare notification
            SimParams = new Hashtable();
            SimParams["agent_id"] = theUser.ID.ToString();
            SimParams["region_secret"] = SimInfo.regionSecret;
            //m_log.Info(SimInfo.regionSecret);
            SimParams["regionhandle"] = theUser.CurrentAgent.Handle.ToString();
            SimParams["message"] = message;
            SendParams = new ArrayList();
            SendParams.Add(SimParams);

            m_log.InfoFormat(
                "[ASSUMED CRASH]: Telling region {0} @ {1},{2} ({3}) that their agent is dead: {4}",
                SimInfo.regionName, SimInfo.regionLocX, SimInfo.regionLocY, SimInfo.httpServerURI,
                theUser.FirstName + " " + theUser.SurName);

            try
            {
                string methodName = "logoff_user";
                XmlRpcRequest GridReq = new XmlRpcRequest(methodName, SendParams);
                XmlRpcResponse GridResp = GridReq.Send(Util.XmlRpcRequestURI(SimInfo.httpServerURI, methodName), 6000);

                if (GridResp.IsFault)
                {
                    m_log.ErrorFormat(
                        "[LOGIN]: XMLRPC request for {0} failed, fault code: {1}, reason: {2}, This is likely an old region revision.",
                        SimInfo.httpServerURI, GridResp.FaultCode, GridResp.FaultString);
                }
            }
            catch (Exception)
            {
                m_log.Error("[LOGIN]: Error telling region to logout user!");
            }
            //base.LogOffUser(theUser);
        }

        protected override RegionInfo RequestClosestRegion(string region)
        {
            RegionProfileData profileData = m_regionProfileService.RequestSimProfileData(region,
                                                                                         m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);

            if (profileData != null)
            {
                return profileData.ToRegionInfo();
            }
            else
            {
                return null;
            }
        }

        protected override RegionInfo GetRegionInfo(ulong homeRegionHandle)
        {
            RegionProfileData profileData = m_regionProfileService.RequestSimProfileData(homeRegionHandle,
                                                                                         m_config.GridServerURL, m_config.GridSendKey,
                                                                                         m_config.GridRecvKey);
            if (profileData != null)
            {
                return profileData.ToRegionInfo();
            }
            else
            {
                return null;
            }
        }

        protected override RegionInfo GetRegionInfo(UUID homeRegionId)
        {
            RegionProfileData profileData = m_regionProfileService.RequestSimProfileData(homeRegionId,
                                                                                         m_config.GridServerURL, m_config.GridSendKey,
                                                                                         m_config.GridRecvKey);
            if (profileData != null)
            {
                return profileData.ToRegionInfo();
            }
            else
            {
                return null;
            }
        }

        protected override bool PrepareLoginToRegion(RegionInfo regionInfo, UserProfileData user, LoginResponse response, string clientVersion)
        {
            return PrepareLoginToRegion(RegionProfileData.FromRegionInfo(regionInfo), user, response, clientVersion);
        }

        /// <summary>
        /// Prepare a login to the given region.  This involves both telling the region to expect a connection
        /// and appropriately customising the response to the user.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="user"></param>
        /// <param name="response"></param>
        /// <returns>true if the region was successfully contacted, false otherwise</returns>
        private bool PrepareLoginToRegion(RegionProfileData regionInfo, UserProfileData user, LoginResponse response, string clientVersion)
        {
            string regionName = regionInfo.regionName;
            bool regionSuccess = false; // region communication result
            bool userSuccess = true;    // user login succeeded result, for now

            try
            {
                lock (_LastRegionFailure)
                {
                    if (_LastRegionFailure.ContainsKey(regionName))
                    {
                        // region failed previously
                        RegionLoginFailure failure = _LastRegionFailure[regionName];
                        if (failure.IsExpired())
                        {
                            // failure has expired, retry this region again
                            _LastRegionFailure.Remove(regionName);
//                            m_log.WarnFormat("[LOGIN]: Region '{0}' was previously down, retrying.", regionName);
                        }
                        else
                        {
                            if (failure.IsFailed())
                            {
//                                m_log.WarnFormat("[LOGIN]: Region '{0}' was recently down, skipping.", regionName);
                                return false;   // within 5 minutes, don't repeat attempt
                            }
//                            m_log.WarnFormat("[LOGIN]: Region '{0}' was recently down but under threshold, retrying.", regionName);
                        }
                    }
                }

                response.SimAddress = regionInfo.OutsideIpOrResolvedHostname;
                response.SimPort = regionInfo.serverPort;
                response.RegionX = regionInfo.regionLocX;
                response.RegionY = regionInfo.regionLocY;

                string capsPath = CapsUtil.GetRandomCapsObjectPath();

                response.SeedCapability = CapsUtil.GetFullCapsSeedURL(regionInfo.httpServerURI, capsPath);

                // Notify the target of an incoming user
                m_log.InfoFormat(
                    "[LOGIN]: Telling {0} @ {1},{2} ({3}) to prepare for client connection",
                    regionInfo.regionName, response.RegionX, response.RegionY, response.SeedCapability);

                // Update agent with target sim
                user.CurrentAgent.Region = regionInfo.UUID;
                user.CurrentAgent.Handle = regionInfo.regionHandle;

                // Prepare notification
                Hashtable loginParams = new Hashtable();
                loginParams["session_id"] = user.CurrentAgent.SessionID.ToString();
                loginParams["secure_session_id"] = user.CurrentAgent.SecureSessionID.ToString();
                loginParams["firstname"] = user.FirstName;
                loginParams["lastname"] = user.SurName;
                loginParams["agent_id"] = user.ID.ToString();
                loginParams["circuit_code"] = (Int32)Convert.ToUInt32(response.CircuitCode);
                loginParams["startpos_x"] = user.CurrentAgent.Position.X.ToString();
                loginParams["startpos_y"] = user.CurrentAgent.Position.Y.ToString();
                loginParams["startpos_z"] = user.CurrentAgent.Position.Z.ToString();
                loginParams["regionhandle"] = user.CurrentAgent.Handle.ToString();
                loginParams["caps_path"] = capsPath;
                loginParams["client_version"] = clientVersion;

                // Get appearance
                AvatarAppearance appearance = m_userManager.GetUserAppearance(user.ID);
                if (appearance != null)
                {
                    loginParams["appearance"] = appearance.ToHashTable();
                    m_log.DebugFormat("[LOGIN]: Found appearance version {0} for {1} {2}", appearance.Serial, user.FirstName, user.SurName);
                }
                else
                {
                    m_log.DebugFormat("[LOGIN]: Appearance not for {0} {1}. Creating default.", user.FirstName, user.SurName);
                    appearance = new AvatarAppearance(user.ID);
                }

                // Tell the client the COF version so it can use cached appearance if it matches.
                response.CofVersion = appearance.Serial.ToString();
                loginParams["cof_version"] = response.CofVersion;

                ArrayList SendParams = new ArrayList();
                SendParams.Add(loginParams);
                SendParams.Add(m_config.GridSendKey);

                // Send
                const string METHOD_NAME = "expect_user";
                XmlRpcRequest GridReq = new XmlRpcRequest(METHOD_NAME, SendParams);
                XmlRpcResponse GridResp = GridReq.Send(Util.XmlRpcRequestURI(regionInfo.httpServerURI, METHOD_NAME), 6000);

                if (!GridResp.IsFault)
                {
                    // We've received a successful response from the region.
                    // From the perspective of communicating with the region, we were able to get a response.
                    // Do not mark the region down if it responds, even if it doesn't allow the user into the region.
                    regionSuccess = true;

                    if (GridResp.Value != null)
                    {
                        Hashtable resp = (Hashtable)GridResp.Value;
                        if (resp.ContainsKey("success"))
                        {
                            if ((string)resp["success"] == "FALSE")
                            {
                                // Tell the user their login failed.
                                userSuccess = false;
                            }
                        }
                        if (!userSuccess)
                        {
                            if (resp.ContainsKey("reason"))
                            {
                                response.ErrorMessage = resp["reason"].ToString();
                            }
                        }
                    }
                    
                    if (userSuccess)
                    {
                        handlerUserLoggedInAtLocation = OnUserLoggedInAtLocation;
                        if (handlerUserLoggedInAtLocation != null)
                        {
                            handlerUserLoggedInAtLocation(user.ID, user.CurrentAgent.SessionID,
                                                          user.CurrentAgent.Region,
                                                          user.CurrentAgent.Handle,
                                                          user.CurrentAgent.Position.X,
                                                          user.CurrentAgent.Position.Y,
                                                          user.CurrentAgent.Position.Z,
                                                          user.FirstName, user.SurName);
                        }
                    }
                    else
                    {
                        m_log.ErrorFormat("[LOGIN]: Region responded that it is not available to accept a login from {0} at this time.", user.Name);
                    }
                }
                else
                {
                    m_log.ErrorFormat("[LOGIN]: XmlRpc request to region failed with message {0}, code {1} ", GridResp.FaultString, GridResp.FaultCode);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[LOGIN]: Region not available for login, {0}", e);
            }

            lock (_LastRegionFailure)
            {
                if (_LastRegionFailure.ContainsKey(regionName))
                {
                    RegionLoginFailure failure = _LastRegionFailure[regionName];
                    if (regionSuccess)
                    {   // Success, so if we've been storing this as a failed region, remove that from the failed list.
                        m_log.WarnFormat("[LOGIN]: Region '{0}' recently down, is available again.", regionName);
                        _LastRegionFailure.Remove(regionName);
                    }
                    else
                    {
                        // Region not available, update cache with incremented count.
                        failure.AddFailure();
//                        m_log.WarnFormat("[LOGIN]: Region '{0}' is still down ({1}).", regionName, failure.Count);
                    }
                }
                else
                {
                    if (!regionSuccess)
                    {
                        // Region not available, cache that temporarily.
                        m_log.WarnFormat("[LOGIN]: Region '{0}' is down, marking.", regionName);
                        _LastRegionFailure[regionName] = new RegionLoginFailure();
                    }
                }
            }
            return regionSuccess && userSuccess;
        }

        public XmlRpcResponse XmlRPCSetLoginParams(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            Hashtable responseData = new Hashtable();

            UUID uid;
            string pass = requestData["password"].ToString();

            if (!UUID.TryParse((string)requestData["avatar_uuid"], out uid))
            {
                responseData["error"] = "No authorization";
                response.Value = responseData;
                return response;
            }

            userProfile = m_userManager.GetUserProfile(uid);

            if (userProfile == null ||
                (!AuthenticateUser(userProfile, pass)) ||
                userProfile.GodLevel < 200)
            {
                responseData["error"] = "No authorization";
                response.Value = responseData;
                return response;
            }

            if (requestData.ContainsKey("login_level"))
            {
                m_minLoginLevel = Convert.ToInt32(requestData["login_level"]);
            }

            if (requestData.ContainsKey("login_motd"))
            {
                m_welcomeMessage = requestData["login_motd"].ToString();
            }

            response.Value = responseData;
            return response;
        }
    }
}
