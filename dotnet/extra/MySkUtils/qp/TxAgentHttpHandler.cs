//using Newtonsoft.Json;
//using Newtonsoft.Json.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace MySkUtils
{
    /// <summary>
    /// TxAgentHttpHandler
    /// </summary>
    public class TxAgentHttpHandler : DelegatingHandler
    {
        private string logfile;
        private static readonly string dtFormat = "yyyy-MM-dd HH:mm zz";
        /// <summary>
        /// Constructory
        /// </summary>
        public TxAgentHttpHandler()
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="innerHandler"></param>
        public TxAgentHttpHandler(string logFile, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            this.logfile = logFile;
        }

        public readonly string ClientName = nameof(TxAgentHttpHandler);

        private static readonly string s_requestsLeft = "x-ratelimit-remaining-requests";
        private static readonly string s_tokensLeft = "x-ratelimit-remaining-tokens";
        private static readonly string s_msReqId = "x-ms-client-request-id";


        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellation)
        {
            string reqId = null;
            bool isExtendedLoggingEnabled = true;

            try
            {
                if (isExtendedLoggingEnabled)
                {
                    request.Headers.TryGetValues(s_msReqId, out var reqIds);
                    reqId = reqIds?.FirstOrDefault();

                    var (restoreBody, reqStr) = await LogHttpRequest(request, reqId, true)
                        .ConfigureAwait(false);
                    if (restoreBody)
                    {
                        //  creatig new content with the same body
                        // to avoid exception
                        // [System.InvalidOperationException: Content stream position is not at beginning of stream.
                        // at System.ClientModel.Primitives.HttpClientPipelineTransport.HttpClientTransportResponse.<BufferContentSyncOrAsync> d__19.MoveNext()
                        request.Content = new StringContent(
                            reqStr,
                            Encoding.UTF8,
                            request.Content.Headers.ContentType?.MediaType ?? "application/json"
                        );
                        // Copy any custom headers from the original content
                        foreach (var header in request.Content.Headers)
                        {
                            request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }

                var resp = await base.SendAsync(request, cancellation).ConfigureAwait(false);


                if (isExtendedLoggingEnabled)
                {
                    var (restoreResp, respStr) = await this.LogHttpResponse(request, reqId, resp, true)
                        .ConfigureAwait(false);

                    if (restoreResp)
                    {
                        resp.Content = new StringContent(
                            respStr,
                            Encoding.UTF8,
                            resp.Content.Headers.ContentType?.MediaType ?? "application/json"
                            );
                        // copy any custom headers from the original content
                        foreach (var header in resp.Content.Headers)
                        {
                            resp.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }

                if (!resp.IsSuccessStatusCode)
                {
                    //CommonAgentEvents.Log.LllmResponseErrorResultCode(principal, reqId, resp.StatusCode, request.RequestUri);
                }
                return resp;

            }
            catch (Exception ex)
            {
                //CommonAgentEvents.Log.LllmResponseException(principal, reqId, request.RequestUri, ex);
                throw;
            }

        }

        /// <summary>
        /// LogHttpRequest
        /// </summary>
        /// <param name="request"></param>
        /// <param name="reqId"></param>
        /// <returns></returns>
        private async Task<(bool, string)> LogHttpRequest(HttpRequestMessage request, string reqId, bool logLlmRequestBody)
        {
            try
            {

                File.AppendAllText(logfile, string.Format("LLM Http Request\nId: {0}\nUrl:{1}\nTimestamp:{2}\n", reqId, request.RequestUri, DateTimeOffset.Now.ToString(dtFormat)));

                if (logLlmRequestBody)
                {
                    if (request.Content != null)
                    {
                        string reqStr = await (request.Content.ReadAsStringAsync())
                            .ConfigureAwait(false);
#if DEBUG
                        reqStr.DumpDebugContentAsync();
#endif
                        if (!string.IsNullOrWhiteSpace(reqStr))
                        {
                            try
                            {
                                var prettifiedRequestJson = reqStr.FilterOutBase64Content();
                                File.AppendAllText(logfile, string.Format("Body:\n{0}\n", prettifiedRequestJson));
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllText(logfile, string.Format("Error parsing json:\n{0}\n{1}", ex, reqStr));
                            }
                        }
                        else
                        {
                            File.AppendAllText(logfile, "Request body is empty!");
                        }
                        return (true, reqStr);
                    }
                    else
                    {
                        return (false, null);
                    }
                }
                else
                {
                    return (false, null);
                }
            }
            catch
            {
                return (false, null);
            }
        }

        /// <summary>
        /// LogHttpResponse
        /// </summary>
        /// <param name="request"></param>
        /// <param name="reqId"></param>
        /// <param name="resp"></param>
        /// <returns></returns>
        private async Task<(bool, string)> LogHttpResponse(HttpRequestMessage request, string reqId, HttpResponseMessage resp, bool logLlmResponseBody)
        {
            try
            {
                if (resp.IsSuccessStatusCode || 429 == (int)resp.StatusCode)
                {
                    DevTimeAssert(request, reqId);
                    resp.Headers.TryGetValues(s_requestsLeft, out var requestsLeft);
                    string rl = requestsLeft?.FirstOrDefault();
                    resp.Headers.TryGetValues(s_tokensLeft, out var tokensLeft);
                    string tl = tokensLeft?.FirstOrDefault();

                    if (resp.IsSuccessStatusCode)
                    {
                        File.AppendAllText(
                            logfile,
                            string.Format(
                            "LLM Http Response Success\nReqId: {0}\nStatus Code: {1}\nRequests Left: {2}\nTokens Left: {3}\nTimestamp:{4}\n",
                            reqId,
                            resp.StatusCode,
                            rl,
                            tl,
                            DateTimeOffset.Now.ToString(dtFormat)));


                        if (logLlmResponseBody)
                        {
                            /*
                             * removing logging of the content body because it is not compatible
                             * with cases when we ask for streaming response:
                             * Headers	{Content-Type: text/event-stream; charset=utf-8   }	System.Net.Http.Headers.HttpContentHeaders
                            */
                            string respStr = await resp.Content
                             .ReadAsStringAsync()
                             .ConfigureAwait(false);

                            if (!string.IsNullOrWhiteSpace(respStr))
                            {
                                var jsonStr = JsonPrettifier.PrettifyLlmRequestJson(respStr);

                                File.AppendAllText(
                                logfile,
                                string.Format("Body:\n{0}\n", jsonStr));
                            }
                            else
                            {
                                File.AppendAllText(
                                logfile,
                                string.Format("Response body is empty")); // should not be here 
                            }
                            return (true, respStr);

                        }

                        return (false, null);
                        //CommonAgentEvents.Log.LllmResponseSuccess(principal, reqId, resp.StatusCode, rl, tl, request.RequestUri, respStr);
                    }
                    else
                    {
                        var d = resp.Headers.RetryAfter?.Delta;

                        File.AppendAllText(logfile,
                            string.Format(
                            "LLM Http Response Error\nReqId: {0}\nStatus Code: {1}\nRetry After: {2}\nRequests Left: {3}\nTokens Left: {4}\n",
                            reqId,
                            resp.StatusCode,
                            d,
                            rl,
                            tl));

                        return (false, null);
                        //CommonAgentEvents.Log.LllmResponseErrorWithBody(principal, reqId, resp.StatusCode, rl, tl, request.RequestUri, respStr);
                    }
                }
                else
                {
                    return (false, null);
                }
            }
            catch
            {
                // ignore logging errors
                return (false, null);
            }
        }

        /// <summary>
        /// DevTimeAssert
        /// </summary>
        /// <param name="request"></param>
        /// <param name="reqId"></param>
        private static void DevTimeAssert(HttpRequestMessage request, string reqId)
        {
            // debug time sanity check
            request.Headers.TryGetValues(s_msReqId, out var respReqIds);
            var respReqId = respReqIds?.FirstOrDefault();
            Debug.Assert(reqId == respReqId);
        }
    }
}
