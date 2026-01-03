using Microsoft.AspNetCore.SignalR;

namespace Shared.Models
{
    public interface ISoks
    {
        IHubCallerClients AllClients { get; }

        void WebLog(string message, string plugin);

        Task EventsAsync(string connectionId, string uid, string name, string data); 
        
        int CountWeblogClients { get; }
    }
}
