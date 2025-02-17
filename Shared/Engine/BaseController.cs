using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine.CORE;
using Lampac.Models.LITE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Model.SISI;
using Shared.Models;
using IO = System.IO;

namespace Lampac.Engine
{
    public class BaseController : Controller, IDisposable
    {
        IServiceScope serviceScope;

        public static string appversion => "135";

        public static string minorversion => "1";

        public HybridCache hybridCache { get; private set; }

        public IMemoryCache memoryCache { get; private set; }

        public RequestModel requestInfo => HttpContext?.Features?.Get<RequestModel>();

        public string host => AppInit.Host(HttpContext);

        public BaseController()
        {
            hybridCache = new HybridCache();

            serviceScope = Startup.ApplicationServices.CreateScope();
            var scopeServiceProvider = serviceScope.ServiceProvider;
            memoryCache = scopeServiceProvider.GetService<IMemoryCache>();
        }

        async public ValueTask<string> mylocalip()
        {
            string key = "BaseController:mylocalip";
            if (!hybridCache.TryGetValue(key, out string userIp))
            {
                var myip = await HttpClient.Get<JObject>("https://api.ipify.org/?format=json");
                if (myip == null || string.IsNullOrWhiteSpace(myip.Value<string>("ip")))
                    return null;

                userIp = myip.Value<string>("ip");
                hybridCache.Set(key, userIp, DateTime.Now.AddMinutes(20));
            }

            return userIp;
        }

        #region httpHeaders
        public List<HeadersModel> httpHeaders(BaseSettings init, List<HeadersModel> _startHeaders = null)
        {
            var headers = HeadersModel.Init(_startHeaders);
            if (init.headers == null)
                return headers;

            string ip = requestInfo.IP;
            string account_email = HttpContext.Request.Query["account_email"].ToString() ?? string.Empty;

            foreach (var h in init.headers)
            {
                if (string.IsNullOrEmpty(h.Value) || string.IsNullOrEmpty(h.Key))
                    continue;

                string val = h.Value;

                if (val.Contains("{encrypt:"))
                {
                    string encrypt = Regex.Match(val, "\\{encrypt:([^\\}]+)").Groups[1].Value;
                    val = new OnlinesSettings(null, encrypt).host;
                }

                val = val.Replace("{account_email}", account_email)
                         .Replace("{ip}", ip)
                         .Replace("{host}", init.host);

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
                            val = val.Replace(m.Groups[0].Value, string.Join(" ", _h));
                        }
                        else
                        {
                            val = val.Replace(m.Groups[0].Value, string.Empty);
                        }

                        string _a = Regex.Match(HttpContext.Request.QueryString.Value, $"&{m.Groups[1].Value}=([^&]+)").Groups[1].Value;
                        val = val.Replace(m.Groups[0].Value, _a);
                    }
                }

