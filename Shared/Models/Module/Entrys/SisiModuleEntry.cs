using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.SISI.Base;

namespace Shared.Models.Module.Entrys
{
    public class SisiModuleEntry
    {
        public RootModule mod;

        // version >= 3
        public Func<HttpContext, IMemoryCache, RequestModel, string, SisiEventsModel, List<ChannelItem>> Invoke = null;
        public Func<HttpContext, IMemoryCache, RequestModel, string, SisiEventsModel, Task<List<ChannelItem>>> InvokeAsync = null;

        // version < 3
        public Func<string, List<ChannelItem>> Events = null;

        public static List<SisiModuleEntry> sisiModulesCache = null;
        static readonly object _sisiModulesCacheLock = new object();

        public static void EnsureCache(bool forced = false)
        {
            if (AppInit.modules == null)
                return;

            if (forced == false && sisiModulesCache != null)
                return;

            lock (_sisiModulesCacheLock)
            {
                if (forced == false && sisiModulesCache != null)
                    return;

                sisiModulesCache = new List<SisiModuleEntry>();

                try
                {
                    foreach (var mod in AppInit.modules.Where(i => i.sisi != null && i.enable))
                    {
                        try
                        {
                            var entry = new SisiModuleEntry() { mod = mod };

                            var assembly = mod.assembly;
                            if (assembly == null)
                                continue;

                            var type = assembly.GetType(mod.NamespacePath(mod.sisi));
                            if (type == null)
                                continue;

                            if (mod.version >= 3)
                            {
                                try
                                {
                                    var m = type.GetMethod("Invoke");
                                    if (m != null)
                                    {
                                        entry.Invoke = (Func<HttpContext, IMemoryCache, RequestModel, string, SisiEventsModel, List<ChannelItem>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, RequestModel, string, SisiEventsModel, List<ChannelItem>>), m);
                                    }
                                }
                                catch { }

                                try
                                {
                                    var m2 = type.GetMethod("InvokeAsync");
                                    if (m2 != null)
                                    {
                                        entry.InvokeAsync = (Func<HttpContext, IMemoryCache, RequestModel, string, SisiEventsModel, Task<List<ChannelItem>>>)Delegate.CreateDelegate(
                                            typeof(Func<HttpContext, IMemoryCache, RequestModel, string, SisiEventsModel, Task<List<ChannelItem>>>), m2);
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
                                        entry.Events = (Func<string, List<ChannelItem>>)Delegate.CreateDelegate(
                                            typeof(Func<string, List<ChannelItem>>), m);
                                    }
                                }
                                catch { }
                            }

                            if (entry.Invoke != null || entry.InvokeAsync != null || entry.Events != null)
                                sisiModulesCache.Add(entry);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
