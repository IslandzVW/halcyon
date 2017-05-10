using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InWorldz.Arbiter.Transform
{
    /// <summary>
    /// Provides a connection to a transform gateway that can calculate the
    /// visual hash for a primitive group.
    /// </summary>
    internal class Gateway
    {
        private string _gatewayHost;

        public Gateway(string gatewayHost)
        {
            _gatewayHost = gatewayHost;
        }

        /*public ulong GetPrimHash()
        {

        }*/
    }
}
