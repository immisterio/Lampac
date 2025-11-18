using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using Shared.Models.Events;
using Shared.Models.JacRed;

namespace Shared
{
    public class EventListener
    {
        public static Action<EventLoadKit> LoadKit;

        public static Func<EventProxyApiCreateHttpRequest, Task> ProxyApiCreateHttpRequest;

        public static Func<EventBadInitialization, Task<ActionResult>> BadInitialization;

        public static Func<EventHostStreamProxy, string> HostStreamProxy;

        public static Func<EventMyLocalIp, Task<string>> MyLocalIp;

        public static Func<EventControllerHttpHeaders, List<HeadersModel>> HttpHeaders;

        public static Func<bool, EventMiddleware, Task<bool>> Middleware;

        public static Func<string, EventAppReplace, string> AppReplace;

        public static Action<TorrentDetails> RedApiAddTorrents;

        public static Action<EventTranscoding> TranscodingCreateProcess;

        public static Action<EventHttpHandler> HttpHandler;

        public static Action<EventHttpHeaders> HttpRequestHeaders;

        public static Func<EventHttpResponse, Task> HttpResponse;

        public static Func<EventExternalids, (string imdb_id, string kinopoisk_id)> Externalids;

        public static Func<EventStreamQuality, (bool? next, string link)> StreamQuality;

        public static Func<EventStreamQualityFirts, (string link, string quality)?> StreamQualityFirts;

        public static Func<string, EventHybridCache, (DateTimeOffset ex, string value)> HybridCache;


        public static Action<EventRchRegistry> RchRegistry;

        public static Action<string> RchDisconnected;

        public static Action<EventNwsConnected> NwsConnected;

        public static Action<string> NwsDisconnected;
    }
}
