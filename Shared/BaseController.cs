using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models;
using Shared.Models.AppConf;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Online.Settings;
using Shared.Models.SISI.OnResult;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using IO = System.IO;

namespace Shared
{
    public class BaseController : Controller, IDisposable
    {
        IServiceScope serviceScope;

        public static string appversion => "149";

        public static string minorversion => "24";

        public HybridCache hybridCache { get; private set; }

        public IMemoryCache memoryCache { get; private set; }

        public RequestModel requestInfo => HttpContext.Features.Get<RequestModel>();

        public string host => AppInit.Host(HttpContext);

        protected static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreLocks = new();

        public ActionResult badInitMsg { get; set; }

        public BaseController()
        {
            hybridCache = new HybridCache();

            serviceScope = Startup.ApplicationServices.CreateScope();
            var scopeServiceProvider = serviceScope.ServiceProvider;
            memoryCache = scopeServiceProvider.GetService<IMemoryCache>();
        }

        #region mylocalip
        static string lastMyIp = null;

        async public ValueTask<string> mylocalip()
        {
            string key = "BaseController:mylocalip";
            if (!memoryCache.TryGetValue(key, out string userIp))
            {
                userIp = await InvkEvent.MyLocalIp(new EventMyLocalIp(requestInfo, HttpContext.Request, HttpContext, hybridCache));

                if (string.IsNullOrEmpty(userIp))
                {
                    var myip = await Http.Get<JObject>("https://api.ipify.org/?format=json");
                    if (myip == null || string.IsNullOrEmpty(myip.Value<string>("ip")))
                        return lastMyIp;

                    userIp = myip.Value<string>("ip");
                    lastMyIp = userIp;
                }

                memoryCache.Set(key, userIp, DateTime.Now.AddMinutes(5));
            }

            return userIp;
        }
        #endregion

        #region httpHeaders
        public List<HeadersModel> httpHeaders(BaseSettings init, List<HeadersModel> _startHeaders = null)
        {
            var headers = HeadersModel.Init(_startHeaders);
            if (init.headers == null)
                return headers;

            return httpHeaders(init.host, HeadersModel.Join(HeadersModel.Init(init.headers), headers));
        }

        public List<HeadersModel> httpHeaders(string site, Dictionary<string, string> headers)
        {
            return httpHeaders(site, HeadersModel.Init(headers));
        }

        public List<HeadersModel> httpHeaders(string site, List<HeadersModel> _headers)
        {
            if (_headers == null || _headers.Count == 0)
                return _headers;

            var headers = new List<HeadersModel>(_headers.Count);

            string ip = requestInfo.IP;
            string account_email = HttpContext.Request.Query["account_email"].ToString()?.ToLower().Trim() ?? string.Empty;

            foreach (var h in _headers)
            {
                if (string.IsNullOrEmpty(h.val) || string.IsNullOrEmpty(h.name))
                    continue;

                var bulder = new StringBuilder(h.val)
                   .Replace("{account_email}", account_email)
                   .Replace("{ip}", ip)
                   .Replace("{host}", site);

                string val = bulder.ToString();

                if (val.StartsWith("encrypt:"))
                {
                    string encrypt = Regex.Match(val, "^encrypt:([^\n\r]+)").Groups[1].Value;
                    val = new OnlinesSettings(null, encrypt).host;
                }

                if (val.Contains("{arg:"))
                {
                    foreach (Match m in Regex.Matches(val, "\\{arg:([^\\}]+)\\}"))
                    {
                        string _a = Regex.Match(HttpContext.Request.QueryString.Value, $"&{m.Groups[1].Value}=([^&]+)").Groups[1].Value;
                        val = val.Replace(m.Groups[0].Value, _a);
                    }
                }

                if (val.Contains("{head:"))
                {
                    foreach (Match m in Regex.Matches(val, "\\{head:([^\\}]+)\\}"))
                    {
                        if (HttpContext.Request.Headers.TryGetValue(m.Groups[1].Value, out var _h))
                        {
                            val = val.Replace(m.Groups[0].Value, string.Join(" ", _h.ToString()));
                        }
                        else
                        {
                            val = val.Replace(m.Groups[0].Value, string.Empty);
                        }

                        string _a = Regex.Match(HttpContext.Request.QueryString.Value, $"&{m.Groups[1].Value}=([^&]+)").Groups[1].Value;
                        val = val.Replace(m.Groups[0].Value, _a);
                    }
                }

                if (headers.FirstOrDefault(i => i.name == h.name) == null)
                    headers.Add(new HeadersModel(h.name, val));
            }

            var eventHeaders = InvkEvent.HttpHeaders(new EventControllerHttpHeaders(site, headers, requestInfo, HttpContext.Request, HttpContext));
            if (eventHeaders != null)
                headers = eventHeaders;

            return headers;
        }
        #endregion

