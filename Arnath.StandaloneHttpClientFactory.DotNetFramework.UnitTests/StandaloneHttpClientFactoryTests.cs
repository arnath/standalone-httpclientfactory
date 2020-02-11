namespace Arnath.StandaloneHttpClientFactory.DotNetFramework.UnitTests
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using FakeItEasy;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class StandaloneHttpClientFactoryTests
    {
        [Fact]
        public void CreateClientUsesDefaultsWhenFactoryUsesDefaults()
        {
            using (StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory())
            {
                using (HttpClient client = httpClientFactory.CreateClient())
                {
                    HttpMessageHandler handler = GetHandler(client);

                    Assert.IsType<ServicePointHttpMessageHandler>(GetHandler(client));
                    Assert.Equal(StandaloneHttpClientFactory.DefaultConnectionLifetime, ((ServicePointHttpMessageHandler)handler).ConnectionLeaseTimeout);
                }
            }
        }

        [Fact]
        public void CreateClientUsesProvidedConnectionLifetime()
        {
            TimeSpan connectionLifetime = TimeSpan.FromMinutes(1);
            using (StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory(connectionLifetime))
            {
                using (HttpClient client = httpClientFactory.CreateClient())
                {
                    HttpMessageHandler handler = GetHandler(client);

                    Assert.IsType<ServicePointHttpMessageHandler>(GetHandler(client));
                    Assert.Equal(connectionLifetime, ((ServicePointHttpMessageHandler)handler).ConnectionLeaseTimeout);
                }
            }
        }

        [Fact]
        public void CreateClientUsesLoggerToCreateLoggingHandler()
        {
            using (StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory(NullLogger.Instance))
            {
                using (HttpClient client = httpClientFactory.CreateClient())
                {
                    HttpMessageHandler handler = GetHandler(client);

                    Assert.IsType<LoggingHttpMessageHandler>(GetHandler(client));
                    LoggingHttpMessageHandler loggingHandler = (LoggingHttpMessageHandler)handler;
                    Assert.IsType<ServicePointHttpMessageHandler>(loggingHandler.InnerHandler);
                }
            }
        }

        [Fact]
        public void CreateClientCreatesHandlerPipeline()
        {
            DelegatingHandler foo = A.Fake<DelegatingHandler>(options => options.CallsBaseMethods());
            DelegatingHandler bar = A.Fake<DelegatingHandler>(options => options.CallsBaseMethods());

            using (StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory(foo, bar))
            {
                using (HttpClient client = httpClientFactory.CreateClient())
                {
                    HttpMessageHandler handler = GetHandler(client);

                    Assert.Same(foo, handler);
                    DelegatingHandler delegatingHandler = (DelegatingHandler)handler;
                    Assert.Same(bar, delegatingHandler.InnerHandler);
                    delegatingHandler = (DelegatingHandler)delegatingHandler.InnerHandler;
                    Assert.IsType<ServicePointHttpMessageHandler>(delegatingHandler.InnerHandler);
                    delegatingHandler = (DelegatingHandler)delegatingHandler.InnerHandler;
                    Assert.IsType<HttpClientHandler>(delegatingHandler.InnerHandler);
                }
            }
        }

        [Fact]
        public void CreateClientCreatesSameClient()
        {
            using (StandaloneHttpClientFactory httpClientFactory = new StandaloneHttpClientFactory())
            {
                using (HttpClient client1 = httpClientFactory.CreateClient())
                {
                    using (HttpClient client2 = httpClientFactory.CreateClient())
                    {
                        HttpMessageHandler handler1 = GetHandler(client1);
                        HttpMessageHandler handler2 = GetHandler(client2);

                        Assert.Same(client1, client2);
                        Assert.Same(handler1, handler2);
                    }
                }
            }
        }

        private HttpMessageHandler GetHandler(HttpClient client)
        {
            FieldInfo handlerField =
                typeof(HttpClient).BaseType.GetField(
                    "handler",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            object o = handlerField.GetValue(client);

            return o as HttpMessageHandler;
        }
    }
}
