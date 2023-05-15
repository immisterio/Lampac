using Lampac;
using Lampac.Engine.CORE;
using Shared.Model.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Shared.Engine.CORE
{
    public class ProxyManager
    {
        static Dictionary<string, string> database = new Dictionary<string, string>();

        #region ProxyManager
        string plugin;

        Iproxy conf;

        public ProxyManager(string plugin, Iproxy conf)
        {
            this.plugin = plugin;
            this.conf = conf;
        }
        #endregion

        #region Get
        public WebProxy Get()
        {
            ICredentials credentials = null;

            if (conf.proxy != null)
            {
                if (conf.proxy.useAuth)
                    credentials = new NetworkCredential(conf.proxy.username, conf.proxy.password);

                if (!database.TryGetValue($"{plugin}:conf", out string proxyip) || !conf.proxy.list.Contains(proxyip))
                {
                    proxyip = conf.proxy.list.OrderBy(a => Guid.NewGuid()).First();
                    database.Add($"{plugin}:conf", proxyip);
                }

                return new WebProxy(proxyip, conf.proxy.BypassOnLocal, null, credentials);
            }
            else
            {
                if (!conf.useproxy)
                    return null;

                string proxyip;
                bool bypassOnLocal = false;

                if (!string.IsNullOrEmpty(conf.globalnameproxy))
                {
                    var p = AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy);
                    if (p == null)
                        return null;

                    if (p.useAuth)
                        credentials = new NetworkCredential(p.username, p.password);

                    bypassOnLocal = p.BypassOnLocal;

                    if (!database.TryGetValue($"{plugin}:globalname", out proxyip) || !p.list.Contains(proxyip))
                    {
                        proxyip = p.list.OrderBy(a => Guid.NewGuid()).First();
                        database.Add($"{plugin}:globalname", proxyip);
                    }
                }
                else
                {
                    if (AppInit.conf.proxy.useAuth)
                        credentials = new NetworkCredential(AppInit.conf.proxy.username, AppInit.conf.proxy.password);

                    bypassOnLocal = AppInit.conf.proxy.BypassOnLocal;

                    if (!database.TryGetValue(plugin, out proxyip) || !AppInit.conf.proxy.list.Contains(proxyip))
                    {
                        proxyip = AppInit.conf.proxy.list.OrderBy(a => Guid.NewGuid()).First();
                        database.Add(plugin, proxyip);
                    }
                }

                return new WebProxy(proxyip, bypassOnLocal, null, credentials);
            }
        }
        #endregion

        #region Refresh
        public void Refresh()
        {
            if (conf.proxy != null)
            {
                if (!string.IsNullOrEmpty(conf.proxy.refresh_uri))
                    _ = HttpClient.Get(conf.proxy.refresh_uri, timeoutSeconds: 4);
            }

            if (!string.IsNullOrEmpty(conf.globalnameproxy))
            {
                string refresh_uri = AppInit.conf.globalproxy.FirstOrDefault(i => i.name == conf.globalnameproxy)?.refresh_uri;
                if (!string.IsNullOrEmpty(refresh_uri))
                    _ = HttpClient.Get(refresh_uri, timeoutSeconds: 4);
            }

            foreach (string key in new string[] { plugin, $"{plugin}:conf", $"{plugin}:globalname" })
            {
                if (database.ContainsKey(key))
                    database.Remove(key);
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
                    if (database.TryGetValue(key, out string proxyip))
                        return proxyip;
                }

                return null;
            }
        }
        #endregion
    }
}
