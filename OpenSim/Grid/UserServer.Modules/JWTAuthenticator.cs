/*
 * Copyright (c) 2016, InWorldz Halcyon Developers
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
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using InWorldz.JWT;
using LitJson;
using log4net;
using OpenSim.Framework.Communications.JWT;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.UserServer.Modules
{
    public class JWTAuthenticator
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IGridServiceCore m_coreService;
        private UserDataBaseService m_userDataBaseService = null;
        private BaseHttpServer m_httpServer;
        private readonly Dictionary<string, int> m_levelsAllowedPerScope = new Dictionary<string, int>
        {
            {"remote-console", 250},
            {"remote-admin", 250},
        };
        private JWTUserAuthenticationGateway m_authGateway = null;

        public JWTAuthenticator()
        {
        }

        #region Setup
        public void Initialize(IGridServiceCore core)
        {
            m_coreService = core;
        }

        public void PostInitialize(string privateKeyPath, string publicKeyPath)
        {
            if (m_coreService.TryGet(out m_userDataBaseService))
            {
                m_authGateway = new JWTUserAuthenticationGateway(m_userDataBaseService, privateKeyPath, publicKeyPath);
            }
            else
            {
                m_userDataBaseService = null;
            }
        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;

            m_httpServer.AddStreamHandler(new RestStreamHandler("POST", "/auth/jwt/", RESTRequestToken));
        }
        #endregion

        #region Handlers
        public string RESTRequestToken(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            httpResponse.ContentType = "application/json";

            if (m_authGateway == null)
            {
                m_log.Error("[JWTAUTH] Hit a bug check: the JWT gatway is not initialized... Why?");
                return JWTAuthErrors.BadAuthGateway;
            }

            if (httpRequest.ContentType != "application/json")
            {
                return JWTAuthErrors.BadJsonRead;
            }

            if (httpRequest.ContentLength <= 1)
            {
                return JWTAuthErrors.BadJsonRead;
            }

            if (!m_levelsAllowedPerScope.ContainsKey(param))
            {
                return JWTAuthErrors.BadScope;
            }

            var username = string.Empty;
            var password = string.Empty;

            try
            {
                var data = JsonMapper.ToObject(request);

                username = data["username"].ToString().Trim();
                password = data["password"].ToString();
            }
            catch (Exception)
            {
                return JWTAuthErrors.BadJsonRead;
            }

            var payload = new PayloadOptions();
            payload.Exp = DateTime.UtcNow.AddDays(1);
            payload.Scope = param;
            payload.Username = username;

            var nameSplit = Regex.Replace(username.ToLower(), @"[\s]+", " ").Split(' ');
            var firstname = nameSplit[0];
            var lastname = nameSplit.Length > 1 ? nameSplit[1] : "resident";

            try
            {
                var response = new Dictionary<string, string>
                {
                    {"token", m_authGateway.Authenticate(firstname, lastname, password, m_levelsAllowedPerScope[param], payload).ToString()}
                };

                return JsonMapper.ToJson(response);
            }
            catch (AuthenticationException ae)
            {
                m_log.Warn($"[JWTAUTH] Failed attempt to get token from {httpRequest.RemoteIPEndPoint} for user '{username}'. Error: {ae.Cause}");
                return JWTAuthErrors.AuthFailed(ae.Cause.ToString());
            }
        }
        #endregion

        static class JWTAuthErrors
        {
            public static string BadAuthGateway = JsonMapper.ToJson(new Dictionary<string, string> {{"denied","Authentication gateway not set up correctly."},});
            public static string BadJsonRead = JsonMapper.ToJson(new Dictionary<string, string> {{"denied","Received bad JSON."},});
            public static string BadScope = JsonMapper.ToJson(new Dictionary<string, string> {{"denied","Unrecognized scope request."},});
            public static string AuthFailed(string reason) {return JsonMapper.ToJson(new Dictionary<string, string> { { "denied", reason }, });}
            public static string Unexpected = JsonMapper.ToJson(new Dictionary<string, string> {{"denied","Unexpected error."},});
        }
    }

}

