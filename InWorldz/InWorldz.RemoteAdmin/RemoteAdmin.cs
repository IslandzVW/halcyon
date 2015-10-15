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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Net;
using System.Reflection;
using System.Timers;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;

using OpenMetaverse;

using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace InWorldz.RemoteAdmin
{
    public class RemoteAdmin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Timer sessionTimer;
        private Dictionary<UUID, DateTime> m_activeSessions = 
            new Dictionary<UUID, DateTime>();
        private Dictionary<string, Dictionary<string, XmlMethodHandler>> m_commands = 
            new Dictionary<string, Dictionary<string, XmlMethodHandler>>();

        public delegate object XmlMethodHandler(IList args, IPEndPoint client);

        public RemoteAdmin()
        {
            AddCommand("session", "login_with_password", SessionLoginWithPassword);
            AddCommand("session", "logout", SessionLogout);

            AddCommand("Console", "Command", ConsoleCommandHandler);

            // AddCommand("GridService", "Shutdown", RegionShutdownHandler);

            sessionTimer = new Timer(60000); // 60 seconds
            sessionTimer.Elapsed += sessionTimer_Elapsed;
            sessionTimer.Enabled = true;
        }

        /// <summary>
        /// Called publicly by server code that is not hosting a scene, but wants remote admin support
        /// </summary>
        /// <param name="server"></param>
        public void AddHandler(BaseHttpServer server)
        {
            m_log.Info("[RADMIN]: Remote Admin CoreInit");

            server.AddStreamHandler(new XmlRpcStreamHandler("POST", Util.XmlRpcRequestPrefix("RemoteAdmin"), XmlRpcCommand));
        }

        public void AddCommand(string classname, string command, XmlMethodHandler method)
        {
            lock (m_commands)
            {
                if (!m_commands.ContainsKey(classname))
                    m_commands.Add(classname, new Dictionary<string, XmlMethodHandler>());
                m_commands[classname].Add(command, method);
            }
        }

        public void RemoveCommand(string classname, string command, XmlMethodHandler method)
        {
            lock (m_commands)
            {
                if (!m_commands.ContainsKey(classname))
                    return;
                m_commands[classname].Remove(command);
            }
        }

        private XmlMethodHandler LookupCommand(string classname, string command)
        {
            lock (m_commands)
            {
                if (!m_commands.ContainsKey(classname))
                    return (null);
                if (m_commands[classname].ContainsKey(command))
                    return m_commands[classname][command];
                else
                    return (null);
            }
        }

        public XmlRpcResponse XmlRpcCommand(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            try
            {
                responseData["Status"] = "Success";
                responseData["Value"] = "";

                XmlMethodHandler handler = LookupCommand(request.MethodNameObject, request.MethodNameMethod);

                if (handler != null)
                {
                    responseData["Value"] = handler(request.Params, remoteClient);
                    response.Value = responseData;
                }
                else
                {
                    // Code set in accordance with http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
                    response.SetFault(
                        XmlRpcErrorCodes.SERVER_ERROR_METHOD,
                        String.Format("Requested method [{0}] not found", request.MethodNameObject + "." + request.MethodNameMethod));
                }
            }
            catch (Exception e)
            {
                responseData["Status"] = "Failure";
                responseData["ErrorDescription"] = e.Message;
                response.Value = responseData;
            }

            return response;
        }

        public void CheckSessionValid(UUID sessionid)
        {
            lock (m_activeSessions)
            {
                if (!m_activeSessions.ContainsKey(sessionid))
                    throw new Exception("SESSION_INVALID");
                m_activeSessions[sessionid] = DateTime.Now;
            }
        }

        // If a session has been inactive for 10 minutes, time it out.
        private void sessionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            List<UUID> expiredSessions = new List<UUID>();

            lock (m_activeSessions)
            {
                foreach (UUID key in m_activeSessions.Keys)
                {
                    if ((DateTime.Now - m_activeSessions[key]) > TimeSpan.FromMinutes(10))
                        expiredSessions.Add(key);
                }

                foreach (UUID key in expiredSessions)
                {
                    m_activeSessions.Remove(key);
                }
            }
        }

        private object SessionLoginWithPassword(IList args, IPEndPoint remoteClient)
        {
            UUID sessionId;
            string username = (string)args[0];
            string password = (string)args[1];

            // Is the username the same as the logged in user and do they have the password correct?
            if ( Util.AuthenticateAsSystemUser(username, password))
            {
                lock (m_activeSessions)
                {
                    sessionId = UUID.Random();
                    m_activeSessions.Add(sessionId, DateTime.Now);
                }
            }
            else
            {
                throw new Exception("Invalid Username or Password");
            }

            return (sessionId.ToString());
        }

        private object SessionLogout(IList args, IPEndPoint remoteClient)
        {
            UUID sessionId = new UUID((string)args[0]);

            lock (m_activeSessions)
            {
                if (m_activeSessions.ContainsKey(sessionId))
                {
                    m_activeSessions.Remove(sessionId);
                    return (true);
                }
                else
                {
                    return (false);
                }
            }
        }

        private object ConsoleCommandHandler(IList args, IPEndPoint client)
        {
            CheckSessionValid(new UUID((string)args[0]));

            string command = (string)args[1];
            MainConsole.Instance.RunCommand(command);
            return "";
        }

