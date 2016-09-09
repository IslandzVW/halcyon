using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace OpenSim.Framework.Communications.JWT
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

            if (!string.IsNullOrWhiteSpace(privateKeyPath))
            {
                // Read the public certificate
                var cert = new X509Certificate2(privateKeyPath);

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

        public bool Verify(string body, string signature, bool isBase64 = true)
        {
            if (m_publicKey == null)
            {
                throw new JWTSignatureException(JWTSignatureFailureCauses.MissingPrivateKey);
            }

            var sig = Convert.FromBase64String(isBase64 ? DecodeBase64(signature) : signature);

            return m_publicKey.VerifyData(System.Text.Encoding.UTF8.GetBytes(body), CryptoConfig.MapNameToOID("SHA256"), sig);
        }

        public static string EncodeBase64(string body)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body));
        }

        public static string DecodeBase64(string body)
        {
            return System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(body));
        }
    }
}

