using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.Module;

namespace Online
{
    public class OnlineModuleEntry
    {
        public RootModule mod;

        // version >= 3
        public Func<HttpContext, IMemoryCache, RequestModel, string, OnlineEventsModel, List<(string name, string url, string plugin, int index)>> Invoke = null;
        public Func<HttpContext, IMemoryCache, RequestModel, string, OnlineEventsModel, Task<List<(string name, string url, string plugin, int index)>>> InvokeAsync = null;
        public Func<HttpContext, IMemoryCache, RequestModel, string, OnlineSpiderModel, List<(string name, string url, int index)>> Spider = null;
        public Func<HttpContext, IMemoryCache, RequestModel, string, OnlineSpiderModel, Task<List<(string name, string url, int index)>>> SpiderAsync = null;

        // version < 3
        public Func<string, long, string, long, string, string, string, int, string, int, string, List<(string name, string url, string plugin, int index)>> Events = null;
        public Func<HttpContext, IMemoryCache, string, long, string, long, string, string, string, int, string, int, string, Task<List<(string name, string url, string plugin, int index)>>> EventsAsync = null;
        public static List<OnlineModuleEntry> onlineModulesCache = null;
        static readonly object _onlineModulesCacheLock = new object();

        public static void EnsureCache()
        {
            if (onlineModulesCache != null || AppInit.modules == null)
                return;

            lock (_onlineModulesCacheLock)
            {
                if (onlineModulesCache != null)
                    return;

                onlineModulesCache = new List<OnlineModuleEntry>();

                try
                {
                    foreach (var mod in AppInit.modules.Where(i => i.online != null))
                    {
                        try
                        {
                            var entry = new OnlineModuleEntry() { mod = mod };

                            var assembly = mod.assembly;
                            if (assembly == null)
                                continue;

                            var type = assembly.GetType(mod.NamespacePath(mod.online));
                            if (type == null)
                                continue;

                            if (mod.version >= 3)
                            {
                                try
                                {
                                    var m = type.GetMethod("Invoke");
                                    if (m != null)
                                    {
                                        entry.Invoke = (Func<HttpContext, IMemoryCache, RequestModel, string, OnlineEventsModel, List<(string name, string url, string plugin, int index)>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, RequestModel, string, OnlineEventsModel, List<(string name, string url, string plugin, int index)>>), m);
                                    }
                                }
                                catch { }

                                try
                                {
                                    var m2 = type.GetMethod("InvokeAsync");
                                    if (m2 != null)
                                    {
                                        entry.InvokeAsync = (Func<HttpContext, IMemoryCache, RequestModel, string, OnlineEventsModel, Task<List<(string name, string url, string plugin, int index)>>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, RequestModel, string, OnlineEventsModel, Task<List<(string name, string url, string plugin, int index)>>>), m2);
                                    }
                                }
                                catch { }

                                try
                                {
                                    var m3 = type.GetMethod("Spider");
                                    if (m3 != null)
                                    {
                                        entry.Spider = (Func<HttpContext, IMemoryCache, RequestModel, string, OnlineSpiderModel, List<(string name, string url, int index)>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, RequestModel, string, OnlineSpiderModel, List<(string name, string url, int index)>>), m3);
                                    }
                                }
                                catch { }

                                try
                                {
                                    var m4 = type.GetMethod("SpiderAsync");
                                    if (m4 != null)
                                    {
                                        entry.SpiderAsync = (Func<HttpContext, IMemoryCache, RequestModel, string, OnlineSpiderModel, Task<List<(string name, string url, int index)>>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, RequestModel, string, OnlineSpiderModel, Task<List<(string name, string url, int index)>>>), m4);
                                    }
                                }
                                catch { }

                            }
                            else
                            {
                                try
                                {
                                    var m = type.GetMethod("Events");
                                    if (m != null)
                                    {
                                        entry.Events = (Func<string, long, string, long, string, string, string, int, string, int, string, List<(string name, string url, string plugin, int index)>>)Delegate.CreateDelegate(
                                            typeof(Func<string, long, string, long, string, string, string, int, string, int, string, List<(string name, string url, string plugin, int index)>>), m);
                                    }
                                }
                                catch { }

                                try
                                {
                                    var m2 = type.GetMethod("EventsAsync");
                                    if (m2 != null)
                                    {
                                        entry.EventsAsync = (Func<HttpContext, IMemoryCache, string, long, string, long, string, string, string, int, string, int, string, Task<List<(string name, string url, string plugin, int index)>>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, string, long, string, long, string, string, string, int, string, int, string, Task<List<(string name, string url, string plugin, int index)>>>), m2);
                                    }
                                }
                                catch { }
                            }

                            if (entry.Invoke != null || entry.InvokeAsync != null || entry.Events != null || entry.EventsAsync != null || entry.Spider != null || entry.SpiderAsync != null)
                                onlineModulesCache.Add(entry);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

    }
}
