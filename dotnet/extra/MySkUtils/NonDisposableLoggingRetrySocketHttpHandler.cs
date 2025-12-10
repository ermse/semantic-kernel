namespace MySkUtils
{
    /// <summary>
    /// Represents a message handler that retries HTTP requests based on specified conditions.
    /// </summary>
    /// <remarks>This handler retries requests that fail due to server errors (5xx) or network issues,  using
    /// an exponential backoff strategy with optional jitter. It does not retry requests  that result in client errors
    /// (4xx) or successful responses. The retry logic is applied  up to a maximum number of attempts specified by the
    /// user.</remarks>
    public sealed class NonDisposableLoggingRetrySocketHttpHandler : LoggingRetryHttpHandler
    {
        private static string LogFilePath = "C:\\tmp\\SemanticKernelDebug\\log.txt";

        private NonDisposableLoggingRetrySocketHttpHandler(string logFilePath)
            : base(logFilePath: logFilePath)
        {
        }

        //TODO:
        // modify this property to functin
        /// <summary>
        /// Gets the singleton instance of the <see cref="NonDisposableLoggingRetrySocketHttpHandler"/>.
        /// </summary>
        public static NonDisposableLoggingRetrySocketHttpHandler Instance { get; } =

            new NonDisposableLoggingRetrySocketHttpHandler(LogFilePath);

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
