using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine
{
    public class soksEvent : Hub
    {
        static IHubCallerClients hubClients = null;

        static Dictionary<string, string> clients = new Dictionary<string, string>();

        public void Registry(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return;

            clients.TryAdd(Context.ConnectionId, uid);
        }

        public async Task events(string uid, string name, string data)
        {
            if (hubClients == null || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(name) || (data != null && data.Length > 10_000000))
                return;

            try
            {
                await hubClients.Clients(clients.Where(i => i.Value == uid && i.Key != Context.ConnectionId).Select(i => i.Key)).SendAsync("event", uid, name, data ?? string.Empty);
            }
            catch { }
        }

        public override Task OnConnectedAsync()
        {
            hubClients = Clients;
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            clients.Remove(Context.ConnectionId);
            hubClients = Clients;
            return base.OnDisconnectedAsync(exception);
        }
    }
}
