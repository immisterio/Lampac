using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;

namespace Shared.Models;

public interface INws
{
    ConcurrentDictionary<string, NwsConnection> AllConnections();

    Task SendAsync(string connectionId, string method, params object[] args);

    Task SendAsync(string connectionId, string method, CancellationToken cancellationToken, params object[] args);

    Task SendAsync(string connectionId, ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);

    Task SendConnectionAsync(NwsConnection connection, ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
}

public class NwsConnection : IDisposable
{
    public NwsConnection(string connectionId, WebSocket socket, string host, RequestModel requestInfo)
    {
        ConnectionId = connectionId;
        Socket = socket;
        Host = host;
        RequestInfo = requestInfo;
        SendLock = new SemaphoreSlim(1, 1);
        UpdateActivity();
    }

    public string ConnectionId { get; }

    public WebSocket Socket { get; }

    public string Host { get; }

    public string Ip => RequestInfo.IP;

    public RequestModel RequestInfo { get; }

    public SemaphoreSlim SendLock { get; }

    #region LastActivityUtc
    long _lastActivityTicks;

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
    #endregion

    #region LastSendActivityUtc
    long _lastSendActivityTicks;

    public DateTime LastSendActivityUtc
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastSendActivityTicks);
            return new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public void UpdateSendActivity()
    {
        Interlocked.Exchange(ref _lastSendActivityTicks, DateTime.UtcNow.Ticks);
    }
    #endregion


    public int SendCancelFlag = 1;
    public bool SendCancel
        => Volatile.Read(ref SendCancelFlag) == 1;

    CancellationTokenSource _cancellationSource;

    public void SetCancellationSource(CancellationTokenSource source)
    {
        var previous = Interlocked.Exchange(ref _cancellationSource, source);
        previous?.Dispose();
    }

    public void Cancel()
    {
        try
        {
            var source = Interlocked.CompareExchange(ref _cancellationSource, null, null);
            if (source == null)
                return;

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
