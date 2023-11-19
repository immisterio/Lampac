using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Model.Base;

namespace Lampac.Engine
{
    public class BaseController : Controller, IDisposable
    {
        IServiceScope serviceScope;

        public static string appversion => "86";

        public IMemoryCache memoryCache { get; private set; }

        public string host => AppInit.Host(HttpContext);

        public BaseController()
        {
            serviceScope = Startup.ApplicationServices.CreateScope();
            var scopeServiceProvider = serviceScope.ServiceProvider;
            memoryCache = scopeServiceProvider.GetService<IMemoryCache>();
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

        public string HostImgProxy( int width, int height, string uri, List<(string name, string val)> headers = null)
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

        public string HostStreamProxy(Istreamproxy conf, string uri, List<(string name, string val)> headers = null, WebProxy proxy = null)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            if (conf == null || conf.streamproxy || conf.useproxystream)
            {
                uri = ProxyLink.Encrypt(uri, HttpContext.Connection.RemoteIpAddress.ToString(), headers, conf != null && conf.useproxystream ? proxy : null);

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

        async public ValueTask<T> InvokeCache<T>(string key, int min, Func<ValueTask<T>> onget)
        {
            if (memoryCache.TryGetValue(key, out T val))
                return val;

            val = await onget.Invoke();
            if (val == null || val.Equals(default(T)))
                return default;

            memoryCache.Set(key, val, DateTime.Now.AddMinutes(min));
            return val;
        }

        public new void Dispose()
        {
            serviceScope?.Dispose();
            base.Dispose();
        }
    }
}
