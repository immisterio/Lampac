using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Shared.Models
{
    public interface ISoks
    {
        IHubCallerClients Clients { get; }

        ConcurrentDictionary<string, HubCallerContext> Connections { get; }

        void WebLog(string message, string plugin);

        Task EventsAsync(string connectionId, string uid, string name, string data);
    }
}
