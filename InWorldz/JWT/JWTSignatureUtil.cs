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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace InWorldz.JWT
{
    public class JWTSignatureUtil
    {
        private RSACryptoServiceProvider m_privateKey;
        private RSACryptoServiceProvider m_publicKey;

        public JWTSignatureUtil(string privateKeyPath = null, string publicKeyPath = null)
        {
            if (!string.IsNullOrWhiteSpace(privateKeyPath))
            {
                // Read the private certificate
                var cert = new X509Certificate2(privateKeyPath);

                m_privateKey = new RSACryptoServiceProvider();
                m_privateKey.FromXmlString(cert.PrivateKey.ToXmlString(true));
            }

            if (!string.IsNullOrWhiteSpace(publicKeyPath))
            {
                // Read the public certificate
                var cert = new X509Certificate2(publicKeyPath);

                m_publicKey = (RSACryptoServiceProvider)cert.PublicKey.Key;
            }
        }

        public string Sign(string body)
        {
            if (m_privateKey == null)
            {
                throw new JWTSignatureException(JWTSignatureFailureCauses.MissingPrivateKey);
            }

            var signature = m_privateKey.SignData(System.Text.Encoding.UTF8.GetBytes(body), CryptoConfig.MapNameToOID("SHA256"));

            return Convert.ToBase64String(signature);
        }

        public bool Verify(string body, string signature)
        {
            if (m_publicKey == null)
            {
                throw new JWTSignatureException(JWTSignatureFailureCauses.MissingPublicKey);
            }

            var sig = DecodeBase64(signature);

            return m_publicKey.VerifyData(System.Text.Encoding.UTF8.GetBytes(body), CryptoConfig.MapNameToOID("SHA256"), sig);
        }

        private static byte[] DecodeBase64(string body)
        {
            // Thank you to http://stackoverflow.com/a/9301545
            body = body.Trim().Replace(" ", "+").Replace('-', '+').Replace('_', '/');
            if (body.Length % 4 > 0)
            {
                body = body.PadRight(body.Length + 4 - body.Length % 4, '=');
            }
            return Convert.FromBase64String(body);
        }
    }
}

