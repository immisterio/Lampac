using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using Shared.Models.Events;
using Shared.Models.JacRed;
using Shared.Models.Templates;

namespace Shared
{
    public class EventListener
    {
        public static Action<EventLoadKit> LoadKitInit;

        public static Action<EventLoadKit> LoadKit;

        public static Func<EventProxyApiCreateHttpRequest, Task> ProxyApiCreateHttpRequest;

        public static Func<EventProxyApiCacheStream, (string uriKey, string contentType)> ProxyApiCacheStream;

        public static Func<EventProxyImgMd5key, string> ProxyImgMd5key;

        public static Func<EventStaticache, bool> Staticache;

        public static Func<EventBadInitialization, Task<ActionResult>> BadInitialization;

        public static Func<EventHostStreamProxy, string> HostStreamProxy;

        public static Func<EventHostImgProxy, string> HostImgProxy;

        public static Func<EventMyLocalIp, Task<string>> MyLocalIp;

        public static Func<EventControllerHttpHeaders, Dictionary<string, string>> HttpHeaders;

        public static Func<bool, EventMiddleware, Task<bool>> Middleware;

        public static Func<string, EventAppReplace, string> AppReplace;

        public static Action<TorrentDetails> RedApiAddTorrents;

        public static Action<EventTranscoding> TranscodingCreateProcess;

        public static Action<EventHttpHandler> HttpHandler;

        public static Action<EventHttpHeaders> HttpRequestHeaders;

        public static Func<EventHttpResponse, Task> HttpResponse;

        public static Action<EventCorseuRequest> CorseuRequest;

        public static Action<EventCorseuHttpRequest> CorseuHttpRequest;

        public static Action<EventCorseuPlaywrightRequest> CorseuPlaywrightRequest;

        public static Func<EventExternalids, (string imdb_id, string kinopoisk_id)> Externalids;

        public static Func<EventStreamQuality, (bool? next, string link)> StreamQuality;

        public static Func<EventStreamQualityFirts, StreamQualityDto?> StreamQualityFirts;

        public static Func<string, EventHybridCache, (DateTimeOffset ex, string value)> HybridCache;


        public static Action<EventRchRegistry> RchRegistry;

        public static Action<string> RchDisconnected;


        public static Action<EventNwsConnected> NwsConnected;

        public static Action<EventNwsMessage> NwsMessage;

        public static Action<string> NwsDisconnected;
    }
}
