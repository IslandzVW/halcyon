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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using JSIStudios.SimpleRESTServices.Client;
using JSIStudios.SimpleRESTServices.Client.Json;

namespace InWorldz.Data.Assets.Stratus.CoreExt
{
    public class ExtendedJsonRestServices : JsonRestServices
    {
        private int _writeRequestTimeout = 10000;
        private int _readRequestTimeout = 45000;

        public ExtendedJsonRestServices(int readTimeout, int writeTimeout)
        {
            _readRequestTimeout = readTimeout;
            _writeRequestTimeout = writeTimeout;
        }

        public override Response Stream(Uri url, HttpMethod method, Func<HttpWebResponse, bool, Response> responseBuilderCallback, Stream content, int bufferSize, long maxReadLength, Dictionary<string, string> headers, Dictionary<string, string> queryStringParameters, RequestSettings settings, Action<long> progressUpdated)
        {
            try
            {
                return DoStream(url, method, responseBuilderCallback, content, bufferSize, maxReadLength, 
                    headers, queryStringParameters, settings, progressUpdated, false);
            }
            catch (ProtocolViolationException)
            {
                ServicePoint servicePoint = ServicePointManager.FindServicePoint(url);
                if (servicePoint.ProtocolVersion < HttpVersion.Version11)
                {
                    // this is a workaround for issue #333
                    // https://github.com/openstacknetsdk/openstack.net/issues/333
                    // http://stackoverflow.com/a/22976809/138304
                    int maxIdleTime = servicePoint.MaxIdleTime;
                    servicePoint.MaxIdleTime = 0;
                    System.Threading.Thread.Sleep(1000);
                    servicePoint.MaxIdleTime = maxIdleTime;
                }

                return DoStream(url, method, responseBuilderCallback, content, bufferSize, maxReadLength, headers, 
                    queryStringParameters, settings, progressUpdated, true);
            }
        }

        private Response DoStream(Uri url, HttpMethod method, Func<HttpWebResponse, bool, Response> responseBuilderCallback, 
            Stream content, int bufferSize, long maxReadLength, Dictionary<string, string> headers, 
            Dictionary<string, string> queryStringParameters, RequestSettings settings, 
            Action<long> progressUpdated, bool allowWriteStreamBuffering)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            if (content == null)
                throw new ArgumentNullException("content");
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize");
            if (maxReadLength < 0)
                throw new ArgumentOutOfRangeException("maxReadLength");

            return ExecuteRequest(url, method, responseBuilderCallback, headers, queryStringParameters, settings, (req) =>
            {
                long bytesWritten = 0;

                if (method == HttpMethod.GET)
                {
                    req.Timeout = _readRequestTimeout;
                    req.ReadWriteTimeout = _readRequestTimeout;
                }
                else
                {
                    req.Timeout = _writeRequestTimeout;
                    req.ReadWriteTimeout = _writeRequestTimeout;
                }

                //Console.WriteLine("TO----> " + req.Timeout.ToString() );

                if (settings.ChunkRequest || maxReadLength > 0)
                {
                    req.SendChunked = settings.ChunkRequest;
                    req.AllowWriteStreamBuffering = allowWriteStreamBuffering;
                    req.ContentLength = maxReadLength > 0 && content.Length > maxReadLength ? maxReadLength : content.Length;
                }

                using (Stream stream = req.GetRequestStream())
                {
                    var buffer = new byte[bufferSize];
                    int count;
                    while (!req.HaveResponse && (count = content.Read(buffer, 0, maxReadLength > 0 ? (int)Math.Min(bufferSize, maxReadLength - bytesWritten) : bufferSize)) > 0)
                    {
                        bytesWritten += count;

                        stream.Write(buffer, 0, count);

                        if (progressUpdated != null)
                            progressUpdated(bytesWritten);

                        if (maxReadLength > 0 && bytesWritten >= maxReadLength)
                            break;
                    }
                }

                return "[STREAM CONTENT]";
            });
        }
    }
}
