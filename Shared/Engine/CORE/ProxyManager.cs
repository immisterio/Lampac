using Lampac;
using Lampac.Engine.CORE;
using Shared.Model.Base;
using Shared.Models.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Shared.Engine.CORE
{
    public class ProxyManager
    {
        static Dictionary<string, ProxyManagerModel> database = new Dictionary<string, ProxyManagerModel>();

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

            ICredentials credentials = null;

            if (conf.proxy != null)
            {
                if (conf.proxy.useAuth)
                    credentials = new NetworkCredential(conf.proxy.username, conf.proxy.password);

                string key = $"{plugin}:conf";

                if (!database.TryGetValue(key, out var val) || !conf.proxy.list.Contains(val.proxyip))
                {
                    val.proxyip = conf.proxy.list.OrderBy(a => Guid.NewGuid()).First();

                    if (database.ContainsKey(key))
                        database.Remove(key);

                    database.TryAdd(key, val);
                }

                return new WebProxy(val.proxyip, conf.proxy.BypassOnLocal, null, credentials);
            }
            else
            {
                string proxyip = null;
                bool bypassOnLocal = false;

                if (!string.IsNullOrEmpty(conf.globalnameproxy))
                {
                    var p = AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy);
                    if (p == null)
                        return null;

                    if (p.useAuth)
                        credentials = new NetworkCredential(p.username, p.password);

                    bypassOnLocal = p.BypassOnLocal;
                    string key = $"{plugin}:globalname";

                    if (!database.TryGetValue(key, out var val) || !p.list.Contains(val.proxyip))
                    {
                        val.proxyip = p.list.OrderBy(a => Guid.NewGuid()).First();

                        if (database.ContainsKey(key))
                            database.Remove(key);

                        database.TryAdd(key, val);
                    }

                    proxyip = val.proxyip;
                }
                else
                {
                    if (AppInit.conf.proxy == null)
                        return null;

                    if (AppInit.conf.proxy.useAuth)
                        credentials = new NetworkCredential(AppInit.conf.proxy.username, AppInit.conf.proxy.password);

                    bypassOnLocal = AppInit.conf.proxy.BypassOnLocal;

                    if (!database.TryGetValue(plugin, out var val) || !AppInit.conf.proxy.list.Contains(val.proxyip))
                    {
                        val.proxyip = AppInit.conf.proxy.list.OrderBy(a => Guid.NewGuid()).First();

                        if (database.ContainsKey(plugin))
                            database.Remove(plugin);

                        database.TryAdd(plugin, val);
                    }

                    proxyip = val.proxyip;
                }

                if (proxyip == null)
                    return null;

                return new WebProxy(proxyip, bypassOnLocal, null, credentials);
            }
        }
        #endregion

        #region Refresh
        public void Refresh()
        {
            if (!refresh)
                return;

            if (database.TryGetValue(plugin, out var val))
            {
                if (val.errors >= 3)
                {
                    database.Remove(plugin);
                }
                else
                {
                    val.errors += 1;
                }
            }
            
            if (database.TryGetValue($"{plugin}:conf", out val))
            {
                if (val.errors >= 3)
                {
                    if (!string.IsNullOrEmpty(conf.proxy.refresh_uri))
                        _ = HttpClient.Get(conf.proxy.refresh_uri, timeoutSeconds: 5);

                    database.Remove($"{plugin}:conf");
                }
                else
                {
                    val.errors += 1;
                }
            }
            
            if (database.TryGetValue($"{plugin}:globalname", out val))
            {
                if (val.errors >= 3)
                {
                    string refresh_uri = AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy)?.refresh_uri;
                    if (!string.IsNullOrEmpty(refresh_uri))
                        _ = HttpClient.Get(refresh_uri, timeoutSeconds: 5);

                    database.Remove($"{plugin}:globalname");
                }
                else
                {
                    val.errors += 1;
                }
            }
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
    }
}
