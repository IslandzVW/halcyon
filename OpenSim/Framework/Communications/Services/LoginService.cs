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
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Net;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;
using OpenSim.Data;

namespace OpenSim.Framework.Communications.Services
{
    public abstract class LoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_welcomeMessage = "Welcome to InWorldz";
        protected string m_MapServerURI = String.Empty;
        protected string m_ProfileServerURI = String.Empty;
        protected int m_minLoginLevel = 0;
        protected UserProfileManager m_userManager = null;

        /// <summary>
        /// Used during login to send the skeleton of the OpenSim Library to the client.
        /// </summary>
        protected LibraryRootFolder m_libraryRootFolder;

        protected uint m_defaultHomeX;
        protected uint m_defaultHomeY;

        protected string m_currencySymbol = String.Empty;

        private const string BAD_VIEWERS_FILE = "badviewers.txt";
        private List<string> _badViewerStrings = new List<string>();

        // For new users' first-time logins
        private const string DEFAULT_LOGINS_FILE = "defaultlogins.txt";
        private List<string> _DefaultLoginsList = new List<string>();

        // For returning users' where the preferred region is down
        private const string DEFAULT_REGIONS_FILE = "defaultregions.txt";
        private List<string> _DefaultRegionsList = new List<string>();

        /// <summary>
        /// LoadStringListFromFile
        /// </summary>
        /// <param name="theList"></param>
        /// <param name="fn"></param>
        /// <param name="desc"></param>
        private void LoadStringListFromFile(List<string> theList, string fn, string desc)
        {
            if (File.Exists(fn))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader(fn))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (!String.IsNullOrEmpty(line) && !line.StartsWith(";") && !line.StartsWith("//"))
                        {
                            theList.Add(line);
                            m_log.InfoFormat("[LOGINSERVICE] Added {0} {1}", desc, line);
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="userManager"></param>
        /// <param name="libraryRootFolder"></param>
        /// <param name="welcomeMess"></param>
        public LoginService(UserProfileManager userManager, LibraryRootFolder libraryRootFolder,
                            string welcomeMess, string mapServerURI, string profileServerURI)
        {
            m_userManager = userManager;
            m_libraryRootFolder = libraryRootFolder;

            if (!String.IsNullOrEmpty(welcomeMess))
                m_welcomeMessage = welcomeMess;
            if (!String.IsNullOrEmpty(mapServerURI))
                m_MapServerURI = mapServerURI;
            if (!String.IsNullOrEmpty(profileServerURI))
                m_ProfileServerURI = profileServerURI;

            // For new users' first-time logins
            LoadDefaultLoginsFromFile(DEFAULT_LOGINS_FILE);
            // For returning users' where the preferred region is down
            LoadDefaultRegionsFromFile(DEFAULT_REGIONS_FILE);
            LoadStringListFromFile(_badViewerStrings, BAD_VIEWERS_FILE, "blacklisted viewer");
        }

        /// <summary>
        /// If the user is already logged in, try to notify the region that the user they've got is dead.
        /// </summary>
        /// <param name="theUser"></param>
        public virtual void LogOffUser(UserProfileData theUser, string message)
        {
        }

        public void DumpRegionsList(List<string> theList, string desc)
        {
                m_log.Info(desc + ":");
                foreach (string location in theList)
                    m_log.Info("  " + location);
        }

        public List<string> LoadRegionsFromFile(string fileName, string desc)
        {
            List<string> newList = new List<string>();
            LoadStringListFromFile(newList, fileName, desc);
            if (newList.Count < 1)
                m_log.ErrorFormat("{0}: No locations found.", fileName);
            else
                m_log.InfoFormat("{0} updated with {1} locations.", desc, newList.Count);
            return newList;
        }
        // For new users' first-time logins
        public void LoadDefaultLoginsFromFile(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
                DumpRegionsList(_DefaultLoginsList, "Default login locations for new users");
            else
                _DefaultLoginsList = LoadRegionsFromFile(fileName, "Default login locations for new users");
        }
        // For returning users' where the preferred region is down
        public void LoadDefaultRegionsFromFile(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
                DumpRegionsList(_DefaultRegionsList, "Default region locations");
            else
                _DefaultRegionsList = LoadRegionsFromFile(fileName, "Default region locations");
        }

        private const string BANS_FILE = "bans.txt";
        private List<string> bannedIPs = new List<string>();
        private DateTime bannedIPsLoadedAt = DateTime.MinValue;
        private void LoadBannedIPs()
        {
            if (File.Exists(BANS_FILE))
            {
                DateTime changedAt = File.GetLastWriteTime(BANS_FILE);
                lock (bannedIPs)    // don't reload it in parallel, block login if reloading
                {
                    if ( changedAt > bannedIPsLoadedAt)
                    {
                        bannedIPsLoadedAt = DateTime.Now;
                        bannedIPs.Clear();
                        string[] lines = File.ReadAllLines("bans.txt");
                        foreach (string line in lines)
                        {
                            bannedIPs.Add(line.Trim());
                        }
                    }
                }
            }
        }

        // Assumes IPstr is trimmed.
        private bool IsBannedIP(string IPstr)
        {
            LoadBannedIPs();    // refresh, if changed

            lock (bannedIPs)
            {
                foreach (string ban in bannedIPs)
                {
                    if (IPstr.StartsWith(ban))
                        return true;
                }
            }
            return false;
        }


        private HashSet<string> _loginsProcessing = new HashSet<string>();

        /// <summary>
        /// Called when we receive the client's initial XMLRPC login_to_simulator request message
        /// </summary>
        /// <param name="request">The XMLRPC request</param>
        /// <returns>The response to send</returns>
        public virtual XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            string loginUsername = null;

            try
            {
                LoginResponse logResponse = new LoginResponse();

                IPAddress IPaddr = remoteClient.Address;
                string IPstr = IPaddr.ToString();
                if (this.IsBannedIP(IPstr))
                {
                    m_log.WarnFormat("[LOGIN]: Denying login, IP {0} is BANNED.", IPstr);
                    return logResponse.CreateIPBannedResponseLLSD();
                }

                XmlRpcResponse response = new XmlRpcResponse();
                Hashtable requestData = (Hashtable)request.Params[0];

                SniffLoginKey((Uri)request.Params[2], requestData);

                bool GoodXML = (requestData.Contains("first") && requestData.Contains("last") &&
                                (requestData.Contains("passwd") || requestData.Contains("web_login_key")));

                string firstname = null;
                string lastname = null;

                if (GoodXML)
                {
                    //make sure the user isn't already trying to log in

                    firstname = (string)requestData["first"];
                    lastname = (string)requestData["last"];

                    loginUsername = firstname + " " + lastname;

                    lock (_loginsProcessing)
                    {
                        if (_loginsProcessing.Contains(loginUsername))
                        {
                            return logResponse.CreateAlreadyLoggedInResponse();
                        }
                        else
                        {
                            _loginsProcessing.Add(loginUsername);
                        }
                    }
                }

                string startLocationRequest = "last";

                UserProfileData userProfile;
                
                string clientChannel = "Unknown";
                string clientVersion = "Unknown";
                string clientPlatform = "Unknown";
                string clientPlatformVer = "Unknown";

                if (GoodXML)
                {
                    if (requestData.Contains("start"))
                    {
                        startLocationRequest = (string)requestData["start"];
                    }

                    m_log.InfoFormat(
                        "[LOGIN BEGIN]: XMLRPC Received login request message from user '{0}' '{1}'",
                        firstname, lastname);

                    if (requestData.Contains("channel"))
                    {
                        clientChannel = (string)requestData["channel"];
                    }
                    if (requestData.Contains("version"))
                    {
                        clientVersion = (string)requestData["version"];
                    }
                    if (requestData.Contains("platform"))
                    {
                        clientPlatform = (string)requestData["platform"];
                    }
                    if (requestData.Contains("platform_version"))
                    {
                        clientPlatformVer = (string)requestData["platform_version"];
                    }

                    if (this.IsViewerBlacklisted(clientVersion))
                    {
                        m_log.WarnFormat("[LOGIN]: Denying login, Client {0} is blacklisted", clientVersion);
                        return logResponse.CreateViewerNotAllowedResponse();
                    }

                    m_log.InfoFormat(
                        "[LOGIN]: XMLRPC Client is {0} {1} on {2} {3}, start location is {4}",
                            clientChannel, clientVersion, clientPlatform, clientPlatformVer, startLocationRequest);

                    if (!TryAuthenticateXmlRpcLogin(request, firstname, lastname, out userProfile))
                    {
                        return logResponse.CreateLoginFailedResponse();
                    }
                }
                else
                {
                    m_log.Info("[LOGIN END]: XMLRPC login_to_simulator login message did not contain all the required data");

                    return logResponse.CreateGridErrorResponse();
                }

                if (userProfile.GodLevel < m_minLoginLevel)
                {
                    return logResponse.CreateLoginBlockedResponse();
                }
                else
                {
                    // If we already have a session...
                    if (userProfile.CurrentAgent != null && userProfile.CurrentAgent.AgentOnline)
                    {
                        // Force a refresh for this due to Commit below
                        UUID userID = userProfile.ID;
                        userProfile = m_userManager.GetUserProfile(userID,true);
                        // on an error, return the former error we returned before recovery was supported.
                        if (userProfile == null) 
                            return logResponse.CreateAlreadyLoggedInResponse();

                        //TODO: The following statements can cause trouble:
                        //      If agentOnline could not turn from true back to false normally
                        //      because of some problem, for instance, the crashment of server or client,
                        //      the user cannot log in any longer.
                        userProfile.CurrentAgent.AgentOnline = false;
                        userProfile.CurrentAgent.LogoutTime = Util.UnixTimeSinceEpoch();

                        m_userManager.CommitAgent(ref userProfile);

                        // try to tell the region that their user is dead.
                        LogOffUser(userProfile, " XMLRPC You were logged off because you logged in from another location");

                        // Don't reject the login. We've already cleaned it up, above.
                        m_log.InfoFormat(
                            "[LOGIN END]: XMLRPC Reset user {0} {1} that we believe is already logged in",
                            firstname, lastname);
                        // return logResponse.CreateAlreadyLoggedInResponse();
                    }

                    // Otherwise...
                    // Create a new agent session

                    m_userManager.ResetAttachments(userProfile.ID);


                    CreateAgent(userProfile, request);

                    // We need to commit the agent right here, even though the userProfile info is not complete
                    // at this point. There is another commit further down.
                    // This is for the new sessionID to be stored so that the region can check it for session authentication. 
                    // CustomiseResponse->PrepareLoginToRegion
                    CommitAgent(ref userProfile);

                    try
                    {
                        UUID agentID = userProfile.ID;
                        InventoryData inventData = null;

                        try
                        {
                            inventData = GetInventorySkeleton(agentID);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[LOGIN END]: Error retrieving inventory skeleton of agent {0} - {1}",
                                agentID, e);

                            return logResponse.CreateLoginInventoryFailedResponse();
                        }

                        if (inventData != null)
                        {
                            ArrayList AgentInventoryArray = inventData.InventoryArray;

                            Hashtable InventoryRootHash = new Hashtable();
                            InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                            ArrayList InventoryRoot = new ArrayList();
                            InventoryRoot.Add(InventoryRootHash);
                            userProfile.RootInventoryFolderID = inventData.RootFolderID;

                            logResponse.InventoryRoot = InventoryRoot;
                            logResponse.InventorySkeleton = AgentInventoryArray;
                        }

                        // Inventory Library Section
                        Hashtable InventoryLibRootHash = new Hashtable();
                        InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                        ArrayList InventoryLibRoot = new ArrayList();
                        InventoryLibRoot.Add(InventoryLibRootHash);

                        logResponse.InventoryLibRoot = InventoryLibRoot;
                        logResponse.InventoryLibraryOwner = GetLibraryOwner();
                        logResponse.InventoryLibrary = GetInventoryLibrary();

                        logResponse.CircuitCode = Util.RandomClass.Next();
                        logResponse.Lastname = userProfile.SurName;
                        logResponse.Firstname = userProfile.FirstName;
                        logResponse.AgentID = agentID;
                        logResponse.SessionID = userProfile.CurrentAgent.SessionID;
                        logResponse.SecureSessionID = userProfile.CurrentAgent.SecureSessionID;
                        logResponse.Message = GetMessage();
                        logResponse.MapServerURI = m_MapServerURI;
                        logResponse.ProfileServerURI = m_ProfileServerURI;
                        logResponse.BuddList = ConvertFriendListItem(m_userManager.GetUserFriendList(agentID));
                        logResponse.StartLocation = startLocationRequest;
                        logResponse.CurrencySymbol = m_currencySymbol;

//                        m_log.WarnFormat("[LOGIN END]: >>> Login response for {0} SSID={1}", logResponse.AgentID, logResponse.SecureSessionID);

                        if (CustomiseResponse(logResponse, userProfile, startLocationRequest, clientVersion))
                        {
                            userProfile.LastLogin = userProfile.CurrentAgent.LoginTime;
                            CommitAgent(ref userProfile);

                            // If we reach this point, then the login has successfully logged onto the grid
                            if (StatsManager.UserStats != null)
                                StatsManager.UserStats.AddSuccessfulLogin();

                            m_log.DebugFormat(
                                "[LOGIN END]: XMLRPC Authentication of user {0} {1} successful.  Sending response to client.",
                                firstname, lastname);

                            return logResponse.ToXmlRpcResponse();
                        }
                        else
                        {
                            m_log.ErrorFormat("[LOGIN END]: XMLRPC informing user {0} {1} that login failed due to an unavailable region", firstname, lastname);
                            return logResponse.CreateDeadRegionResponse();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[LOGIN END]: XMLRPC Login failed, " + e);
                        m_log.Error(e.StackTrace);
                    }
                }

                m_log.Info("[LOGIN END]: XMLRPC Login failed.  Sending back blank XMLRPC response");
                return response;
            }
            finally
            {
                if (loginUsername != null)
                {
                    lock (_loginsProcessing)
                    {
                        _loginsProcessing.Remove(loginUsername);
                    }
                }
            }
        }

