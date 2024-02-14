using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace Lampac.Engine
{
    public class soks : Hub
    {
        public static IClientProxy clients = null;

        async public static Task Send(string message, string plugin)
        {
            if (!AppInit.conf.weblog || clients == null || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(plugin) || message.Length > 700_000)
                return;

            await clients.SendAsync("Receive", message, plugin);
        }

        public override Task OnConnectedAsync()
        {
            clients = Clients.All;
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            clients = Clients.All;
            return base.OnDisconnectedAsync(exception);
        }
    }
}
