using Microsoft.AspNetCore.SignalR;

namespace Shared.Models
{
    public interface ISoks
    {
        IHubCallerClients AllClients { get; }

        Task EventsAsync(string connectionId, string uid, string name, string data);
    }
}
