using System.Net.WebSockets;
using System.Threading;

namespace Shared.Models
{
    public interface INws
    {
        void WebLog(string message, string plugin);

        Task EventsAsync(string connectionId, string uid, string name, string data);
    }

    public class NwsConnection : IDisposable
    {
        public NwsConnection(string connectionId, WebSocket socket, string host, string ip, string userAgent)
        {
            ConnectionId = connectionId;
            Socket = socket;
            Host = host;
            Ip = ip;
            UserAgent = userAgent;
            SendLock = new SemaphoreSlim(1, 1);
            UpdateActivity();
        }

        public string ConnectionId { get; }

        public WebSocket Socket { get; }

        public string Host { get; }

        public string Ip { get; }

        public string UserAgent { get; }

        public SemaphoreSlim SendLock { get; }

        long _lastActivityTicks;

        CancellationTokenSource _cancellationSource;

        public DateTime LastActivityUtc
        {
            get
            {
                long ticks = Interlocked.Read(ref _lastActivityTicks);
                return new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        public void UpdateActivity()
        {
            Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
        }

        public void SetCancellationSource(CancellationTokenSource source)
        {
            var previous = Interlocked.Exchange(ref _cancellationSource, source);
            previous?.Dispose();
        }

        public void Cancel()
        {
            var source = Interlocked.CompareExchange(ref _cancellationSource, null, null);
            if (source == null)
                return;

            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            SendLock.Dispose();
            Interlocked.Exchange(ref _cancellationSource, null)?.Dispose();
        }
    }
}
