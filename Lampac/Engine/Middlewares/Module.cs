using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Events;
using Shared.Models.Module;
using System;
using System.Collections.Generic;
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
            ModuleEntry.EnsureCache();

            if (ModuleEntry.middlewareModulesCache != null && ModuleEntry.middlewareModulesCache.Count > 0)
            {
                foreach (var entry in ModuleEntry.middlewareModulesCache)
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


    public class ModuleEntry
    {
        public RootModule mod;
        public Func<bool, HttpContext, IMemoryCache, bool> Invoke = null;
        public Func<bool, HttpContext, IMemoryCache, Task<bool>> InvokeAsync = null;
        public Func<HttpContext, IMemoryCache, bool> InvokeV1 = null;
        public Func<HttpContext, IMemoryCache, Task<bool>> InvokeAsyncV1 = null;


        public static List<ModuleEntry> middlewareModulesCache = null;
        static readonly object _middlewareModulesCacheLock = new object();

        public static void EnsureCache()
        {
            if (middlewareModulesCache != null || AppInit.modules == null)
                return;

            lock (_middlewareModulesCacheLock)
            {
                if (middlewareModulesCache != null)
                    return;

                middlewareModulesCache = new List<ModuleEntry>();

                foreach (var mod in AppInit.modules.Where(i => i.middlewares != null))
                {
                    try
                    {
                        var entry = new ModuleEntry() { mod = mod };

                        Assembly assembly = mod.assembly;
                        if (assembly == null)
                            continue;

                        var type = assembly.GetType(mod.NamespacePath(mod.middlewares));
                        if (type == null)
                            continue;

                        if (mod.version >= 2)
                        {
                            try
                            {
                                var m = type.GetMethod("Invoke");
                                if (m != null)
                                {
                                    entry.Invoke = (Func<bool, HttpContext, IMemoryCache, bool>)Delegate.CreateDelegate(
                                        typeof(Func<bool, HttpContext, IMemoryCache, bool>), m);
                                }
                            }
                            catch { }

                            try
                            {
                                var m2 = type.GetMethod("InvokeAsync");
                                if (m2 != null)
                                {
                                    entry.InvokeAsync = (Func<bool, HttpContext, IMemoryCache, Task<bool>>)Delegate.CreateDelegate(
                                        typeof(Func<bool, HttpContext, IMemoryCache, Task<bool>>), m2);
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                var m = type.GetMethod("Invoke");
                                if (m != null)
                                {
                                    entry.InvokeV1 = (Func<HttpContext, IMemoryCache, bool>)Delegate.CreateDelegate(
                                        typeof(Func<HttpContext, IMemoryCache, bool>), m);
                                }
                            }
                            catch { }

                            try
                            {
                                var m2 = type.GetMethod("InvokeAsync");
                                if (m2 != null)
                                {
                                    entry.InvokeAsyncV1 = (Func<HttpContext, IMemoryCache, Task<bool>>)Delegate.CreateDelegate(
                                        typeof(Func<HttpContext, IMemoryCache, Task<bool>>), m2);
                                }
                            }
                            catch { }
                        }

                        if (entry.Invoke != null || entry.InvokeAsync != null || entry.InvokeV1 != null || entry.InvokeAsyncV1 != null)
                            middlewareModulesCache.Add(entry);
                    }
                    catch { }
                }
            }
        }
    }
}
