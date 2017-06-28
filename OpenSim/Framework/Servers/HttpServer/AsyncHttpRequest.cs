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
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class AsyncHttpRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public delegate void TimeoutHandler(AsyncHttpRequest pRequest);
        public TimeoutHandler m_Timeout;

        private Timer m_requestTimer;
        private ulong m_requestTime;
        private int m_timeout; 
        
        private readonly Hashtable m_requestData;
        private readonly UUID m_requestID;

        private UUID m_agentID;
        private IHttpServer m_httpServer;
        private OSHttpRequest m_httpRequest;
        private OSHttpResponse m_httpResponse;

        private object m_lock;
        private bool m_requestCompleted;

        public AsyncHttpRequest(
            IHttpServer pServer, OSHttpRequest pRequest, OSHttpResponse pResponse, 
            UUID pAgentID,  TimeoutHandler pTimeoutHandler, int pTimeout)
        {
            m_lock = new Object();
            m_requestCompleted = false;

            m_httpServer = pServer;
            m_requestID = UUID.Random();
            m_agentID = pAgentID;
            m_httpRequest = pRequest;
            m_httpResponse = pResponse;
            m_requestData = new Hashtable();

            Hashtable headervals = new Hashtable();
            string[] querystringkeys = HttpRequest.QueryString.AllKeys;
            string[] rHeaders = HttpRequest.Headers.AllKeys;

            string requestBody;
            using (StreamReader reader = new StreamReader(pRequest.InputStream, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            RequestData.Add("body", requestBody);
            RequestData.Add("uri", HttpRequest.RawUrl);
            RequestData.Add("content-type", HttpRequest.ContentType);
            RequestData.Add("http-method", HttpRequest.HttpMethod);

            foreach (string queryname in querystringkeys)
            {
                // HttpRequest.QueryString.AllKeys returns a one-item array, with a null only,
                // if passed something without an '=' in the query, such as URL/?abc or URL/?abc+def
                if (queryname != null)
                    RequestData.Add(queryname, HttpRequest.QueryString[queryname]);
            }

            foreach (string headername in rHeaders)
                headervals[headername] = HttpRequest.Headers[headername];

            RequestData.Add("headers", headervals);
            RequestData.Add("querystringkeys", querystringkeys);

            // Timeout
            m_Timeout = pTimeoutHandler;
            m_timeout = pTimeout;
            m_requestTime = Util.GetLongTickCount();
            TimedOut = false;

            if (m_timeout != 0)
            {
                m_requestTimer = new Timer(TimerHandler, null, m_timeout, -1);
            }
        }

        public bool ResponseSent { get; set; }

        public UUID RequestID
        {
            get { return m_requestID; }
        }

        public UUID AgentID
        {
            get { return m_agentID; }
        }

        public ulong RequestTime
        {
            get { return m_requestTime; }
        } 

        public bool TimedOut { get; set; }

        public OSHttpRequest HttpRequest 
        { 
            get { return m_httpRequest; } 
        }

        public OSHttpResponse HttpResponse
        {
            get { return m_httpResponse; }
        }

        public Hashtable RequestData
        {
            get { return m_requestData; }
        }

        public void SendResponse(byte[] buffer)
        {
            lock (m_lock)
            {
                if (m_requestCompleted)
                {
                    return;
                }
                else
                {
                    if (m_requestTimer != null)
                    {
                        m_requestTimer.Dispose();
                        m_requestTimer = null;
                    }

                    m_requestCompleted = true;
                }
            }

            try
            {
                m_httpServer.SendResponse(this.HttpRequest, this.HttpResponse, null, buffer);
            }
            catch (Exception e)
            {
                m_log.Error("[PollServiceHttpRequest] SendResponse failed. : " + e.ToString());
            }
        }

        public void SendResponse(Hashtable respData)
        {
            byte[] buffer = m_httpServer.DoHTTPGruntWork(respData, this.HttpRequest, this.HttpResponse);
            SendResponse(buffer);
        }

        internal void TimerHandler(object state)
        {
            TimedOut = true;

            try
            {
                if (m_Timeout == null)
                {
                    byte[] buffer = m_httpServer.SendHTML404(this.HttpResponse);
                    SendResponse(buffer);
                }
                else
                {
                    m_Timeout(this);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PollServiceHttpRequest] Exception in TimeoutHandler : " + e.ToString());
            }
        }
    }
}
