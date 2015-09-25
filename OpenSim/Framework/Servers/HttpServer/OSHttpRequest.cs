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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
//using HttpServer;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class OSHttpRequest
    {
        //private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public HttpListenerRequest Request { get; set; }

        /// <summary>
        /// Used when logging packet messages.  A sequence number to identify the request.
        /// </summary>
        public uint SeqNo { get; set; }

        /// <summary>
        /// Start Time for the request
        /// </summary>
        public int StartTime { get; set; }

        /// <summary>
        /// End Time for the request. When we finished processing. Doesnt include transmission time.
        /// </summary>
        public int EndTime { get; set; }

        public string[] AcceptTypes 
        {
            get { return (Request.AcceptTypes); }
        }

        public Encoding ContentEncoding 
        {
            get { return (Request.ContentEncoding); }
        }

        public long ContentLength
        {
            get { return (Request.ContentLength64); }
        }

        public string ContentType 
        { 
            get { return (Request.ContentType); }
        }

        public CookieCollection Cookies 
        {
            get { return (Request.Cookies); }
        }

        public bool HasEntityBody
        {
            get { return ContentLength != 0; }
        }

        public NameValueCollection Headers 
        {
            get { return (Request.Headers); }
        }

        public string HttpMethod 
        {
            get { return (Request.HttpMethod); }
        }

        public Stream InputStream
        {
            get { return (Request.InputStream); }
        }

        public bool KeepAlive 
        {
            get { return (Request.KeepAlive); }
        } 
        
        public NameValueCollection QueryString 
        {
            get { return (Request.QueryString); }
        }

        public Uri Url 
        {
            get { return (Request.Url); }
        }

        public string RawUrl
        {
            get { return Url.AbsolutePath; }
        }

        public IPEndPoint RemoteIPEndPoint 
        {
            get { return (Request.RemoteEndPoint); }
        }

        public string UserAgent 
        {
            get { return (Request.UserAgent); }
        }

        public OSHttpRequest() {}

        public OSHttpRequest(HttpListenerRequest req)
        {
            Request = req;
        }

        public override string ToString()
        {
            StringBuilder me = new StringBuilder();
            me.Append(String.Format("OSHttpRequest: {0} {1}\n", HttpMethod, RawUrl));
            foreach (string k in Headers.AllKeys)
            {
                me.Append(String.Format("    {0}: {1}\n", k, Headers[k]));
            }
            if (null != RemoteIPEndPoint)
            {
                me.Append(String.Format("    IP: {0}\n", RemoteIPEndPoint));
            }

            return me.ToString();
        }
    }
}
