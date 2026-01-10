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
using Shared.Models.SISI.OnResult;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using IO = System.IO;

namespace Shared
{
    public class BaseController : Controller
    {
        public static string appversion => "151";

        public static string minorversion => "9";


        protected static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreLocks = new();

        protected ActionResult badInitMsg { get; set; }

        #region hybridCache
        private HybridCache _hybridCache;

        protected HybridCache hybridCache
        {
            get
            {
                if (_hybridCache == null)
                    _hybridCache = new HybridCache(requestInfo);

                return _hybridCache;
            }
        }
        #endregion

        #region memoryCache
        private IMemoryCache _memoryCache;

        protected IMemoryCache memoryCache
        {
            get
            {
                if (_memoryCache != null)
                    return _memoryCache;

                var httpContext = HttpContext;
                if (httpContext == null)
                    throw new InvalidOperationException(
                        "HttpContext is not available. MemoryCache can only be accessed during an HTTP request.");

                _memoryCache = httpContext.RequestServices
                    .GetRequiredService<IMemoryCache>();

                return _memoryCache;
            }
        }
        #endregion

        #region requestInfo
        private RequestModel _requestInfo;

        protected RequestModel requestInfo
        {
            get
            {
                if (_requestInfo == null)
                    _requestInfo = HttpContext.Features.Get<RequestModel>();

                return _requestInfo;
            }
        }
        #endregion

        #region host
        private string _host;

        public string host 
        { 
            get 
            {
                if (_host == null)
                    _host = AppInit.Host(HttpContext);

                return _host; 
            } 
        }
        #endregion


        #region mylocalip
        static string lastMyIp = null;

