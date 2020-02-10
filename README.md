This project is a standalone HTTP client factory for .NET Core and .NET Standard that follows the best practices recommended by Microsoft for the creation and disposal of `HttpClient` instances. 

## Motivation

I wrote this because the .NET `HttpClient` class is great but has some quirks that make it difficult to use properly (see [here](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests) for an official explanation):
1. For one thing, the class is disposable but when you dispose it, the underlying socket is not immediately released. This means that if you are making a bunch of outgoing connections and you create and dispose it every time you make a call, you can run into socket exhaustion. For more info, see the [this](https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/) excellent blog post. As such, the class is intended to be instantiated once and reused throughout the life of your application.
2. If you do actually use a singleton `HttpClient` instance, you can run into a second issue where DNS changes aren't respected. By default, the connection close timeout is set to infinite so the client never refreshes its DNS cache so if an entry changes, your client will not update until it is disposed.

There is a Microsoft official `IHttpClientFactory` in the [Microsoft.Extensions.Http](https://www.nuget.org/packages/Microsoft.Extensions.Http/) NuGet package. However, Microsoft has decided to follow a path with a lot of the .NET Core libraries where things are super tightly tied into ASP.NET Core and its dependency injection framework. If you don't want to use this, there's no way to use the official Microsoft package. ASP.NET Core is often great but I don't like being tied into so much magic and in particular, Microsoft's specific dependency injection framework. However, I found a GitHub [issue](https://github.com/dotnet/extensions/issues/1345#issuecomment-480548175) that described the best practices for using `HttpClient` in both .NET Core and .NET Standard and thought it would be useful to make a standalone package.

## Installation

You can find the latest release here: https://www.nuget.org/packages/Arnath.StandaloneHttpClientFactory

## Usage

To create and use a simple `HttpClient` instance, just instantiate the factory and use the client in a using statement. The `HttpClient` instance returned by the factory will handle the proper disposal behavior for .NET Standard. The `StandaloneHttpClientFactory` itself is disposable but should only be created and disposed once, at the beginning and end of your application's lifetime.'

```csharp
// This uses a default value of 15 minutes for the pooled connection lifetime
// that was recommended by the .NET foundation. 
StandaloneHttpClientFactory factory = new StandaloneHttpClientFactory();
using (HttpClient client = factory.CreateClient())
{
	// Do stuff with client.
}

// The factory implements IDisposable. You should only dispose it at the end
// of your application's lifetime.
factory.Dispose();
```

You can pass an `ILogger` instance to the factory to add a `LoggingHttpMessageHandler` to the handler chain that logs some basic information about requests. The logging functions in this class can be customized by overriding them.
```csharp
// Adds a LoggingHttpMessageHandler to the handler chain.
ILogger logger = new ConcreteLogger();
StandaloneHttpClientFactory factory = new StandaloneHttpClientFactory(logger);
```

You can also specify your own set of delegating handlers if desired. They must inherit from the `System.Net.Http.DelegatingHandler` class. For example, if you want to add a correlation ID header to every request, you could do something like this:
```csharp
public class CorrelationIdMessageHandler : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Headers.Add("CorrelationId", Guid.NewGuid().ToString());

        return base.SendAsync(request, cancellationToken);
    }
}

StandaloneHttpClientFactory factory = new StandaloneHttpClientFactory(new CorrelationIdMessageHandler());
```
You can look at `LoggingHttpMessageHandler` for an example delegating handler.