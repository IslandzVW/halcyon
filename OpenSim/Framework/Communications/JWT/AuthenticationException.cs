using System;

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
        public AuthenticationException(AuthenticationFailureCause cause) : base("Authentication failure: " + cause)
        {
            Cause = cause;
        }

        public AuthenticationException(AuthenticationFailureCause cause, Exception innerException) 
            : base("Authentication failure: " + cause, innerException)
        {
            Cause = cause;
        }
    }
}
