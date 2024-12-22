using Lampac.Engine.CORE;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine
{
    public class soks : Hub
    {
        #region soks
        static Dictionary<string, HubCallerContext> _connections = new Dictionary<string, HubCallerContext>();

        public static IHubCallerClients hubClients = null;
        #endregion

        #region Rch
        public void RchRegistry(string json)
        {
            JObject job = null;

            try
            {
                job = JsonConvert.DeserializeObject<JObject>(json);
            }
            catch { }

            RchClient.Registry(Context.GetHttpContext().Connection.RemoteIpAddress.ToString(), Context.ConnectionId, job);
        }

        /// <summary>
        /// Старые версии online.js
        /// </summary>
        public void Registry(string type)
        {
            if (string.IsNullOrEmpty(type))
                return;

            switch (type)
            {
                case "rch":
                    if (AppInit.conf.rch.enable)
                        RchClient.Registry(Context.GetHttpContext().Connection.RemoteIpAddress.ToString(), Context.ConnectionId);
                    break;
            }
        }
        #endregion

        #region WebLog
        static Dictionary<string, byte> weblog_clients = new Dictionary<string, byte>();

        public void RegistryWebLog(string token)
        {
            if (AppInit.conf.weblog.enable)
            {
                if (string.IsNullOrEmpty(AppInit.conf.weblog.token) || AppInit.conf.weblog.token == token)
                {
                    weblog_clients.TryAdd(Context.ConnectionId, 0);
                    return;
                }
            }

            Context.Abort();
        }

        public static void SendLog(string message, string plugin)
        {
            if (!AppInit.conf.weblog.enable || hubClients == null || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(plugin) || message.Length > 1_000000)
                return;

            hubClients.Clients(weblog_clients.Keys).SendAsync("Receive", message, plugin);
        }
        #endregion

        #region Events
        static Dictionary<string, string> event_clients = new Dictionary<string, string>();

        public void RegistryEvent(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return;

            event_clients.TryAdd(Context.ConnectionId, uid);
        }

        public async Task events(string uid, string name, string data)
        {
            if (hubClients == null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(name) || (data != null && data.Length > 10_000000))
                return;

            try
            {
                await hubClients.Clients(event_clients.Where(i => i.Value == uid && i.Key != Context.ConnectionId).Select(i => i.Key)).SendAsync("event", uid, name, data ?? string.Empty).ConfigureAwait(false);
            }
            catch { }
        }
        #endregion

        #region Connected / Disconnected
        public override Task OnConnectedAsync()
        {
            hubClients = Clients;
            _connections.TryAdd(Context.ConnectionId, Context);

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            weblog_clients.Remove(Context.ConnectionId);
            event_clients.Remove(Context.ConnectionId);
            RchClient.OnDisconnected(Context.ConnectionId);

            hubClients = Clients;
            _connections.Remove(Context.ConnectionId);

            return base.OnDisconnectedAsync(exception);
        }
        #endregion
    }
}
