using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Shared.Engine
{
    public static class FrendlyHttp
    {
        #region static
        static ConcurrentDictionary<string, (DateTime lifetime, HttpClient http)> _clients = new ConcurrentDictionary<string, (DateTime, HttpClient)>();

        static FrendlyHttp()
        {
            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));

                    try
                    {
                        foreach (var c in _clients.Where(c => DateTime.UtcNow > c.Value.lifetime).ToArray())
                        {
                            try
                            {
                                if (_clients.TryRemove(c.Key, out var _c))
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(20));
                                    _c.http.Dispose();
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            });
        }
        #endregion

        #region HttpMessageClient
        public static HttpClient HttpMessageClient
        (
            string factoryClient,
            HttpClientHandler handler,
            long MaxResponseContentBufferSize = -1
        )
        {
            // 10MB
            long maxBufferSize = 10_000_000;
            if (MaxResponseContentBufferSize > 0)
                maxBufferSize = MaxResponseContentBufferSize;

            if ((handler != null && handler.CookieContainer.Count > 0) || Http.httpClientFactory == null)
            {
                var client = new HttpClient(handler);
                client.MaxResponseContentBufferSize = maxBufferSize;
                return client;
            }

            var webProxy = handler?.Proxy != null ? handler.Proxy as WebProxy : null;

            if (webProxy == null)
            {
                if (handler != null && handler.AllowAutoRedirect == false)
                {
                    if (factoryClient is "base" or "http2")
                        factoryClient += "NoRedirect";
                }

                var factory = Http.httpClientFactory.CreateClient(factoryClient);

                if (maxBufferSize > factory.MaxResponseContentBufferSize)
                    factory.MaxResponseContentBufferSize = maxBufferSize;

                return factory;
            }

            int port = 0;
            string ip = null, username = null, password = null;

            ip = webProxy.Address?.Host;
            port = webProxy.Address?.Port ?? 0;

            if (webProxy.Credentials is NetworkCredential credentials)
            {
                username = credentials.UserName;
                password = credentials.Password;
            }

            return _clients.GetOrAdd($"{ip}:{port}:{username}:{password}:{MaxResponseContentBufferSize}:{handler?.AllowAutoRedirect}", k => 
            {
                var client = new HttpClient(handler);
                client.MaxResponseContentBufferSize = maxBufferSize;

                return (DateTime.UtcNow.AddMinutes(30), client);

            }).http;
        }
        #endregion
    }
}
