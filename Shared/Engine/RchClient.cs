using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Base;
using Shared.Models;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Engine
{
    public struct RchClientInfo
    {
        public int version { get; set; }
        public string host { get; set; }
        public string href { get; set; }
        public string rchtype { get; set; }
        public int apkVersion { get; set; }
    }

    public struct RchClient
    {
        #region static
        static RchClient()
        {
            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

                    try
                    {
                        await Parallel.ForEachAsync(clients.ToArray(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (client, cancellationToken) =>
                        {
                            if (clients.ContainsKey(client.Key))
                            {
                                var rch = new RchClient(client.Key);
                                string result = await rch.SendHub("ping");
                                if (result != "pong")
                                    OnDisconnected(client.Key);
                            }
                        }).ConfigureAwait(false);
                    }
                    catch { }
                }
            });
        }


        public static string ErrorMsg => AppInit.conf.rch.enable ? "rhub не работает с данным балансером" : "Включите rch в init.conf";

        public static EventHandler<(string connectionId, string rchId, string url, string data, Dictionary<string, string> headers, bool returnHeaders)> hub = null;

        public static ConcurrentDictionary<string, (string ip, string host, string json, RchClientInfo info)> clients = new ConcurrentDictionary<string, (string, string, string, RchClientInfo)>();

        public static ConcurrentDictionary<string, TaskCompletionSource<string>> rchIds = new ConcurrentDictionary<string, TaskCompletionSource<string>>();


        public static void Registry(string ip, string connectionId, string host = null, string json = null)
        {
            clients.AddOrUpdate(connectionId, (ip, host, json, default), (i,j) => (ip, host, json, default));
        }


        public static void OnDisconnected(string connectionId)
        {
            clients.TryRemove(connectionId, out _);
        }
        #endregion

        BaseSettings init;

        HttpContext httpContext;

        string ip, connectionId;

        bool enableRhub, rhub_fallback;

        public bool enable => enableRhub;

        public string connectionMsg { get; private set; }

        public string ipkey(string key, ProxyManager? proxy) => $"{key}:{(enableRhub ? ip : proxy?.CurrentProxyIp)}";

        public RchClient(string connectionId) 
        {
            this.connectionId = connectionId;
        }

        public RchClient(HttpContext context, string host, BaseSettings init, RequestModel requestInfo, int? keepalive = null)
        {
            this.init = init;
            httpContext = context;
            enableRhub = init.rhub;
            rhub_fallback = init.rhub_fallback;
            ip = requestInfo.IP;
            connectionId = clients.FirstOrDefault(i => i.Value.ip == requestInfo.IP).Key;

            if (enableRhub && rhub_fallback && init.rhub_geo_disable != null)
            {
                if (requestInfo.Country != null && init.rhub_geo_disable.Contains(requestInfo.Country))
                {
                    enableRhub = false;
                    init.rhub = false;
                }
            }

            int kplv = AppInit.conf.rch.keepalive;
            if (AppInit.conf.rch.permanent_connection && kplv != -1 && keepalive != null)
                kplv = keepalive == -1 ? 36000 : (int)keepalive; // 10h

            connectionMsg = System.Text.Json.JsonSerializer.Serialize(new
            {
                rch = true,
                ws = $"{host}/ws",
                //keepalive = kplv,
                //result = $"{host}/rch/result",
                //timeout = init.rhub_fallback ? 5 : 8
            });
        }


        public void Disabled()
        {
            enableRhub = false;
        }


        #region Eval
        async public Task<string> Eval(string data)
        {
            return await SendHub("eval", data).ConfigureAwait(false);
        }

        async public Task<T> Eval<T>(string data, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await SendHub("eval", data).ConfigureAwait(false);
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

        #region Headers
        async public Task<(JObject headers, string currentUrl, string body)> Headers(string url, string data, List<HeadersModel> headers = null, bool useDefaultHeaders = true)
        {
            try
            {
                string json = await SendHub(url, data, headers, useDefaultHeaders, true).ConfigureAwait(false);
                if (json == null)
                    return default;

                var job = JsonConvert.DeserializeObject<JObject>(json);
                if (!job.ContainsKey("body"))
                    return default;

                return (job.Value<JObject>("headers"), job.Value<string>("currentUrl"), job.Value<string>("body"));
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region Get
        async public ValueTask<string> Get(string url, List<HeadersModel> headers = null, bool useDefaultHeaders = true)
        {
            return await SendHub(url, null, headers, useDefaultHeaders).ConfigureAwait(false);
        }

        async public ValueTask<T> Get<T>(string url, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, bool useDefaultHeaders = true)
        {
            try
            {
                string html = await SendHub(url, null, headers, useDefaultHeaders).ConfigureAwait(false);
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
        async public ValueTask<string> Post(string url, string data, List<HeadersModel> headers = null, bool useDefaultHeaders = true) 
        {
            return await SendHub(url, data, headers, useDefaultHeaders).ConfigureAwait(false);
        }

        async public ValueTask<T> Post<T>(string url, string data, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, bool useDefaultHeaders = true)
        {
            try
            {
                string json = await SendHub(url, data, headers, useDefaultHeaders).ConfigureAwait(false);
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
        async Task<string> SendHub(string url, string data = null, List<HeadersModel> headers = null, bool useDefaultHeaders = true, bool returnHeaders = false)
        {
            if (hub == null)
                return null;

            string rchId = Guid.NewGuid().ToString();

            try
            {
                var tcs = new TaskCompletionSource<string>();
                rchIds.TryAdd(rchId, tcs);

                var send_headers = !useDefaultHeaders ? null : new Dictionary<string, string>()
                {
                    { "Accept-Language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5" },
                    { "User-Agent", Http.UserAgent }
                };

                if (headers != null)
                {
                    if (send_headers == null)
                        send_headers = new Dictionary<string, string>();

                    foreach (var h in headers)
                    {
                        if (!send_headers.ContainsKey(h.name))
                            send_headers.TryAdd(h.name, h.val);
                    }
                }

                hub.Invoke(null, (connectionId, rchId, url, data, send_headers, returnHeaders));

                string result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(rhub_fallback ? 8 : 12)).ConfigureAwait(false);
                rchIds.TryRemove(rchId, out _);

                if (string.IsNullOrWhiteSpace(result))
                    return null;

                return result;
            }
            catch
            {
                return null;
            }
            finally 
            {
                rchIds.TryRemove(rchId, out _);
            }
        }
        #endregion

        #region IsNotConnected
        public bool IsNotConnected() => IsNotConnected(ip);

        public bool IsNotConnected(string ip)
        {
            if (!enableRhub)
                return false; // rch не используется

            if (httpContext.Request.QueryString.Value.Contains("&checksearch=true"))
                return true; // заглушка для checksearch

            return !clients.Select(i => i.Value.ip).ToList().Contains(ip);
        }
        #endregion

        #region IsNotSupport
        public bool IsNotSupport(string rch_deny, out string rch_msg)
        {
            rch_msg = null;

            if (!enableRhub)
                return false; // rch не используется

            if (httpContext.Request.QueryString.Value.Contains("&checksearch=true"))
                return false; // заглушка для checksearch

            var info = InfoConnected();
            if (string.IsNullOrEmpty(info.rchtype))
                return false; // клиент не в сети

            // разрешен возврат на сервер
            if (rhub_fallback)
            {
                if (rch_deny.Contains(info.rchtype)) {
                    enableRhub = false;
                    init.rhub = false;
                }
                return false;
            }

            if (AppInit.conf.rch.notSupportMsg != null)
                rch_msg = AppInit.conf.rch.notSupportMsg;
            else if (info.rchtype == "web")
                rch_msg = "На MSX недоступно";
            else
                rch_msg = "Только на android";

            return rch_deny.Contains(info.rchtype);
        }
        #endregion

        #region InfoConnected
        public RchClientInfo InfoConnected()
        {
            string _ip = ip;
            var client = clients.FirstOrDefault(i => i.Value.ip == _ip);
            if (client.Value.json == null && client.Value.info.rchtype == null)
                return default;

            var info = client.Value.info;

            if (info.rchtype == null)
            {
                try
                {
                    info = JsonConvert.DeserializeObject<RchClientInfo>(client.Value.json);
                    clients[client.Key] = (client.Value.ip, client.Value.host, null, info);
                }
                catch { return default; }
            }

            return info;
        }
        #endregion
    }
}
