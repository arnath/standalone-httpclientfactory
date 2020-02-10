namespace Arnath.StandaloneHttpClientFactory
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Factory for System.Net.Http.HttpClient instances that follow the recommended
    /// best practices for creation and disposal. See <a href="https://github.com/arnath/standalonehttpclientfactory">
    /// https://github.com/arnath/standalonehttpclientfactory</a> for more details.
    /// </summary>
    public class StandaloneHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly IHttpClientFactory frameworkSpecificFactory;

        /// <summary>
        /// Creates a new instance of the StandaloneHttpClientFactory class with the
        /// default value of 15 mins for pooled connection lifetime and no delegating
        /// handlers.
        /// </summary>
        public StandaloneHttpClientFactory()
            : this(logger: null)
        {
        }

        /// <summary>
        /// Creates an instance of the StandaloneHttpClientFactory class with the
        /// default value of 15 mins for pooled connection lifetime and a single
        /// delegating handler that uses the provided ILogger to create a
        /// LoggingHttpMessageHandler that logs some info about requests.
        /// </summary>
        /// <param name="logger">The logger to which to log info about requests.</param>
        public StandaloneHttpClientFactory(ILogger logger)
            : this(TimeSpan.FromMinutes(15), logger)
        {
        }

        /// <summary>
        /// Creates an instance of the StandaloneHttpClientFactory class with the
        /// specified value for pooled connection lifetime and a single
        /// delegating handler that uses the provided ILogger to create a
        /// LoggingHttpMessageHandler that logs some info about requests.
        /// </summary>
        /// <param name="pooledConnectionLifetime">The length of time to use pooled connections.</param>
        /// <param name="logger">The logger to which to log info about requests.</param>
        public StandaloneHttpClientFactory(TimeSpan pooledConnectionLifetime, ILogger logger)
            : this(pooledConnectionLifetime, new LoggingHttpMessageHandler(logger))
        {
            
        }

        /// <summary>
        /// Creates an instance of the StandaloneHttpClientFactory class with the
        /// specified value for pooled connection lifetime and the specified
        /// set of delegating handlers.
        /// </summary>
        /// <param name="pooledConnectionLifetime">The length of time to use pooled connections.</param>
        /// <param name="delegatingHandlers">Array of DelegatingHandler instances that can be
        /// used for logging, etc. See LoggingHttpMessageHandler for an example.</param>
        public StandaloneHttpClientFactory(TimeSpan pooledConnectionLifetime, params DelegatingHandler[] delegatingHandlers)
        {
#if NETCOREAPP
            this.frameworkSpecificFactory = new DotNetCoreHttpClientFactory(pooledConnectionLifetime, delegatingHandlers);
#else
            this.frameworkSpecificFactory = new DotNetStandardHttpClientFactory(delegatingHandlers);
#endif
        }

        /// <summary>
        /// Creates an HTTP client with the factory's pooled connection lifetime and
        /// delegating handlers.
        /// </summary>
        /// <returns>The HTTP client instance. This can be disposed freely; the instances
        /// returned by the factory will handle doing the right thing.</returns>
        public HttpClient CreateClient()
        {
            return this.frameworkSpecificFactory.CreateClient();
        }

        /// <summary>
        /// Releases the resources used by the StandaloneHttpClientFactory. Does nothing
        /// when called from .NET Core.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Linsk together a set of DelegatingHandler instances with the framework specific
        /// handler to generate a handler pipeline that can be used to do things like log
        /// request and response information.
        /// </summary>
        private static HttpMessageHandler CreateHandlerPipeline(HttpMessageHandler handler, params DelegatingHandler[] delegatingHandlers)
        {
            HttpMessageHandler next = handler;
            if (delegatingHandlers != null)
            {
                for (int i = delegatingHandlers.Length - 1; i >= 0; i--)
                {
                    delegatingHandlers[i].InnerHandler = next;
                    next = delegatingHandlers[i];
                }
            }

            return next;
        }

        private void Dispose(bool disposing)
        {
#if !NETCOREAPP
            if (this.frameworkSpecificFactory is DotNetStandardHttpClientFactory standardClientFactory)
            {
                standardClientFactory.Dispose();
            }
#endif
        }

#if NETCOREAPP
        /// <summary>
        /// .NET Core version of the HTTP client factory. This uses a single
        /// shared instance of SocketsHttpHandler with a pooled connection
        /// lifetime set and generates new HttpClient instances on every request.
        /// </summary>
        private class DotNetCoreHttpClientFactory : IHttpClientFactory
        {
            private readonly Lazy<HttpMessageHandler> lazyHandler;

            internal DotNetCoreHttpClientFactory(TimeSpan pooledConnectionLifetime, params DelegatingHandler[] delegatingHandlers)
            {
                this.lazyHandler = new Lazy<HttpMessageHandler>(
                    () => CreateLazyHandler(pooledConnectionLifetime, delegatingHandlers),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }

            public HttpClient CreateClient()
            {
                return new HttpClient(this.lazyHandler.Value, disposeHandler: false);
            }

            private static HttpMessageHandler CreateLazyHandler(TimeSpan pooledConnectionLifetime, params DelegatingHandler[] delegatingHandlers)
            {
                SocketsHttpHandler handler = new SocketsHttpHandler();
                handler.PooledConnectionLifetime = pooledConnectionLifetime;

                return CreateHandlerPipeline(handler, delegatingHandlers);
            }
        }
#else
        /// <summary>
        /// .NET Standard version of the HTTP client factory. This uses a single
        /// shared instance of HttpClient that does nothing when disposed. Every
        /// call to CreateClient returns this same instance.
        /// </summary>
        private class DotNetStandardHttpClientFactory : IHttpClientFactory
        {
            private readonly Lazy<NonDisposableHttpClient> lazyClient;

            internal DotNetStandardHttpClientFactory(params DelegatingHandler[] delegatingHandlers)
            {
                this.lazyClient = new Lazy<NonDisposableHttpClient>(
                    () => CreateLazyClient(delegatingHandlers),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }

            public HttpClient CreateClient()
            {
                return this.lazyClient.Value;
            }

            internal void Dispose()
            {
                if (this.lazyClient.IsValueCreated)
                {
                    this.lazyClient.Value.DoDispose();
                }
            }

            private static NonDisposableHttpClient CreateLazyClient(params DelegatingHandler[] delegatingHandlers)
            {
                HttpMessageHandler handler = CreateHandlerPipeline(new HttpClientHandler());

                return new NonDisposableHttpClient(handler);
            }
        }

        /// <summary>
        /// Non-disposable HTTP client wrapper that is used to stop clients from
        /// accidentally disposing the shared HTTP client instance. To actually
        /// dispose, call DoDispose().
        /// </summary>
        private class NonDisposableHttpClient : HttpClient
        {
            public NonDisposableHttpClient(HttpMessageHandler handler)
                : base(handler)
            {
            }

            protected override void Dispose(bool disposing)
            {
                // Don't do anything here because we don't want the singleton client instance
                // to be disposed.
            }

            internal void DoDispose()
            {
                base.Dispose(true);
            }
        }
#endif
    }
}
