// Copyright (c) Vijay Prakash. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Arnath.StandaloneHttpClientFactory
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Factory for <see cref="HttpClient"/> instances that follows the recommended
    /// best practices for creation and disposal. See <a href="https://github.com/arnath/standalonehttpclientfactory" />
    /// for more details.
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
        /// <param name="connectionLifetime">The lifetime of connections to each host.</param>
        /// <param name="logger">The logger to which to log info about requests.</param>
        public StandaloneHttpClientFactory(TimeSpan connectionLifetime, ILogger logger)
            : this(connectionLifetime, new LoggingHttpMessageHandler(logger))
        {
            
        }

        /// <summary>
        /// Creates an instance of the StandaloneHttpClientFactory class with the
        /// specified value for pooled connection lifetime and the specified
        /// set of delegating handlers.
        /// </summary>
        /// <param name="connectionLifetime">The lifetime of connections to each host.</param>
        /// <param name="delegatingHandlers">Array of DelegatingHandler instances that can be
        /// used for logging, etc. See LoggingHttpMessageHandler for an example.</param>
        public StandaloneHttpClientFactory(TimeSpan connectionLifetime, params DelegatingHandler[] delegatingHandlers)
        {
#if NETCOREAPP
            this.frameworkSpecificFactory = new DotNetCoreHttpClientFactory(connectionLifetime, delegatingHandlers);
#else
            this.frameworkSpecificFactory = new DotNetStandardHttpClientFactory(connectionLifetime, delegatingHandlers);
#endif
        }

        /// <summary>
        /// Creates an HTTP client instance with the factory's pooled connection lifetime
        /// and delegating handlers.
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
            if (disposing)
            {
#if !NETCOREAPP
                if (this.frameworkSpecificFactory is DotNetStandardHttpClientFactory standardClientFactory)
                {
                    standardClientFactory.Dispose();
                }
#endif
            }
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

            internal DotNetStandardHttpClientFactory(TimeSpan connectionLifetime, params DelegatingHandler[] delegatingHandlers)
            {
                this.lazyClient = new Lazy<NonDisposableHttpClient>(
                    () => CreateLazyClient(connectionLifetime, delegatingHandlers),
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

            private static NonDisposableHttpClient CreateLazyClient(TimeSpan connectionLifetime, params DelegatingHandler[] delegatingHandlers)
            {
                ServicePointHttpMessageHandler handler =
                    new ServicePointHttpMessageHandler(
                        connectionLifetime,
                        new HttpClientHandler());

                return new NonDisposableHttpClient(
                    CreateHandlerPipeline(
                        handler,
                        delegatingHandlers));
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
