using System;
using System.Net.Http;

namespace MySkUtils
{
    /// <summary>
    /// Represents a message handler that retries HTTP requests based on specified conditions.
    /// </summary>
    /// <remarks>This handler retries requests that fail due to server errors (5xx) or network issues,  using
    /// an exponential backoff strategy with optional jitter. It does not retry requests  that result in client errors
    /// (4xx) or successful responses. The retry logic is applied  up to a maximum number of attempts specified by the
    /// user.</remarks>
    public sealed class NonDisposableTxAgentHttpHandler : TxAgentHttpHandler
    {
        private static NonDisposableTxAgentHttpHandler _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="NonDisposableLoggingRetrySocketHttpHandler"/> class.
        /// </summary>
         private NonDisposableTxAgentHttpHandler(string logFile, HttpMessageHandler innerHandler)
            : base(logFile, innerHandler)
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="NonDisposableTxAgentHttpHandler"/> with custom settings.
        /// The first call to this method initializes the singleton with the provided parameters. Subsequent calls return the same instance and ignore the parameters.
        /// </summary>
        /// <param name="logFilePath">The path to the log file.</param>
        /// <param name="innerHandler">The inner HTTP message handler.</param>
        /// <returns>The singleton instance of <see cref="NonDisposableTxAgentHttpHandler"/>.</returns>
        public static NonDisposableTxAgentHttpHandler GetInstance(string logFilePath, HttpMessageHandler innerHandler)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new NonDisposableTxAgentHttpHandler(logFilePath, innerHandler);
                    }
                }
            }
            
            return _instance;
        }
       
        /// <summary>
        /// Disposes the underlying resources held by the <see cref="NonDisposableLoggingRetrySocketHttpHandler"/>.
        /// This implementation does nothing to prevent unintended disposal, as it may affect all references.
        /// </summary>
        /// <param name="disposing">True if called from <see cref="Dispose"/>, false if called from a finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            // Do nothing if called explicitly from Dispose, as it may unintentionally affect all references.
            // The base.Dispose(disposing) is not called to avoid invoking the disposal of HttpClientHandler resources.
            // This implementation assumes that the HttpMessageHandler is being used as a singleton and should not be disposed directly.
        }
    }
}
