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
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Scripting.LSLHttp
{
    public class UrlData
    {
        public UUID hostID;
        public UUID itemID;
        public IScriptModule engine;
        public string url;
        public UUID urlcode;
        public List<UUID> requests;
    }

    public class RequestData
    {
        public UUID requestId;
        public AsyncHttpRequest polledRequest;
        public Dictionary<string, string> headers;
        public string body;
        public int responseCode;
        public string responseBody;
        public string contentType = "text/plain";
        public bool requestDone;
        public int startTime;
        public string uri;
    }

    public class UrlModule : ISharedRegionModule, IUrlModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Dictionary<string, UrlData> m_UrlMap = new Dictionary<string, UrlData>();
        private readonly Dictionary<UUID, RequestData> m_RequestMap = new Dictionary<UUID, RequestData>();

        private const int m_TotalUrls = 15000;
        private const int m_DefaultTimeout = 25 * 1000;         // 25 sec timeout

        private uint https_port = 0;
        private IHttpServer m_HttpServer = null;
        private IHttpServer m_HttpsServer = null;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "UrlModule"; }
        }

        public void Initialize(IConfigSource config)
        {
            bool ssl_enabled = config.Configs["Network"].GetBoolean("http_listener_ssl", false);
            if (ssl_enabled)
                https_port = (uint)config.Configs["Network"].GetInt("http_listener_sslport", ((int)ConfigSettings.DefaultRegionHttpPort + 1));
        }

        public void PostInitialize()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_HttpServer == null)
            {
                // There can only be one
                m_HttpServer = MainServer.Instance;

                // We can use the https if it is enabled
                if (https_port > 0)
                {
                    m_HttpsServer = MainServer.GetHttpServer(https_port);
                }
            }

            scene.RegisterModuleInterface<IUrlModule>(this);
            scene.EventManager.OnScriptReset += OnScriptReset;
        }

        public void RegionLoaded(Scene scene)
        {
            /*
                        IScriptModule[] scriptModules = scene.RequestModuleInterfaces<IScriptModule>();
                        foreach (IScriptModule scriptModule in scriptModules)
                        {
                            scriptModule.OnScriptRemoved += ScriptRemoved;
                            scriptModule.OnObjectRemoved += ObjectRemoved;
                        }
             */
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public UUID RequestURL(IScriptModule engine, SceneObjectPart host, UUID itemID)
        {
            UUID urlcode = UUID.Random();
            string url = String.Empty;

            lock (m_UrlMap)
            {
                if (m_UrlMap.Count >= m_TotalUrls)
                {
                    engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_DENIED", String.Empty });
                    return urlcode;
                }

                url = m_HttpServer.ServerURI + "/lslhttp/" + urlcode.ToString() + "/";

                UrlData urlData = new UrlData
                {
                    hostID = host.UUID,
                    itemID = itemID,
                    engine = engine,
                    url = url,
                    urlcode = urlcode,
                    requests = new List<UUID>()
                };

                m_UrlMap[url] = urlData;
            }

            string uri = "/lslhttp/" + urlcode.ToString() + "/";
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("POST", uri, AsyncHttpRequest, "HTTP-IN-POST", "Http In Request Handler (Asynch)"));
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("GET", uri, AsyncHttpRequest,  "HTTP-IN-GET", "Http In Request Handler (Asynch)")); 
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("PUT", uri, AsyncHttpRequest,  "HTTP-IN-PUT", "Http In Request Handler (Asynch)")); 
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("DELETE", uri, AsyncHttpRequest,  "HTTP-IN-DELETE", "Http In Request Handler (Asynch)")); 

            engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_GRANTED", url });

            return urlcode;
        }

        public UUID RequestSecureURL(IScriptModule engine, SceneObjectPart host, UUID itemID)
        {
            UUID urlcode = UUID.Random();
            string url = String.Empty;

            if (m_HttpsServer == null)
            {
                engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_DENIED", String.Empty });
                return urlcode;
            }

            lock (m_UrlMap)
            {
                if (m_UrlMap.Count >= m_TotalUrls)
                {
                    engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_DENIED", String.Empty });
                    return urlcode;
                }

                url = m_HttpsServer.ServerURI + "/lslhttps/" + urlcode.ToString() + "/";

                UrlData urlData = new UrlData
                {
                    hostID = host.UUID,
                    itemID = itemID,
                    engine = engine,
                    url = url,
                    urlcode = urlcode,
                    requests = new List<UUID>()
                };

                m_UrlMap[url] = urlData;
            }

            string uri = "/lslhttps/" + urlcode.ToString() + "/";
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("POST", uri, AsyncHttpRequest, "HTTP-IN-POST", "Http In Request Handler (Asynch)"));
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("GET", uri, AsyncHttpRequest, "HTTP-IN-GET", "Http In Request Handler (Asynch)"));
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("PUT", uri, AsyncHttpRequest,  "HTTP-IN-PUT", "Http In Request Handler (Asynch)")); 
            m_HttpServer.AddStreamHandler(new AsyncRequestHandler("DELETE", uri, AsyncHttpRequest,  "HTTP-IN-DELETE", "Http In Request Handler (Asynch)")); 

            engine.PostScriptEvent(itemID, "http_request", new Object[] { urlcode.ToString(), "URL_REQUEST_GRANTED", url });

            return urlcode;
        }

        public void ReleaseURL(string url)
        {
            UrlData data;

            lock (m_UrlMap)
            {
                if (!m_UrlMap.TryGetValue(url, out data))
                    return;

                // Remove the URL so we dont accept any new requests
                RemoveUrl(data);
                m_UrlMap.Remove(url);
            }

            List<RequestData> requests = new List<RequestData>();

            // Clean up existing requests
            lock (m_RequestMap)
            {
                foreach (UUID requestId in data.requests)
                {
                    RequestData req;
                    if (m_RequestMap.TryGetValue(requestId, out req))
                        requests.Add(req);
                }

                foreach (RequestData request in requests)
                {
                    m_RequestMap.Remove(request.requestId);
                }
            }

            foreach (RequestData request in requests)
            {
                // Signal this request. A 404 will be returned
                request.polledRequest.SendResponse(ProcessEvents(request.polledRequest, false));
            }
        }

        public void HttpContentType(UUID requestId, string content_type)
        {
            RequestData data;

            lock (m_RequestMap)
            {
                if (!m_RequestMap.TryGetValue(requestId, out data))
                    return;
                data.contentType = content_type;
            }
        }

        public void HttpResponse(UUID requestId, int status, string body)
        {
            RequestData data = null;

            lock (m_RequestMap)
            {
                if (!m_RequestMap.TryGetValue(requestId, out data))
                    return;
            }

            if (data != null)
            {
                data.responseCode = status;
                data.responseBody = body;
                data.requestDone = true;

                data.polledRequest.SendResponse(ProcessEvents(data.polledRequest, false));
            }
        }

        public string GetHttpHeader(UUID requestId, string header)
        {
            RequestData data;

            lock (m_RequestMap)
            {
                if (!m_RequestMap.TryGetValue(requestId, out data))
                    return String.Empty;

                string value;
                if (!data.headers.TryGetValue(header, out value))
                    return (String.Empty);
                else
                    return value;
            }
        }

        public int GetFreeUrls()
        {
            return m_TotalUrls - m_UrlMap.Count;
        }

        private void OnScriptReset(uint localID, UUID itemID)
        {
            ScriptRemoved(itemID);
        }

        public void ScriptRemoved(UUID itemID)
        {
            List<UrlData> removeURLs = new List<UrlData>();

            lock (m_UrlMap)
            {
                foreach (UrlData url in m_UrlMap.Values)
                {
                    if (url.itemID == itemID)
                    {
                        RemoveUrl(url);
                        removeURLs.Add(url);
                    }
                }

                foreach (UrlData data in removeURLs)
                {
                    m_UrlMap.Remove(data.url);
                }
            }

            List<RequestData> requests = new List<RequestData>();

            lock (m_RequestMap)
            {
                foreach (UrlData url in removeURLs)
                {
                    foreach (UUID id in url.requests)
                    {
                        RequestData req;
                        if (m_RequestMap.TryGetValue(id, out req))
                        {
                            m_RequestMap.Remove(id);
                            requests.Add(req);
                        }
                    }
                }
            }

            foreach (RequestData request in requests)
            {
                // Pulse this request. A 404 will be returned
                request.polledRequest.SendResponse(ProcessEvents(request.polledRequest, false));
            }
        }

        public void ObjectRemoved(UUID objectID)
        {
            List<UrlData> removeURLs = new List<UrlData>();

            lock (m_UrlMap)
            {
                foreach (UrlData url in m_UrlMap.Values)
                {
                    if (url.hostID == objectID)
                    {
                        RemoveUrl(url);
                        removeURLs.Add(url);
                    }
                }

                foreach (UrlData data in removeURLs)
                {
                    m_UrlMap.Remove(data.url);
                }
            }

            List<RequestData> requests = new List<RequestData>();

            lock (m_RequestMap)
            {
                foreach (UrlData url in removeURLs)
                {
                    foreach (UUID id in url.requests)
                    {
                        RequestData req;
                        if (m_RequestMap.TryGetValue(id, out req))
                        {
                            requests.Add(req);
                            m_RequestMap.Remove(id);
                        }
                    }
                }
            }

            foreach (RequestData request in requests)
            {
                // Pulse this request. A 404 will be returned
                request.polledRequest.SendResponse(ProcessEvents(request.polledRequest, false));
            }
        }

        private void RemoveUrl(UrlData data)
        {
            string url = data.url;
            bool is_ssl = url.Contains("lslhttps");
            string protocol = (is_ssl ? "/lslhttps/" : "/lslhttp/");

            m_HttpServer.RemoveStreamHandler("POST", protocol + data.urlcode.ToString() + "/");
            m_HttpServer.RemoveStreamHandler("GET", protocol + data.urlcode.ToString() + "/");
        }

        private string URLFromURI(string uri)
        {
            bool is_ssl = uri.Contains("lslhttps");

            int pos1 = uri.IndexOf("/");// /lslhttp
            int pos2 = uri.IndexOf("/", pos1 + 1);// /lslhttp/
            int pos3 = uri.IndexOf("/", pos2 + 1);// /lslhttp/<UUID>/
            string uri_tmp = uri.Substring(0, pos3 + 1);

            if (!is_ssl)
                return (m_HttpServer.ServerURI + uri_tmp);
            else
                return (m_HttpsServer.ServerURI + uri_tmp);
        }

        #region PolledService Interface

        public void AsyncHttpRequest(IHttpServer server, string path, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            UUID urlcode;
            if (UUID.TryParse(path, out urlcode))
                return;

            AsyncHttpRequest asyncRequest = 
                new AsyncHttpRequest(server, httpRequest, httpResponse, urlcode, TimeoutHandler, m_DefaultTimeout);

            UUID requestID = asyncRequest.RequestID;
            Hashtable request = asyncRequest.RequestData;

            string uri = request["uri"].ToString();

            try
            {
                Hashtable headers = (Hashtable)request["headers"];
                //HTTP server code doesn't provide us with QueryStrings
                string queryString = String.Empty;

                int pos1 = uri.IndexOf("/");// /lslhttp
                int pos2 = uri.IndexOf("/", pos1 + 1);// /lslhttp/
                int pos3 = uri.IndexOf("/", pos2 + 1);// /lslhttp/<UUID>/
                string pathInfo = uri.Substring(pos3);

                string url = URLFromURI(uri);
                UrlData urlData = null;

                lock (m_UrlMap)
                {
                    m_UrlMap.TryGetValue(url, out urlData);

                    // Returning NULL sends a 404 from the base server
                    if (urlData == null)
                        asyncRequest.SendResponse(server.SendHTML404(httpResponse));
                        
                }

                //for llGetHttpHeader support we need to store original URI here
                //to make x-path-info / x-query-string / x-script-url / x-remote-ip headers 
                //as per http://wiki.secondlife.com/wiki/LlGetHTTPHeader

                RequestData requestData =
                    new RequestData
                    {
                        requestId = asyncRequest.RequestID,
                        polledRequest = asyncRequest,
                        requestDone = false,
                        startTime = Environment.TickCount,
                        uri = uri
                    };

                if (requestData.headers == null)
                    requestData.headers = new Dictionary<string, string>();

                // Copy in the headers, convert keys to lower case: See.
                // http://wiki.secondlife.com/wiki/LlGetHTTPHeader
                foreach (DictionaryEntry header in headers)
                {
                    string key = (string)header.Key;
                    string value = (string)header.Value;
                    requestData.headers.Add(key.ToLower(), value);
                }

                foreach (DictionaryEntry de in request)
                {
                    if (de.Key.ToString() == "querystringkeys")
                    {
                        String[] keys = (String[])de.Value;
                        foreach (String key in keys)
                        {
                            if ((key != null) && request.ContainsKey(key))
                            {
                                string val = (String)request[key];
                                queryString = queryString + key + "=" + val + "&";
                            }
                        }

                        if (queryString.Length > 1)
                            queryString = queryString.Substring(0, queryString.Length - 1);
                    }
                }

                // Grab the raw unprocessed original query string, if any.
                int rawQueryPos = httpRequest.Url.Query.IndexOf('?');
                string rawQueryStr = (rawQueryPos < 0) ? httpRequest.Url.Query : httpRequest.Url.Query.Substring(rawQueryPos + 1);

                //if this machine is behind DNAT/port forwarding, currently this is being
                //set to address of port forwarding router
                requestData.headers["x-remote-ip"] = httpRequest.RemoteIPEndPoint.ToString();
                requestData.headers["x-path-info"] = pathInfo;
                requestData.headers["x-query-string"] = rawQueryStr;        // raw original (SL-compatible)
                requestData.headers["x-query-string-compat"] = queryString; // processed (old Halcyon scripts)
                requestData.headers["x-script-url"] = urlData.url;

                lock (m_RequestMap)
                {
                    m_RequestMap.Add(requestID, requestData);
                }

                lock (m_UrlMap)
                {
                    urlData.requests.Add(requestID);
                }

                urlData.engine.PostScriptEvent(urlData.itemID, "http_request", new Object[] { requestID.ToString(), request["http-method"].ToString(), request["body"].ToString() }); 
            }
            catch (Exception we)
            {
                m_log.Warn("[HttpRequestHandler]: http-in request failed");
                m_log.Warn(we.Message);
                m_log.Warn(we.StackTrace);

                asyncRequest.SendResponse(server.SendHTML500(httpResponse));
            }
        }

        private void TimeoutHandler(AsyncHttpRequest pRequest)
        {
            pRequest.SendResponse(ProcessEvents(pRequest, true));
        }

        private Hashtable ProcessEvents(AsyncHttpRequest pRequest, bool timedOut)
        {
            UUID requestID = pRequest.RequestID;
            UrlData urlData = null;
            RequestData requestData = null;
            Hashtable response = new Hashtable();

            response["content_type"] = "text/plain";

            lock (m_RequestMap)
            {
                if (m_RequestMap.TryGetValue(requestID, out requestData))
                    m_RequestMap.Remove(requestID);
            }

            if (requestData != null)
            {
                string url = URLFromURI(requestData.uri);

                lock (m_UrlMap)
                {
                    if (m_UrlMap.TryGetValue(url, out urlData))
                        urlData.requests.Remove(requestID);
                }
            }

            if ((requestData == null) || (urlData == null))
            {
                response["int_response_code"] = 404;
                response["str_response_string"] = "Request not found";
                return response;
            }

            if ((timedOut == true) ||
                ((requestData != null) && (requestData.requestDone == false)))
            {
                response["int_response_code"] = 500;
                response["str_response_string"] = "Script timeout";
            }
            else
            {
                //put response
                response["int_response_code"] = requestData.responseCode;
                response["str_response_string"] = requestData.responseBody;
                response["content_type"] = requestData.contentType;
            }

            return response;
        }

        #endregion
    }
}
