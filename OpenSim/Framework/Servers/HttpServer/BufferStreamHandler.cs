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

using System.IO;
using System.Text;

namespace OpenSim.Framework.Servers.HttpServer
{
    public delegate byte[] BufferMethod(byte[] data, string path, string param);
    public delegate byte[] BufferStreamMethod(Stream data, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse);

    /// <summary>
    /// This class provides fairly simple unprocessed handler for messages made of binary buffers such as ProtoBuf messages.
    /// </summary>
    public class BufferStreamHandler : BaseStreamHandler
    {
        private BufferMethod m_method;
        private BufferStreamMethod m_streamMethod;

        public BufferStreamHandler(string httpMethod, string path, BufferMethod binaryMethod)
            : this(httpMethod, path, binaryMethod, null, null) {}

        public BufferStreamHandler(string httpMethod, string path, BufferStreamMethod binaryMethod)
            : this(httpMethod, path, binaryMethod, null, null) { }

        public BufferStreamHandler(string httpMethod, string path, BufferMethod binaryMethod, string name, string description)
            : base(httpMethod, path, name, description)
        {
            m_method = binaryMethod;
        }

        public BufferStreamHandler(string httpMethod, string path, BufferStreamMethod binaryMethod, string name, string description)
            : base(httpMethod, path, name, description)
        {
            m_streamMethod = binaryMethod;
        }

        public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);
            byte[] response;

            if (m_method != null)
            {
                byte[] data = ReadFully(request);
                response = m_method(data, path, param);
            }
            else
            {
                response = m_streamMethod(request, path, param, httpRequest, httpResponse);
            }

            return response;
        }

        private static byte[] ReadFully(Stream stream)
        {
            byte[] buffer = new byte[1024];
            using (MemoryStream ms = new MemoryStream(1024*256))
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);

                    if (read <= 0)
                    {
                        return ms.ToArray();
                    }

                    ms.Write(buffer, 0, read);
                }
            }
        }
    }
}