        async public ValueTask<string> mylocalip()
        {
            string key = "BaseController:mylocalip";
            if (!memoryCache.TryGetValue(key, out string userIp))
            {
                if (InvkEvent.IsMyLocalIp())
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
                return null;

            bool changeHeaders = false;
            var tempHeaders = new Dictionary<string, string>(_headers.Count);

            string ip = requestInfo.IP;
            string account_email = HttpContext.Request.Query["account_email"].ToString()?.ToLower().Trim() ?? string.Empty;

            foreach (var h in _headers)
            {
                if (string.IsNullOrEmpty(h.val) || string.IsNullOrEmpty(h.name))
                    continue;

                string val = h.val;

                if (val.Contains("{account_email}"))
                {
                    changeHeaders = true;
                    val = val.Replace("{account_email}", account_email);
                }

                if (val.Contains("{ip}"))
                {
                    changeHeaders = true;
                    val = val.Replace("{ip}", ip);
                }

                if (val.Contains("{host}"))
                {
                    changeHeaders = true;
                    val = val.Replace("{host}", site);
                }

                if (val.StartsWith("encrypt:"))
                {
                    changeHeaders = true;
                    string encrypt = Regex.Match(val, "^encrypt:([^\n\r]+)").Groups[1].Value;
                    val = BaseSettings.BaseDecrypt(encrypt);
                }

                if (val.Contains("{arg:"))
                {
                    changeHeaders = true;
                    foreach (Match m in Regex.Matches(val, "\\{arg:([^\\}]+)\\}"))
                    {
                        string _a = Regex.Match(HttpContext.Request.QueryString.Value, $"&{m.Groups[1].Value}=([^&]+)").Groups[1].Value;
                        val = val.Replace(m.Groups[0].Value, _a);
                    }
                }

                if (val.Contains("{head:"))
                {
                    changeHeaders = true;
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

                tempHeaders[h.name] = val;
            }

            if (InvkEvent.IsHttpHeaders())
            {
                var eventHeaders = InvkEvent.HttpHeaders(new EventControllerHttpHeaders(site, tempHeaders, requestInfo, HttpContext.Request, HttpContext));
                if (eventHeaders != null)
                {
                    changeHeaders = true;
                    tempHeaders = eventHeaders;
                }
            }

            if (changeHeaders)
                return HeadersModel.InitOrNull(tempHeaders);

            return _headers;
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

            if (plugin != null && init.proxyimg_disable != null && init.proxyimg_disable.Contains(plugin))
                return uri;

            if (InvkEvent.IsHostImgProxy())
            {
                string eventUri = InvkEvent.HostImgProxy(requestInfo, HttpContext, uri, height, headers, plugin);
                if (eventUri != null)
                    return eventUri;
            }

            if (width == 0 && height == 0 || plugin != null && init.rsize_disable != null && init.rsize_disable.Contains(plugin))
            {
                if (!string.IsNullOrEmpty(init.bypass_host))
                {
                    string bypass_host = init.bypass_host
                        .Replace("{sheme}", uri.StartsWith("https:") ? "https" : "http")
                        .Replace("{uri}", Regex.Replace(uri, "^https?://", ""));

                    if (bypass_host.Contains("{encrypt_uri}"))
                        bypass_host = bypass_host.Replace("{encrypt_uri}", ImgProxyToEncryptUri(HttpContext, uri, plugin, requestInfo.IP, headers));

                    return bypass_host;
                }

                return $"{host}/proxyimg/{ImgProxyToEncryptUri(HttpContext, uri, plugin, requestInfo.IP, headers)}";
            }

            if (!string.IsNullOrEmpty(init.rsize_host))
            {
                string rsize_host = init.rsize_host
                    .Replace("{width}", width.ToString())
                    .Replace("{height}", height.ToString())
                    .Replace("{sheme}", uri.StartsWith("https:") ? "https" : "http")
                    .Replace("{uri}", Regex.Replace(uri, "^https?://", ""));

                if (rsize_host.Contains("{encrypt_uri}"))
                    rsize_host = rsize_host.Replace("{encrypt_uri}", ImgProxyToEncryptUri(HttpContext, uri, plugin, requestInfo.IP, headers));

                return rsize_host;
            }

            return $"{host}/proxyimg:{width}:{height}/{ImgProxyToEncryptUri(HttpContext, uri, plugin, requestInfo.IP, headers)}";
        }

        static string ImgProxyToEncryptUri(HttpContext httpContext, string uri, string plugin, string ip, List<HeadersModel> headers)
        {
            var _head = headers != null && headers.Count > 0 ? headers : null;

            string encrypt_uri = ProxyLink.Encrypt(uri, ip, _head, plugin: plugin, verifyip: false, ex: DateTime.Now.AddMinutes(20), IsProxyImg: true);
            if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                encrypt_uri = AccsDbInvk.Args(encrypt_uri, httpContext);

            return encrypt_uri;
        }
        #endregion

        #region HostStreamProxy
        public string HostStreamProxy(BaseSettings conf, string uri, List<HeadersModel> headers = null, WebProxy proxy = null, bool force_streamproxy = false, RchClient rch = null)
        {
            if (!AppInit.conf.serverproxy.enable || string.IsNullOrEmpty(uri) || conf == null)
                return uri?.Split(" ")?[0]?.Trim();

            if (InvkEvent.IsHostStreamProxy())
            {
                string _eventUri = InvkEvent.HostStreamProxy(new EventHostStreamProxy(conf, uri, headers, proxy, requestInfo, HttpContext, hybridCache));
                if (_eventUri != null)
                    return _eventUri;
            }

            if (conf.rhub && !conf.rhub_streamproxy)
                return uri.Split(" ")[0].Trim();

            bool streamproxy = conf.streamproxy || conf.apnstream || conf.useproxystream || force_streamproxy;

            #region geostreamproxy
            if (!streamproxy && conf.geostreamproxy != null && conf.geostreamproxy.Length > 0)
            {
                string country = requestInfo.Country;
                if (!string.IsNullOrEmpty(country) && country.Length == 2)
                {
                    if (conf.geostreamproxy.Contains("ALL") || conf.geostreamproxy.Contains(country))
                        streamproxy = true;
                }
            }
            #endregion

            #region rchstreamproxy
            if (!streamproxy && conf.rchstreamproxy != null && rch != null)
            {
                var rchinfo = rch.InfoConnected();
                if (rchinfo?.rchtype != null)
                    streamproxy = conf.rchstreamproxy.Contains(rchinfo.rchtype);
            }
            #endregion

            if (streamproxy)
            {
                if (AppInit.conf.serverproxy.forced_apn || conf.apnstream)
                {
                    if (!string.IsNullOrEmpty(conf.apn?.host) && conf.apn.host.StartsWith("http"))
                        return apnlink(conf.apn, uri, requestInfo.IP, headers);

                    if (!string.IsNullOrEmpty(AppInit.conf?.apn?.host) && AppInit.conf.apn.host.StartsWith("http"))
                        return apnlink(AppInit.conf.apn, uri, requestInfo.IP, headers);

                    return uri;
                }

                if (headers == null && conf.headers_stream != null && conf.headers_stream.Count > 0)
                    headers = HeadersModel.Init(conf.headers_stream);

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

        static string apnlink(ApnConf apn, string uri, string ip, List<HeadersModel> headers)
        {
            string link = uri.Split(" ")[0].Split("#")[0].Trim();

            if (apn.secure == "nginx")
            {
                using (MD5 md5 = MD5.Create())
                {
                    long ex = ((DateTimeOffset)DateTime.Now.AddHours(12)).ToUnixTimeSeconds();
                    string hash = Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes($"{ex}{ip} {apn.secret}"))).Replace("=", "").Replace("+", "-").Replace("/", "_");

                    return $"{apn.host}/{hash}:{ex}/{link}";
                }
            }
            else if (apn.secure == "cf")
            {
                using (var sha1 = SHA1.Create())
                {
                    var data = Encoding.UTF8.GetBytes($"{ip}{link}{apn.secret}");
                    return Convert.ToBase64String(sha1.ComputeHash(data));
                }
            }
            else if (apn.secure == "lampac")
            {
                string aes = AesTo.Encrypt(System.Text.Json.JsonSerializer.Serialize(new
                {
                    u = link,
                    i = ip,
                    v = true,
                    e = DateTime.Now.AddHours(36),
                    h = headers?.ToDictionary()
                }));

                if (uri.Contains(".m3u"))
                    aes += ".m3u8";

                return $"{apn.host}/proxy/{aes}";
            }

            if (apn.host.Contains("{encode_uri}") || apn.host.Contains("{uri}"))
                return apn.host.Replace("{encode_uri}", HttpUtility.UrlEncode(link)).Replace("{uri}", link);

            return $"{apn.host}/{link}";
        }
        #endregion

        #region InvokeBaseCache
        async public ValueTask<T> InvokeBaseCache<T>(string key, TimeSpan time, RchClient rch, Func<Task<T>> onget, ProxyManager proxyManager = null, bool? memory = null)
        {
            var semaphore = new SemaphorManager(key, TimeSpan.FromSeconds(40));

            try
            {
                if (rch?.enable != true)
                    await semaphore.WaitAsync();

                if (hybridCache.TryGetValue(key, out T val, memory))
                {
                    HttpContext.Response.Headers["X-Invoke-Cache"] = "HIT";
                    return val;
                }

                HttpContext.Response.Headers["X-Invoke-Cache"] = "MISS";

                val = await onget.Invoke();
                if (val == null || val.Equals(default(T)))
                    return default;

                if (rch?.enable != true)
                    proxyManager?.Success();

                hybridCache.Set(key, val, time, memory);
                return val;
            }
            finally
            {
                semaphore.Release();
            }
        }
        #endregion

        #region InvokeBaseCacheResult
        async public ValueTask<CacheResult<T>> InvokeBaseCacheResult<T>(string key, TimeSpan time, RchClient rch, ProxyManager proxyManager, Func<CacheResult<T>, Task<CacheResult<T>>> onget, bool? memory = null)
        {
            var semaphore = new SemaphorManager(key, TimeSpan.FromSeconds(40));

            try
            {
                if (rch?.enable != true)
                    await semaphore.WaitAsync();

                if (hybridCache.TryGetValue(key, out T _val, memory))
                {
                    HttpContext.Response.Headers["X-Invoke-Cache"] = "HIT";
                    return new CacheResult<T>() { IsSuccess = true, Value = _val };
                }

                HttpContext.Response.Headers["X-Invoke-Cache"] = "MISS";

                var val = await onget.Invoke(new CacheResult<T>());

                if (val == null || val.Value == null)
                    return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "null" };

                if (!val.IsSuccess)
                {
                    if (val.refresh_proxy && rch?.enable != true)
                        proxyManager?.Refresh();

                    return val;
                }

                if (val.Value.Equals(default(T)))
                {
                    if (val.refresh_proxy && rch?.enable != true)
                        proxyManager?.Refresh();

                    return val;
                }

                if (typeof(T) == typeof(string) && string.IsNullOrWhiteSpace(val.ToString()))
                {
                    if (val.refresh_proxy && rch?.enable != true)
                        proxyManager?.Refresh();

                    return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "empty" };
                }

                if (rch?.enable != true)
                    proxyManager?.Success();

                hybridCache.Set(key, val.Value, time, memory);
                return new CacheResult<T>() { IsSuccess = true, Value = val.Value };
            }
            finally
            {
                semaphore.Release();
            }
        }
        #endregion

