using System;

namespace OpenSim.Framework.Communications.JWT
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

