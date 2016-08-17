using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSim.Framework.Communications.JWT
{
    /// <summary>
    /// Enumerates the possible causes for a user authentication failure
    /// </summary>
    public enum AuthenticationFailureCause
    {
        /// <summary>
        /// The given username was not found
        /// </summary>
        UserNameNotFound,

        /// <summary>
        /// The given password was invalid
        /// </summary>
        InvalidPassword,

        /// <summary>
        /// The user was not at the requested level
        /// </summary>
        WrongUserLevel
    }
}
