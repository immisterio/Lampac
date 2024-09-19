using Lampac.Engine.CORE;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lampac.Engine
{
    public class soks : Hub
    {
        #region WebLog
        static Dictionary<string, byte> weblog_clients = new Dictionary<string, byte>();

        public static void SendLog(string message, string plugin)
        {
            if (!AppInit.conf.weblog.enable || hubClients == null || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(plugin) || message.Length > 700_000)
                return;

            hubClients.Clients(weblog_clients.Keys).SendAsync("Receive", message, plugin);
        }
        #endregion

        public static IHubCallerClients hubClients = null;

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

        public override Task OnConnectedAsync()
        {
            hubClients = Clients;
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            weblog_clients.Remove(Context.ConnectionId);
            RchClient.OnDisconnected(Context.ConnectionId);

            hubClients = Clients;
            return base.OnDisconnectedAsync(exception);
        }
    }
}
