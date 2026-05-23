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

    static readonly ConcurrentDictionary<ProxyClientKey, HttpClientModel> _clients = new();

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
    public static HttpClient MessageClient(string factoryClient, HttpClientHandler handler, out bool disposeHttpClient, long MaxResponseContentBufferSize = -1, HttpClient httpClient = null, bool allowAutoRedirect = true, bool findNoRedirectClient = true)
    {
        // 10MB
        long maxBufferSize = 10_000_000;
        if (MaxResponseContentBufferSize > 0)
            maxBufferSize = MaxResponseContentBufferSize;

        if (handler?.CookieContainer?.Count > 0 || Http.httpClientFactory == null)
        {
            disposeHttpClient = true;

            if (handler == null)
            {
                handler = new HttpClientHandler()
                {
                    AllowAutoRedirect = allowAutoRedirect,
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    ServerCertificateCustomValidationCallback = Http.AlwaysAllowCertificate
                };
            }

            return new HttpClient(handler)
            {
                MaxResponseContentBufferSize = maxBufferSize
            };
        }
        else
        {
            disposeHttpClient = false;
            var webProxy = handler?.Proxy as WebProxy;

            if (webProxy == null)
            {
                if (httpClient != null)
                    return httpClient;

                string targetClient = factoryClient;

                if (findNoRedirectClient)
                {
                    if ((handler != null && handler.AllowAutoRedirect == false) || allowAutoRedirect == false)
                    {
                        targetClient = factoryClient switch
                        {
                            "base" => "baseNoRedirect",
                            "http2" => "http2NoRedirect",
                            "http3" => "http3NoRedirect",
                            _ => factoryClient
                        };
                    }
                }

                var factory = Http.httpClientFactory.CreateClient(targetClient);

                if (maxBufferSize > factory.MaxResponseContentBufferSize)
                    factory.MaxResponseContentBufferSize = maxBufferSize;

                return factory;
            }
            else
            {
                if (handler != null)
                {
                    handler = new HttpClientHandler()
                    {
                        AllowAutoRedirect = allowAutoRedirect,
                        AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        ServerCertificateCustomValidationCallback = Http.AlwaysAllowCertificate
                    };
                }

                var address = webProxy.Address;
                var credentials = webProxy.Credentials as NetworkCredential;

                var key = new ProxyClientKey(
                    address?.Host,
                    address?.Port ?? 0,
                    credentials?.UserName,
                    credentials?.Password,
                    maxBufferSize,
                    handler?.AllowAutoRedirect == true
                );

                return _clients.GetOrAdd(
                    key,
                    static (key, handler) => new HttpClientModel(
                        DateTime.UtcNow.AddMinutes(30),
                        new HttpClient(handler)
                        {
                            MaxResponseContentBufferSize = key.MaxBufferSize
                        }),
                    handler
                ).http;
            }
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
