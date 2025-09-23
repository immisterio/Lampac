using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.JacRed;
using Shared.Models.Online.Settings;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

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


    public record EventHostStreamProxy(BaseSettings conf, string uri, List<HeadersModel> headers, WebProxy proxy, bool force_streamproxy, bool streamproxy, RequestModel requestInfo, HttpContext httpContext, HybridCache hybridCache);


    public record EventHttpHandler(string url, HttpClientHandler handler, WebProxy proxy, CookieContainer cookieContainer, IMemoryCache memoryCache);

    public record EventHttpHeaders(string url, HttpRequestMessage client, string cookie, string referer, List<HeadersModel> headers, bool useDefaultHeaders, IMemoryCache memoryCache);

    public record EventHttpResponse(string url, HttpContent data, HttpClient client, string result, HttpResponseMessage response, IMemoryCache memoryCache);
}