        #region HostImgProxy
        public string HostImgProxy(string uri, int height = 0, List<HeadersModel> headers = null, string plugin = null)
        {
            if (!AppInit.conf.sisi.rsize || string.IsNullOrWhiteSpace(uri)) 
                return uri;

            var init = AppInit.conf.sisi;
            int width = init.widthPicture;
            height = height > 0 ? height : init.heightPicture;

            string goEncryptUri(string _uri)
            {
                string encrypt_uri = ProxyLink.Encrypt(_uri, requestInfo.IP, headers, verifyip: false, ex: DateTime.Now.AddMinutes(20));
                if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                    encrypt_uri = AccsDbInvk.Args(encrypt_uri, HttpContext);

                return encrypt_uri;
            }

            if (plugin != null && init.proxyimg_disable != null && init.proxyimg_disable.Contains(plugin))
                return uri;

            if (width == 0 && height == 0 || plugin != null && init.rsize_disable != null && init.rsize_disable.Contains(plugin))
            {
                if (!string.IsNullOrEmpty(init.bypass_host))
                {
                    string sheme = uri.StartsWith("https:") ? "https" : "http";
                    string bypass_host = init.bypass_host.Replace("{sheme}", sheme).Replace("{uri}", Regex.Replace(uri, "^https?://", ""));

                    if (bypass_host.Contains("{encrypt_uri}"))
                        bypass_host = bypass_host.Replace("{encrypt_uri}", goEncryptUri(uri));

                    return bypass_host;
                }

                return $"{host}/proxyimg/{goEncryptUri(uri)}";
            }

            if (!string.IsNullOrEmpty(init.rsize_host))
            {
                string sheme = uri.StartsWith("https:") ? "https" : "http";
                string rsize_host = init.rsize_host.Replace("{width}", width.ToString()).Replace("{height}", height.ToString())
                                                   .Replace("{sheme}", sheme).Replace("{uri}", Regex.Replace(uri, "^https?://", ""));

                if (rsize_host.Contains("{encrypt_uri}"))
                    rsize_host = rsize_host.Replace("{encrypt_uri}", goEncryptUri(uri));

                return rsize_host;
            }

            return $"{host}/proxyimg:{width}:{height}/{goEncryptUri(uri)}";
        }
        #endregion

