using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FlatBuffers;

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

        public ulong GetPrimHash(ByteBuffer halcyonPrimBuffer)
        {
            using (HttpClient htp = new HttpClient())
            using (ByteArrayContent content = new ByteArrayContent(halcyonPrimBuffer.Data))
            using (HttpResponseMessage result = htp.PostAsync($"{_gatewayHost}/geometry/primhash", content).Result)
            {
                if (result.IsSuccessStatusCode)
                {
                    return ulong.Parse(result.Content.ReadAsStringAsync().Result);
                }

                throw new Exception($"Server returned error {result.StatusCode}");
            }
        }

        public ulong GetObjectGroupHash(ByteBuffer halcyonGroupBuffer)
        {
            using (HttpClient htp = new HttpClient())
            using (ByteArrayContent content = new ByteArrayContent(halcyonGroupBuffer.Data))
            using (HttpResponseMessage result = htp.PostAsync($"{_gatewayHost}/geometry/grouphash", content).Result)
            {
                if (result.IsSuccessStatusCode)
                {
                    return ulong.Parse(result.Content.ReadAsStringAsync().Result);
                }
                throw new Exception($"Server returned error {result.StatusCode}");
            }
        }
    }
}