        #region InvkSemaphore
        async public Task<ActionResult> InvkSemaphore(string key, RchClient rch, Func<Task<ActionResult>> func)
        {
            if (rch?.enable == true)
                return await func.Invoke();

            var semaphore = new SemaphorManager(key, TimeSpan.FromSeconds(40));

            try
            {
                await semaphore.WaitAsync();
                return await func.Invoke();
            }
            finally
            {
                semaphore.Release();
            }
        }
        #endregion

        #region cacheTime
        public TimeSpan cacheTimeBase(int multiaccess, int home = 5, int mikrotik = 2, BaseSettings init = null, int rhub = -1)
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
        public bool IsCacheError(BaseSettings init, RchClient rch)
        {
            if (!AppInit.conf.multiaccess || init.rhub)
                return false;

            if (rch?.enable == true)
                return false;

            if (memoryCache.TryGetValue(ResponseCache.ErrorKey(HttpContext), out object errorCache))
            {
                HttpContext.Response.Headers.TryAdd("X-RCache", "true");

                if (errorCache is OnErrorResult)
                {
                    HttpContext.Response.StatusCode = 503;
                    badInitMsg = Json(errorCache);
                    return true;
                }
                else if (errorCache is string)
                {
                    string msg = errorCache.ToString();
                }

                badInitMsg = StatusCode(503);
                return true;
            }

            return false;
        }
        #endregion

