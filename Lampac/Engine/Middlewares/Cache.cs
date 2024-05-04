using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Model.SISI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class Cache
    {
        private readonly RequestDelegate _next;
        IMemoryCache memoryCache;

        public Cache(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            memoryCache = mem;
        }

        public Task Invoke(HttpContext httpContext)
        {
            if (!AppInit.conf.multiaccess)
                return _next(httpContext);

            var gbc = new ResponseCache();

            if (memoryCache.TryGetValue(gbc.ErrorKey(httpContext), out object errorCache))
            {
                httpContext.Response.Headers.TryAdd("X-RCache", "true");

                if (errorCache is OnErrorResult)
                {
                    httpContext.Response.ContentType = "application/json; charset=utf-8";
                    return httpContext.Response.WriteAsJsonAsync(errorCache);
                }
                else if (errorCache is string)
                {
                    string msg = errorCache.ToString();
                    if (!string.IsNullOrEmpty(msg))
                        httpContext.Response.Headers.TryAdd("emsg", errorCache.ToString());
                }

                return httpContext.Response.WriteAsync(string.Empty);
            }

            return _next(httpContext);
        }
    }
}
