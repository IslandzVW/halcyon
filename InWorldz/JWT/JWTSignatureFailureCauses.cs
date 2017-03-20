using System;

namespace InWorldz.JWT
{
    public enum JWTSignatureFailureCauses
    {
        /// <summary>
        /// The private key was not configured or had an error during read.
        /// </summary>
        MissingPrivateKey,

        /// <summary>
        /// The public key was not configured or had an error during read.
        /// </summary>
        MissingPublicKey,
    }
}

