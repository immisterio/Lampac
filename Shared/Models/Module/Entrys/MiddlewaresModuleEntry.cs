using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace Shared.Models.Module.Entrys
{
    public class MiddlewaresModuleEntry
    {
        public RootModule mod;
        public Func<bool, HttpContext, IMemoryCache, bool> Invoke = null;
        public Func<bool, HttpContext, IMemoryCache, Task<bool>> InvokeAsync = null;
        public Func<HttpContext, IMemoryCache, bool> InvokeV1 = null;
        public Func<HttpContext, IMemoryCache, Task<bool>> InvokeAsyncV1 = null;


        public static List<MiddlewaresModuleEntry> middlewareModulesCache = null;
        static readonly object _middlewareModulesCacheLock = new object();

        public static void EnsureCache(bool forced = false)
        {
            if (AppInit.modules == null)
                return;

            if (forced == false && middlewareModulesCache != null)
                return;

            lock (_middlewareModulesCacheLock)
            {
                if (forced == false && middlewareModulesCache != null)
                    return;

                middlewareModulesCache = new List<MiddlewaresModuleEntry>();

                foreach (var mod in AppInit.modules.Where(i => i.middlewares != null && i.enable))
                {
                    try
                    {
                        var entry = new MiddlewaresModuleEntry() { mod = mod };

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
