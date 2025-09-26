using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Shared.Models
{
    public interface ISoks
    {
        IHubCallerClients AllClients { get; }

        ConcurrentDictionary<string, HubCallerContext> Connections { get; }

        Task EventsAsync(string connectionId, string uid, string name, string data);
    }
}