        #region HostStreamProxy
        public string HostStreamProxy(BaseSettings conf, string uri, List<HeadersModel> headers = null, WebProxy proxy = null, bool force_streamproxy = false)
        {
            if (!AppInit.conf.serverproxy.enable || string.IsNullOrEmpty(uri) || conf == null)
                return uri?.Split(" ")?[0]?.Trim();

            string _eventUri = InvkEvent.HostStreamProxy(new EventHostStreamProxy(conf, uri, headers, proxy, requestInfo, HttpContext, hybridCache));
            if (_eventUri != null)
                return _eventUri;

            if (conf.rhub && !conf.rhub_streamproxy)
                return uri.Split(" ")[0].Trim();

            bool streamproxy = conf.streamproxy || conf.useproxystream || force_streamproxy;
            if (!streamproxy && conf.geostreamproxy != null && conf.geostreamproxy.Length > 0)
            {
                string country = requestInfo.Country;
                if (!string.IsNullOrEmpty(country) && country.Length == 2)
                {
                    if (conf.geostreamproxy.Contains("ALL") || conf.geostreamproxy.Contains(country))
                        streamproxy = true;
                }
            }

            if (streamproxy)
            {
                if (conf.headers_stream != null && conf.headers_stream.Count > 0)
                    headers = HeadersModel.Init(conf.headers_stream);

                #region apnstream
                string apnlink(ApnConf apn)
                {
                    string link = uri.Split(" ")[0].Split("#")[0].Trim();

                    if (apn.secure == "nginx")
                    {
                        using (MD5 md5 = MD5.Create())
                        {
                            long ex = ((DateTimeOffset)DateTime.Now.AddHours(12)).ToUnixTimeSeconds();
                            string hash = Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes($"{ex}{requestInfo.IP} {apn.secret}"))).Replace("=", "").Replace("+", "-").Replace("/", "_");

                            return $"{apn.host}/{hash}:{ex}/{link}";
                        }
                    }
                    else if (apn.secure == "cf")
                    {
                        using (var sha1 = SHA1.Create())
                        {
                            var data = Encoding.UTF8.GetBytes($"{requestInfo.IP}{link}{apn.secret}");
                            return Convert.ToBase64String(sha1.ComputeHash(data));
                        }
                    }
                    else if (apn.secure == "lampac")
                    {
                        string aes = AesTo.Encrypt(System.Text.Json.JsonSerializer.Serialize(new 
                        {
                            u = link,
                            i = requestInfo.IP,
                            v = true,
                            e = DateTime.Now.AddHours(36),
                            h = headers?.ToDictionary() 
                        }));

                        if (uri.Contains(".m3u"))
                            aes += ".m3u8";

                        return $"{apn.host}/proxy/{aes}";
                    }

                    return $"{apn.host}/{link}";
                }

                if (!string.IsNullOrEmpty(conf.apn?.host) && conf.apn.host.StartsWith("http"))
                    return apnlink(conf.apn);

                if (AppInit.conf.serverproxy.forced_apn || conf.apnstream)
                {
                    if (!string.IsNullOrEmpty(AppInit.conf?.apn?.host) && AppInit.conf.apn.host.StartsWith("http"))
                        return apnlink(AppInit.conf.apn);

                    return uri;
                }  
                #endregion

                uri = ProxyLink.Encrypt(uri, requestInfo.IP, httpHeaders(conf.host ?? conf.apihost, headers), conf != null && conf.useproxystream ? proxy : null, conf?.plugin);

                if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                    uri = AccsDbInvk.Args(uri, HttpContext);

                return $"{host}/proxy/{uri}";
            }

            if (conf.url_reserve && !uri.Contains(" or ") && !uri.Contains("/proxy/") &&
                !Regex.IsMatch(HttpContext.Request.QueryString.Value, "&play=true", RegexOptions.IgnoreCase))
            {
                string url_reserve = ProxyLink.Encrypt(uri, requestInfo.IP, httpHeaders(conf.host ?? conf.apihost, headers), conf != null && conf.useproxystream ? proxy : null, conf?.plugin);

                if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                    url_reserve = AccsDbInvk.Args(uri, HttpContext);

                uri += $" or {host}/proxy/{url_reserve}";
            }

