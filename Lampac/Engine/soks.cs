using Microsoft.AspNetCore.SignalR;
using Shared;
using Shared.Engine;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine
{
    public class soks : Hub, ISoks
    {
        #region soks
        public static ConcurrentDictionary<string, HubCallerContext> _connections = new ConcurrentDictionary<string, HubCallerContext>();

        public static IHubCallerClients hubClients = null;

        public IHubCallerClients AllClients => hubClients;

        public ConcurrentDictionary<string, HubCallerContext> Connections => _connections;
        #endregion

        #region Rch
        public void RchRegistry(string json)
        {
            if (!AppInit.conf.rch.enable)
                return;

            var httpContext = Context.GetHttpContext();
            var requestInfo = httpContext.Features.Get<RequestModel>();
            RchClient.Registry(requestInfo.IP, Context.ConnectionId, AppInit.Host(httpContext), json);
        }

        public void RchResult(string id, string value)
        {
            if (!AppInit.conf.rch.enable)
                return;

            if (RchClient.rchIds.TryGetValue(id, out var tcs))
                tcs.SetResult(value ?? string.Empty);
        }
        #endregion

        #region Events
        static ConcurrentDictionary<string, string> event_clients = new ConcurrentDictionary<string, string>();

        public void RegistryEvent(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return;

            event_clients.AddOrUpdate(Context.ConnectionId, uid, (k,v) => uid);
        }

        /// <summary>
        /// Отправка сообщений через js
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public async Task events(string uid, string name, string data)
        {
            if (hubClients == null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(name) || (data != null && data.Length > 10_000000))
                return;

            try
            {
                var clients = event_clients.Where(i => i.Value == uid && i.Key != Context.ConnectionId);
                if (clients.Any())
                    await hubClients.Clients(clients.Select(i => i.Key)).SendAsync("event", uid, name, data ?? string.Empty).ConfigureAwait(false);
            }
            catch { }
        }

        public async Task eventsId(string connectionId, string uid, string name, string data)
        {
            if (hubClients == null || string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(name) || (data != null && data.Length > 10_000000))
                return;

            try
            {
                var clients = event_clients.Where(i => i.Key == connectionId);
                if (clients.Any())
                    await hubClients.Clients(clients.Select(i => i.Key)).SendAsync("event", uid ?? string.Empty, name, data ?? string.Empty).ConfigureAwait(false);
            }
            catch { }
        }

        public Task EventsAsync(string connectionId, string uid, string name, string data) => SendEvents(connectionId, uid, name, data);

        /// <summary>
        /// Отправка сообщений через сервер
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="uid"></param>
        /// <param name="name"></param>
        /// <param name="data"></param>
        async public static Task SendEvents(string connectionId, string uid, string name, string data)
        {
            if (hubClients == null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(name) || (data != null && data.Length > 10_000000))
                return;

            try
            {
                var clients = event_clients.Where(i => i.Value == uid && i.Key != connectionId);
                if (clients.Any())
                    await hubClients.Clients(clients.Select(i => i.Key)).SendAsync("event", uid, name, data ?? string.Empty).ConfigureAwait(false);
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
            event_clients.TryRemove(Context.ConnectionId, out _);
            RchClient.OnDisconnected(Context.ConnectionId);

            hubClients = Clients;
            _connections.TryRemove(Context.ConnectionId, out _);

            return base.OnDisconnectedAsync(exception);
        }
        #endregion
    }
}
