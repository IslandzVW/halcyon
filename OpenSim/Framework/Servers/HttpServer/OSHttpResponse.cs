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

using System.IO;
using System.Net;
using System.Text;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// OSHttpResponse is the OpenSim representation of an HTTP
    /// response.
    /// </summary>
    public class OSHttpResponse
    {
        public HttpListenerResponse Response { get; set; }

        /// <summary>
        /// Content type property.
        /// </summary>
        /// <remarks>
        /// Setting this property will also set IsContentTypeSet to
        /// true.
        /// </remarks>
        public virtual string ContentType
        {
            get { return Response.ContentType; }
            set { Response.ContentType = value; }
        }

        /// <summary>
        /// Length of the body content; 0 if there is no body.
        /// </summary>
        public long ContentLength
        {
            get { return Response.ContentLength64; }
            set { Response.ContentLength64 = value; }
        }

        /// <summary>
        /// Alias for ContentLength.
        /// </summary>
        public long ContentLength64
        {
            get { return ContentLength; }
            set { ContentLength = value; }
        }

        /// <summary>
        /// Encoding of the body content.
        /// </summary>
        public Encoding ContentEncoding
        {
            get { return Response.ContentEncoding; }
            set { Response.ContentEncoding = value; }
        }

        public bool KeepAlive
        {
            get { return (Response.KeepAlive); }
            set { Response.KeepAlive = value; }
        }

        /// <summary>
        /// Return the output stream feeding the body.
        /// </summary>
        /// <remarks>
        /// On its way out...
        /// </remarks>
        public Stream OutputStream
        {
            get { return (Response.OutputStream); }
        }

        public System.Version ProtocolVersion
        {
            get { return (Response.ProtocolVersion); }
            set { Response.ProtocolVersion = value; }
        }

        /// <summary>
        /// Set a redirct location.
        /// </summary>
        public string RedirectLocation
        {
            // get { return _redirectLocation; }
            set
            {
                Response.Redirect(value);
            }
        }


        /// <summary>
        /// Chunk transfers.
        /// </summary>
        public bool SendChunked
        {
            get { return (Response.SendChunked); }
            set { Response.SendChunked = value; }
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public virtual int StatusCode
        {
            get { return (Response.StatusCode); }
            set { Response.StatusCode = value; }
        }

        /// <summary>
        /// HTTP status description.
        /// </summary>
        public string StatusDescription
        {
            get { return (Response.StatusDescription); }
            set { Response.StatusDescription = value; }
        }

        public OSHttpResponse() { }

        public OSHttpResponse(HttpListenerResponse resp)
        {
            Response = resp;
        }

        /// <summary>
        /// Add a header field and content to the response.
        /// </summary>
        /// <param name="key">string containing the header field
        /// name</param>
        /// <param name="value">string containing the header field
        /// value</param>
        public void AddHeader(string key, string value)
        {
            Response.AddHeader(key, value);
        }
    }
}
