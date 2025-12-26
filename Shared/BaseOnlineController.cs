using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MonoTorrent.Client;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Online.Settings;
using Shared.Models.Templates;
using System.Net;
using System.Reflection;

namespace Shared
{
    public class BaseOnlineController : BaseOnlineController<OnlinesSettings>
    {
        public BaseOnlineController(OnlinesSettings init) : base(init) { }
    }

    public class BaseOnlineController<T> : BaseController where T : BaseSettings, ICloneable
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
                badInitMsg = OnError("disable", gbcache: false, statusCode: 403);
                return true;
            }

            if (NoAccessGroup(init, out string error_msg))
            {
                badInitMsg = new JsonResult(new { accsdb = true, msg = error_msg });
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
                        badInitMsg = ShowError(RchClient.ErrorMsg);
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
        public ActionResult OnError(ProxyManager proxyManager, bool refresh_proxy = true, string weblog = null) => OnError(string.Empty, proxyManager, refresh_proxy, weblog: weblog);

        public ActionResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true, string weblog = null)
        {
            if (string.IsNullOrEmpty(msg) || !msg.StartsWith("{\"rch\""))
            {
                if (refresh_proxy)
                    proxyManager?.Refresh();
            }

            return OnError(msg, weblog: weblog);
        }

        public ActionResult OnError() => OnError(string.Empty);

        public ActionResult OnError(string msg, bool? gbcache = true, string weblog = null, int statusCode = 503)
        {
            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.StartsWith("{\"rch\""))
                    return Content(msg);

                string log = $"{HttpContext.Request.Path.Value}\n{msg}";
                if (!string.IsNullOrEmpty(weblog))
                    log += $"\n\n\n===================\n\n{weblog}";

                Http.onlog?.Invoke(null, log);
            }

            if (AppInit.conf.multiaccess && gbcache == true && rch.enable == false)
                memoryCache.Set(ResponseCache.ErrorKey(HttpContext), msg ?? string.Empty, DateTime.Now.AddSeconds(15));

            HttpContext.Response.StatusCode = statusCode;
            return ContentTo(msg ?? string.Empty);
        }
        #endregion

        #region OnResult
        public ActionResult OnResult<Tresut>(CacheResult<Tresut> cache, ITplResult tpl)
            => OnResult(cache, () => tpl);

        public ActionResult OnResult<Tresut>(CacheResult<Tresut> cache, Func<ITplResult> tpl)
        {
            return OnResult(cache, () => 
            {
                var tplResult = tpl();
                return HttpContext.Request.Query["rjson"].ToString().Contains("true", StringComparison.OrdinalIgnoreCase)
                    ? tplResult.ToJson()
                    : tplResult.ToHtml();
            });
        }

        public ActionResult OnResult<Tresut>(CacheResult<Tresut> cache, Func<string> html)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            if (HttpContext.Request.Query["origsource"].ToString().Contains("true", StringComparison.OrdinalIgnoreCase) 
                && cache.Value != null)
                return Json(cache.Value);

            return ContentTo(html.Invoke());
        }
        #endregion

        #region ContentTo
        public ActionResult ContentTo(ITplResult tpl)
        {
            return ContentTo(HttpContext.Request.Query["rjson"].ToString().Contains("true", StringComparison.OrdinalIgnoreCase)
                ? tpl.ToJson()
                : tpl.ToHtml()
            );
        }
        #endregion

        #region ShowError
        public ActionResult ShowError(string msg) => Json(new { accsdb = true, msg });

        public string ShowErrorString(string msg) => System.Text.Json.JsonSerializer.Serialize(new { accsdb = true, msg });
        #endregion


        #region IsRhubFallback
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
            => HostStreamProxy(init, uri, headers, proxy, force_streamproxy);
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
    }
}