        private bool IsViewerBlacklisted(string clientVersion)
        {
            foreach (string badClient in _badViewerStrings)
            {
                if (clientVersion.Contains(badClient))
                {
                    return true;
                }
            }

            return false;
        }

        protected virtual bool TryAuthenticateXmlRpcLogin(
            XmlRpcRequest request, string firstname, string lastname, out UserProfileData userProfile)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            bool GoodLogin = false;

            userProfile = GetTheUser(firstname, lastname);
            if (userProfile == null)
            {
                m_log.Info("[LOGIN END]: XMLRPC Could not find a profile for " + firstname + " " + lastname);
            }
            else
            {
                if (requestData.Contains("passwd"))
                {
                    string passwd = (string)requestData["passwd"];
                    GoodLogin = AuthenticateUser(userProfile, passwd);
                }
                if (!GoodLogin && (requestData.Contains("web_login_key")))
                {
                    try
                    {
                        UUID webloginkey = new UUID((string)requestData["web_login_key"]);
                        GoodLogin = AuthenticateUser(userProfile, webloginkey);
                    }
                    catch (Exception e)
                    {
                        m_log.InfoFormat(
                            "[LOGIN END]: XMLRPC  Bad web_login_key: {0} for user {1} {2}, exception {3}",
                            requestData["web_login_key"], firstname, lastname, e);
                    }
                }
            }

