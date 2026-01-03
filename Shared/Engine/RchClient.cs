using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Events;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;

namespace Shared.Engine
{
    public class RchClientInfo
    {
        public int version { get; set; }
        public string host { get; set; }
        public string rchtype { get; set; }
        public int apkVersion { get; set; }
        public string player { get; set; }
        public string account_email { get; set; }
        public string unic_id { get; set; }
        public string profile_id { get; set; }
        public string token { get; set; }

        public object ob { get; set; }
        public Dictionary<string, object> obs { get; set; } = new Dictionary<string, object>();
    }

    public class RchClient
    {
        #region static
        static readonly Timer _checkConnectionTimer = new Timer(CheckConnection, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4));

        static int _cronCheckConnectionWork = 0;

        static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };

        async static void CheckConnection(object state)
        {
            if (clients.IsEmpty)
                return;

            if (Interlocked.Exchange(ref _cronCheckConnectionWork, 1) == 1)
                return;

            try
            {
                string[] wsKeys = clients
                    .Where(c => c.Value.connection != null)
                    .Select(k => k.Key)
                    .ToArray();

                if (wsKeys.Length == 0)
                    return;

                await Parallel.ForEachAsync(wsKeys, new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) 
                }, 
                async (connectionId, cancellationToken) =>
                {
                    if (clients.TryGetValue(connectionId, out var client) && client.connection == null)
                    {
                        var rch = new RchClient(connectionId);
                        string result = await rch.SendHub("ping", useDefaultHeaders: false);
                        if (result != "pong")
                            OnDisconnected(connectionId);
                    }
                });
            }
            catch { }
            finally
            {
                Volatile.Write(ref _cronCheckConnectionWork, 0);
            }
        }


        public static string ErrorMsg => AppInit.conf.rch.enable ? "rhub не работает с данным балансером" : "Включите rch в init.conf";

        public static EventHandler<(string connectionId, string rchId, string url, string data, Dictionary<string, string> headers, bool returnHeaders)> hub = null;

        public static readonly ConcurrentDictionary<string, (string ip, string host, RchClientInfo info, NwsConnection connection)> clients = new ConcurrentDictionary<string, (string, string, RchClientInfo, NwsConnection)>();

        public static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> rchIds = new ConcurrentDictionary<string, TaskCompletionSource<string>>();


        public static void Registry(string ip, string connectionId, string host = null, string json = null, NwsConnection connection = null)
        {
            var info = new RchClientInfo();

            if (json != null)
            {
                try
                {
                    info = System.Text.Json.JsonSerializer.Deserialize<RchClientInfo>(json);
                }
                catch { }
            }

            if (AppInit.conf.rch.blacklistHost != null && info.host != null)
            {
                foreach (string h in AppInit.conf.rch.blacklistHost)
                {
                    if (info.host.Contains(h))
                        return;
                }
            }

            if (info == null)
                info = new RchClientInfo() { version = -1 };

            clients.AddOrUpdate(connectionId, (ip, host, info, connection), (i, j) => (ip, host, info, connection));

            if (InvkEvent.IsRchRegistry())
                InvkEvent.RchRegistry(new EventRchRegistry(connectionId, ip, host, info, connection));
        }


        public static void OnDisconnected(string connectionId)
        {
            if (clients.TryRemove(connectionId, out _))
            {
                if (InvkEvent.IsRchDisconnected())
                    InvkEvent.RchDisconnected(new EventRchDisconnected(connectionId));
            }
        }
        #endregion

        BaseSettings init;

        HttpContext httpContext;

        string ip, connectionId;

        bool enableRhub, rhub_fallback;

        public bool enable => init != null && init.rhub && enableRhub;

        public string connectionMsg { get; private set; }

        public string ipkey(string key) => enableRhub ? $"{key}:{ip}" : key;

        public string ipkey(string key, ProxyManager proxy) => $"{key}:{(enableRhub ? ip : proxy?.CurrentProxyIp)}";

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

            if (enableRhub && rhub_fallback && init.rhub_geo_disable != null)
            {
                if (requestInfo.Country != null && init.rhub_geo_disable.Contains(requestInfo.Country))
                {
                    enableRhub = false;
                    init.rhub = false;
                }
            }

            connectionMsg = System.Text.Json.JsonSerializer.Serialize(new
            {
                rch = true,
                ws = $"{host}/ws",
                nws = $"{(host.StartsWith("https") ? "wss" : "ws")}://{Regex.Replace(host, "^https?://", "")}/nws"
            });
        }


        public void Disabled()
        {
            enableRhub = false;
        }


        #region Eval
        public void EvalRun(string data)
        {
            _= SendHub("evalrun", data).ConfigureAwait(false);
        }

        public Task<string> Eval(string data)
        {
            return SendHub("eval", data);
        }

        async public Task<T> Eval<T>(string data, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await SendHub("eval", data, useDefaultHeaders: false).ConfigureAwait(false);
                if (json == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(json, jsonSettings);

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
                // на версиях ниже java.lang.OutOfMemoryError
                if (484 > InfoConnected()?.apkVersion)
                    return default;

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
        public Task<string> Get(string url, List<HeadersModel> headers = null, bool useDefaultHeaders = true)
        {
            return SendHub(url, null, headers, useDefaultHeaders);
        }

        async public Task<T> Get<T>(string url, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, bool useDefaultHeaders = true)
        {
            try
            {
                string html = await SendHub(url, null, headers, useDefaultHeaders).ConfigureAwait(false);
                if (html == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(html, jsonSettings);

                return JsonConvert.DeserializeObject<T>(html);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region Post
        public Task<string> Post(string url, string data, List<HeadersModel> headers = null, bool useDefaultHeaders = true) 
        {
            return SendHub(url, data, headers, useDefaultHeaders);
        }

        async public Task<T> Post<T>(string url, string data, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, bool useDefaultHeaders = true)
        {
            try
            {
                string json = await SendHub(url, data, headers, useDefaultHeaders).ConfigureAwait(false);
                if (json == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(json, jsonSettings);

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region SendHub
        async Task<string> SendHub(string url, string data = null, List<HeadersModel> headers = null, bool useDefaultHeaders = true, bool returnHeaders = false, bool waiting = true)
        {
            if (hub == null)
                return null;

            var clientInfo = SocketClient();

            if (string.IsNullOrEmpty(connectionId) || !clients.ContainsKey(connectionId))
                connectionId = clientInfo.connectionId;

            if (string.IsNullOrEmpty(connectionId))
                return null;

            string rchId = Guid.NewGuid().ToString();

            try
            {
                var tcs = rchIds.GetOrAdd(rchId, _ => new TaskCompletionSource<string>());

                #region send_headers
                Dictionary<string, string> send_headers = null;

                if (useDefaultHeaders && clientInfo.data.rch_info.rchtype == "apk")
                {
                    send_headers = new Dictionary<string, string>(Http.defaultUaHeaders)
                    {
                        { "accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5" }
                    };
                }

                if (headers != null)
                {
                    if (send_headers == null)
                        send_headers = new Dictionary<string, string>(headers.Count);

                    foreach (var h in headers)
                        send_headers[h.name.ToLower().Trim()] = h.val;
                }

                if (send_headers != null && send_headers.Count > 0 && clientInfo.data.rch_info.rchtype != "apk")
                {
                    var new_headers = new Dictionary<string, string>(Math.Min(10, send_headers.Count));

                    foreach (var h in send_headers)
                    {
                        var key = h.Key;

                        if (key.StartsWith("sec-ch-", StringComparison.OrdinalIgnoreCase) ||
                            key.StartsWith("sec-fetch-", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (key.Equals("user-agent", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("cookie", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("referer", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("origin", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("accept", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("accept-language", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("accept-encoding", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("cache-control", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("dnt", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("pragma", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("priority", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("upgrade-insecure-requests", StringComparison.OrdinalIgnoreCase))
                            continue;

                        new_headers[key] = h.Value;
                    }

                    send_headers = new_headers;
                }
                #endregion

                hub.Invoke(null, (connectionId, rchId, url, data, Http.NormalizeHeaders(send_headers), returnHeaders));

                if (!waiting)
                    return null;

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

        bool IsNotConnected(string ip)
        {
            if (!enableRhub)
                return false; // rch не используется

            if (httpContext != null && httpContext.Request.QueryString.Value.Contains("&checksearch=true"))
                return true; // заглушка для checksearch

            return SocketClient().connectionId == null;
        }
        #endregion

        #region IsRequiredConnected
        public bool IsRequiredConnected()
        {
            if (httpContext != null)
            {
                var requestInfo = httpContext.Features.Get<RequestModel>();
                if (requestInfo.IsLocalRequest)
                    return false;
            }

            if (AppInit.conf.rch.requiredConnected  // Обязательное подключение
                || init.rchstreamproxy != null)     // Нужно знать rchtype устройства 
                return SocketClient().connectionId == null;

            return false;
        }
        #endregion

        #region IsNotSupport
        public bool IsNotSupport(out string rch_msg)
        {
            rch_msg = null;

            if (!enableRhub)
                return false; // rch не используется

            if (httpContext != null && httpContext.Request.QueryString.Value.Contains("&checksearch=true"))
                return false; // заглушка для checksearch

            if (IsNotSupportRchAccess(init.RchAccessNotSupport(nocheck: true), out rch_msg))
                return true;

            return IsNotSupportStreamAccess(init.StreamAccessNotSupport(), out rch_msg);
        }

        public bool IsNotSupportRchAccess(string rch_deny, out string rch_msg)
        {
            rch_msg = null;

            if (rch_deny == null)
                return false;

            if (!enableRhub)
                return false; // rch не используется

            if (httpContext != null && httpContext.Request.QueryString.Value.Contains("&checksearch=true"))
                return false; // заглушка для checksearch

            var info = InfoConnected();
            if (info == null || string.IsNullOrEmpty(info.rchtype))
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

            // указан webcorshost или включен corseu
            if (!string.IsNullOrWhiteSpace(init.webcorshost) || init.corseu)
                return false;

            if (AppInit.conf.rch.notSupportMsg != null)
                rch_msg = AppInit.conf.rch.notSupportMsg;
            else if (info.rchtype == "web")
                rch_msg = "На MSX недоступно";
            else
                rch_msg = "Только на android";

            return rch_deny.Contains(info.rchtype);
        }

        public bool IsNotSupportStreamAccess(string deny, out string rch_msg)
        {
            rch_msg = null;

            if (deny == null)
                return false;

            if (!enableRhub)
                return false; // rch не используется

            if (httpContext != null && httpContext.Request.QueryString.Value.Contains("&checksearch=true"))
                return false; // заглушка для checksearch

            var info = InfoConnected();
            if (info == null || string.IsNullOrEmpty(info.rchtype))
                return false; // клиент не в сети

            if (AppInit.conf.rch.notSupportMsg != null)
                rch_msg = AppInit.conf.rch.notSupportMsg;
            else if (info.rchtype == "web")
                rch_msg = "На MSX недоступно";
            else
                rch_msg = "Только на android";

            return deny.Contains(info.rchtype);
        }
        #endregion

        #region InfoConnected
        public RchClientInfo InfoConnected()
        {
            return SocketClient().data.rch_info;
        }
        #endregion

        #region SocketClient
        public (string connectionId, (string ip, string host, RchClientInfo rch_info, NwsConnection connection) data) SocketClient()
        {
            string _ip = ip;

            if (AppInit.conf.WebSocket.type == "nws")
            {
                if (!string.IsNullOrEmpty(connectionId) && clients.ContainsKey(connectionId))
                    return (connectionId, clients[connectionId]);

                if (httpContext == null)
                    return default;

                if (httpContext.Request.Query.ContainsKey("nws_id"))
                {
                    string nws_id = httpContext.Request.Query["nws_id"].ToString()?.ToLower()?.Trim();
                    if (!string.IsNullOrEmpty(nws_id) && clients.ContainsKey(nws_id))
                        return (nws_id, clients[nws_id]);
                }
            }
            else
            {
                var client = clients.LastOrDefault(i => i.Value.ip == _ip);
                if (client.Value.info?.rchtype != null)
                    return (client.Key, client.Value);
            }

            return default;
        }
        #endregion
    }
}
