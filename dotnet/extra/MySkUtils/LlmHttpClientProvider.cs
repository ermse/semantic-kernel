// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;

namespace MySkUtils
{
    public static class LlmHttpClientProvider
    {

        public static HttpClient GetHttpClient()
        {
            var client = new HttpClient(NonDisposableLoggingRetrySocketHttpHandler.Instance, disposeHandler: false);
            client.Timeout = TimeSpan.FromSeconds(600);
            return client;
        }
    }
}
