using System;

namespace InWorldz.JWT
{
    public class JWTokenException : Exception
    {
        /// <summary>
        /// The cause for this exception
        /// </summary>
        public string Cause { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cause">The cause of the exception</param>
        public JWTokenException(string cause) : base("Signature failure: " + cause)
        {
            Cause = cause;
        }

        public JWTokenException(string cause, Exception innerException) : base("Signature failure: " + cause, innerException)
        {
            Cause = cause;
        }
    }
}

