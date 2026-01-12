using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.SISI.Base;
using Shared.Models.SISI.OnResult;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace Shared
{
    public class BaseSisiController : BaseSisiController<SisiSettings>
    {
        public BaseSisiController(SisiSettings init) : base(init) { }
    }

    public class BaseSisiController<T> : BaseController where T : BaseSettings, ICloneable
    {
        static readonly List<MenuItem> emptyMenu = new();

        #region jsonOptions
        static readonly JsonWriterOptions jsonWriterOptions = new JsonWriterOptions 
        { 
            Indented = false, 
            SkipValidation = true 
        };
        #endregion

        #region RchClient
        RchClient _rch = null;

        public RchClient rch
        {
            get
            {
                if (_rch == null && AppInit.conf.rch.enable)
                {
                    if (init.rhub || init.rchstreamproxy != null || AppInit.conf.rch.requiredConnected)
                        _rch = new RchClient(HttpContext, host, init, requestInfo);
                }

                return _rch;
            }
        }
        #endregion

        #region HttpHydra
        HttpHydra _httpHydra = null;

        public HttpHydra httpHydra
        {
            get
            {
                if (_httpHydra == null)
                    _httpHydra = new HttpHydra(init, httpHeaders(init), rch, proxy);

                return _httpHydra;
            }
        }
        #endregion

        #region proxyManager
        ProxyManager _proxyManager = null;

        public ProxyManager proxyManager
        {
            get
            {
                if (_proxyManager == null && (init.useproxy || init.useproxystream))
                    _proxyManager = new ProxyManager(init, rch);

                return _proxyManager;
            }
        }
        #endregion

        #region proxy
        WebProxy _proxy = null;

        public WebProxy proxy
        {
            get
            {
                if (_proxy == null)
                    _proxy = proxyManager?.Get();

                return _proxy;
            }
        }
        #endregion

        #region proxy_data
        public (string ip, string username, string password) _proxy_data = default;

        public (string ip, string username, string password) proxy_data
        {
            get
            {
                if (_proxy_data == default && proxyManager != null)
                    _proxy_data = proxyManager.BaseGet().data;

                return _proxy_data;
            }
        }
        #endregion

        public T init { get; private set; }

        BaseSettings baseconf { get; set; }

        public Func<JObject, T, T, T> loadKitInitialization { get; set; }

        public Action requestInitialization { get; set; }

        public Func<ValueTask> requestInitializationAsync { get; set; }

        static List<RootModule> modulesInitialization;


        public BaseSisiController(T init)
        {
            if (init != default)
                Initialization(init);
        }

        public void Initialization(T init)
        {
            if (baseconf != default)
                return;

            baseconf = init;
            this.init = (T)init.Clone();
            this.init.IsCloneable = true;
        }


        #region IsRequestBlocked
        public ValueTask<bool> IsRequestBlocked(T init, bool? rch = null, int? rch_keepalive = null, bool rch_check = true)
        {
            Initialization(init);
            return IsRequestBlocked(rch, rch_keepalive, rch_check);
        }

        async public ValueTask<bool> IsRequestBlocked(bool? rch = null, int? rch_keepalive = null, bool rch_check = true)
        {
            if (IsLoadKit(init))
            {
                if (loadKitInitialization != null)
                    init = await loadKit(init, loadKitInitialization);
                else
                    init = await loadKit(init);
            }

            requestInitialization?.Invoke();

            if (requestInitializationAsync != null)
                await requestInitializationAsync.Invoke();

            #region module initialization
            if (modulesInitialization == null && AppInit.modules != null)
                modulesInitialization = AppInit.modules.Where(i => i.initialization != null).ToList();

            if (modulesInitialization != null && modulesInitialization.Count > 0)
            {
                var args = new InitializationModel(init, rch);

                foreach (RootModule mod in modulesInitialization)
                {
                    try
                    {
                        if (mod.assembly.GetType(mod.NamespacePath(mod.initialization)) is Type t)
                        {
                            if (t.GetMethod("Invoke") is MethodInfo m2)
                            {
                                badInitMsg = (ActionResult)m2.Invoke(null, [HttpContext, memoryCache, requestInfo, host, args]);
                                if (badInitMsg != null)
                                    return true;
                            }

                            if (t.GetMethod("InvokeAsync") is MethodInfo m)
                            {
                                badInitMsg = await (Task<ActionResult>)m.Invoke(null, [HttpContext, memoryCache, requestInfo, host, args]);
                                if (badInitMsg != null)
                                    return true;
                            }
                        }
                    }
                    catch { }
                }
            }
            #endregion

            if (InvkEvent.IsBadInitialization())
            {
                badInitMsg = await InvkEvent.BadInitialization(new EventBadInitialization(init, rch, requestInfo, host, HttpContext.Request, HttpContext, hybridCache));
                if (badInitMsg != null)
                    return true;
            }

            if (!init.enable || init.rip)
            {
                badInitMsg = OnError("disable", rcache: false, statusCode: 403);
                return true;
            }

            if (NoAccessGroup(init, out string error_msg))
            {
                badInitMsg = OnError(error_msg, rcache: false, statusCode: 401);
                return true;
            }

            if (IsOverridehost(init))
            {
                var overridehost = await InvokeOverridehost(init);
                if (overridehost != null)
                {
                    badInitMsg = overridehost;
                    return true;
                }
            }

            if (rch != null)
            {
                if ((bool)rch)
                {
                    if (init.rhub && !AppInit.conf.rch.enable)
                    {
                        badInitMsg = OnError(RchClient.ErrorMsg);
                        return true;
                    }

                    if (rch_check && this.rch != null)
                    {
                        if (this.rch.IsNotConnected())
                        {
                            badInitMsg = ContentTo(this.rch.connectionMsg);
                            return true;
                        }

                        if (this.rch.IsNotSupport(out string rch_error))
                        {
                            badInitMsg = OnError(rch_error);
                            return true;
                        }
                    }
                }
                else
                {
                    if (init.rhub)
                    {
                        badInitMsg = OnError(RchClient.ErrorMsg);
                        return true;
                    }
                }
            }

            if (rch_check && this.rch != null && this.rch.IsRequiredConnected())
            {
                badInitMsg = ContentTo(this.rch.connectionMsg);
                return true;
            }

            return IsCacheError(init, this.rch);
        }
        #endregion

        #region OnError
        public ActionResult OnError(int statusCode = 503, bool refresh_proxy = false)
            => OnError(string.Empty, statusCode, refresh_proxy);

        public ActionResult OnError(string msg, int statusCode, bool refresh_proxy = false)
            => OnError(msg, true, refresh_proxy, statusCode);

        public JsonResult OnError(string msg, bool rcache = true, bool refresh_proxy = true, int statusCode = 503)
        {
            var model = new OnErrorResult(msg);

            if (AppInit.conf.multiaccess && rcache && rch?.enable != true)
                memoryCache.Set(ResponseCache.ErrorKey(HttpContext), model, DateTime.Now.AddSeconds(15));

            if (refresh_proxy && rch?.enable != true)
                proxyManager?.Refresh();

            HttpContext.Response.StatusCode = statusCode;
            return Json(model);
        }
        #endregion

        #region PlaylistResult
        public async Task<ActionResult> PlaylistResult(CacheResult<List<PlaylistItem>> cache, IList<MenuItem> menu = null, int total_pages = 0)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return await PlaylistResult(
                cache.Value,
                menu,
                total_pages
            );
        }

        public async Task<ActionResult> PlaylistResult(IList<PlaylistItem> playlists, IList<MenuItem> menu, int total_pages = 0)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists", false);

            var ct = HttpContext.RequestAborted;

            Response.ContentType = "application/json; charset=utf-8";
            Response.Headers.CacheControl = "no-cache";

            var headers_stream = HeadersModel.InitOrNull(init.headers_stream);
            var headers_image = httpHeaders(init.host, HeadersModel.InitOrNull(init.headers_image));

            using (var writer = new Utf8JsonWriter(Response.BodyWriter, jsonWriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteNumber("count", playlists.Count);
                writer.WriteNumber("totalPages", total_pages);

                writer.WritePropertyName("menu");
                JsonSerializer.Serialize(writer, menu ?? emptyMenu, SisiResultJsonContext.Default.ListMenuItem);

                writer.WritePropertyName("list");
                writer.WriteStartArray();

                for (int i = 0; i < playlists.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var pl = playlists[i];

                    string video = pl.video.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? pl.video
                        : $"{host}/{pl.video}";

                    if (!video.Contains(host, StringComparison.OrdinalIgnoreCase))
                        video = HostStreamProxy(video, headers_stream);

                    JsonSerializer.Serialize(writer, new OnResultPlaylistItem
                    {
                        name = pl.name,
                        video = video,
                        model = pl.model != null
                            ? new OnResultModel(pl.model.name, pl.model.uri)
                            : null,
                        picture = HostImgProxy(pl.picture, 0, headers_image, init.plugin),
                        preview = pl.preview,
                        time = pl.time,
                        json = pl.json,
                        related = pl.related,
                        quality = pl.quality,
                        qualitys = pl.qualitys,
                        bookmark = pl.bookmark != null
                            ? new OnResultBookmark(pl.bookmark.uid, pl.bookmark.site, pl.bookmark.image, pl.bookmark.href)
                            : null,
                        hide = pl.hide,
                        myarg = pl.myarg
                    }, SisiResultJsonContext.Default.OnResultPlaylistItem);

                    // Cбрасываем накопленное из Utf8JsonWriter в транспорт
                    if (writer.BytesPending > 60_000)
                    {
                        writer.Flush(); // flush в PipeWriter
                        await Response.BodyWriter.FlushAsync(ct); // flush в транспорт
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();

                writer.Flush();
                await Response.BodyWriter.FlushAsync(ct);
            }

            return new EmptyResult();
        }
        #endregion

        #region OnResult
        public JsonResult OnResult(CacheResult<Dictionary<string, string>> cache)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return OnResult(cache.Value);
        }

        public JsonResult OnResult(Dictionary<string, string> stream_links)
        {
            return OnResult(new StreamItem() { qualitys = stream_links });
        }


        public JsonResult OnResult(CacheResult<StreamItem> cache)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return OnResult(cache.Value);
        }

        public JsonResult OnResult(StreamItem stream_links)
        {
            var result = new OnStreamResult(stream_links?.recomends?.Count ?? 0);

            var headers_stream = HeadersModel.InitOrNull(init.headers_stream);
            var headers_image = httpHeaders(init.host, HeadersModel.InitOrNull(init.headers_image));

            if (!init.streamproxy && init.qualitys_proxy)
                result.qualitys_proxy = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(v.Value, headers_stream, force_streamproxy: true));

            if (stream_links.recomends != null && stream_links.recomends.Count > 0)
            {
                for (int i = 0; i < stream_links.recomends.Count; i++)
                {
                    var pl = stream_links.recomends[i];
                    result.recomends[i] = new OnResultPlaylistItem
                    {
                        name = pl.name,
                        video = pl.video.StartsWith("http") ? pl.video : $"{host}/{pl.video}",
                        picture = HostImgProxy(pl.picture, 110, headers_image, init?.plugin),
                        json = pl.json
                    };
                }
            }

            result.qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(v.Value, headers_stream));
            result.headers_stream = init.streamproxy ? null : Http.NormalizeHeaders(init.headers_stream);

            return new JsonResult(result);
        }
        #endregion

        #region IsRhubFallback
        public bool IsRhubFallback()
        {
            if (init.rhub && init.rhub_fallback)
            {
                if (rch?.enable == true)
                {
                    init.rhub = false;
                    return true;
                }
            }

            return false;
        }

        public bool IsRhubFallback<Tresut>(CacheResult<Tresut> cache)
        {
            if (cache.IsSuccess)
                return false;

            if (cache.ErrorMsg != null && cache.ErrorMsg.StartsWith("{\"rch\""))
                return false;

            if (cache.Value == null && init.rhub && init.rhub_fallback)
            {
                if (rch?.enable == true)
                {
                    init.rhub = false;
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Semaphore
        public Task<ActionResult> SemaphoreResult(string key, Func<(string key, SemaphorManager semaphore), Task<ActionResult>> func) 
        {
            var semaphore = new SemaphorManager(key, TimeSpan.FromSeconds(30));

            try
            {
                return func.Invoke((key, semaphore));
            }
            finally
            {
                semaphore.Release();
            }
        }

        public Task<ActionResult> InvkSemaphore(string key, Func<string, Task<ActionResult>> func)
            => InvkSemaphore(key, rch, () => func.Invoke(key));
        #endregion

        #region cacheTime
        public TimeSpan cacheTime(int multiaccess)
        {
            return cacheTimeBase(multiaccess, init: baseconf);
        }
        #endregion

        #region HostStreamProxy
        public string HostStreamProxy(string uri, List<HeadersModel> headers = null, bool force_streamproxy = false)
            => HostStreamProxy(init, uri, headers, proxy, force_streamproxy, rch);
        #endregion

        #region InvokeCacheResult
        async public ValueTask<CacheResult<Tresut>> InvokeCacheResult<Tresut>(string key, int cacheTime, Func<Task<Tresut>> onget, bool? memory = null)
            => await InvokeBaseCacheResult<Tresut>(key, this.cacheTime(cacheTime), rch, proxyManager, async e => e.Success(await onget()), memory);

        public ValueTask<CacheResult<Tresut>> InvokeCacheResult<Tresut>(string key, int cacheTime, Func<CacheResult<Tresut>, Task<CacheResult<Tresut>>> onget, bool? memory = null)
            => InvokeBaseCacheResult(key, this.cacheTime(cacheTime), rch, proxyManager, onget, memory);
        #endregion

        #region ipkey
        public string ipkey(string key)
            => ipkey(key, proxyManager, rch);
        #endregion

        #region headerKeys
        public string headerKeys(string key, params string[] headersKey)
            => headerKeys(key, proxyManager, rch, headersKey);
        #endregion
    }
}
