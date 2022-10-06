// Copyright (c) Vijay Prakash. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Arnath.StandaloneHttpClientFactory
{
    using System.Net.Http;

    /// <summary>
    /// Factory for <see cref="HttpClient"/> instances that follows the recommended
    /// best practices for creation and disposal. See <a href="https://github.com/arnath/standalonehttpclientfactory">
    /// https://github.com/arnath/standalonehttpclientfactory</a> for more details.
    /// </summary>
    public interface IHttpClientFactory
    {
        /// <summary>
        /// Creates an HTTP client instance.
        /// </summary>
        /// <returns></returns>
        HttpClient CreateClient();
    }
}
