using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MySkUtils
{
    /// <summary>
    /// Represents a message handler that retries HTTP requests based on specified conditions.
    /// </summary>
    /// <remarks>This handler retries requests that fail due to server errors (5xx) or network issues,  using
    /// an exponential backoff strategy with optional jitter. It does not retry requests  that result in client errors
    /// (4xx) or successful responses. The retry logic is applied  up to a maximum number of attempts specified by the
    /// user.</remarks>
    public class LoggingRetryHttpHandler : DelegatingHandler
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;
        private readonly string _logFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingRetryHttpHandler"/> class with specified retry settings.
        /// </summary>
        /// <param name="maxRetries"></param>
        /// <param name="baseDelay"></param>
        /// <param name="logFilePath"></param>
        public LoggingRetryHttpHandler(int maxRetries, TimeSpan? baseDelay, string logFilePath)
            : this(next: new HttpClientHandler(), maxRetries, baseDelay, logFilePath)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryHttpHandler"/> class with specified retry settings.
        /// </summary>
        /// <param name="maxRetries"></param>
        /// <param name="baseDelay"></param>
        /// <param name="logFilePath">Path to the log file. If null, uses "retry-log.txt" in the current directory.</param>
        public LoggingRetryHttpHandler(HttpMessageHandler next, int maxRetries, TimeSpan? baseDelay, string logFilePath)
            : base(next)
        {
            this._maxRetries = maxRetries;
            this._baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
            this._logFilePath = logFilePath;
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var logBuilder = new StringBuilder();
            var retryIntervals = new List<double>();
            var startTime = DateTimeOffset.Now;
            var requestId = Guid.NewGuid().ToString("N"); // Short unique identifier

            logBuilder.AppendLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] HTTP Request started - ID: {requestId} - URL: {request.RequestUri}");

            for (int attempt = 0; attempt <= this._maxRetries; attempt++)
            {
                var attemptStartTime = DateTimeOffset.Now;

                try
                {
                    var response = await base.SendAsync(request, cancellationToken);

                    // Don't retry on success or client errors (4xx)
                    if (response.IsSuccessStatusCode || ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500))
                    {
                        var totalDuration = DateTimeOffset.Now - startTime;
                        var intervals = retryIntervals.Count > 0 ? $"[{string.Join(", ", retryIntervals.ConvertAll(x => $"{x:F2}s"))}]" : "[]";

                        logBuilder.AppendLine($"Attempts: {attempt + 1} - Intervals: {intervals} - Final Result: {response.StatusCode} - Total Duration: {totalDuration.TotalSeconds:F2}s");

                        this.WriteLogToFile(logBuilder.ToString());
                        return response;
                    }

                    // Retry on server errors (5xx) and specific conditions
                    if (this.ShouldRetry(response, attempt))
                    {
                        var delay = this.CalculateDelay(attempt, response);
                        retryIntervals.Add(delay.TotalSeconds);

                        logBuilder.AppendLine($"Attempt {attempt + 1}/{this._maxRetries + 1} - {attemptStartTime:HH:mm:ss.fff} - Status: {response.StatusCode} - Retrying in {delay.TotalSeconds:F2}s - Reason: Server error");

                        response.Dispose();
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    // Final attempt reached with server error but no more retries
                    var finalDuration = DateTimeOffset.Now - startTime;
                    var finalIntervals = retryIntervals.Count > 0 ? $"[{string.Join(", ", retryIntervals.ConvertAll(x => $"{x:F2}s"))}]" : "[]";

                    logBuilder.AppendLine($"Attempts: {attempt + 1} - Intervals: {finalIntervals} - Final Result: {response.StatusCode} - Total Duration: {finalDuration.TotalSeconds:F2}s");

                    this.WriteLogToFile(logBuilder.ToString());
                    return response;
                }
                catch (HttpRequestException ex) when (attempt < this._maxRetries)
                {
                    var delay = this.CalculateDelay(attempt, null);
                    retryIntervals.Add(delay.TotalSeconds);

                    logBuilder.AppendLine($"Attempt {attempt + 1}/{this._maxRetries + 1} - {attemptStartTime:HH:mm:ss.fff} - Status: N/A - Retrying in {delay.TotalSeconds:F2}s - Reason: Network error: {ex.Message}");

                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < _maxRetries)
                {
                    var delay = this.CalculateDelay(attempt, null);
                    retryIntervals.Add(delay.TotalSeconds);

                    logBuilder.AppendLine($"Attempt {attempt + 1}/{this._maxRetries + 1} - {attemptStartTime:HH:mm:ss.fff} - Status: N/A - Retrying in {delay.TotalSeconds:F2}s - Reason: Request timeout");

                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    // $exception	{"Cannot access a disposed object.\r\nObject name: 'SslStream'."}	System.ObjectDisposedException
                    var errorDuration = DateTimeOffset.Now - startTime;
                    var errorIntervals = retryIntervals.Count > 0 ? $"[{string.Join(", ", retryIntervals.ConvertAll(x => $"{x:F2}s"))}]" : "[]";

                    logBuilder.AppendLine($"Attempts: {attempt + 1} - Intervals: {errorIntervals} - Final Result: Exception ({ex.GetType().Name}: {ex.Message}) - Total Duration: {errorDuration.TotalSeconds:F2}s");

                    this.WriteLogToFile(logBuilder.ToString());
                    throw;
                }
            }

            // This should never be reached due to the loop structure, but included for completeness
            var fallbackDuration = DateTimeOffset.Now - startTime;
            var fallbackIntervals = retryIntervals.Count > 0 ? $"[{string.Join(", ", retryIntervals.ConvertAll(x => $"{x:F2}s"))}]" : "[]";

            logBuilder.AppendLine($"Attempts: {this._maxRetries + 1} - Intervals: {fallbackIntervals} - Final Result: Retry logic exhausted - Total Duration: {fallbackDuration.TotalSeconds:F2}s");

            this.WriteLogToFile(logBuilder.ToString());
            throw new InvalidOperationException("Retry logic exhausted without returning a response.");
        }

        private bool ShouldRetry(HttpResponseMessage response, int attempt)
        {
            if (attempt >= this._maxRetries) return false;

            return response.StatusCode == HttpStatusCode.InternalServerError || // 500
                   response.StatusCode == HttpStatusCode.BadGateway ||          // 502
                   response.StatusCode == HttpStatusCode.ServiceUnavailable ||  // 503
                   response.StatusCode == HttpStatusCode.GatewayTimeout;        // 504
        }

        private TimeSpan CalculateDelay(int attempt, HttpResponseMessage response)
        {
            // Check for Retry-After header
            if (response?.Headers.RetryAfter != null)
            {
                if (response.Headers.RetryAfter.Delta.HasValue)
                {
                    return response.Headers.RetryAfter.Delta.Value;
                }
                if (response.Headers.RetryAfter.Date.HasValue)
                {
                    return response.Headers.RetryAfter.Date.Value - DateTimeOffset.Now;
                }
            }

            // Exponential backoff with jitter
            var baseDelayMs = this._baseDelay.TotalMilliseconds;
            var exponentialDelay = baseDelayMs * Math.Pow(2, attempt);
            var jitter = new Random().Next(1, 10) * 0.1 * exponentialDelay; // ±10% jitter

            return TimeSpan.FromMilliseconds(exponentialDelay + jitter);
        }

        private void WriteLogToFile(string logContent)
        {
            var fullContent = $"=== HTTP Retry Session ==={Environment.NewLine}{logContent}=== End Session ==={Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(this._logFilePath, fullContent, Encoding.UTF8);
        }
    }
}
