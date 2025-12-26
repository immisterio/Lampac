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

namespace Shared
{
    public class BaseSisiController : BaseSisiController<SisiSettings>
    {
        public BaseSisiController(SisiSettings init) : base(init) { }
    }

    public class BaseSisiController<T> : BaseController where T : BaseSettings, ICloneable
    {
        #region RchClient
        RchClient? _rch = null;
        public RchClient rch
        {
            get
            {
                if (_rch == null)
                    _rch = new RchClient(HttpContext, host, init, requestInfo);

                return (RchClient)_rch;
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

        public ProxyManager proxyManager { get; private set; }

        public WebProxy proxy { get; private set; }

        public (string ip, string username, string password) proxy_data { get; private set; }

        public T init { get; private set; }

        BaseSettings baseconf { get; set; }

        public Func<JObject, T, T, T> loadKitInitialization { get; set; }

        public Action requestInitialization { get; set; }

        public Func<ValueTask> requestInitializationAsync { get; set; }


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

            proxyManager = new ProxyManager(this.init);
            var bp = proxyManager.BaseGet();
            proxy = bp.proxy;
            proxy_data = bp.data;
        }


        #region IsRequestBlocked
        public ValueTask<bool> IsRequestBlocked(T init, bool? rch = null, int? rch_keepalive = null, bool rch_check = true)
        {
            Initialization(init);
            return IsRequestBlocked(rch, rch_keepalive, rch_check);
        }

        async public ValueTask<bool> IsRequestBlocked(bool? rch = null, int? rch_keepalive = null, bool rch_check = true)
        {
            init = await loadKit(init, loadKitInitialization);

            #region module initialization
            if (AppInit.modules != null)
            {
                var args = new InitializationModel(init, rch);

                foreach (RootModule mod in AppInit.modules.Where(i => i.initialization != null))
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

            badInitMsg = await InvkEvent.BadInitialization(new EventBadInitialization(init, rch, requestInfo, host, HttpContext.Request, HttpContext, hybridCache));
            if (badInitMsg != null)
                return true;

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

            var overridehost = await IsOverridehost(init);
            if (overridehost != null)
            {
                badInitMsg = overridehost;
                return true;
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

                    if (rch_check)
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

            if (rch_check && this.rch.IsRequiredConnected())
            {
                badInitMsg = ContentTo(this.rch.connectionMsg);
                return true;
            }

            if (IsCacheError(init, this.rch))
                return true;

            requestInitialization?.Invoke();

            if (requestInitializationAsync != null)
                await requestInitializationAsync.Invoke();

            return false;
        }
        #endregion

        #region OnError
        public JsonResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true, bool rcache = true, int statusCode = 503)
        {
            if (refresh_proxy && rch.enable == false)
                proxyManager?.Refresh();

            return OnError(msg, rcache: rcache, statusCode: statusCode);
        }

        public JsonResult OnError(string msg, bool rcache = true, int statusCode = 503)
        {
            var model = new OnErrorResult(msg);

            if (AppInit.conf.multiaccess && rcache && rch.enable == false)
                memoryCache.Set(ResponseCache.ErrorKey(HttpContext), model, DateTime.Now.AddSeconds(15));

            HttpContext.Response.StatusCode = statusCode;
            return Json(model);
        }
        #endregion

        #region OnResult
        public JsonResult OnResult(CacheResult<List<PlaylistItem>> cache, IList<MenuItem> menu = null, int total_pages = 0)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return OnResult(
                cache.Value,
                menu,
                total_pages
            );
        }

        public JsonResult OnResult(IList<PlaylistItem> playlists, IList<MenuItem> menu, int total_pages = 0)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists", false);

            var result = new OnListResult(playlists.Count, total_pages, menu);

            for (int i = 0; i < playlists.Count; i++)
            {
                var pl = playlists[i];

                string video = pl.video.StartsWith("http") ? pl.video : $"{host}/{pl.video}";
                if (!video.Contains(host))
                    video = HostStreamProxy(video);

                result.list[i] = new OnResultPlaylistItem
                {
                    name = pl.name,
                    video = video,
                    model = pl.model,
                    picture = HostImgProxy(pl.picture, plugin: init.plugin, headers: httpHeaders(init.host, init.headers_image)),
                    preview = pl.preview,
                    time = pl.time,
                    json = pl.json,
                    related = pl.related,
                    quality = pl.quality,
                    qualitys = pl.qualitys,
                    bookmark = pl.bookmark,
                    hide = pl.hide,
                    myarg = pl.myarg
                };
            }

            return new JsonResult(result);
        }

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
            var headers_stream = init.headers_stream != null && init.headers_stream.Count > 0 ? httpHeaders(init.host, init.headers_stream) : null;

            if (!init.streamproxy && (init.geostreamproxy == null || init.geostreamproxy.Length == 0))
            {
                if (init.qualitys_proxy)
                    result.qualitys_proxy = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(init, v.Value, proxy: proxy, headers: headers_stream, force_streamproxy: true));
            }

            if (stream_links.recomends != null && stream_links.recomends.Count > 0)
            {
                for (int i = 0; i < stream_links.recomends.Count; i++)
                {
                    var pl = stream_links.recomends[i];
                    result.recomends[i] = new OnResultPlaylistItem
                    {
                        name = pl.name,
                        video = pl.video.StartsWith("http") ? pl.video : $"{host}/{pl.video}",
                        picture = HostImgProxy(pl.picture, height: 110, plugin: init?.plugin, headers: httpHeaders(init.host, init.headers_image)),
                        json = pl.json
                    };
                }
            }

            result.qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(init, v.Value, proxy: proxy, headers: headers_stream));
            result.headers_stream = init.streamproxy ? null : Http.NormalizeHeaders(headers_stream?.ToDictionary() ?? init.headers_stream);

            return new JsonResult(result);
        }
        #endregion

        #region IsRhubFallback
        public bool IsRhubFallback(BaseSettings init)
        {
            if (init.rhub && init.rhub_fallback)
            {
                init.rhub = false;
                return true;
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
                init.rhub = false;
                return true;
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
            => HostStreamProxy(init, uri, headers, proxy, force_streamproxy);
        #endregion

        #region InvokeCacheResult
        async public ValueTask<CacheResult<Tresut>> InvokeCacheResult<Tresut>(string key, int cacheTime, Func<Task<Tresut>> onget, bool? memory = null)
            => await InvokeBaseCacheResult<Tresut>(key, this.cacheTime(cacheTime), rch, proxyManager, async e => e.Success(await onget()), memory);

        public ValueTask<CacheResult<Tresut>> InvokeCacheResult<Tresut>(string key, int cacheTime, Func<CacheResult<Tresut>, Task<CacheResult<Tresut>>> onget, bool? memory = null)
            => InvokeBaseCacheResult(key, this.cacheTime(cacheTime), rch, proxyManager, onget, memory);
        #endregion
    }
}
