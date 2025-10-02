using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp4x
{
    /// <summary>
    /// LlmHttpClient
    /// </summary>
    public class LlmHttpClient : HttpClient
    {
        /// <summary>
        /// LlmHttpClient
        /// </summary>
        /// <param name="handler"></param>
        public LlmHttpClient(DelegatingHandler handler) : base(handler)
        {

        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var resp = await base.SendAsync(request, cancellationToken);
                if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable) // 503 service unavailable
                {
                    return resp; // this is for debugging convenience
                }
                else if (((int)resp.StatusCode) == 429) // 429 too many requests
                {
                    return resp; // this is for debugging convenience
                }
                return resp;
            }
            catch(Exception)
            {
                throw; // this is for debugging convenience
            }
        }
    }
}