            return uri;
        }
        #endregion

        #region InvokeCache
        public ValueTask<CacheResult<T>> InvokeCache<T>(string key, TimeSpan time, Func<CacheResult<T>, ValueTask<dynamic>> onget) => InvokeCache(key, time, null, onget);

        async public ValueTask<CacheResult<T>> InvokeCache<T>(string key, TimeSpan time, ProxyManager? proxyManager, Func<CacheResult<T>, ValueTask<dynamic>> onget, bool? memory = null)
        {
            var semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            try
            {
                await semaphore.WaitAsync(TimeSpan.FromSeconds(40));

                if (hybridCache.TryGetValue(key, out T _val, memory))
                {
                    HttpContext.Response.Headers.TryAdd("X-Invoke-Cache", "HIT");
                    return new CacheResult<T>() { IsSuccess = true, Value = _val };
                }

                HttpContext.Response.Headers.TryAdd("X-Invoke-Cache", "MISS");

                var val = await onget.Invoke(new CacheResult<T>());

                if (val == null)
                    return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "null" };

                if (val.GetType() == typeof(CacheResult<T>))
                    return (CacheResult<T>)val;

                if (val.Equals(default(T)))
                    return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "default" };

                if (typeof(T) == typeof(string) && string.IsNullOrEmpty(val.ToString()))
                    return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "empty" };

                proxyManager?.Success();
                hybridCache.Set(key, val, time, memory);
                return new CacheResult<T>() { IsSuccess = true, Value = val };
            }
            finally
            {
                try
                {
                    semaphore.Release();
                }
                finally
                {
                    if (semaphore.CurrentCount == 1)
                        _semaphoreLocks.TryRemove(key, out _);
                }
            }
        }

        async public ValueTask<T> InvokeCache<T>(string key, TimeSpan time, Func<ValueTask<T>> onget, ProxyManager? proxyManager = null, bool? memory = null)
        {
            var semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            try
            {
                await semaphore.WaitAsync(TimeSpan.FromSeconds(40));

                if (hybridCache.TryGetValue(key, out T val, memory))
                    return val;

                val = await onget.Invoke();
                if (val == null || val.Equals(default(T)))
                    return default;

                proxyManager?.Success();
                hybridCache.Set(key, val, time, memory);
                return val;
            }
            finally
            {
                try
                {
                    semaphore.Release();
                }
                finally
                {
                    if (semaphore.CurrentCount == 1)
                        _semaphoreLocks.TryRemove(key, out _);
                }
            }
        }
        #endregion

        #region InvkSemaphore
        async public Task<ActionResult> InvkSemaphore(BaseSettings init, string key, Func<ValueTask<ActionResult>> func)
        {
            if (init != null)
            {
                if (init.rhub && init.rhub_fallback == false)
                    return await func.Invoke();
            }

            var semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            try
            {
                await semaphore.WaitAsync(TimeSpan.FromSeconds(40));
                return await func.Invoke();
            }
            finally
            {
                try
                {
                    semaphore.Release();
                }
                finally
                {
                    if (semaphore.CurrentCount == 1)
                        _semaphoreLocks.TryRemove(key, out _);
                }
            }
        }
        #endregion

        #region cacheTime
        public TimeSpan cacheTime(int multiaccess, int home = 5, int mikrotik = 2, BaseSettings init = null, int rhub = -1)
        {
            if (init != null && init.rhub && rhub != -1)
                return TimeSpan.FromMinutes(rhub);

            int ctime = AppInit.conf.mikrotik ? mikrotik : AppInit.conf.multiaccess ? init != null && init.cache_time > 0 ? init.cache_time : multiaccess : home;
            if (ctime > multiaccess)
                ctime = multiaccess;

            return TimeSpan.FromMinutes(ctime);
        }
        #endregion

        #region IsCacheError
        public bool IsCacheError(BaseSettings init)
        {
            if (!AppInit.conf.multiaccess || init.rhub)
                return false;

            if (memoryCache.TryGetValue(ResponseCache.ErrorKey(HttpContext), out object errorCache))
            {
                HttpContext.Response.Headers.TryAdd("X-RCache", "true");

                if (errorCache is OnErrorResult)
                {
                    badInitMsg = Json(errorCache);
                    return true;
                }
                else if (errorCache is string)
                {
                    string msg = errorCache.ToString();
                }

                badInitMsg = Ok();
                return true;
            }

            return false;
        }
        #endregion

        #region IsOverridehost
        async public ValueTask<ActionResult> IsOverridehost(BaseSettings init)
        {
            string overridehost = null;

            if (!string.IsNullOrEmpty(init.overridehost))
                overridehost = init.overridehost;

            if (string.IsNullOrEmpty(overridehost) && init.overridehosts != null && init.overridehosts.Length > 0)
                overridehost = init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];

            if (string.IsNullOrEmpty(overridehost))
                return null;

            if (string.IsNullOrEmpty(init.overridepasswd))
            {
                if (overridehost.Contains("?"))
                    overridehost += "&" + HttpContext.Request.QueryString.Value.Remove(0, 1);
                else
                    overridehost += HttpContext.Request.QueryString.Value;

                return new RedirectResult(overridehost);
            }

            overridehost = Regex.Replace(overridehost, "^(https?://[^/]+)/.*", "$1");
            string uri = overridehost + HttpContext.Request.Path.Value + HttpContext.Request.QueryString.Value;

            string clientip = requestInfo.IP;
            if (requestInfo.Country == null)
                clientip = await mylocalip();

            string html = await Http.Get(uri, timeoutSeconds: 10, headers: HeadersModel.Init
            (
                ("localrequest", init.overridepasswd),
                ("x-client-ip", clientip)
            ));

            if (html == null)
                return new ContentResult() { StatusCode = 502, Content = string.Empty };

            html = Regex.Replace(html, "\"(https?://[^/]+/proxy/)", "\"_tmp_ $1");
            html = Regex.Replace(html, $"\"{overridehost}", $"\"{host}");
            html = html.Replace("\"_tmp_ ", "\"");

            return ContentTo(html);
        }
        #endregion

        #region NoAccessGroup
        public bool NoAccessGroup(Igroup init, out string error_msg)
        {
            error_msg = null;

            if (init.group > 0)
            {
                var user = requestInfo.user;
                if (user == null || init.group > user.group)
                {
                    error_msg = AppInit.conf.accsdb.denyGroupMesage.
                                Replace("{account_email}", requestInfo.user_uid).
                                Replace("{user_uid}", requestInfo.user_uid);

                    return true;
                }
            }

            return false;
        }
        #endregion

        #region accsArgs
        public string accsArgs(string uri)
        {
            return AccsDbInvk.Args(uri, HttpContext);
        }
        #endregion

        #region loadKit
        public bool IsKitConf { get; private set; }

        async public ValueTask<JObject> loadKitConf()
        {
            var init = AppInit.conf.kit;
            if (!init.enable || string.IsNullOrEmpty(init.path) || string.IsNullOrEmpty(requestInfo.user_uid))
                return null;

            if (init.IsAllUsersPath)
            {
                if (init.allUsers != null && init.allUsers.TryGetValue(requestInfo.user_uid, out JObject userInit))
                    return userInit;

                return null;
            }
            else
            {
                string memKey = $"loadKit:{requestInfo.user_uid}";
                if (!memoryCache.TryGetValue(memKey, out JObject appinit))
                {
                    string json;

                    if (Regex.IsMatch(init.path, "^https?://"))
                    {
                        string uri = init.path.Replace("{uid}", HttpUtility.UrlEncode(requestInfo.user_uid));
                        json = await Http.Get(uri, timeoutSeconds: 5);
                    }
                    else
                    {
                        string init_file = $"{init.path}/{CrypTo.md5(requestInfo.user_uid)}";

                        if (init.eval_path != null)
                            init_file = CSharpEval.Execute<string>(init.eval_path, new KitConfEvalPath(init.path, requestInfo.user_uid));
                       
                        if (!IO.File.Exists(init_file))
                            return null;

                        json = IO.File.ReadAllText(init_file);
                    }

                    if (json == null)
                        return null;

                    try
                    {
                        if (!json.TrimStart().StartsWith("{"))
                            json = "{" + json + "}";

                        appinit = JsonConvert.DeserializeObject<JObject>(json);
                    }
                    catch { return null; }

                    memoryCache.Set(memKey, appinit, DateTime.Now.AddSeconds(Math.Max(5, init.cacheToSeconds)));
                }

                return appinit;
            }
        }

        async public ValueTask<T> loadKit<T>(T _init, Func<JObject, T, T, T> func = null) where T : BaseSettings, ICloneable
        {
            if (_init.kit == false && _init.rhub_fallback == false)
                return (T)_init.Clone();

            return loadKit((T)_init.Clone(), await loadKitConf(), func, clone: false);
        }

        public T loadKit<T>(T _init, JObject appinit, Func<JObject, T, T, T> func = null, bool clone = true) where T : BaseSettings, ICloneable
        {
            var init = clone ? (T)_init.Clone() : _init;
            var defaultinit = InvkEvent.conf.LoadKit != null ? (clone ? _init : (T)_init.Clone()) : null;

            if (init == null || !init.kit || appinit == null || string.IsNullOrEmpty(init.plugin) || !appinit.ContainsKey(init.plugin))
            {
                InvkEvent.LoadKit(new EventLoadKit(defaultinit, init, appinit, requestInfo, hybridCache));
                return init;
            }

            var conf = appinit.Value<JObject>(init.plugin);

            void update<T2>(string key, Action<T2> updateAction)
            {
                if (conf.ContainsKey(key))
                    updateAction(conf.Value<T2>(key));
            }

            update<bool>("enable", v => init.enable = v);
            if (conf.ContainsKey("enable") && init.enable)
                init.geo_hide = null;

            update<string>("displayname", v => init.displayname = v);
            update<int>("displayindex", v => init.displayindex = v);
            update<string>("client_type", v => init.client_type = v);

            update<string>("cookie", v => init.cookie = v);
            update<string>("token", v => init.token = v);

            update<string>("host", v => init.host = v);
            update<string>("apihost", v => init.apihost = v);
            update<string>("scheme", v => init.scheme = v);
            update<bool>("hls", v => init.hls = v);

            update<string>("overridehost", v => init.overridehost = v);
            update<string>("overridepasswd", v => init.overridepasswd = v);
            if (conf.ContainsKey("overridehosts"))
                init.overridehosts = conf["overridehosts"].ToObject<string[]>();

            if (conf.ContainsKey("headers"))
                init.headers = conf["headers"].ToObject<Dictionary<string, string>>();

            init.apnstream = true;
            if (conf.ContainsKey("apn"))
                init.apn = conf["apn"].ToObject<ApnConf>();

            init.useproxystream = false;
            update<bool>("streamproxy", v => init.streamproxy = v);
            if (conf.ContainsKey("geostreamproxy"))
                init.geostreamproxy = conf["geostreamproxy"].ToObject<string[]>();

            if (conf.ContainsKey("proxy"))
            {
                init.proxy = conf["proxy"].ToObject<ProxySettings>();
                if (init?.proxy?.list != null && init.proxy.list.Length > 0)
                    update<bool>("useproxy", v => init.useproxy = v);
            }

            if (init.useproxy)
            {
                init.rhub = false;
                init.rhub_fallback = true;
            }
            else if (AppInit.conf.kit.rhub_fallback || init.rhub_fallback)
            {
                update<bool>("rhub", v => init.rhub = v);
                update<bool>("rhub_fallback", v => init.rhub_fallback = v);
            }
            else
            {
                init.rhub = true;
                init.rhub_fallback = true;
            }

            if (init.rhub)
                update<int>("cache_time", v => init.cache_time = v);

            IsKitConf = true;

            if (func != null)
                return func.Invoke(conf, init, conf.ToObject<T>());

            InvkEvent.LoadKit(new EventLoadKit(defaultinit, init, conf, requestInfo, hybridCache));

            return init;
        }
        #endregion

        #region RedirectToPlay
        public RedirectResult RedirectToPlay(string url)
        {
            if (!url.Contains(" "))
                return new RedirectResult(url);

            return new RedirectResult(url.Split(" ")[0].Trim());
        }
        #endregion

        #region ContentTo / Dispose
        public ActionResult ContentTo(in string html)
        {
            return Content(html, html.StartsWith("{") || html.StartsWith("[") ? "application/json; charset=utf-8" : "text/html; charset=utf-8");
        }

        public new void Dispose()
        {
            serviceScope?.Dispose();
            base.Dispose();
        }
        #endregion
    }
}
