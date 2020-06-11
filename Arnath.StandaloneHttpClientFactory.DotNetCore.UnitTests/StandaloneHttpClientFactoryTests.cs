// Copyright (c) Vijay Prakash. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Arnath.StandaloneHttpClientFactory.DotNetCore.UnitTests
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using FakeItEasy;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class StandaloneHttpClientFactoryTests
    {
        [Fact]
        public void CreateClientUsesDefaultsWhenFactoryUsesDefaults()
        {
            using StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory();
            using HttpClient client = httpClientFactory.CreateClient();
            HttpMessageHandler handler = GetHandler(client);

            Assert.IsType<SocketsHttpHandler>(handler);
            Assert.Equal(StandaloneHttpClientFactory.DefaultConnectionLifetime, ((SocketsHttpHandler)handler).PooledConnectionLifetime);
        }

        [Fact]
        public void CreateClientUsesProvidedConnectionLifetime()
        {
            TimeSpan connectionLifetime = TimeSpan.FromMinutes(1);
            using StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory(connectionLifetime);
            using HttpClient client = httpClientFactory.CreateClient();
            HttpMessageHandler handler = GetHandler(client);

            Assert.IsType<SocketsHttpHandler>(handler);
            Assert.Equal(connectionLifetime, ((SocketsHttpHandler)handler).PooledConnectionLifetime);
        }

        [Fact]
        public void CreateClientUsesLoggerToCreateLoggingHandler()
        {
            using StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory(NullLogger.Instance);
            using HttpClient client = httpClientFactory.CreateClient();
            HttpMessageHandler handler = GetHandler(client);

            Assert.IsType<LoggingHttpMessageHandler>(handler);
            LoggingHttpMessageHandler loggingHandler = (LoggingHttpMessageHandler)handler;
            Assert.IsType<SocketsHttpHandler>(loggingHandler.InnerHandler);
        }

        [Fact]
        public void CreateClientCreatesHandlerPipeline()
        {
            DelegatingHandler foo = A.Fake<DelegatingHandler>(options => options.CallsBaseMethods());
            DelegatingHandler bar = A.Fake<DelegatingHandler>(options => options.CallsBaseMethods());

            using StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory(foo, bar);
            using HttpClient client = httpClientFactory.CreateClient();
            HttpMessageHandler handler = GetHandler(client);

            Assert.Same(foo, handler);
            DelegatingHandler delegatingHandler = (DelegatingHandler)handler;
            Assert.Same(bar, delegatingHandler.InnerHandler);
            delegatingHandler = (DelegatingHandler)delegatingHandler.InnerHandler;
            Assert.IsType<SocketsHttpHandler>(delegatingHandler.InnerHandler);
        }

        [Fact]
        public void CreateClientCreatesNewClientWithSameHandler()
        {
            using StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory();
            using HttpClient client1 = httpClientFactory.CreateClient();
            using HttpClient client2 = httpClientFactory.CreateClient();
            HttpMessageHandler handler1 = GetHandler(client1);
            HttpMessageHandler handler2 = GetHandler(client2);

            Assert.NotSame(client1, client2);
            Assert.Same(handler1, handler2);
        }

        private HttpMessageHandler GetHandler(HttpClient client)
        {
            FieldInfo handlerField = 
                typeof(HttpClient).BaseType.GetField(
                    "_handler",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            object o = handlerField.GetValue(client);

            return o as HttpMessageHandler;
        }
    }
}
