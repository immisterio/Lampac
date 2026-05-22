using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Shared.Services;

public static class FriendlyHttp
{
    #region static
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(FriendlyHttp));

    readonly record struct HttpClientModel(DateTime lifetime, HttpClient http);
    readonly record struct ProxyClientKey(string Host, int Port, string UserName, string Password, long MaxBufferSize, bool AllowAutoRedirect);

    static ConcurrentDictionary<ProxyClientKey, HttpClientModel> _clients = new();

    static readonly RemoteCertificateValidationCallback AcceptAnyCertificate = AcceptAnyCertificateHandler;
    static bool AcceptAnyCertificateHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        => true;

    static FriendlyHttp()
    {
        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                try
                {
                    var now = DateTime.UtcNow;

                    foreach (var client in _clients)
                    {
                        try
                        {
                            if (now > client.Value.lifetime && _clients.TryRemove(client.Key, out var _c))
                                _ = DisposeLaterAsync(_c.http);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "CatchId={CatchId}", "id_p6bez4hd");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "CatchId={CatchId}", "id_1bbm5ot5");
                }
            }
        });
    }

    static async Task DisposeLaterAsync(HttpClient http)
    {
        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        http.Dispose();
    }
    #endregion

    #region MessageClient
    public static HttpClient MessageClient(string factoryClient, HttpClientHandler handler, out bool disposeHttpClient, long MaxResponseContentBufferSize = -1, HttpClient httpClient = null)
    {
        // 10MB
        long maxBufferSize = 10_000_000;
        if (MaxResponseContentBufferSize > 0)
            maxBufferSize = MaxResponseContentBufferSize;

        if (handler?.CookieContainer?.Count > 0 || Http.httpClientFactory == null)
        {
            disposeHttpClient = true;
            return new HttpClient(handler)
            {
                MaxResponseContentBufferSize = maxBufferSize
            };
        }

        disposeHttpClient = false;
        var webProxy = handler?.Proxy as WebProxy;

        if (webProxy == null)
        {
            if (httpClient != null)
                return httpClient;

            string targetClient = handler?.AllowAutoRedirect == false
                ? factoryClient switch
                {
                    "base" => "baseNoRedirect",
                    "http2" => "http2NoRedirect",
                    _ => factoryClient
                }
                : factoryClient;

            var factory = Http.httpClientFactory.CreateClient(targetClient);

            if (maxBufferSize > factory.MaxResponseContentBufferSize)
                factory.MaxResponseContentBufferSize = maxBufferSize;

            return factory;
        }
        else
        {
            int port = 0;
            string ip = null, username = null, password = null;

            ip = webProxy.Address?.Host;
            port = webProxy.Address?.Port ?? 0;

            if (webProxy.Credentials is NetworkCredential credentials)
            {
                username = credentials.UserName;
                password = credentials.Password;
            }

            var key = new ProxyClientKey(
                ip,
                port,
                username,
                password,
                maxBufferSize,
                handler?.AllowAutoRedirect == true
            );

            return _clients.GetOrAdd(key, static (key, state)
                => new HttpClientModel(DateTime.UtcNow.AddMinutes(30), new HttpClient(state.Handler)
                {
                    MaxResponseContentBufferSize = key.MaxBufferSize
                }), new { Handler = handler }
            ).http;
        }
    }
    #endregion


    #region CreateHttp2Client
    public static HttpClient CreateHttp2Client(bool useCookies = true)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            CookieContainer = useCookies ? new CookieContainer() : null,
            UseCookies = useCookies
        };

        handler.SslOptions.RemoteCertificateValidationCallback = AcceptAnyCertificate;

        return new HttpClient(handler)
        {
            MaxResponseContentBufferSize = 10_000_000,
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };
    }
    #endregion

    #region CreateHttpClient
    public static HttpClient CreateHttpClient(bool useCookies = true)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            CookieContainer = useCookies ? new CookieContainer() : null,
            UseCookies = useCookies
        };

        handler.SslOptions.RemoteCertificateValidationCallback = AcceptAnyCertificate;

        return new HttpClient(handler)
        {
            MaxResponseContentBufferSize = 10_000_000,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };
    }
    #endregion
}
