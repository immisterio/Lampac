using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models.AppConf;
using Shared.Models.Base;
using Shared.Models.JacRed;
using Shared.Models.Online.Settings;
using Shared.Models.Proxy;
using Shared.Models.Templates;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Shared.Models.Events
{
    public record EventLoadKit(BaseSettings defaultinit, BaseSettings init, JObject userconf, RequestModel requestInfo, HybridCache hybridCache);

    public record EventMiddleware(RequestModel requestInfo, HttpRequest request, HttpContext httpContext, HybridCache hybridCache, IMemoryCache memoryCache);

    public record EventBadInitialization(BaseSettings init, bool? rch, RequestModel requestInfo, string host, HttpRequest request, HttpContext httpContext, HybridCache hybridCache);

    public record EventAppReplace(string source, string token, string arg, string host, RequestModel requestInfo, HttpRequest request, HybridCache hybridCache);

    public record EventExternalids(string id, string imdb_id, string kinopoisk_id, int serial);

    public record EventHybridCache(string key, string value, DateTimeOffset ex);

    public record EventRedApi(TorrentDetails torrent);

    public record EventPidTor(PidTorSettings init, RequestModel requestInfo, HybridCache hybridCache);

    public record EventHostStreamProxy(BaseSettings conf, string uri, List<HeadersModel> headers, WebProxy proxy, RequestModel requestInfo, HttpContext httpContext, HybridCache hybridCache);

    public record EventHostImgProxy(RequestModel requestInfo, HttpContext httpContext, string uri, int height, List<HeadersModel> headers, string plugin);

    public record EventMyLocalIp(RequestModel requestInfo, HttpRequest request, HttpContext httpContext, HybridCache hybridCache);

    public record EventControllerHttpHeaders(string site, Dictionary<string, string> headers, RequestModel requestInfo, HttpRequest request, HttpContext httpContext);

    public record EventStreamQuality(string link, string quality, bool prepend);

    public record EventStreamQualityFirts(IReadOnlyList<StreamQualityDto> data);

    public record EventHttpHandler(string url, HttpClientHandler handler, WebProxy proxy, CookieContainer cookieContainer, IMemoryCache memoryCache);

    public record EventHttpHeaders(string url, HttpRequestMessage client, string cookie, string referer, List<HeadersModel> headers, bool useDefaultHeaders, IMemoryCache memoryCache);

    public record EventHttpResponse(string url, HttpContent data, HttpClient client, string result, HttpResponseMessage response, IMemoryCache memoryCache);

    public record EventProxyApiCreateHttpRequest(string plugin, HttpRequest request, List<HeadersModel> headers, Uri uri, bool ismedia, HttpRequestMessage requestMessage);

    public record EventProxyApiCacheStream(HttpContext httpContext, ProxyLinkModel decryptLink);

    public record EventProxyImgMd5key(HttpContext httpContext, RequestModel requestInfo, ProxyLinkModel decryptLink, string href, int width, int height);

    public record EventStaticache(HttpContext httpContext, RequestModel requestInfo);

    public record EventTranscoding(Collection<string> args, int? startNumber, TranscodingStartContext context);

    public record EventRchRegistry(string connectionId, string ip, string host, RchClientInfo info, NwsConnection connection);

    public record EventRchDisconnected(string connectionId);

    public record EventNwsConnected(string connectionId, RequestModel requestInfo, NwsConnection connection, CancellationToken token);

    public record EventNwsDisconnected(string connectionId);

    public record EventNwsMessage(string connectionId, StringBuilder payload, string method, JsonElement args);

    public record EventCorseuRequest(CorseuRequest request);

    public record EventCorseuHttpRequest(string method, string url, HttpRequestMessage request);

    public record EventCorseuPlaywrightRequest(string method, string url, APIRequestNewContextOptions contextOptions, APIRequestContextOptions requestOptions);
}
