using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Lampac.Engine
{
    public class BaseController : Controller, IDisposable
    {
        IServiceScope serviceScope;

        public IMemoryCache memoryCache { get; private set; }

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

        public new void Dispose()
        {
            serviceScope?.Dispose();
            base.Dispose();
        }
    }
}
