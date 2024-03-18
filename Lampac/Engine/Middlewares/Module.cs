using Lampac.Models.Module;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
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
                        if (mod.assembly.GetType(mod.middlewares) is Type t)
                        {
                            if (t.GetMethod("Invoke") is MethodInfo m2)
                            {
                                bool next = (bool)m2.Invoke(null, new object[] { httpContext, memoryCache });
                                if (!next)
                                    return;
                            }
                            else if (t.GetMethod("InvokeAsync") is MethodInfo m)
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