            return GoodLogin;
        }

        protected virtual bool TryAuthenticateLLSDLogin(string firstname, string lastname, string passwd, out UserProfileData userProfile)
        {
            bool GoodLogin = false;
            userProfile = GetTheUser(firstname, lastname);
            if (userProfile == null)
            {
                m_log.Info("[LOGIN]: LLSD Could not find a profile for " + firstname + " " + lastname);

                return false;
            }

            GoodLogin = AuthenticateUser(userProfile, passwd);
            return GoodLogin;
        }

        public Hashtable ProcessHTMLLogin(Hashtable keysvals)
        {
            // Matches all unspecified characters
            // Currently specified,; lowercase letters, upper case letters, numbers, underline
            //    period, space, parens, and dash.

            Regex wfcut = new Regex("[^a-zA-Z0-9_\\.\\$ \\(\\)\\-]");

            Hashtable returnactions = new Hashtable();
            int statuscode = 200;

            string firstname = String.Empty;
            string lastname = String.Empty;
            string location = String.Empty;
            string region = String.Empty;
            string grid = String.Empty;
            string channel = String.Empty;
            string version = String.Empty;
            string lang = String.Empty;
            string password = String.Empty;
            string errormessages = String.Empty;

            // the client requires the HTML form field be named 'username'
            // however, the data it sends when it loads the first time is 'firstname'
            // another one of those little nuances.

            if (keysvals.Contains("firstname"))
                firstname = wfcut.Replace((string)keysvals["firstname"], String.Empty, 99999);

            if (keysvals.Contains("username"))
                firstname = wfcut.Replace((string)keysvals["username"], String.Empty, 99999);

            if (keysvals.Contains("lastname"))
                lastname = wfcut.Replace((string)keysvals["lastname"], String.Empty, 99999);

            if (keysvals.Contains("location"))
                location = wfcut.Replace((string)keysvals["location"], String.Empty, 99999);

            if (keysvals.Contains("region"))
                region = wfcut.Replace((string)keysvals["region"], String.Empty, 99999);

            if (keysvals.Contains("grid"))
                grid = wfcut.Replace((string)keysvals["grid"], String.Empty, 99999);

            if (keysvals.Contains("channel"))
                channel = wfcut.Replace((string)keysvals["channel"], String.Empty, 99999);

            if (keysvals.Contains("version"))
                version = wfcut.Replace((string)keysvals["version"], String.Empty, 99999);

            if (keysvals.Contains("lang"))
                lang = wfcut.Replace((string)keysvals["lang"], String.Empty, 99999);

            if (keysvals.Contains("password"))
                password = wfcut.Replace((string)keysvals["password"], String.Empty, 99999);

            // load our login form.
            string loginform = GetLoginForm(firstname, lastname, location, region, grid, channel, version, lang, password, errormessages);

            if (keysvals.ContainsKey("show_login_form"))
            {
                UserProfileData user = GetTheUser(firstname, lastname);
                bool goodweblogin = false;

                if (user != null)
                    goodweblogin = AuthenticateUser(user, password);

                if (goodweblogin)
                {
                    UUID webloginkey = UUID.Random();
                    m_userManager.StoreWebLoginKey(user.ID, webloginkey);
                    //statuscode = 301;

                    //                    string redirectURL = "about:blank?redirect-http-hack=" +
                    //                                         HttpUtility.UrlEncode("secondlife:///app/login?first_name=" + firstname + "&last_name=" +
                    //                                                               lastname +
                    //                                                               "&location=" + location + "&grid=Other&web_login_key=" + webloginkey.ToString());
                    //m_log.Info("[WEB]: R:" + redirectURL);
                    returnactions["int_response_code"] = statuscode;
                    //returnactions["str_redirect_location"] = redirectURL;
                    //returnactions["str_response_string"] = "<HTML><BODY>GoodLogin</BODY></HTML>";
                    returnactions["str_response_string"] = webloginkey.ToString();
                }
                else
                {
                    errormessages = "The Username and password supplied did not match our records. Check your caps lock and try again";

                    loginform = GetLoginForm(firstname, lastname, location, region, grid, channel, version, lang, password, errormessages);
                    returnactions["int_response_code"] = statuscode;
                    returnactions["str_response_string"] = loginform;
                }
            }
            else
            {
                returnactions["int_response_code"] = statuscode;
                returnactions["str_response_string"] = loginform;
            }
            return returnactions;
        }

        public string GetLoginForm(string firstname, string lastname, string location, string region,
                                   string grid, string channel, string version, string lang,
                                   string password, string errormessages)
        {
            // inject our values in the form at the markers

            string loginform = String.Empty;
            string file = Path.Combine(Util.configDir(), "http_loginform.html");
            if (!File.Exists(file))
            {
                loginform = GetDefaultLoginForm();
            }
            else
            {
                StreamReader sr = File.OpenText(file);
                loginform = sr.ReadToEnd();
                sr.Close();
            }

            loginform = loginform.Replace("[$firstname]", firstname);
            loginform = loginform.Replace("[$lastname]", lastname);
            loginform = loginform.Replace("[$location]", location);
            loginform = loginform.Replace("[$region]", region);
            loginform = loginform.Replace("[$grid]", grid);
            loginform = loginform.Replace("[$channel]", channel);
            loginform = loginform.Replace("[$version]", version);
            loginform = loginform.Replace("[$lang]", lang);
            loginform = loginform.Replace("[$password]", password);
            loginform = loginform.Replace("[$errors]", errormessages);

            return loginform;
        }

        public string GetDefaultLoginForm()
        {
            string responseString =
                "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">";
            responseString += "<html xmlns=\"http://www.w3.org/1999/xhtml\">";
            responseString += "<head>";
            responseString += "<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />";
            responseString += "<meta http-equiv=\"cache-control\" content=\"no-cache\">";
            responseString += "<meta http-equiv=\"Pragma\" content=\"no-cache\">";
            responseString += "<title>InWorldz Login</title>";
            responseString += "<body><br />";
            responseString += "<div id=\"login_box\">";

            responseString += "<form action=\"/go.cgi\" method=\"GET\" id=\"login-form\">";

            responseString += "<div id=\"message\">[$errors]</div>";
            responseString += "<fieldset id=\"firstname\">";
            responseString += "<legend>First Name:</legend>";
            responseString += "<input type=\"text\" id=\"firstname_input\" size=\"15\" maxlength=\"100\" name=\"username\" value=\"[$firstname]\" />";
            responseString += "</fieldset>";
            responseString += "<fieldset id=\"lastname\">";
            responseString += "<legend>Last Name:</legend>";
            responseString += "<input type=\"text\" size=\"15\" maxlength=\"100\" name=\"lastname\" value=\"[$lastname]\" />";
            responseString += "</fieldset>";
            responseString += "<fieldset id=\"password\">";
            responseString += "<legend>Password:</legend>";
            responseString += "<table cellspacing=\"0\" cellpadding=\"0\" border=\"0\">";
            responseString += "<tr>";
            responseString += "<td colspan=\"2\"><input type=\"password\" size=\"15\" maxlength=\"100\" name=\"password\" value=\"[$password]\" /></td>";
            responseString += "</tr>";
            responseString += "<tr>";
            responseString += "<td valign=\"middle\"><input type=\"checkbox\" name=\"remember_password\" id=\"remember_password\" [$remember_password] style=\"margin-left:0px;\"/></td>";
            responseString += "<td><label for=\"remember_password\">Remember password</label></td>";
            responseString += "</tr>";
            responseString += "</table>";
            responseString += "</fieldset>";
            responseString += "<input type=\"hidden\" name=\"show_login_form\" value=\"FALSE\" />";
            responseString += "<input type=\"hidden\" name=\"method\" value=\"login\" />";
            responseString += "<input type=\"hidden\" id=\"grid\" name=\"grid\" value=\"[$grid]\" />";
            responseString += "<input type=\"hidden\" id=\"region\" name=\"region\" value=\"[$region]\" />";
            responseString += "<input type=\"hidden\" id=\"location\" name=\"location\" value=\"[$location]\" />";
            responseString += "<input type=\"hidden\" id=\"channel\" name=\"channel\" value=\"[$channel]\" />";
            responseString += "<input type=\"hidden\" id=\"version\" name=\"version\" value=\"[$version]\" />";
            responseString += "<input type=\"hidden\" id=\"lang\" name=\"lang\" value=\"[$lang]\" />";
            responseString += "<div id=\"submitbtn\">";
            responseString += "<input class=\"input_over\" type=\"submit\" value=\"Connect\" />";
            responseString += "</div>";
            responseString += "<div id=\"connecting\" style=\"visibility:hidden\"> Connecting...</div>";

            responseString += "<div id=\"helplinks\"><!---";
            responseString += "<a href=\"#join now link\" target=\"_blank\"></a> | ";
            responseString += "<a href=\"#forgot password link\" target=\"_blank\"></a>";
            responseString += "---></div>";

            responseString += "<div id=\"channelinfo\"> [$channel] | [$version]=[$lang]</div>";
            responseString += "</form>";
            responseString += "<script language=\"JavaScript\">";
            responseString += "document.getElementById('firstname_input').focus();";
            responseString += "</script>";
            responseString += "</div>";
            responseString += "</div>";
            responseString += "</body>";
            responseString += "</html>";

            return responseString;
        }

        /// <summary>
        /// Saves a target agent to the database
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <returns>Successful?</returns>
        public bool CommitAgent(ref UserProfileData profile)
        {
            return m_userManager.CommitAgent(ref profile);
        }

        /// <summary>
        /// Checks a user against it's password hash
        /// </summary>
        /// <param name="profile">The users profile</param>
        /// <param name="password">The supplied password</param>
        /// <returns>Authenticated?</returns>
        public virtual bool AuthenticateUser(UserProfileData profile, string password)
        {
            bool passwordSuccess = false;
            //m_log.InfoFormat("[LOGIN]: Authenticating {0} {1} ({2})", profile.FirstName, profile.SurName, profile.ID);

            // Web Login method seems to also occasionally send the hashed password itself

            // we do this to get our hash in a form that the server password code can consume
            // when the web-login-form submits the password in the clear (supposed to be over SSL!)
            if (!password.StartsWith("$1$"))
                password = "$1$" + Util.Md5Hash(password);

            password = password.Remove(0, 3); //remove $1$

            string s = Util.Md5Hash(password + ":" + profile.PasswordSalt);
            // Testing...
            //m_log.Info("[LOGIN]: SubHash:" + s + " userprofile:" + profile.passwordHash);
            //m_log.Info("[LOGIN]: userprofile:" + profile.passwordHash + " SubCT:" + password);

            passwordSuccess = (profile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                               || profile.PasswordHash.Equals(password, StringComparison.InvariantCultureIgnoreCase));

            return passwordSuccess;
        }

        public virtual bool AuthenticateUser(UserProfileData profile, UUID webloginkey)
        {
            bool passwordSuccess = false;
            m_log.InfoFormat("[LOGIN]: Authenticating {0} {1} ({2})", profile.FirstName, profile.SurName, profile.ID);

            // Match web login key unless it's the default weblogin key UUID.Zero
            passwordSuccess = ((profile.WebLoginKey == webloginkey) && profile.WebLoginKey != UUID.Zero);

            return passwordSuccess;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="request"></param>
        public void CreateAgent(UserProfileData profile, XmlRpcRequest request)
        {
            m_userManager.CreateAgent(profile, request);
        }

        public void CreateAgent(UserProfileData profile, OSD request)
        {
            m_userManager.CreateAgent(profile, request);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <returns></returns>
        public virtual UserProfileData GetTheUser(string firstname, string lastname)
        {
            // Login service must always force a refresh here, since local/VM User server may have updated data on logout.
            UserProfileData userProfile = m_userManager.GetUserProfile(firstname, lastname, true);
            if (userProfile == null)
                return null;

            // Is this a deleted account?
            if (m_userManager.IsCustomTypeDeleted(userProfile.CustomType))
                return null;

            // Is it a profile that was remapped from a deleted account?
            if (m_userManager.IsDeletedUserAccount(userProfile))
                if ((userProfile.FirstName != firstname) || (userProfile.SurName != lastname))
                    return null;

            return userProfile;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public virtual string GetMessage()
        {
            return m_welcomeMessage;
        }

        private static LoginResponse.BuddyList ConvertFriendListItem(List<FriendListItem> LFL)
        {
            LoginResponse.BuddyList buddylistreturn = new LoginResponse.BuddyList();
            foreach (FriendListItem fl in LFL)
            {
                LoginResponse.BuddyList.BuddyInfo buddyitem = new LoginResponse.BuddyList.BuddyInfo(fl.Friend);
                buddyitem.BuddyID = fl.Friend;
                buddyitem.BuddyRightsHave = (int)fl.FriendListOwnerPerms;
                buddyitem.BuddyRightsGiven = (int)fl.FriendPerms;
                buddylistreturn.AddNewBuddy(buddyitem);
            }
            return buddylistreturn;
        }

        /// <summary>
        /// Converts the inventory library skeleton into the form required by the rpc request.
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetInventoryLibrary()
        {
            Dictionary<UUID, InventoryFolderImpl> rootFolders
                = m_libraryRootFolder.RequestSelfAndDescendentFolders();
            ArrayList folderHashes = new ArrayList();

            foreach (InventoryFolderBase folder in rootFolders.Values)
            {
                Hashtable TempHash = new Hashtable();
                TempHash["name"] = folder.Name;
                TempHash["parent_id"] = folder.ParentID.ToString();
                TempHash["version"] = (Int32)folder.Version;
                TempHash["type_default"] = (Int32)folder.Type;
                TempHash["folder_id"] = folder.ID.ToString();
                folderHashes.Add(TempHash);
            }

            return folderHashes;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetLibraryOwner()
        {
            //for now create random inventory library owner
            Hashtable TempHash = new Hashtable();
            TempHash["agent_id"] = "11111111-1111-0000-0000-000100bba000";
            ArrayList inventoryLibOwner = new ArrayList();
            inventoryLibOwner.Add(TempHash);
            return inventoryLibOwner;
        }

        public class InventoryData
        {
            public ArrayList InventoryArray = null;
            public UUID RootFolderID = UUID.Zero;

            public InventoryData(ArrayList invList, UUID rootID)
            {
                InventoryArray = invList;
                RootFolderID = rootID;
            }
        }

        protected void SniffLoginKey(Uri uri, Hashtable requestData)
        {
            string uri_str = uri.ToString();
            string[] parts = uri_str.Split(new char[] { '=' });
            if (parts.Length > 1)
            {
                string web_login_key = parts[1];
                requestData.Add("web_login_key", web_login_key);
                m_log.InfoFormat("[LOGIN]: Login with web_login_key {0}", web_login_key);
            }
        }

        protected bool PrepareLoginToREURI(Regex reURI, LoginResponse response, UserProfileData theUser, string startLocationRequest, string StartLocationType, string desc, string clientVersion)
        {
            string region;
            RegionInfo regionInfo = null;
            Match uriMatch = reURI.Match(startLocationRequest);
            if (uriMatch == null)
            {
                m_log.InfoFormat("[LOGIN]: Got {0} {1}, but can't process it", desc, startLocationRequest);
                return false;
            }

            region = uriMatch.Groups["region"].ToString();
            regionInfo = RequestClosestRegion(region);
            if (regionInfo == null)
            {
                m_log.InfoFormat("[LOGIN]: Got {0} {1}, can't locate region {2}", desc, startLocationRequest, region);
                return false;
            }

            Vector3 newPos = new Vector3(float.Parse(uriMatch.Groups["x"].Value),
                                                        float.Parse(uriMatch.Groups["y"].Value), float.Parse(uriMatch.Groups["z"].Value));
            // m_log.WarnFormat("[LOGIN]: PrepareLoginToREURI for user {0} at {1} was {2}", theUser.ID, newPos, theUser.CurrentAgent.Position);

            theUser.CurrentAgent.Position = newPos;
            response.LookAt = "[r0,r1,r0]";
            // can be: last, home, safe, url
            response.StartLocation = StartLocationType;
            return PrepareLoginToRegion(regionInfo, theUser, response, clientVersion);
        }

        protected bool PrepareNextRegion(LoginResponse response, UserProfileData theUser, List<string> theList, string startLocationRequest, string clientVersion)
        {
            Regex reURI = new Regex(@"^(?<region>[^&]+)/(?<x>\d+)/(?<y>\d+)/(?<z>\d+)$");
            if ((startLocationRequest != "home") && (startLocationRequest != "last"))
                startLocationRequest = "safe";

            foreach (string location in theList)
            {
                if (PrepareLoginToREURI(reURI, response, theUser, location, "safe", "default region", clientVersion))
                    return true;
            }
            return false;
        }

        // For new users' first-time logins
        protected bool PrepareNextDefaultLogin(LoginResponse response, UserProfileData theUser, string startLocationRequest, string clientVersion)
        {
            return PrepareNextRegion(response, theUser, _DefaultLoginsList, startLocationRequest, clientVersion);
        }

        // For returning users' where the preferred region is down
        protected bool PrepareNextDefaultRegion(LoginResponse response, UserProfileData theUser, string clientVersion)
        {
            return PrepareNextRegion(response, theUser, _DefaultRegionsList, "safe", clientVersion);
        }

        /// <summary>
        /// Customises the login response and fills in missing values.  This method also tells the login region to
        /// expect a client connection.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        /// <param name="startLocationRequest">The requested start location</param>
        /// <returns>true on success, false if the region was not successfully told to expect a user connection</returns>
        public bool CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest, string clientVersion)
        {
            // add active gestures to login-response
            AddActiveGestures(response, theUser);

            // HomeLocation
            RegionInfo homeInfo = null;

            // use the homeRegionID if it is stored already. If not, use the regionHandle as before
            UUID homeRegionId = theUser.HomeRegionID;
            ulong homeRegionHandle = theUser.HomeRegion;
            if (homeRegionId != UUID.Zero)
            {
                homeInfo = GetRegionInfo(homeRegionId);
            }
            else
            {
                homeInfo = GetRegionInfo(homeRegionHandle);
            }

            if (homeInfo != null)
            {
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        (homeInfo.RegionLocX * Constants.RegionSize),
                        (homeInfo.RegionLocY * Constants.RegionSize),
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
            }
            else
            {
                m_log.InfoFormat("not found the region at {0} {1}", theUser.HomeRegionX, theUser.HomeRegionY);
                // Emergency mode: Home-region isn't available, so we can't request the region info.
                // Use the stored home regionHandle instead.
                // NOTE: If the home-region moves, this will be wrong until the users update their user-profile again
                ulong regionX = homeRegionHandle >> 32;
                ulong regionY = homeRegionHandle & 0xffffffff;
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        regionX, regionY,
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);

                m_log.InfoFormat("[LOGIN] Home region of user {0} {1} is not available; using computed region position {2} {3}",
                                 theUser.FirstName, theUser.SurName,
                                 regionX, regionY);
            }

            // StartLocation
            RegionInfo regionInfo = null;
            if (theUser.LastLogin == 0)
            {
                // New user first login
                if (PrepareNextDefaultLogin(response, theUser, startLocationRequest, clientVersion))
                    return true;
            }
            else
            {
                // Returning user login
                if (startLocationRequest == "home")
                {
                    regionInfo = homeInfo;
                    theUser.CurrentAgent.Position = theUser.HomeLocation;
                    response.LookAt = String.Format("[r{0},r{1},r{2}]", theUser.HomeLookAt.X.ToString(),
                                                    theUser.HomeLookAt.Y.ToString(), theUser.HomeLookAt.Z.ToString());
                }
                else if (startLocationRequest == "last")
                {
                    UUID lastRegion = theUser.CurrentAgent.Region;
                    regionInfo = GetRegionInfo(lastRegion);
                    response.LookAt = String.Format("[r{0},r{1},r{2}]", theUser.CurrentAgent.LookAt.X.ToString(),
                                                    theUser.CurrentAgent.LookAt.Y.ToString(), theUser.CurrentAgent.LookAt.Z.ToString());
                }
                else
                {
                    // Logging in to a specific URL... try to find the region
                    Regex reURI = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                    if (PrepareLoginToREURI(reURI, response, theUser, startLocationRequest, "url", "Custom Login URL", clientVersion))
                        return true;
                }

                // Logging in to home or last... try to find the region
                if ((regionInfo != null) && (PrepareLoginToRegion(regionInfo, theUser, response, clientVersion)))
                {
                    return true;
                }

                // StartLocation not available, send him to a nearby region instead
                // regionInfo = m_gridService.RequestClosestRegion(String.Empty);
                //m_log.InfoFormat("[LOGIN]: StartLocation not available sending to region {0}", regionInfo.regionName);

                // Normal login failed, try to find an alternative region, starting with Home.
                if (homeInfo != null)
                {
                    if ((regionInfo == null) || (regionInfo.RegionID != homeInfo.RegionID))
                    {
                        regionInfo = homeInfo;
                        theUser.CurrentAgent.Position = theUser.HomeLocation;
                        response.LookAt = String.Format("[r{0},r{1},r{2}]", theUser.HomeLookAt.X.ToString(),
                                                        theUser.HomeLookAt.Y.ToString(), theUser.HomeLookAt.Z.ToString());
                        m_log.InfoFormat("[LOGIN]: StartLocation not available, trying user's Home region {0}", regionInfo.RegionName);
                        if (PrepareLoginToRegion(regionInfo, theUser, response, clientVersion))
                            return true;
                    }
                }

                // No Home location available either, try to find a default region from the list
                if (PrepareNextDefaultRegion(response, theUser, clientVersion))
                    return true;
            }

            // No default regions available either.
            // Send him to global default region home location instead (e.g. 1000,1000)
            ulong defaultHandle = (((ulong)m_defaultHomeX * Constants.RegionSize) << 32) |
                                  ((ulong)m_defaultHomeY * Constants.RegionSize);

            if ((regionInfo != null) && (defaultHandle == regionInfo.RegionHandle))
            {
                m_log.ErrorFormat("[LOGIN]: Not trying the default region since this is the same as the selected region");
                return false;
            }

            m_log.Error("[LOGIN]: Sending user to default region " + defaultHandle + " instead");
            regionInfo = GetRegionInfo(defaultHandle);

            if (regionInfo == null)
            {
                m_log.ErrorFormat("[LOGIN]: No default region available. Aborting.");
                return false;
            }

            theUser.CurrentAgent.Position = new Vector3(128, 128, 0);
            response.StartLocation = "safe";

            return PrepareLoginToRegion(regionInfo, theUser, response, clientVersion);
        }

        protected abstract RegionInfo RequestClosestRegion(string region);
        protected abstract RegionInfo GetRegionInfo(ulong homeRegionHandle);
        protected abstract RegionInfo GetRegionInfo(UUID homeRegionId);
        protected abstract bool PrepareLoginToRegion(RegionInfo regionInfo, UserProfileData user, LoginResponse response, string clientVersion);

        /// <summary>
        /// Add active gestures of the user to the login response.
        /// </summary>
        /// <param name="response">
        /// A <see cref="LoginResponse"/>
        /// </param>
        /// <param name="theUser">
        /// A <see cref="UserProfileData"/>
        /// </param>
        protected void AddActiveGestures(LoginResponse response, UserProfileData theUser)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            IInventoryStorage inventory = inventorySelect.GetProvider(theUser.ID);

            List<InventoryItemBase> gestures = null;
            try
            {
                gestures = inventory.GetActiveGestureItems(theUser.ID);
            }
            catch (Exception e)
            {
                m_log.Debug("[LOGIN]: Unable to retrieve active gestures from inventory server. Reason: " + e.Message);
            }
            //m_log.DebugFormat("[LOGIN]: AddActiveGestures, found {0}", gestures == null ? 0 : gestures.Count);
            ArrayList list = new ArrayList();
            if (gestures != null)
            {
                foreach (InventoryItemBase gesture in gestures)
                {
                    Hashtable item = new Hashtable();
                    item["item_id"] = gesture.ID.ToString();
                    item["asset_id"] = gesture.AssetID.ToString();
                    list.Add(item);
                }
            }
            response.ActiveGestures = list;
        }

        /// <summary>
        /// Get the initial login inventory skeleton (in other words, the folder structure) for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        /// <exception cref='System.Exception'>This will be thrown if there is a problem with the inventory service</exception>
        protected InventoryData GetInventorySkeleton(UUID userID)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            IInventoryStorage inventory = inventorySelect.GetProvider(userID);

            List<InventoryFolderBase> folders = inventory.GetInventorySkeleton(userID);

            if (folders == null || folders.Count == 0)
            {
                throw new Exception(
                    String.Format(
                        "A root inventory folder for user {0} could not be retrieved from the inventory service",
                        userID));
            }

            UUID rootID = UUID.Zero;
            ArrayList AgentInventoryArray = new ArrayList();
            Hashtable TempHash;
            foreach (InventoryFolderBase InvFolder in folders)
            {
                if (InvFolder.ParentID == UUID.Zero)
                {
                    rootID = InvFolder.ID;
                }
                TempHash = new Hashtable();
                TempHash["name"] = InvFolder.Name;
                TempHash["parent_id"] = InvFolder.ParentID.ToString();
                TempHash["version"] = (Int32)InvFolder.Version;
                TempHash["type_default"] = (Int32)InvFolder.Type;
                TempHash["folder_id"] = InvFolder.ID.ToString();
                AgentInventoryArray.Add(TempHash);
            }

            return new InventoryData(AgentInventoryArray, rootID);
        }

        protected virtual bool AllowLoginWithoutInventory()
        {
            return false;
        }

        public XmlRpcResponse XmlRPCCheckAuthSession(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            string authed = "FALSE";
            if (requestData.Contains("avatar_uuid") && requestData.Contains("session_id"))
            {
                UUID guess_aid; // avatar ID
                UUID guess_sid; // session ID

                try
                {
                    UUID.TryParse((string)requestData["avatar_uuid"], out guess_aid);
                    if (guess_aid == UUID.Zero)
                    {
                        return Util.CreateUnknownUserErrorResponse();
                    }
                    UUID.TryParse((string)requestData["session_id"], out guess_sid);
                    if (guess_sid == UUID.Zero)
                    {
                        return Util.CreateUnknownUserErrorResponse();
                    }
                    if (m_userManager.VerifySession(guess_aid, guess_sid))
                    {
                        authed = "TRUE";
                        m_log.InfoFormat("[UserManager]: CheckAuthSession TRUE for user {0}", guess_aid);
                    }
                    else
                    {
                        m_log.InfoFormat("[UserManager]: CheckAuthSession FALSE for user {0}, refreshing caches", guess_aid);
                        //purge the cache and retry
                        m_userManager.FlushCachedInfo(guess_aid);

                        if (m_userManager.VerifySession(guess_aid, guess_sid))
                        {
                            authed = "TRUE";
                            m_log.InfoFormat("[UserManager]: CheckAuthSession TRUE for user {0}", guess_aid);
                        }
                        else
                        {
                            m_log.InfoFormat("[UserManager]: CheckAuthSession FALSE");
                            return Util.CreateUnknownUserErrorResponse();
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[UserManager]: Unable to check auth session: {0}", e);
                    return Util.CreateUnknownUserErrorResponse();
                }
            }
            
            Hashtable responseData = new Hashtable();
            responseData["auth_session"] = authed;
            response.Value = responseData;
            return response;
        }

    }
}
