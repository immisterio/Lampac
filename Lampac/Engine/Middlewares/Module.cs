using Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shared.Models.Module;

namespace Lampac.Engine.Middlewares
{
    public class Module
    {
        private readonly RequestDelegate _next;
        IMemoryCache memoryCache;

        public Module(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            memoryCache = mem;
        }


        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (AppInit.modules != null)
            {
                foreach (RootModule mod in AppInit.modules.Where(i => i.middlewares != null))
                {
                    try
                    {
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
                            if (t.GetMethod("Invoke") is MethodInfo m2)
                            {
                                bool next = (bool)m2.Invoke(null, new object[] { httpContext, memoryCache });
                                if (!next)
                                    return;
                            }
                            
                            if (t.GetMethod("InvokeAsync") is MethodInfo m)
                            {
                                bool next = await (Task<bool>)m.Invoke(null, new object[] { httpContext, memoryCache });
                                if (!next)
                                    return;
                            }
                        }
                    }
                    catch { }
                }
            }

            await _next(httpContext);
        }
    }
}
