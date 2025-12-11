// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;

namespace MySkUtils
{
    public static class LlmHttpClientProvider
    {
        private static NonDisposableTxAgentHttpHandler _singletonHandler;
        private static readonly object _lock = new object();

        public static HttpClient GetHttpClient(TimeSpan timeout, int maxRetries, TimeSpan baseDelay, string logFilePath)
        {
            if (_singletonHandler == null)
            {
                lock (_lock)
                {
                    if (_singletonHandler == null)
                    {
                        var loggingRetryHandler = new LoggingRetryHttpHandler(maxRetries, baseDelay, logFilePath);
                        _singletonHandler = NonDisposableTxAgentHttpHandler.GetInstance(logFilePath, loggingRetryHandler);
                    }
                }
            }

            var client = new HttpClient(_singletonHandler, disposeHandler: false);
            client.Timeout = timeout;
            return client;
        }
    }
}
