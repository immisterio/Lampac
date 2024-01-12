using Lampac;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Shared.Model.Base;
using Shared.Models.Proxy;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;

namespace Shared.Engine.CORE
{
    public class ProxyManager
    {
        static ConcurrentDictionary<string, ProxyManagerModel> database = new ConcurrentDictionary<string, ProxyManagerModel>();

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

            if (conf.proxy?.list != null && conf.proxy.list.Count > 0)
            {
                if (conf.proxy.useAuth)
                    credentials = new NetworkCredential(conf.proxy.username, conf.proxy.password);

                string key = $"{plugin}:conf";

                if (!database.TryGetValue(key, out ProxyManagerModel val) || val.proxyip == null || !conf.proxy.list.Contains(val.proxyip))
                {
                    val = new ProxyManagerModel();
                    val.proxyip = conf.proxy.list.OrderBy(a => Guid.NewGuid()).First();
                    val.refresh = conf.proxy.list.Count > 1 || !string.IsNullOrEmpty(conf.proxy.refresh_uri);
                    database.AddOrUpdate(key, val, (k, v) => val);
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
                    if (p?.list == null || p.list.Count == 0)
                        return null;

                    if (p.useAuth)
                        credentials = new NetworkCredential(p.username, p.password);

                    bypassOnLocal = p.BypassOnLocal;
                    string key = $"{plugin}:globalname";

                    if (!database.TryGetValue(key, out ProxyManagerModel val) || val.proxyip == null || !p.list.Contains(val.proxyip))
                    {
                        val = new ProxyManagerModel();
                        val.proxyip = p.list.OrderBy(a => Guid.NewGuid()).First();
                        val.refresh = p.list.Count > 1 || !string.IsNullOrEmpty(p.refresh_uri);
                        database.AddOrUpdate(key, val, (k, v) => val);
                    }

                    proxyip = val.proxyip;
                }
                else
                {
                    if (AppInit.conf.proxy == null || AppInit.conf.proxy.list.Count == 0)
                        return null;

                    if (AppInit.conf.proxy.useAuth)
                        credentials = new NetworkCredential(AppInit.conf.proxy.username, AppInit.conf.proxy.password);

                    bypassOnLocal = AppInit.conf.proxy.BypassOnLocal;

                    if (!database.TryGetValue(plugin, out ProxyManagerModel val) || val.proxyip == null || !AppInit.conf.proxy.list.Contains(val.proxyip))
                    {
                        val = new ProxyManagerModel();
                        val.proxyip = AppInit.conf.proxy.list.OrderBy(a => Guid.NewGuid()).First();
                        val.refresh = AppInit.conf.proxy.list.Count > 1 || !string.IsNullOrEmpty(AppInit.conf.proxy.refresh_uri);
                        database.AddOrUpdate(plugin, val, (k, v) => val);
                    }

                    proxyip = val.proxyip;
                }

                return new WebProxy(proxyip, bypassOnLocal, null, credentials);
            }
        }
        #endregion

        #region Refresh
        public void Refresh()
        {
            if (!refresh)
                return;

            int maxerror = 3;

            if (database.TryGetValue(plugin, out ProxyManagerModel val))
            {
                if (val.errors >= maxerror && val.refresh)
                {
                    if (!string.IsNullOrEmpty(AppInit.conf.proxy.refresh_uri))
                        _ = HttpClient.Get(AppInit.conf.proxy.refresh_uri, timeoutSeconds: 5);

                    val.proxyip = null;
                }
                else
                {
                    val.errors += 1;
                }
            }

            if (conf == null)
                return;

            if (database.TryGetValue($"{plugin}:conf", out val))
            {
                if (val.errors >= maxerror && val.refresh)
                {
                    if (!string.IsNullOrEmpty(conf.proxy.refresh_uri))
                        _ = HttpClient.Get(conf.proxy.refresh_uri, timeoutSeconds: 5);

                    val.proxyip = null;
                }
                else
                {
                    val.errors += 1;
                }
            }
            
            if (database.TryGetValue($"{plugin}:globalname", out val))
            {
                if (val.errors >= maxerror && val.refresh)
                {
                    string refresh_uri = AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy)?.refresh_uri;
                    if (!string.IsNullOrEmpty(refresh_uri))
                        _ = HttpClient.Get(refresh_uri, timeoutSeconds: 5);

                    val.proxyip = null;
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
