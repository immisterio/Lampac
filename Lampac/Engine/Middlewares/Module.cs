using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Events;
using Shared.Models.Module;
using System;
using System.Linq;
using System.Reflection;
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
            if (AppInit.modules != null)
            {
                foreach (RootModule mod in AppInit.modules.Where(i => i.middlewares != null))
                {
                    try
                    {
                        if (first && mod.version != 2)
                            continue;

                        Assembly assembly = null;

                        if (mod.dynamic)
                        {
                            string cacheKey = $"{mod.dll}:{mod.middlewares}";
                            if (!memoryCache.TryGetValue(cacheKey, out assembly))
                            {
                                assembly = CSharpEval.Compilation(mod);
                                var cacheEntryOptions = new MemoryCacheEntryOptions
                                {
                                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
                                };

                                if (assembly != null)
                                    memoryCache.Set(cacheKey, assembly, cacheEntryOptions);
                            }
                        }
                        else
                        {
                            assembly = mod.assembly;
                        }

                        if (assembly != null && assembly.GetType(mod.NamespacePath(mod.middlewares)) is Type t)
                        {
                            if (mod.version == 2)
                            {
                                if (t.GetMethod("Invoke") is MethodInfo m2)
                                {
                                    bool next = (bool)m2.Invoke(null, [first, _next, httpContext, memoryCache]);
                                    if (!next)
                                        return;
                                }

                                if (t.GetMethod("InvokeAsync") is MethodInfo m)
                                {
                                    bool next = await (Task<bool>)m.Invoke(null, [first, _next, httpContext, memoryCache]);
                                    if (!next)
                                        return;
                                }
                            }
                            else
                            {
                                if (t.GetMethod("Invoke") is MethodInfo m2)
                                {
                                    bool next = (bool)m2.Invoke(null, [httpContext, memoryCache]);
                                    if (!next)
                                        return;
                                }

                                if (t.GetMethod("InvokeAsync") is MethodInfo m)
                                {
                                    bool next = await (Task<bool>)m.Invoke(null, [httpContext, memoryCache]);
                                    if (!next)
                                        return;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            #endregion

            if (InvkEvent.conf?.Middleware != null)
            {
                var rqinfo = first ? new RequestModel() : httpContext.Features.Get<RequestModel>();
                await InvkEvent.Middleware(first, new EventMiddleware(_next, rqinfo, httpContext.Request, httpContext, new HybridCache(), memoryCache));
                return;
            }

            await _next(httpContext);
        }
    }
}