#if false
        /// <summary>
        /// Create a new user account.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcCreateUserMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// <item><term>user_password</term>
        ///       <description>avatar's password</description></item>
        /// <item><term>user_email</term>
        ///       <description>email of the avatar's owner (optional)</description></item>
        /// <item><term>start_region_x</term>
        ///       <description>avatar's start region coordinates, X value</description></item>
        /// <item><term>start_region_y</term>
        ///       <description>avatar's start region coordinates, Y value</description></item>
        /// </list>
        ///
        /// XmlRpcCreateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// <item><term>avatar_uuid</term>
        ///       <description>UUID of the newly created avatar
        ///                    account; UUID.Zero if failed.
        ///       </description></item>
        /// </list>
        /// </remarks>
        public XmlRpcResponse XmlRpcCreateUserMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: CreateUser: new request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            lock (rslock)
            {
                try
                {
                    Hashtable requestData = (Hashtable)request.Params[0];

                    // check completeness
                    checkStringParameters(request, new string[]
                                                       {
                                                           "password", "user_firstname",
                                                           "user_lastname", "user_password",
                                                       });
                    checkIntegerParams(request, new string[] { "start_region_x", "start_region_y" });

                    // check password
                    if (!String.IsNullOrEmpty(m_requiredPassword) &&
                        (string)requestData["password"] != m_requiredPassword) throw new Exception("wrong password");

                    // do the job
                    string firstname = (string)requestData["user_firstname"];
                    string lastname = (string)requestData["user_lastname"];
                    string passwd = (string)requestData["user_password"];
                    uint regX = Convert.ToUInt32((Int32)requestData["start_region_x"]);
                    uint regY = Convert.ToUInt32((Int32)requestData["start_region_y"]);

                    string email = ""; // empty string for email
                    if (requestData.Contains("user_email"))
                        email = (string)requestData["user_email"];

                    CachedUserInfo userInfo =
                        m_app.CommunicationsManager.UserProfileCacheService.GetUserDetails(firstname, lastname);

                    if (null != userInfo)
                        throw new Exception(String.Format("Avatar {0} {1} already exists", firstname, lastname));

                    UUID userID =
                        m_app.CommunicationsManager.UserAdminService.AddUser(firstname, lastname,
                                                                             passwd, email, regX, regY);

                    if (userID == UUID.Zero)
                        throw new Exception(String.Format("failed to create new user {0} {1}",
                                                          firstname, lastname));

                    // Establish the avatar's initial appearance

                    updateUserAppearance(responseData, requestData, userID);

                    responseData["success"] = true;
                    responseData["avatar_uuid"] = userID.ToString();

                    response.Value = responseData;

                    m_log.InfoFormat("[RADMIN]: CreateUser: User {0} {1} created, UUID {2}", firstname, lastname, userID);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[RADMIN] CreateUser: failed: {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] CreateUser: failed: {0}", e.ToString());

                    responseData["success"] = false;
                    responseData["avatar_uuid"] = UUID.Zero.ToString();
                    responseData["error"] = e.Message;

                    response.Value = responseData;
                }
                m_log.Info("[RADMIN]: CreateUser: request complete");
                return response;
            }
        }

        /// <summary>
        /// Check whether a certain user account exists.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcUserExistsMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// </list>
        ///
        /// XmlRpcCreateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        public XmlRpcResponse XmlRpcUserExistsMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: UserExists: new request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            try
            {
                Hashtable requestData = (Hashtable)request.Params[0];

                // check completeness
                checkStringParameters(request, new string[] { "password", "user_firstname", "user_lastname" });

                string firstname = (string)requestData["user_firstname"];
                string lastname = (string)requestData["user_lastname"];

                CachedUserInfo userInfo
                    = m_app.CommunicationsManager.UserProfileCacheService.GetUserDetails(firstname, lastname);

                responseData["user_firstname"] = firstname;
                responseData["user_lastname"] = lastname;

                if (null == userInfo)
                    responseData["success"] = false;
                else
                    responseData["success"] = true;

                response.Value = responseData;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[RADMIN] UserExists: failed: {0}", e.Message);
                m_log.DebugFormat("[RADMIN] UserExists: failed: {0}", e.ToString());

                responseData["success"] = false;
                responseData["error"] = e.Message;

                response.Value = responseData;
            }

            m_log.Info("[RADMIN]: UserExists: request complete");
            return response;
        }

        /// <summary>
        /// Update a user account.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcUpdateUserAccountMethod takes the following XMLRPC
        /// parameters (changeable ones are optional)
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name (cannot be changed)</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name (cannot be changed)</description></item>
        /// <item><term>user_password</term>
        ///       <description>avatar's password (changeable)</description></item>
        /// <item><term>start_region_x</term>
        ///       <description>avatar's start region coordinates, X
        ///                    value (changeable)</description></item>
        /// <item><term>start_region_y</term>
        ///       <description>avatar's start region coordinates, Y
        ///                    value (changeable)</description></item>
        /// <item><term>about_real_world</term>
        ///       <description>"about" text of avatar owner (changeable)</description></item>
        /// <item><term>about_virtual_world</term>
        ///       <description>"about" text of avatar (changeable)</description></item>
        /// </list>
        ///
        /// XmlRpcCreateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>

        public XmlRpcResponse XmlRpcUpdateUserAccountMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: UpdateUserAccount: new request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            lock (rslock)
            {
                try
                {
                    Hashtable requestData = (Hashtable)request.Params[0];

                    // check completeness
                    checkStringParameters(request, new string[] {
                            "password", "user_firstname",
                            "user_lastname"});

                    // check password
                    if (!String.IsNullOrEmpty(m_requiredPassword) &&
                        (string)requestData["password"] != m_requiredPassword) throw new Exception("wrong password");

                    // do the job
                    string firstname = (string)requestData["user_firstname"];
                    string lastname = (string)requestData["user_lastname"];

                    string passwd = String.Empty;
                    uint? regX = null;
                    uint? regY = null;
                    uint? ulaX = null;
                    uint? ulaY = null;
                    uint? ulaZ = null;
                    uint? usaX = null;
                    uint? usaY = null;
                    uint? usaZ = null;
                    string aboutFirstLive = String.Empty;
                    string aboutAvatar = String.Empty;

                    if (requestData.ContainsKey("user_password")) passwd = (string)requestData["user_password"];
                    if (requestData.ContainsKey("start_region_x"))
                        regX = Convert.ToUInt32((Int32)requestData["start_region_x"]);
                    if (requestData.ContainsKey("start_region_y"))
                        regY = Convert.ToUInt32((Int32)requestData["start_region_y"]);

                    if (requestData.ContainsKey("start_lookat_x"))
                        ulaX = Convert.ToUInt32((Int32)requestData["start_lookat_x"]);
                    if (requestData.ContainsKey("start_lookat_y"))
                        ulaY = Convert.ToUInt32((Int32)requestData["start_lookat_y"]);
                    if (requestData.ContainsKey("start_lookat_z"))
                        ulaZ = Convert.ToUInt32((Int32)requestData["start_lookat_z"]);

                    if (requestData.ContainsKey("start_standat_x"))
                        usaX = Convert.ToUInt32((Int32)requestData["start_standat_x"]);
                    if (requestData.ContainsKey("start_standat_y"))
                        usaY = Convert.ToUInt32((Int32)requestData["start_standat_y"]);
                    if (requestData.ContainsKey("start_standat_z"))
                        usaZ = Convert.ToUInt32((Int32)requestData["start_standat_z"]);
                    if (requestData.ContainsKey("about_real_world"))
                        aboutFirstLive = (string)requestData["about_real_world"];
                    if (requestData.ContainsKey("about_virtual_world"))
                        aboutAvatar = (string)requestData["about_virtual_world"];

                    UserProfileData userProfile
                        = m_app.CommunicationsManager.UserService.GetUserProfile(firstname, lastname);

                    if (null == userProfile)
                        throw new Exception(String.Format("avatar {0} {1} does not exist", firstname, lastname));

                    if (null != passwd)
                    {
                        string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(passwd) + ":" + String.Empty);
                        userProfile.PasswordHash = md5PasswdHash;
                    }

                    if (null != regX) userProfile.HomeRegionX = (uint)regX;
                    if (null != regY) userProfile.HomeRegionY = (uint)regY;

                    if (null != usaX) userProfile.HomeLocationX = (uint)usaX;
                    if (null != usaY) userProfile.HomeLocationY = (uint)usaY;
                    if (null != usaZ) userProfile.HomeLocationZ = (uint)usaZ;

                    if (null != ulaX) userProfile.HomeLookAtX = (uint)ulaX;
                    if (null != ulaY) userProfile.HomeLookAtY = (uint)ulaY;
                    if (null != ulaZ) userProfile.HomeLookAtZ = (uint)ulaZ;

                    if (String.Empty != aboutFirstLive) userProfile.FirstLifeAboutText = aboutFirstLive;
                    if (String.Empty != aboutAvatar) userProfile.AboutText = aboutAvatar;

                    // User has been created. Now establish gender and appearance.

                    updateUserAppearance(responseData, requestData, userProfile.ID);

                    if (!m_app.CommunicationsManager.UserService.UpdateUserProfile(userProfile))
                        throw new Exception("did not manage to update user profile");

                    responseData["success"] = true;

                    response.Value = responseData;

                    m_log.InfoFormat("[RADMIN]: UpdateUserAccount: account for user {0} {1} updated, UUID {2}",
                                     firstname, lastname,
                                     userProfile.ID);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[RADMIN] UpdateUserAccount: failed: {0}", e.Message);
                    m_log.DebugFormat("[RADMIN] UpdateUserAccount: failed: {0}", e.ToString());

                    responseData["success"] = false;
                    responseData["error"] = e.Message;

                    response.Value = responseData;
                }
            }

            m_log.Info("[RADMIN]: UpdateUserAccount: request complete");
            return response;

        } 
#endif
        public void Dispose()
        {
        }

    }

}
