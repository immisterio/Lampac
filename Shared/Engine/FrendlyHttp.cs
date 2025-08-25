using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace Shared.Engine
{
    public static class FrendlyHttp
    {
        static ConcurrentDictionary<string, (DateTime lifetime, System.Net.Http.HttpClient http)> _clients = new ConcurrentDictionary<string, (DateTime, System.Net.Http.HttpClient)>();

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
                                _clients.TryRemove(c.Key, out (DateTime, System.Net.Http.HttpClient) _);
                            }
                        }
                    }
                    catch { }
                }
            });
        }


        public static System.Net.Http.HttpClient CreateClient
        (
            string name,
            System.Net.Http.HttpClientHandler handler, 
            string factoryClient, 
            Dictionary<string, string> headers = null, 
            int timeoutSeconds = 8,
            long MaxResponseContentBufferSize = 0,
            string cookie = null,
            string referer = null,
            bool useDefaultHeaders = true, 
            Action<System.Net.Http.HttpClient> updateClient = null
        )
        {
            if (handler != null && handler.CookieContainer.Count > 0 || Http.httpClientFactory == null)
            {
                var client = new System.Net.Http.HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(client);
                return client;
            }

            if (handler == null || handler.UseProxy == false)
            {
                var factory = Http.httpClientFactory.CreateClient(factoryClient);
                factory.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(factory);
                return factory;
            }

            int port = 0;
            string ip = null, username = null, password = null;

            var webProxy = handler.Proxy as WebProxy;
            if (webProxy == null)
            {
                var factory = Http.httpClientFactory.CreateClient(factoryClient);
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
                if (_clients.TryGetValue(key, out (DateTime, System.Net.Http.HttpClient http) value))
                    return value.http;

                var client = new System.Net.Http.HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                updateClient?.Invoke(client);

                _clients.TryAdd(key, (DateTime.UtcNow.AddMinutes(10), client));

                return client;
            }
        }
    }
}
