﻿using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public class RchClient
    {
        #region static
        public static string ErrorMsg => AppInit.conf.rch.enable ? "rhub не работает с данным балансером" : "Включите rch в init.conf";

        public static EventHandler<(string connectionId, string rchId, string url, string data, Dictionary<string, string> headers, bool returnHeaders)> hub = null;

        static ConcurrentDictionary<string, (string ip, JObject json)> clients = new ConcurrentDictionary<string, (string, JObject)>();

        public static ConcurrentDictionary<string, TaskCompletionSource<string>> rchIds = new ConcurrentDictionary<string, TaskCompletionSource<string>>();


        public static void Registry(string ip, string connectionId, JObject json = null)
        {
            clients.AddOrUpdate(connectionId, (ip, json), (i,j) => (ip, json));
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

        public string ipkey(string key, ProxyManager proxy) => $"{key}:{(enableRhub ? ip : proxy?.CurrentProxyIp)}";

        public RchClient(HttpContext context, string host, BaseSettings init, RequestModel requestInfo, int? keepalive = null)
        {
            this.init = init;
            httpContext = context;
            enableRhub = init.rhub;
            rhub_fallback = init.rhub_fallback;
            ip = context.Connection.RemoteIpAddress.ToString();
            connectionId = clients.FirstOrDefault(i => i.Value.ip == ip).Key;

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
                keepalive = kplv,
                result = $"{host}/rch/result",
                ws = $"{host}/ws",
                timeout = init.rhub_fallback ? 5 : 8
            });
        }


        #region Eval
        public ValueTask<string> Eval(string data) => SendHub("eval", data);

        async public ValueTask<T> Eval<T>(string data, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await SendHub("eval", data);
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
        async public ValueTask<(JObject headers, string currentUrl, string body)> Headers(string url, string data, List<HeadersModel> headers = null, bool useDefaultHeaders = true)
        {
            try
            {
                string json = await SendHub(url, data, headers, useDefaultHeaders, true);
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
        public ValueTask<string> Get(string url, List<HeadersModel> headers = null, bool useDefaultHeaders = true) => SendHub(url, null, headers, useDefaultHeaders);

        async public ValueTask<T> Get<T>(string url, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, bool useDefaultHeaders = true)
        {
            try
            {
                string html = await SendHub(url, null, headers, useDefaultHeaders);
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
        public ValueTask<string> Post(string url, string data, List<HeadersModel> headers = null, bool useDefaultHeaders = true) => SendHub(url, data, headers, useDefaultHeaders);

        async public ValueTask<T> Post<T>(string url, string data, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, bool useDefaultHeaders = true)
        {
            try
            {
                string json = await SendHub(url, data, headers, useDefaultHeaders);
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
        async ValueTask<string> SendHub(string url, string data = null, List<HeadersModel> headers = null, bool useDefaultHeaders = true, bool returnHeaders = false)
        {
            if (hub == null)
                return null;

            try
            {
                string rchId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<string>();
                rchIds.TryAdd(rchId, tcs);

                var send_headers = !useDefaultHeaders ? null : new Dictionary<string, string>() 
                {
                    { "Accept-Language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5" },
                    { "User-Agent", HttpClient.UserAgent }
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

                string result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(rhub_fallback ? 8 : 12));
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
        public (int version, string host, string href, string rchtype) InfoConnected()
        {
            var client = clients.FirstOrDefault(i => i.Value.ip == ip);
            if (client.Value.json == null)
                return default;

            JObject json = client.Value.json;
            return (json.Value<int>("version"), json.Value<string>("host"), json.Value<string>("href"), json.Value<string>("rchtype"));
        }
        #endregion
    }
}
