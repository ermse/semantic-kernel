// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp4x
{
    internal class DebugHttpHandler : DelegatingHandler
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        public DebugHttpHandler() : this(next: new HttpClientHandler())
#pragma warning restore CA2000 // Dispose objects before losing scope
        {

        }
        public DebugHttpHandler(HttpMessageHandler next) : base(next)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var resp = await base.SendAsync(request, cancellationToken);

                await SaveResponseAsync(request, resp, cancellationToken).ConfigureAwait(false);


                if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable) // 503 service unavailable
                {
                    return resp; // this is for debugging convenience
                }
                else if (((int)resp.StatusCode) == 429) // 429 too many requests
                {
                    return resp; // this is for debugging convenience
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable) // 500 internal server error
                {
                    return resp; // this is for debugging convenience
                }
                return resp;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static async Task SaveResponseAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancel)
        {
            if (response.Content != null)
            {
                string res = await (response.Content.ReadAsStringAsync())
                    .ConfigureAwait(false);
                File.WriteAllText("C:\\tmp\\SemanticKernelDebug\\response.json", res);
            }
        }
    }
}
