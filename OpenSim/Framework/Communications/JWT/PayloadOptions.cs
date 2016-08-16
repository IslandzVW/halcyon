using System;

namespace OpenSim.Framework.Communications.JWT
{
    /// <summary>
    /// Options that can are passed through a JWT authentication payload
    /// </summary>
    class PayloadOptions
    {
        /// <summary>
        /// The expiration date for this payload
        /// </summary>
        public DateTime Exp { get; set; }

        /// <summary>
        /// The auth scope that this authentication token applies to (eg: remote-console)
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// The username that was used to generate this authentication payload
        /// </summary>
        public string Username { get; set; }
    }
}
