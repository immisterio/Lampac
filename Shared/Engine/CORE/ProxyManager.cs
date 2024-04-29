using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Model.Base;
using Shared.Models.Proxy;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Shared.Engine.CORE
{
    public class ProxyManager
    {
        static IMemoryCache memoryCache;
        static ConcurrentDictionary<string, ProxyManagerModel> database = new ConcurrentDictionary<string, ProxyManagerModel>();

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
        }

        #region ProxyManager
        string plugin;
        bool refresh;
        Iproxy conf;

        public ProxyManager(string plugin, Iproxy conf, bool refresh = true)
        {
            this.plugin = plugin;
            this.conf = conf;
            this.refresh = refresh;
        }
        #endregion

        #region Get
        public WebProxy Get()
        {
            if (!conf.useproxy && !conf.useproxystream)
                return null;

            WebProxy proxy(ProxySettings p_orig, string key)
            {
                ProxySettings p = ConfigureProxy(p_orig);

                if (p?.list == null || p.list.Count == 0)
                    return null;

                if (!database.TryGetValue(key, out ProxyManagerModel val) || val.proxyip == null || !p.list.Contains(val.proxyip))
                {
                    val = new ProxyManagerModel();

                    if (p.actions != null && p.actions.Count > 0)
                    {
                        val.proxyip = p.list.First();
                        start_action(p, key, val.proxyip);
                    }
                    else
                    {
                        val.proxyip = p.list.OrderBy(a => Guid.NewGuid()).First();
                        database.AddOrUpdate(key, val, (k, v) => val);
                    }
                }

                return ConfigureWebProxy(p, val.proxyip);
            }


            if ((conf.proxy?.list != null && conf.proxy.list.Count > 0) || !string.IsNullOrEmpty(conf.proxy?.file) || !string.IsNullOrEmpty(conf.proxy?.url))
            {
                return proxy(conf.proxy, $"{plugin}:conf");
            }
            else
            {
                if (!string.IsNullOrEmpty(conf.globalnameproxy))
                {
                    return proxy(AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy), $"{plugin}:globalname");
                }
                else
                {
                    return proxy(AppInit.conf.proxy, $"{plugin}:conf");
                }
            }
        }
        #endregion

        #region Refresh
        public void Refresh()
        {
            if (!refresh)
                return;

            void update(ProxySettings p, string key)
            {
                if (database.TryGetValue(key, out ProxyManagerModel val))
                {
                    int maxRequestError = 2;
                    if (p?.maxRequestError > 0)
                        maxRequestError = p.maxRequestError;

                    if (val.errors >= maxRequestError)
                    {
                        if (!string.IsNullOrEmpty(p?.refresh_uri))
                            _ = HttpClient.Get(p.refresh_uri, timeoutSeconds: 5).ConfigureAwait(false);

                        if (p?.actions != null && p.actions.Count > 0)
                        {
                            val.errors = 0;
                            start_action(ConfigureProxy(p), key, val.proxyip);
                            return;
                        }

                        val.errors = 0;
                        val.proxyip = null;
                        database.TryRemove(key, out _);
                    }
                    else
                    {
                        val.errors += 1;
                    }
                }
            }

            update(AppInit.conf.proxy, plugin);

            if (conf == null)
                return;

            update(conf.proxy, $"{plugin}:conf");
            update(AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy), $"{plugin}:globalname");
        }
        #endregion

        #region Success
        public void Success()
        {
            foreach (string key in new string[] { plugin, $"{plugin}:conf", $"{plugin}:globalname" })
            {
                if (database.TryGetValue(key, out var val) && val.errors > 0)
                    val.errors = 0;
            }
        }
        #endregion

        #region CurrentProxyIp
        public string CurrentProxyIp
        {
            get
            {
                foreach (string key in new string[] { plugin, $"{plugin}:conf", $"{plugin}:globalname" })
                {
                    if (database.TryGetValue(key, out var val))
                        return val.proxyip;
                }

                return null;
            }
        }
        #endregion



        ProxySettings ConfigureProxy(ProxySettings orig)
        {
            if (orig == null)
                return null;

            ProxySettings p = (ProxySettings)orig.Clone();

            if (!string.IsNullOrEmpty(p.file) && File.Exists(p.file))
                p.list = new ConcurrentBag<string>(File.ReadAllLines(p.file));

            if (!string.IsNullOrEmpty(p.url))
            {
                string mkey = $"ProxyManager:{p.url}";
                if (!memoryCache.TryGetValue(mkey, out List<string> list))
                {
                    list = new List<string>();

                    string txt = HttpClient.Get(p.url, timeoutSeconds: 5).Result;
                    if (txt != null)
                    {
                        foreach (string line in txt.Split("\n"))
                        {
                            if (line.Contains(":"))
                                list.Add(line.Trim());
                        }
                    }

                    memoryCache.Set(mkey, list, DateTime.Now.AddMinutes(list.Count == 0 ? 4 : 15));
                }

                p.list = new ConcurrentBag<string>(list);
            }

            return p;
        }


        WebProxy ConfigureWebProxy(ProxySettings p, string proxyip)
        {
            NetworkCredential credentials = null;

            if (proxyip.Contains("@"))
            {
                var g = Regex.Match(proxyip, p.pattern_auth).Groups;
                proxyip = g["sheme"].Value + g["host"].Value;
                credentials = new NetworkCredential(g["username"].Value, g["password"].Value);
            }
            else if (p.useAuth)
                credentials = new NetworkCredential(p.username, p.password);

            return new WebProxy(proxyip, p.BypassOnLocal, null, credentials);
        }


        void start_action(ProxySettings p, string key, string current_proxyip = null)
        {
            if (p == null)
                return;

            string mkey = $"ProxyManager:start_action:{key}";
            if (memoryCache.TryGetValue(mkey, out _))
                return;

            memoryCache.Set(mkey, 0);

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                try
                {
                    string proxyip = null;
                    var list = p.list.OrderBy(a => Guid.NewGuid()).ToList();

                    if (!string.IsNullOrEmpty(current_proxyip))
                    {
                        if (list.Contains(current_proxyip))
                        {
                            list.Remove(current_proxyip);
                            list.Insert(0, current_proxyip);
                        }
                    }

                    foreach (string proxy in list)
                    {
                        bool isok = true;

                        foreach (var action in p.actions)
                        {
                            string result = string.Empty;

                            if (!string.IsNullOrEmpty(action.data))
                                result = await HttpClient.Post(action.url, action.data, timeoutSeconds: action.timeoutSeconds, proxy: ConfigureWebProxy(p, proxy));
                            else
                                result = await HttpClient.Get(action.url, timeoutSeconds: action.timeoutSeconds, proxy: ConfigureWebProxy(p, proxy));

                            if (result == null || !result.Contains(action.contains))
                            {
                                isok = false;
                                break;
                            }
                        }

                        if (isok)
                        {
                            proxyip = proxy;
                            break;
                        }
                    }

                    if (proxyip != null)
                    {
                        var val = new ProxyManagerModel();
                        val.proxyip = proxyip;
                        database.AddOrUpdate(key, val, (k, v) => val);
                    }
                }
                catch { }

                memoryCache.Remove(mkey);
            });
        }
    }
}
