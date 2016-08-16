using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSim.Framework.Communications.JWT
{
    class JWTUserAuthenticationGateway
    {
        private IUserService m_userService;

        public JWTUserAuthenticationGateway(IUserService userService)
        {
            m_userService = userService;

        }
    }
}
