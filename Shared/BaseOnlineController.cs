using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Engine.Pools;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Online.Settings;
using Shared.Models.Templates;
using System.Net;
using System.Reflection;
using System.Text;

namespace Shared
{
    public class BaseOnlineController : BaseOnlineController<OnlinesSettings>
    {
        public BaseOnlineController(OnlinesSettings init) : base(init) { }
    }

    public class BaseOnlineController<T> : BaseController where T : BaseSettings, ICloneable
    {
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


        public BaseOnlineController(T init)
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

                if (!init.enable || init.rip)
                {
                    badInitMsg = OnError("disable", gbcache: false, statusCode: 403);
                    return true;
                }
            }

            if (NoAccessGroup(init, out string error_msg))
            {
                badInitMsg = new JsonResult(new { accsdb = true, msg = error_msg });
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
                        badInitMsg = ShowError(RchClient.ErrorMsg);
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
                            badInitMsg = ShowError(rch_error);
                            return true;
                        }
                    }
                }
                else
                {
                    if (init.rhub)
                    {
                        badInitMsg = ShowError(RchClient.ErrorMsg);
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


        #region MaybeInHls
        public bool MaybeInHls(bool hls)
        {
            if (!string.IsNullOrEmpty(init.apn?.host) && AppInit.IsDefaultApnOrCors(init.apn?.host))
                return false;

            if (init.apnstream && AppInit.IsDefaultApnOrCors(AppInit.conf.apn?.host))
                return false;

            return hls;
        }
        #endregion

        #region OnLog
        public void OnLog(string msg)
        {
            if (AppInit.conf.weblog.enable)
                Http.onlog?.Invoke(null, msg + "\n");
        }
        #endregion

        #region OnError
        public ActionResult OnError(int statusCode = 503, bool refresh_proxy = false) 
            => OnError(string.Empty, statusCode, refresh_proxy);

        public ActionResult OnError(string msg, int statusCode, bool refresh_proxy = false)
            => OnError(msg, null, refresh_proxy, null, statusCode);

        public ActionResult OnError(string msg, bool? gbcache = true, bool refresh_proxy = false, string weblog = null, int statusCode = 503)
        {
            if (string.IsNullOrEmpty(msg) || !msg.StartsWith("{\"rch\""))
            {
                if (refresh_proxy && rch?.enable != true)
                    proxyManager?.Refresh();
            }

            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.StartsWith("{\"rch\""))
                    return Content(msg);

                string log = $"{HttpContext.Request.Path.Value}\n{msg}";
                if (!string.IsNullOrEmpty(weblog))
                    log += $"\n\n\n===================\n\n{weblog}";

                Http.onlog?.Invoke(null, log);
            }

            if (AppInit.conf.multiaccess && gbcache == true && rch?.enable != true)
                memoryCache.Set(ResponseCache.ErrorKey(HttpContext), msg ?? string.Empty, DateTime.Now.AddSeconds(15));

            HttpContext.Response.StatusCode = statusCode;
            return ContentTo(msg ?? string.Empty);
        }
        #endregion

        #region OnResult
        public ActionResult OnResult<Tresut>(CacheResult<Tresut> cache, Func<string> html)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            if (cache.Value != null && IsOrigsourceRequest())
                return Json(cache.Value);

            return ContentTo(html.Invoke());
        }
        #endregion

        #region ContentTo
        public ActionResult ContentTo(ITplResult tpl)
        {
            if (tpl == null)
                return OnError();

            if (IsRjsonRequest())
                return Content(tpl.ToJson(), "application/json; charset=utf-8");

            return Content(tpl.ToHtml(), "text/html; charset=utf-8");
        }
        #endregion

        #region ContentTpl
        async public Task<ActionResult> ContentTpl<Tresut>(CacheResult<Tresut> cache, Func<ITplResult> tpl)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            if (cache.Value != null && IsOrigsourceRequest())
                return Json(cache.Value);

            return await ContentTpl(tpl());
        }

        async public Task<ActionResult> ContentTpl(ITplResult tpl)
        {
            bool rjson = IsRjsonRequest();

            if (tpl == null || tpl.IsEmpty)
                return OnError(rjson ? "{}" : string.Empty);

            var response = HttpContext.Response;

            response.Headers.CacheControl = "no-cache";

            response.ContentType = rjson 
                ? "application/json; charset=utf-8" 
                : "text/html; charset=utf-8";

            var encoder = Encoding.UTF8.GetEncoder();
            var ct = HttpContext.RequestAborted;

            var bodyWriter = response.BodyWriter;
            long pendingBytes = 0;

            var sb = rjson 
                ? tpl.ToBuilderJson()
                : tpl.ToBuilderHtml();

            try
            {
                foreach (var chunk in sb.GetChunks())
                {
                    ct.ThrowIfCancellationRequested();

                    ReadOnlySpan<char> chars = chunk.Span;
                    if (chars.IsEmpty)
                        continue;

                    int maxBytes = Encoding.UTF8.GetMaxByteCount(chars.Length);

                    // Берём буфер напрямую у PipeWriter
                    Span<byte> dest = bodyWriter.GetSpan(maxBytes);

                    encoder.Convert(
                        chars,
                        dest,
                        flush: false,
                        out int charsUsed,
                        out int bytesUsed,
                        out bool completed);

                    // Обычно Convert при достаточном dest использует все chars.
                    // Но оставим защитный цикл на случай, если кто-то поменяет логику/буфер.
                    bodyWriter.Advance(bytesUsed);
                    pendingBytes += bytesUsed;

                    while (!completed)
                    {
                        chars = chars.Slice(charsUsed);

                        maxBytes = Encoding.UTF8.GetMaxByteCount(chars.Length);
                        dest = bodyWriter.GetSpan(maxBytes);

                        encoder.Convert(
                            chars,
                            dest,
                            flush: false,
                            out charsUsed,
                            out bytesUsed,
                            out completed);

                        bodyWriter.Advance(bytesUsed);
                        pendingBytes += bytesUsed;
                    }

                    // Сбрасываем накопленное в транспорт
                    if (pendingBytes >= (StringBuilderPool.rent * 2))
                    {
                        await bodyWriter.FlushAsync(ct);
                        pendingBytes = 0;
                    }
                }
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }

            // Дофлашить состояние энкодера (границы чанков/суррогаты)
            {
                Span<byte> tail = bodyWriter.GetSpan(256);
                encoder.Convert(
                    ReadOnlySpan<char>.Empty,
                    tail,
                    flush: true,
                    out _,
                    out int tailBytes,
                    out _);

                if (tailBytes > 0)
                {
                    bodyWriter.Advance(tailBytes);
                    pendingBytes += tailBytes;
                }
            }

            // Финальный flush (если что-то осталось в writer)
            if (pendingBytes > 0)
                await bodyWriter.FlushAsync(ct);

            return new EmptyResult();
        }
        #endregion

        #region ShowError
        public ActionResult ShowError(string msg) => Json(new { accsdb = true, msg });

        public string ShowErrorString(string msg) => System.Text.Json.JsonSerializer.Serialize(new { accsdb = true, msg });
        #endregion


        #region IsRhubFallback
        public bool IsRhubFallback<Tresut>(CacheResult<Tresut> cache, bool safety = false)
        {
            if (cache.IsSuccess)
                return false;

            if (cache.ErrorMsg != null && cache.ErrorMsg.StartsWith("{\"rch\""))
                return false;

            if (cache.Value == null && init.rhub && init.rhub_fallback)
            {
                init.rhub = false;

                if (safety && init.rhub_safety)
                    return false;

                return rch != null;
            }

            return false;
        }
        #endregion

        #region InvokeCacheResult
        async public ValueTask<CacheResult<Tresut>> InvokeCacheResult<Tresut>(string key, int cacheTime, Func<Task<Tresut>> onget, bool? memory = null)
            => await InvokeBaseCacheResult<Tresut>(key, this.cacheTime(cacheTime), rch, proxyManager, async e => e.Success(await onget()), memory);

        public ValueTask<CacheResult<Tresut>> InvokeCacheResult<Tresut>(string key, int cacheTime, Func<CacheResult<Tresut>, Task<CacheResult<Tresut>>> onget, bool? memory = null)
            => InvokeBaseCacheResult(key, this.cacheTime(cacheTime), rch, proxyManager, onget, memory);
        #endregion

        #region InvokeCache
        public ValueTask<Tresut> InvokeCache<Tresut>(string key, int cacheTime, Func<Task<Tresut>> onget, bool? memory = null)
            => InvokeBaseCache(key, this.cacheTime(cacheTime), rch, onget, proxyManager, memory);
        #endregion

        #region HostStreamProxy
        public string HostStreamProxy(string uri, List<HeadersModel> headers = null, bool force_streamproxy = false)
            => HostStreamProxy(init, uri, headers, proxy, force_streamproxy, rch);
        #endregion

        #region InvkSemaphore
        public Task<ActionResult> InvkSemaphore(string key, Func<string, Task<ActionResult>> func)
            => InvkSemaphore(key, rch, () => func.Invoke(key));

        public Task<ActionResult> InvkSemaphore(string key, Func<Task<ActionResult>> func)
            => InvkSemaphore(key, rch, func);
        #endregion


        #region cacheTime
        public TimeSpan cacheTime(int multiaccess)
        {
            return cacheTimeBase(multiaccess, init: baseconf);
        }
        #endregion

        #region ipkey
        public string ipkey(string key)
            => ipkey(key, proxyManager, rch);
        #endregion


        #region IsRjsonRequest
        public bool IsRjsonRequest()
        {
            return HttpContext.Request.Query.TryGetValue("rjson", out StringValues value)
                && value.Count > 0
                && value[0].Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region IsOrigsourceRequest
        public bool IsOrigsourceRequest()
        {
            return HttpContext.Request.Query.TryGetValue("origsource", out StringValues value)
                && value.Count > 0
                && value[0].Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
