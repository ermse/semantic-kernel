using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp4x
{
    /// <summary>
    /// Represents a message handler that retries HTTP requests based on specified conditions.
    /// </summary>
    /// <remarks>This handler retries requests that fail due to server errors (5xx) or network issues,  using
    /// an exponential backoff strategy with optional jitter. It does not retry requests  that result in client errors
    /// (4xx) or successful responses. The retry logic is applied  up to a maximum number of attempts specified by the
    /// user.</remarks>
    public class RetryHttpHandler : DelegatingHandler
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryHttpHandler"/> class with specified retry settings.
        /// </summary>
        /// <param name="innerHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="baseDelay"></param>
        public RetryHttpHandler(HttpMessageHandler innerHandler, int maxRetries = 5, TimeSpan? baseDelay = null)
            : base(innerHandler)
        {
            this._maxRetries = maxRetries;
            this._baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt <= this._maxRetries; attempt++)
            {
                try
                {
                    var response = await base.SendAsync(request, cancellationToken);

                    // Don't retry on success or client errors (4xx)
                    if (response.IsSuccessStatusCode || ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500))
                    {
                        return response;
                    }

                    // Retry on server errors (5xx) and specific conditions
                    if (this.ShouldRetry(response, attempt))
                    {
                        var delay = this.CalculateDelay(attempt, response);
                        Console.WriteLine($"Request failed with {response.StatusCode}. Retrying in {delay.TotalSeconds} seconds... (Attempt {attempt + 1}/{this._maxRetries + 1})");

                        response.Dispose();
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    return response;
                }
                catch (HttpRequestException ex) when (attempt < this._maxRetries)
                {
                    var delay = this.CalculateDelay(attempt, null);
                    Console.WriteLine($"Network error: {ex.Message}. Retrying in {delay.TotalSeconds} seconds... (Attempt {attempt + 1}/{_maxRetries + 1})");
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < _maxRetries)
                {
                    var delay = this.CalculateDelay(attempt, null);
                    Console.WriteLine($"Request timeout. Retrying in {delay.TotalSeconds} seconds... (Attempt {attempt + 1}/{_maxRetries + 1})");
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            // Final attempt without retry logic
            //return await base.SendAsync(request, cancellationToken);
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
                    return response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                }
            }

            // Exponential backoff with jitter
            var baseDelayMs = this._baseDelay.TotalMilliseconds;
            var exponentialDelay = baseDelayMs * Math.Pow(2, attempt);
            var jitter = new Random().Next(1, 10) * 0.1 * exponentialDelay; // ±10% jitter

            return TimeSpan.FromMilliseconds(exponentialDelay + jitter);
        }
    }
}
