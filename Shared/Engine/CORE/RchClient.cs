using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Shared.Engine.CORE;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public class RchClient
    {
        #region static
        public static EventHandler<(string connectionId, string rchId, string url, string data)> hub = null;

        static ConcurrentDictionary<string, string> clients = new ConcurrentDictionary<string, string>();

        public static ConcurrentDictionary<string, TaskCompletionSource<string>> rchIds = new ConcurrentDictionary<string, TaskCompletionSource<string>>();


        public static void Registry(string ip, string connectionId)
        {
            clients.TryAdd(connectionId, ip);
        }


        public static void OnDisconnected(string connectionId)
        {
            clients.TryRemove(connectionId, out _);
        }
        #endregion

        string ip, connectionId;

        bool enableRhub;

        public string connectionMsg { get; private set; }

        public string ipkey(string key, ProxyManager proxy) => $"{key}:{(enableRhub ? ip : proxy.CurrentProxyIp)}";

        public RchClient(HttpContext context, string host, bool enableRhub)
        {
            this.enableRhub = enableRhub;
            ip = context.Connection.RemoteIpAddress.ToString();
            connectionId = clients.FirstOrDefault(i => i.Value == ip).Key;

            connectionMsg = System.Text.Json.JsonSerializer.Serialize(new
            {
                rch = true,
                AppInit.conf.rch.keepalive,
                result = $"{host}/rch/result",
                ws = $"{host}/ws"
            });
        }


        #region Get
        public ValueTask<string> Get(string url) => SendHub(url);

        async public ValueTask<T> Get<T>(string url, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string html = await SendHub(url);
                if (html == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(html, new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } });

                return JsonConvert.DeserializeObject<T>(html);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region Post
        public ValueTask<string> Post(string url, string data) => SendHub(url, data);

        async public ValueTask<T> Post<T>(string url, string data, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await SendHub(url, data);
                if (json == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } });

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region SendHub
        async ValueTask<string> SendHub(string url, string data = null)
        {
            if (hub == null)
                return null;

            try
            {
                string rchId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<string>();
                rchIds.TryAdd(rchId, tcs);

                hub.Invoke(null, (connectionId, rchId, url, data));

                string result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
                rchIds.TryRemove(rchId, out _);

                if (string.IsNullOrWhiteSpace(result))
                    return null;

                return result;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region IsNotConnected
        public bool IsNotConnected() => IsNotConnected(ip);

        public bool IsNotConnected(string ip)
        {
            if (!enableRhub)
                return false; // rch не используется

            return !clients.Values.Contains(ip);
        }
        #endregion
    }
}
