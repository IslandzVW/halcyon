using System;
using System.Runtime.Serialization;

namespace OpenSim.Framework.Communications.JWT
{
    /// <summary>
    /// Exception thrown when JWT user authentication fails
    /// </summary>
    public class AuthenticationException : Exception
    {
        /// <summary>
        /// The cause for this exception
        /// </summary>
        public AuthenticationFailureCause Cause { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cause">The cause of the exception</param>
        public AuthenticationException(AuthenticationFailureCause cause)
        {
            Cause = cause;
        }

        public AuthenticationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
