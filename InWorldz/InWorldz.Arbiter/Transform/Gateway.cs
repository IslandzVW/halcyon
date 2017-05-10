using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using OpenSim.Region.Framework.Scenes;

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

        public async Task<ulong> GetPrimHash(SceneObjectPart part)
        {
            using (HttpClient htp = new HttpClient())
            {
                HttpClient hc = new HttpClient();
                HttpResponseMessage result = await hc.GetAsync(_gatewayHost + "/geometry/primhash");

                if (result.IsSuccessStatusCode)
                {
                    return ulong.Parse(await result.Content.ReadAsStringAsync());
                }

                throw new Exception("Server returned error " + result.StatusCode);
            }
        }
    }
}
