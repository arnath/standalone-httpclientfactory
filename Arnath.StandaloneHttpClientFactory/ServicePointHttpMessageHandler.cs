namespace Arnath.StandaloneHttpClientFactory
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ServicePointHttpMessageHandler : DelegatingHandler
    {
        public ServicePointHttpMessageHandler(TimeSpan connectionLeaseTimeout)
            : this(connectionLeaseTimeout, null)
        {
        }

        public ServicePointHttpMessageHandler(TimeSpan connectionTimeout, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.ConnectionLeaseTimeout = connectionTimeout;
        }

        public TimeSpan ConnectionLeaseTimeout { get; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ServicePoint servicePoint = ServicePointManager.FindServicePoint(request.RequestUri);
            servicePoint.ConnectionLeaseTimeout = (int)this.ConnectionLeaseTimeout.TotalMilliseconds;

            return base.SendAsync(request, cancellationToken);
        }
    }
}
