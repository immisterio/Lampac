using System.Collections.Concurrent;

namespace Shared.Models
{
    public interface INws
    {
        ConcurrentDictionary<string, NwsClientInfo> Connections { get; }

        void WebLog(string message, string plugin);

        Task EventsAsync(string connectionId, string uid, string name, string data);
    }

    public class NwsClientInfo
    {
        public string ConnectionId { get; set; }

        public string Ip { get; set; }

        public string Host { get; set; }

        public string UserAgent { get; set; }

        public DateTime ConnectedAtUtc { get; set; }
    }
}
