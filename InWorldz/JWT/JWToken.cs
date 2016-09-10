using System;
namespace InWorldz.JWT
{
    /// <summary>
    /// Simplified access to and from a JWT token and the data contained therein.
    /// </summary>
    public class JWToken
    {
        public static readonly string ValidHeader = "{\"alg\":\"SH256\",\"typ\":\"JWT\"}"; // Because that's the only header supported.

        public string Header
        {
            get;
            private set;
        }

        public PayloadOptions Payload
        {
            get;
            private set;
        }

        public bool HasValidSignature
        {
            get;
            private set;
        }

        private readonly string m_token;

        public JWToken(string token, JWTSignatureUtil sigUtil)
        {
            m_token = token;

            var parts = m_token.Split('.');
            Header = DecodeBase64(parts[0]);
            Payload = LitJson.JsonMapper.ToObject<PayloadOptions>(parts[1]);
            HasValidSignature = sigUtil.Verify(body: parts[0] + "." + parts[1], signature: parts[2], isBase64: true);
        }

        public JWToken(PayloadOptions payloadOptions, JWTSignatureUtil sigUtil)
        {
            Header = ValidHeader;
            Payload = payloadOptions;
            HasValidSignature = true;

            var body = EncodeBase64(Header) + "." + EncodeBase64(LitJson.JsonMapper.ToJson(payloadOptions));

            m_token = body + "." + sigUtil.Sign(body);
        }

        /// <summary>
        /// Returns the JWT string as needed for JWT clients.
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:OpenSim.Framework.Communications.JWT.JWToken"/>.</returns>
        public override string ToString()
        {
            return m_token;
        }

        private static string EncodeBase64(string body)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body));
        }

        private static string DecodeBase64(string body)
        {
            return System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(body));
        }
    }
}