                if (headers.Find(i => i.name == h.Key) == null)
                    headers.Add(new HeadersModel(h.Key, val));
            }

            return headers;
        }
        #endregion

        #region proxy
        public string HostImgProxy(string uri, int width = 0, int height = 0, List<HeadersModel> headers = null, string plugin = null)
        {
            if (!AppInit.conf.sisi.rsize || string.IsNullOrWhiteSpace(uri)) 
                return uri;

            var init = AppInit.conf.sisi;
            width = Math.Max(width, init.widthPicture);
            height = Math.Max(height, init.heightPicture);

            if (plugin != null && init.rsize_disable != null && init.rsize_disable.Contains(plugin))
                return uri;

            if (!string.IsNullOrEmpty(init.rsize_host))
            {
                string sheme = uri.StartsWith("https:") ? "https" : "http";
                return init.rsize_host.Replace("{width}", width.ToString()).Replace("{height}", height.ToString())
                           .Replace("{sheme}", sheme).Replace("{uri}", Regex.Replace(uri, "^https?://", ""));
            }

            uri = ProxyLink.Encrypt(uri, requestInfo.IP, headers);

            if (AppInit.conf.accsdb.enable)
                uri = AccsDbInvk.Args(uri, HttpContext);

            return $"{host}/proxyimg:{width}:{height}/{uri}";
        }

        public string HostStreamProxy(BaseSettings conf, string uri, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            if (!AppInit.conf.serverproxy.enable || string.IsNullOrEmpty(uri) || conf == null)
                return uri;

            if (conf.rhub && !conf.rhub_streamproxy)
                return uri;

            bool streamproxy = conf.streamproxy || conf.useproxystream;
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
                #region apnstream
                string apnlink(ApnConf apn)
                {
                    if (apn.secure == "nginx")
                    {
                        using (MD5 md5 = MD5.Create())
                        {
                            long ex = ((DateTimeOffset)DateTime.Now.AddHours(12)).ToUnixTimeSeconds();
                            string hash = Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes($"{ex}{requestInfo.IP} {apn.secret}"))).Replace("=", "").Replace("+", "-").Replace("/", "_");

                            return $"{apn.host}/{hash}:{ex}/{uri}";
                        }
                    }
                    else if (apn.secure == "cf")
                    {
                        using (var sha1 = SHA1.Create())
                        {
                            var data = Encoding.UTF8.GetBytes($"{requestInfo.IP}{uri}{apn.secret}");
                            return Convert.ToBase64String(sha1.ComputeHash(data));
                        }
                    }

                    return $"{apn.host}/{uri}";
                }

                if (!string.IsNullOrEmpty(conf.apn?.host) && conf.apn.host.StartsWith("http"))
                    return apnlink(conf.apn);

                if ((AppInit.conf.serverproxy.forced_apn || conf.apnstream) && !string.IsNullOrEmpty(AppInit.conf?.apn?.host) && AppInit.conf.apn.host.StartsWith("http"))
                    return apnlink(AppInit.conf.apn);
                #endregion

                if (conf.headers_stream != null && conf.headers_stream.Count > 0)
                    headers = HeadersModel.Init(conf.headers_stream);

                uri = ProxyLink.Encrypt(uri, requestInfo.IP, headers, conf != null && conf.useproxystream ? proxy : null, conf?.plugin);

                if (AppInit.conf.accsdb.enable)
                    uri = AccsDbInvk.Args(uri, HttpContext);

                return $"{host}/proxy/{uri}";
            }

            return uri;
        }
        #endregion

        #region cache
        public ValueTask<CacheResult<T>> InvokeCache<T>(string key, TimeSpan time, Func<CacheResult<T>, ValueTask<dynamic>> onget) => InvokeCache(key, time, null, onget);

        async public ValueTask<CacheResult<T>> InvokeCache<T>(string key, TimeSpan time, ProxyManager proxyManager, Func<CacheResult<T>, ValueTask<dynamic>> onget, bool inmemory = false)
        {
            if (hybridCache.TryGetValue(key, out T _val))
            {
                HttpContext.Response.Headers.TryAdd("X-InvokeCache", "HIT");
                return new CacheResult<T>() { IsSuccess = true, Value = _val };
            }

            HttpContext.Response.Headers.TryAdd("X-InvokeCache", "MISS");

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
            hybridCache.Set(key, val, time, inmemory);
            return new CacheResult<T>() { IsSuccess = true, Value = val };
        }

        async public ValueTask<T> InvokeCache<T>(string key, TimeSpan time, Func<ValueTask<T>> onget, ProxyManager proxyManager = null, bool inmemory = false)
        {
            if (hybridCache.TryGetValue(key, out T val, inmemory))
                return val;

            val = await onget.Invoke();
            if (val == null || val.Equals(default(T)))
                return default;

            proxyManager?.Success();
            hybridCache.Set(key, val, time, inmemory);
            return val;
        }

        public TimeSpan cacheTime(int multiaccess, int home = 5, int mikrotik = 2, BaseSettings init = null, int rhub = -1)
        {
            if (init != null && init.rhub && rhub != -1)
                return TimeSpan.FromMinutes(rhub);

            int ctime = AppInit.conf.mikrotik ? mikrotik : AppInit.conf.multiaccess ? (init != null && init.cache_time > 0 ? init.cache_time : multiaccess) : home;
            if (ctime > multiaccess)
                ctime = multiaccess;

            return TimeSpan.FromMinutes(ctime);
        }
        #endregion

        #region IsCacheError
        public bool IsCacheError(BaseSettings init, out ActionResult result)
        {
            result = null;
            if (!AppInit.conf.multiaccess || init.rhub)
                return false;

            var gbc = new ResponseCache();
            if (memoryCache.TryGetValue(gbc.ErrorKey(HttpContext), out object errorCache))
            {
                HttpContext.Response.Headers.TryAdd("X-RCache", "true");

                if (errorCache is OnErrorResult)
                {
                    result = Json(errorCache);
                    return true;
                }
                else if (errorCache is string)
                {
                    string msg = errorCache.ToString();
                    if (!string.IsNullOrEmpty(msg))
                        HttpContext.Response.Headers.TryAdd("emsg", HttpUtility.UrlEncode(CrypTo.Base64(msg)));
                }

                result = Ok();
                return true;
            }

            return false;
        }
        #endregion

        #region IsOverridehost
        public bool IsOverridehost(BaseSettings init, out string overridehost)
        {
            overridehost = null;

            if (!string.IsNullOrEmpty(init.overridehost))
                overridehost = init.overridehost;

            if (string.IsNullOrEmpty(overridehost) && init.overridehosts != null && init.overridehosts.Length > 0)
                overridehost = init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];

            if (string.IsNullOrEmpty(overridehost))
                return false;

            overridehost += HttpContext.Request.QueryString.Value;
            return true;
        }
        #endregion

        #region NoAccessGroup
        public bool NoAccessGroup(Igroup init, out string error_msg)
        {
            error_msg = null;

            if (!AppInit.conf.accsdb.enable || init.group == 0 || requestInfo.IsLocalRequest)
                return false;

            var user = requestInfo.user;
            if (user == null || init.group > user.group)
            {
                error_msg = AppInit.conf.accsdb.denyGroupMesage;
                return true;
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
        public T loadKit<T>(T init) where T : BaseSettings
        {
            if (!AppInit.conf.kit.enable || string.IsNullOrEmpty(AppInit.conf.kit.path))
                return init;

            string init_file = $"{AppInit.conf.kit.path}/{CrypTo.md5(requestInfo.user_uid)}";
            if (!IO.File.Exists(init_file))
                return init;

            JObject conf;

            try
            {
                var appinit = JsonConvert.DeserializeObject<JObject>(IO.File.ReadAllText(init_file));
                if (init.plugin == null || !appinit.ContainsKey(init.plugin))
                    return init;

                conf = appinit.Value<JObject>(init.plugin);
            }
            catch { return init; }

            void update<T>(string key, Action<T> updateAction)
            {
                if (conf.ContainsKey(key))
                    updateAction(conf.Value<T>(key));
            }

            update<bool>("enable", v => init.enable = v);
            update<string>("displayname", v => init.displayname = v);
            update<int>("displayindex", v => init.displayindex = v);

            update<string>("host", v => init.host = v);
            update<string>("apihost", v => init.apihost = v);
            update<string>("scheme", v => init.scheme = v);
            update<bool>("hls", v => init.hls = v);
            update<Dictionary<string, string>>("headers", v => init.headers = v);
            update<string>("overridehost", v => init.overridehost = v);

            init.apnstream = true;
            if (conf.ContainsKey("apn"))
                init.apn = conf["apn"].ToObject<ApnConf>();

            init.useproxystream = false;
            update<bool>("streamproxy", v => init.streamproxy = v);
            update<string[]>("geostreamproxy", v => init.geostreamproxy = v);

            if (conf.ContainsKey("proxy"))
            {
                init.proxy = conf["proxy"].ToObject<ProxySettings>();
                if (init?.proxy?.list != null && init.proxy.list.Count > 0)
                    update<bool>("useproxy", v => init.useproxy = v);
            }

            if (init.useproxy)
            {
                init.rhub = false;
                init.rhub_fallback = false;
            }
            else if (AppInit.conf.kit.rhub_fallback)
            {
                update<bool>("rhub", v => init.rhub = v);
                update<bool>("rhub_fallback", v => init.rhub_fallback = v);
            }

            return init;
        }
        #endregion


        #region ContentTo / Dispose
        public ActionResult ContentTo(string html)
        {
            return Content(html, ((html.StartsWith("{") || html.StartsWith("[")) ? "application/json; charset=utf-8" : "text/html; charset=utf-8"));
        }

        public new void Dispose()
        {
            serviceScope?.Dispose();
            base.Dispose();
        }
        #endregion
    }
}
