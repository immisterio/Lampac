using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using httpcore = Lampac.Engine.CORE.HttpClient;

namespace Shared.Engine.CORE
{
    public static class FrendlyHttp
    {
        static ConcurrentDictionary<string, (DateTime lifetime, HttpClient http)> _clients = new ConcurrentDictionary<string, (DateTime, HttpClient)>();

        static FrendlyHttp()
        {
            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

                    try
                    {
                        foreach (var c in _clients)
                        {
                            if (DateTime.UtcNow > c.Value.lifetime)
                            {
                                c.Value.http.Dispose();
                                _clients.TryRemove(c.Key, out (DateTime, HttpClient) _);
                            }
                        }
                    }
                    catch { }
                }
            });
        }


        public static HttpClient CreateClient
        (
            in string name, 
            HttpClientHandler handler, 
            in string factoryClient, 
            Dictionary<string, string> headers = null, 
            in int timeoutSeconds = 8,
            in long MaxResponseContentBufferSize = 0,
            in string cookie = null,
            in string referer = null,
            in bool useDefaultHeaders = true, 
            Action<HttpClient> updateClient = null
        )
        {
            if (handler != null && handler.CookieContainer.Count > 0 || httpcore.httpClientFactory == null)
            {
                var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(client);
                return client;
            }

            if (handler == null || handler.UseProxy == false)
            {
                var factory = httpcore.httpClientFactory.CreateClient(factoryClient);
                factory.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(factory);
                return factory;
            }

            int port = 0;
            string ip = null, username = null, password = null;

            var webProxy = handler.Proxy as WebProxy;
            if (webProxy == null)
            {
                var factory = httpcore.httpClientFactory.CreateClient(factoryClient);
                factory.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(factory);
                return factory;
            }

            ip = webProxy.Address?.Host;
            port = webProxy.Address?.Port ?? 0;

            if (webProxy.Credentials is NetworkCredential credentials)
            {
                username = credentials.UserName;
                password = credentials.Password;
            }

            string key = $"{name}:{ip}:{port}:{username}:{password}:{timeoutSeconds}:{MaxResponseContentBufferSize}:{cookie}:{referer}:{useDefaultHeaders}";
            if (headers != null)
                key += ":" + string.Join(";", headers.Select(h => $"{h.Key}={h.Value}"));

            lock (_clients)
            {
                if (_clients.TryGetValue(key, out (DateTime, HttpClient http) value))
                    return value.http;

                var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(client);

                _clients.TryAdd(key, (DateTime.UtcNow.AddMinutes(10), client));

                return client;
            }
        }
    }
}
