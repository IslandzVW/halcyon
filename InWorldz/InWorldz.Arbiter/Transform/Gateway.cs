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

        public async Task<ulong> GetPrimHash(ByteBuffer halcyonPrimBuffer)
        {
            using (HttpClient htp = new HttpClient())
            using (ByteArrayContent content = new ByteArrayContent(halcyonPrimBuffer.Data))
            using (HttpResponseMessage result = await htp.PostAsync($"{_gatewayHost}/geometry/primhash", content))
            {
                if (result.IsSuccessStatusCode)
                {
                    return ulong.Parse(await result.Content.ReadAsStringAsync());
                }

                throw new Exception($"Server returned error {result.StatusCode}");
            }
        }

        public async Task<ulong> GetObjectGroupHash(ByteBuffer halcyonGroupBuffer)
        {
            using (HttpClient htp = new HttpClient())
            using (ByteArrayContent content = new ByteArrayContent(halcyonGroupBuffer.Data))
            using (HttpResponseMessage result = await htp.PostAsync($"{_gatewayHost}/geometry/grouphash", content))
            {
                if (result.IsSuccessStatusCode)
                {
                    return ulong.Parse(await result.Content.ReadAsStringAsync());
                }
                throw new Exception($"Server returned error {result.StatusCode}");
            }
        }
    }
}
