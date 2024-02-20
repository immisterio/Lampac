using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Models;

namespace Lampac.Engine
{
    public class BaseController : Controller, IDisposable
    {
        IServiceScope serviceScope;

        public static string appversion => "102";

        public HybridCache memoryCache { get; private set; }

        public string host => AppInit.Host(HttpContext);

        public BaseController()
        {
            memoryCache = new HybridCache();

            //serviceScope = Startup.ApplicationServices.CreateScope();
            //var scopeServiceProvider = serviceScope.ServiceProvider;
            //memoryCache = scopeServiceProvider.GetService<IMemoryCache>();
        }

        async public ValueTask<string> mylocalip()
        {
            string key = "BaseController:mylocalip";
            if (!memoryCache.TryGetValue(key, out string userIp))
            {
                var myip = await HttpClient.Get<JObject>($"{AppInit.conf.FilmixPartner.host}/my_ip");
                if (myip == null || string.IsNullOrWhiteSpace(myip.Value<string>("ip")))
                    return null;

                userIp = myip.Value<string>("ip");
                memoryCache.Set(key, userIp, DateTime.Now.AddMinutes(20));
            }

            return userIp;
        }

        #region httpHeaders
        public List<HeadersModel> httpHeaders(BaseSettings init, List<HeadersModel> _startHeaders = null)
        {
            var headers = HeadersModel.Init(_startHeaders);
            if (init.headers == null)
                return headers;

            string ip = HttpContext.Connection.RemoteIpAddress.ToString();
            string account_email = Regex.Match(HttpContext.Request.QueryString.Value, "&account_email=([^&]+)").Groups[1].Value;

            foreach (var h in init.headers)
            {
                if (string.IsNullOrEmpty(h.val) || string.IsNullOrEmpty(h.name))
                    continue;

                string val = h.name.Replace("{account_email}", account_email).Replace("{ip}", ip);

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

                headers.Add(new HeadersModel(h.val, val));
            }

            return headers;
        }
        #endregion

        #region proxy
        public string HostImgProxy(int width, int height, string uri, List<HeadersModel> headers = null)
        {
            if (string.IsNullOrWhiteSpace(uri)) 
                return null;

            uri = ProxyLink.Encrypt(uri, HttpContext.Connection.RemoteIpAddress.ToString(), headers);

            if (AppInit.conf.accsdb.enable)
            {
                string account_email = Regex.Match(HttpContext.Request.QueryString.Value, "(\\?|&)account_email=([^&]+)").Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(account_email))
                    uri = uri + (uri.Contains("?") ? "&" : "?") + $"account_email={account_email}";
            }

            return $"{host}/proxyimg:{width}:{height}/{uri}";
        }

        public string HostStreamProxy(Istreamproxy conf, string uri, List<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            if (conf == null)
                return uri;

            bool streamproxy = conf.streamproxy || conf.useproxystream;
            if (!streamproxy && conf.geostreamproxy != null && conf.geostreamproxy.Count > 0)
            {
                string country = GeoIP2.Country(HttpContext.Connection.RemoteIpAddress.ToString());
                if (country != null && conf.geostreamproxy.Contains(country))
                    streamproxy = true;
            }

            if (streamproxy)
            {
                if (!string.IsNullOrEmpty(conf.apn) && conf.apn.StartsWith("http"))
                    return $"{conf.apn}/{uri}";

                if (conf.apnstream && !string.IsNullOrEmpty(AppInit.conf.apn) && AppInit.conf.apn.StartsWith("http"))
                    return $"{AppInit.conf.apn}/{uri}";

                uri = ProxyLink.Encrypt(uri, HttpContext.Connection.RemoteIpAddress.ToString(), headers, conf != null && conf.useproxystream ? proxy : null, plugin);

                if (AppInit.conf.accsdb.enable)
                {
                    string account_email = Regex.Match(HttpContext.Request.QueryString.Value, "(\\?|&)account_email=([^&]+)").Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(account_email))
                        uri = uri + (uri.Contains("?") ? "&" : "?") + $"account_email={account_email}";
                }

                return $"{host}/proxy/{uri}";
            }

            return uri;
        }
        #endregion

        #region cache
        async public ValueTask<CacheResult<T>> InvokeCache<T>(string key, TimeSpan time, ProxyManager proxyManager, Func<CacheResult<T>, ValueTask<CacheResult<T>>> onget) 
        {
            if (memoryCache.TryGetValue(key, out T _val))
                return new CacheResult<T>() { IsSuccess = true, Value = _val };

            var cache = await onget.Invoke(new CacheResult<T>());
            if (cache == null || !cache.IsSuccess)
                return cache;

            proxyManager?.Success();
            memoryCache.Set(key, cache.Value, time);
            return cache;
        }

        async public ValueTask<T> InvokeCache<T>(string key, TimeSpan time, Func<ValueTask<T>> onget, ProxyManager proxyManager = null)
        {
            if (memoryCache.TryGetValue(key, out T val))
                return val;

            val = await onget.Invoke();
            if (val == null || val.Equals(default(T)))
                return default;

            proxyManager?.Success();
            memoryCache.Set(key, val, time);
            return val;
        }

        public TimeSpan cacheTime(int multiaccess, int home = 5, int mikrotik = 2)
        {
            int ctime = AppInit.conf.mikrotik ? mikrotik : AppInit.conf.multiaccess ? multiaccess : home;
            if (ctime > multiaccess)
                ctime = multiaccess;

            return TimeSpan.FromMinutes(ctime);
        }
        #endregion

        public new void Dispose()
        {
            serviceScope?.Dispose();
            base.Dispose();
        }
    }
}
