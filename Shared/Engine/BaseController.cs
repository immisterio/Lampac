using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Lampac.Engine
{
    public class BaseController : Controller, IDisposable
    {
        IServiceScope serviceScope;

        public IMemoryCache memoryCache { get; private set; }

        public string host => AppInit.Host(HttpContext);

        public BaseController()
        {
            serviceScope = Startup.ApplicationServices.CreateScope();
            var scopeServiceProvider = serviceScope.ServiceProvider;
            memoryCache = scopeServiceProvider.GetService<IMemoryCache>();
        }

        public JsonResult OnError(string msg)
        {
            return new JsonResult(new { success = false, msg });
        }

        async public ValueTask<string> mylocalip()
        {
            string key = "BaseController:mylocalip";
            if (!memoryCache.TryGetValue(key, out string userIp))
            {
                string ipinfo = await HttpClient.Get("https://ipinfo.io/", timeoutSeconds: 5);
                userIp = Regex.Match(ipinfo ?? string.Empty, "\"userIp\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(userIp))
                    return null;

                memoryCache.Set(key, userIp, DateTime.Now.AddHours(1));
            }

            return userIp;
        }

        public string HostImgProxy( int width, int height, string uri)
        {
            uri = ProxyLink.Encrypt(uri, HttpContext.Connection.RemoteIpAddress.ToString());
            string account_email = Regex.Match(HttpContext.Request.QueryString.Value, "(\\?|&)account_email=([^&]+)").Groups[2].Value;
            if (AppInit.conf.accsdb.enable && !string.IsNullOrWhiteSpace(account_email))
                uri = uri + (uri.Contains("?") ? "&" : "?") + $"account_email={account_email}";

            return $"{host}/proxyimg:{width}:{height}/{uri}";
        }

        public string HostStreamProxy(bool streamproxy, string uri)
        {
            if (streamproxy)
            {
                uri = ProxyLink.Encrypt(uri, HttpContext.Connection.RemoteIpAddress.ToString());
                string account_email = Regex.Match(HttpContext.Request.QueryString.Value, "(\\?|&)account_email=([^&]+)").Groups[2].Value;
                if (AppInit.conf.accsdb.enable && !string.IsNullOrWhiteSpace(account_email))
                    uri = uri + (uri.Contains("?") ? "&" : "?") + $"account_email={account_email}";

                return $"{host}/proxy/{uri}";
            }

            return uri;
        }

        public new void Dispose()
        {
            serviceScope?.Dispose();
            base.Dispose();
        }
    }
}