        #region IsOverridehost
        public bool IsOverridehost(BaseSettings init)
        {
            if (!string.IsNullOrEmpty(init.overridehost))
                return true;

            if (init.overridehosts != null && init.overridehosts.Length > 0) 
                return true;

            return true;
        }

        async public Task<ActionResult> InvokeOverridehost(BaseSettings init)
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
                    error_msg = AppInit.conf.accsdb.denyGroupMesage
                        .Replace("{account_email}", requestInfo.user_uid)
                        .Replace("{user_uid}", requestInfo.user_uid);

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

        bool IsLoadKitConf()
        {
            var init = AppInit.conf.kit;
            if (!init.enable || string.IsNullOrEmpty(init.path) || string.IsNullOrEmpty(requestInfo.user_uid))
                return false;

            return true;
        }

        async public ValueTask<JObject> loadKitConf()
        {
            if (IsLoadKitConf())
            {
                var kit_init = AppInit.conf.kit;

                if (kit_init.path.StartsWith("http") && !kit_init.IsAllUsersPath)
                    return await loadHttpKitConf();
                else
                    return loadFileKitConf();
            }

            return null;
        }

        JObject loadFileKitConf()
        {
            var init = AppInit.conf.kit;

            if (init.IsAllUsersPath)
            {
                if (init.allUsers != null && init.allUsers.TryGetValue(requestInfo.user_uid, out JObject userInit))
                    return userInit;

                return null;
            }
            else
            {
                string memKey = $"loadFileKit:{requestInfo.user_uid}";
                if (!memoryCache.TryGetValue(memKey, out JObject appinit))
                {
                    try
                    {
                        string init_file = $"{init.path}/{CrypTo.md5(requestInfo.user_uid)}";

                        if (init.eval_path != null)
                            init_file = CSharpEval.Execute<string>(init.eval_path, new KitConfEvalPath(init.path, requestInfo.user_uid));

                        if (!IO.File.Exists(init_file))
                            return null;

                        string json = IO.File.ReadAllText(init_file);

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

        async Task<JObject> loadHttpKitConf()
        {
            var init = AppInit.conf.kit;

            string memKey = $"loadHttpKit:{requestInfo.user_uid}";
            if (!memoryCache.TryGetValue(memKey, out JObject appinit))
            {
                try
                {
                    string uri = init.path.Replace("{uid}", HttpUtility.UrlEncode(requestInfo.user_uid));
                    string json = await Http.Get(uri, timeoutSeconds: 5);

                    if (json == null)
                        return null;

                    if (!json.TrimStart().StartsWith("{"))
                        json = "{" + json + "}";

                    appinit = JsonConvert.DeserializeObject<JObject>(json);
                }
                catch { return null; }

                memoryCache.Set(memKey, appinit, DateTime.Now.AddSeconds(Math.Max(5, init.cacheToSeconds)));
            }

            return appinit;
        }

        public bool IsLoadKit<T>(T _init) where T : BaseSettings
        {
            if (InvkEvent.IsLoadKitInit() || InvkEvent.IsLoadKit())
                return true;

            if (IsLoadKitConf())
                return _init.kit;

            return false;
        }

        async public ValueTask<T> loadKit<T>(T _init, Func<JObject, T, T, T> func = null) where T : BaseSettings, ICloneable
        {
            var clone = _init.IsCloneable ? _init : (T)_init.Clone();

            if (_init.kit == false)
            {
                if (InvkEvent.IsLoadKitInit())
                    InvkEvent.LoadKitInit(new EventLoadKit(null, clone, null, requestInfo, hybridCache));

                return clone;
            }

            JObject appinit = null;

            if (IsLoadKitConf())
            {
                var kit_init = AppInit.conf.kit;

                if (kit_init.path.StartsWith("http") && !kit_init.IsAllUsersPath)
                    appinit = await loadHttpKitConf();
                else
                    appinit = loadFileKitConf();
            }

            return loadKit(clone, appinit, func, false);
        }

        public T loadKit<T>(T _init, JObject appinit, Func<JObject, T, T, T> func = null, bool clone = true) where T : BaseSettings, ICloneable
        {
            var init = clone ? (T)_init.Clone() : _init;
            init.IsKitConf = false;
            init.IsCloneable = true;

            var defaultinit = InvkEvent.IsLoadKitInit() || InvkEvent.IsLoadKit()
                ? (clone ? _init : (T)_init.Clone()) 
                : null;

            if (InvkEvent.IsLoadKitInit())
                InvkEvent.LoadKitInit(new EventLoadKit(defaultinit, init, appinit, requestInfo, hybridCache));

            if (!init.kit || appinit == null || string.IsNullOrEmpty(init.plugin) || !appinit.ContainsKey(init.plugin))
            {
                if (InvkEvent.IsLoadKit())
                    InvkEvent.LoadKit(new EventLoadKit(defaultinit, init, appinit, requestInfo, hybridCache));

                return init;
            }

            var conf = appinit.Value<JObject>(init.plugin);

            if (AppInit.conf.kit.absolute)
            {
                foreach (var prop in conf.Properties())
                {
                    try
                    {
                        var propertyInfo = typeof(T).GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (propertyInfo?.CanWrite != true)
                            continue;

                        var value = prop.Value.ToObject(propertyInfo.PropertyType);
                        propertyInfo.SetValue(init, value);
                    }
                    catch { }
                }
            }
            else
            {
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
                update<bool>("qualitys_proxy", v => init.qualitys_proxy = v);
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
            }

            IsKitConf = true;
            init.IsKitConf = true;

            if (InvkEvent.IsLoadKit())
                InvkEvent.LoadKit(new EventLoadKit(defaultinit, init, conf, requestInfo, hybridCache));

            if (func != null)
                return func.Invoke(conf, init, conf.ToObject<T>());

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

        #region ContentTo
        public ActionResult ContentTo(string html)
        {
            return Content(html, html.StartsWith("{") || html.StartsWith("[") ? "application/json; charset=utf-8" : "text/html; charset=utf-8");
        }
        #endregion

        #region ipkey
        public string ipkey(string key, ProxyManager proxy, RchClient rch)
        {
            if (rch != null)
                return $"{key}:{(rch.enable ? requestInfo.IP : proxy?.CurrentProxyIp)}";

            return $"{key}:{proxy?.CurrentProxyIp}";
        }

        public string ipkey(string key, RchClient rch) => rch?.enable == true ? $"{key}:{requestInfo.IP}" : key;
        #endregion

        #region headerKeys
        static readonly ThreadLocal<StringBuilder> sbHeaderKeys = new(() => new StringBuilder(PoolInvk.rentLargeChunk));

        public string headerKeys(string key, ProxyManager proxy, RchClient rch, params string[] headersKey)
        {
            if (rch?.enable != true)
                return $"{key}:{proxy?.CurrentProxyIp}";

            var value = sbHeaderKeys.Value;
            value.Clear();

            const char splitKey = ':';

            value.Append(key);
            value.Append(splitKey);

            foreach (string hk in headersKey)
            {
                if (HttpContext.Request.Headers.TryGetValue(hk, out var headerValue))
                {
                    value.Append(headerValue);
                    value.Append(splitKey);
                }
            }

            return value.ToString();
        }
        #endregion
    }
}
