using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Events;
using Shared.Models.Module.Entrys;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class Module
    {
        private readonly RequestDelegate _next;
        IMemoryCache memoryCache;
        private readonly bool first;

        public Module(RequestDelegate next, IMemoryCache mem, bool first)
        {
            _next = next;
            memoryCache = mem;
            this.first = first;
        }

        async public Task InvokeAsync(HttpContext httpContext)
        {
            #region modules
            MiddlewaresModuleEntry.EnsureCache();

            if (MiddlewaresModuleEntry.middlewareModulesCache != null && MiddlewaresModuleEntry.middlewareModulesCache.Count > 0)
            {
                foreach (var entry in MiddlewaresModuleEntry.middlewareModulesCache)
                {
                    var mod = entry.mod;

                    try
                    {
                        if (first && (mod.version == 0 || mod.version == 1))
                            continue;

                        if (mod.version >= 2)
                        {
                            if (entry.Invoke != null)
                            {
                                bool next = entry.Invoke(first, httpContext, memoryCache);
                                if (!next)
                                    return;
                            }

                            if (entry.InvokeAsync != null)
                            {
                                bool next = await entry.InvokeAsync(first, httpContext, memoryCache);
                                if (!next)
                                    return;
                            }
                        }
                        else
                        {
                            if (entry.InvokeV1 != null)
                            {
                                bool next = entry.InvokeV1(httpContext, memoryCache);
                                if (!next)
                                    return;
                            }

                            if (entry.InvokeAsyncV1 != null)
                            {
                                bool next = await entry.InvokeAsyncV1(httpContext, memoryCache);
                                if (!next)
                                    return;
                            }
                        }
                    }
                    catch { }
                }
            }
            #endregion 

            if ((first && InvkEvent.conf?.Middleware?.first != null) || (!first && InvkEvent.conf?.Middleware?.end != null))
            {
                var rqinfo = httpContext.Features.Get<RequestModel>();
                bool next = await InvkEvent.Middleware(first, new EventMiddleware(rqinfo, httpContext.Request, httpContext, new HybridCache(), memoryCache));
                if (!next)
                    return;
            }

            await _next(httpContext);
        }
    }
}
