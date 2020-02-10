namespace Arnath.StandaloneHttpClientFactory
{
    using System.Net.Http;

    interface IHttpClientFactory
    {
        HttpClient CreateClient();
    }
}
