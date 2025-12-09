// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;

namespace ConsoleApp4x
{
    internal static class LlmHttpClientProvider
    {

        internal static HttpClient GetHttpClient()
        {
            var client = new HttpClient(NonDisposableLoggingRetrySocketHttpHandler.Instance, disposeHandler: false);
            client.Timeout = TimeSpan.FromSeconds(20);
            return client;
        }
    }
}
