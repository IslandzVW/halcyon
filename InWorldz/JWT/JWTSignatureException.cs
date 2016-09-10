using System;

namespace InWorldz.JWT
{
    public class JWTSignatureException : Exception
    {
        /// <summary>
        /// The cause for this exception
        /// </summary>
        public JWTSignatureFailureCauses Cause { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cause">The cause of the exception</param>
        public JWTSignatureException(JWTSignatureFailureCauses cause) : base("Signature failure: " + cause)
        {
            Cause = cause;
        }

        public JWTSignatureException(JWTSignatureFailureCauses cause, Exception innerException) : base("Signature failure: " + cause, innerException)
        {
            Cause = cause;
        }
    }
}

