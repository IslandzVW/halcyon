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
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Reflection;
using log4net;
using Nwc.XmlRpc;
using System.IO.Compression;

namespace OpenSim.Framework.Servers.HttpServer
{
    public delegate XmlRpcResponse XmlRpcMethod(XmlRpcRequest request, IPEndPoint client);

    public class XmlRpcStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private XmlRpcMethod m_xmlrpcMethod;

        public XmlRpcStreamHandler(string httpMethod, string path, XmlRpcMethod xmlrpcMethod) : base(httpMethod, path)
        {
            m_xmlrpcMethod = xmlrpcMethod;
        }
        
        public XmlRpcMethod Method
        {
            get { return m_xmlrpcMethod; }
        }

        public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            XmlRpcRequest xmlRpcRequest = null;
             XmlRpcResponse xmlRpcResponse = null;
            string requestBody = null;
            byte[] response;

            Encoding encoding = Encoding.UTF8;
            StreamReader streamReader = new StreamReader(request, encoding);
            requestBody = streamReader.ReadToEnd();
            streamReader.Close();

            try
            {
                xmlRpcRequest = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);
            }
            catch (XmlException e)
            {
                m_log.ErrorFormat("[XMLRPC STREAM HANDLER]: XmlRpc request failed to deserialize: {0} -> {1}", e, requestBody);
                xmlRpcRequest = null;
            }

            if (xmlRpcRequest != null)
            {
                string methodName = xmlRpcRequest.MethodName;
                    
                if (methodName != null)
                {
                    xmlRpcRequest.Params.Add(httpRequest.RemoteIPEndPoint); // Param[1]
                    xmlRpcRequest.Params.Add(httpRequest.Url);              // Param[2]
                    xmlRpcRequest.Params.Add(getForwardedFor(httpRequest)); // Param[3]                        
                        
                    try
                    {
                        xmlRpcResponse = m_xmlrpcMethod(xmlRpcRequest, httpRequest.RemoteIPEndPoint);
                    }
                    catch(Exception e)
                    {
                        string errorMessage = 
                            String.Format(
                                "Requested method [{0}] from {1} threw exception: {2} {3}",
                                methodName, httpRequest.RemoteIPEndPoint.Address, e.Message, e.StackTrace);

                        m_log.ErrorFormat("[XMLRPC STREAM HANDLER]: {0}", errorMessage);
                        
                        // if the registered XmlRpc method threw an exception, we pass a fault-code along
                        xmlRpcResponse = new XmlRpcResponse();
                        
                        // Code probably set in accordance with http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
                        xmlRpcResponse.SetFault(-32603, errorMessage);
                    }
                }
                else
                {
                    xmlRpcResponse = new XmlRpcResponse();
            
                    // Code set in accordance with http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
                    xmlRpcResponse.SetFault(
                        XmlRpcErrorCodes.SERVER_ERROR_METHOD,
                        String.Format("Requested method [{0}] not found", methodName));
                }
            
                response = Encoding.UTF8.GetBytes(XmlRpcResponseSerializer.Singleton.Serialize(xmlRpcResponse));
                httpResponse.ContentType = "text/xml";
            }
            else
            {    
                response = Encoding.UTF8.GetBytes("Not found");
                    
                httpResponse.ContentType = "text/plain";
                httpResponse.StatusCode = 404;
                httpResponse.StatusDescription = "Not Found";
                httpResponse.ProtocolVersion = new System.Version("1.0");
        
                m_log.ErrorFormat(
                    "[XMLRPC STREAM HANDLER]: Handler not found for http request {0} {1}",
                    httpRequest.HttpMethod, httpRequest.Url.PathAndQuery);
            }

            httpResponse.ContentLength64 = response.LongLength;
            httpResponse.ContentEncoding = Encoding.UTF8;                  
            httpResponse.SendChunked = false;

            return (response);
        }

        private static string getForwardedFor(OSHttpRequest httpRequest)
        {
            string xff = "X-Forwarded-For";
            string xfflower = xff.ToLower();

            foreach (string s in httpRequest.Headers.AllKeys)
            {
                if ((s != null) && s.Equals(xfflower))
                {
                    xff = xfflower;
                    break;
                }
            }

            return (xff);
        }

    }
}
