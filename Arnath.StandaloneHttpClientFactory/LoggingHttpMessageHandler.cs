namespace Arnath.StandaloneHttpClientFactory
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Delegating HTTP message handler that logs some basic info about the
    /// request and response. The LogRequestStart and LogRequestEnd methods
    /// can be overridden to do custom logging.
    /// </summary>
    public class LoggingHttpMessageHandler : DelegatingHandler
    {
        /// <summary>
        /// Creates a new instance of the LoggingHttpMessageHandler class that
        /// uses the specified logger to log request information.
        /// </summary>
        /// <param name="logger">The logger to which to log info about requests.</param>
        public LoggingHttpMessageHandler(ILogger logger)
            : this(logger, null)
        {
        }

        /// <summary>
        /// Creates a new instance of the LoggingHttpMessageHandler class that
        /// uses the specified logger to log request information and calls the
        /// specified inner handler.
        /// </summary>
        /// <param name="logger">The logger to which to log info about requests.</param>
        /// <param name="innerHandler">The inner handler to call in-between logging.</param>
        public LoggingHttpMessageHandler(ILogger logger, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            this.LogRequestStart(request);
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            this.LogRequestEnd(request, response, stopwatch.Elapsed);

            return response;
        }

        protected ILogger Logger { get; }

        /// <summary>
        /// Called before the request is dispatched to log information about the request.
        /// </summary>
        /// <param name="request">The HTTP request that is about to be dispatched.</param>
        protected virtual void LogRequestStart(HttpRequestMessage request)
        {
            this.Logger.LogInformation(
                "Sending HTTP request {HttpMethod} {Uri}",
                request.Method,
                request.RequestUri);
        }

        /// <summary>
        /// Called after the response has been received to log information about the
        /// request and response. Not called if an exception is thrown.
        /// </summary>
        /// <param name="request">The HTTP request that was sent to the server.</param>
        /// <param name="response">The HTTP response from the server</param>
        /// <param name="duration">The duration of the request.</param>
        protected virtual void LogRequestEnd(HttpRequestMessage request, HttpResponseMessage response, TimeSpan duration)
        {
            this.Logger.LogInformation(
                "Received HTTP response after {ElapsedMilliseconds}ms - {StatusCode}",
                duration.TotalMilliseconds,
                response.StatusCode);
        }
    }
}
