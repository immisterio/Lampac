using Microsoft.AspNetCore.Http;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Events;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine
{
    public class nws : INws
    {
        #region fields
        static readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        public static readonly ConcurrentDictionary<string, NwsConnection> _connections = new ConcurrentDictionary<string, NwsConnection>();

        static readonly Timer ConnectionMonitorTimer = new Timer(ConnectionMonitorCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        public readonly static ConcurrentDictionary<string, byte> weblog_clients = new ConcurrentDictionary<string, byte>();

        public readonly static ConcurrentDictionary<string, string> event_clients = new ConcurrentDictionary<string, string>();

        public static int ConnectionCount => _connections.Count;
        #endregion

        #region interface
        public void WebLog(string message, string plugin) => SendLog(message, plugin);

        public Task EventsAsync(string connectionId, string uid, string name, string data) => SendEvents(connectionId, uid, name, data);
        #endregion

        #region handle
        public static async Task HandleWebSocketAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using (var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false))
            {
                string connectionId = Guid.NewGuid().ToString("N");

                if (context.Request.Query.TryGetValue("id", out var _connectionId) && !string.IsNullOrEmpty(_connectionId.ToString()))
                    connectionId = _connectionId.ToString();

                try
                {
                    var requestInfo = context.Features.Get<RequestModel>();
                    string ip = requestInfo.IP ?? context.Connection.RemoteIpAddress?.ToString();

                    var connection = new NwsConnection(connectionId, socket, AppInit.Host(context), ip, requestInfo.UserAgent);

                    var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                    connection.SetCancellationSource(cancellationSource);

                    _connections.AddOrUpdate(connectionId, connection, (k, v) => connection);

                    InvkEvent.NwsConnected(new EventNwsConnected(connectionId, ip, requestInfo, connection, cancellationSource.Token));

                    await SendAsync(connection, "Connected", connectionId).ConfigureAwait(false);
                    await ReceiveLoopAsync(connection, cancellationSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    Cleanup(connectionId);
                    InvkEvent.NwsDisconnected(new EventNwsDisconnected(connectionId));
                }
            }
        }
        #endregion

        #region receive loop
        static async Task ReceiveLoopAsync(NwsConnection connection, CancellationToken token)
        {
            WebSocket socket = connection.Socket;
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            var builder = new StringBuilder();

            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    builder.Clear();

                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                                await socket.CloseAsync(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, cts.Token).ConfigureAwait(false);
                            return;
                        }

                        if (result.Count > 0)
                        {
                            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                            if (builder.Length > 10_000000)
                            {
                                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                                    await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "payload too large", cts.Token).ConfigureAwait(false);
                                return;
                            }
                        }
                    }
                    while (!result.EndOfMessage);

                    connection.UpdateActivity();

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = builder.ToString();
                        builder.Clear();

                        if (!string.IsNullOrWhiteSpace(message) && message != "ping")
                            await HandleMessageAsync(connection, message).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                builder.Clear();
            }
        }
        #endregion

        #region message handling
        static async Task HandleMessageAsync(NwsConnection connection, string payload)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return;

                if (!document.RootElement.TryGetProperty("method", out var methodProp))
                    return;

                string method = methodProp.GetString();
                JsonElement args = default;

                if (document.RootElement.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Array)
                    args = argsProp;

                await InvokeAsync(connection, method, args).ConfigureAwait(false);
            }
            catch (JsonException)
            {
            }
        }

        static async Task InvokeAsync(NwsConnection connection, string method, JsonElement args)
        {
            if (string.IsNullOrEmpty(method))
                return;

            switch (method.ToLower())
            {
                case "rchregistry":
                    if (AppInit.conf.rch.enable)
                    {
                        string json = GetStringArg(args, 0);
                        RchClient.Registry(connection.Ip, connection.ConnectionId, connection.Host, json, connection);
                        await SendAsync(connection, "RchRegistry", connection.Ip).ConfigureAwait(false);
                    }
                    break;

                case "rchresult":
                    if (AppInit.conf.rch.enable)
                    {
                        string id = GetStringArg(args, 0);
                        string value = GetStringArg(args, 1) ?? string.Empty;

                        if (!string.IsNullOrEmpty(id) && RchClient.rchIds.TryGetValue(id, out var tcs))
                            tcs.TrySetResult(value);
                    }
                    break;

                case "registryweblog":
                    if (AppInit.conf.weblog.enable)
                    {
                        string token = GetStringArg(args, 0);
                        if (string.IsNullOrEmpty(AppInit.conf.weblog.token) || AppInit.conf.weblog.token == token)
                            weblog_clients.AddOrUpdate(connection.ConnectionId, 0, static (_, __) => 0);
                    }
                    break;

                case "weblog":
                    SendLog(GetStringArg(args, 0), GetStringArg(args, 1));
                    break;

                case "registryevent":
                {
                    string uid = GetStringArg(args, 0);
                    if (!string.IsNullOrEmpty(uid))
                        event_clients.AddOrUpdate(connection.ConnectionId, uid, (_, __) => uid);
                    break;
                }

                case "events":
                {
                    string uid = GetStringArg(args, 0);
                    string name = GetStringArg(args, 1);
                    string data = GetStringArg(args, 2);

                    if (name == "devices")
                    {
                        var evc = event_clients.Where(i => i.Value == uid).ToArray();

                        var devices = _connections
                            .Where(i => i.Value.ConnectionId != connection.ConnectionId)
                            .Where(i => i.Value.Ip == connection.Ip || event_clients.Values.Contains(uid))
                            .Select(i => new {
                                uid = event_clients.TryGetValue(i.Value.ConnectionId, out var _uid) ? _uid : null, 
                                i.Value.ConnectionId, 
                                i.Value.UserAgent
                            })
                            .ToArray();

                        await SendAsync(connection, "event", uid, name, devices).ConfigureAwait(false);
                        break;
                    }

                    await SendEvents(connection.ConnectionId, uid, name, data).ConfigureAwait(false);
                    break;
                }

                case "eventsid":
                {
                    string targetConnection = GetStringArg(args, 0);
                    string uid = GetStringArg(args, 1);
                    string name = GetStringArg(args, 2);
                    string data = GetStringArg(args, 3);
                    await SendEventToConnection(targetConnection, uid, name, data).ConfigureAwait(false);
                    break;
                }

                case "ping":
                    await SendAsync(connection, "pong").ConfigureAwait(false);
                    break;
            }
        }

        static string GetStringArg(JsonElement args, int index)
        {
            if (args.ValueKind != JsonValueKind.Array || args.GetArrayLength() <= index)
                return null;

            var element = args[index];
            if (element.ValueKind == JsonValueKind.String)
                return element.GetString();

            if (element.ValueKind == JsonValueKind.Null)
                return null;

            return element.ToString();
        }
        #endregion

        #region send
        static async Task SendAsync(NwsConnection connection, string method, params object[] args)
        {
            if (connection.Socket.State != WebSocketState.Open || string.IsNullOrEmpty(method))
                return;

            var payload = new { method, args = args ?? Array.Empty<object>() };
            byte[] bytes;

            try
            {
                bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, serializerOptions));
            }
            catch
            {
                return;
            }

            try
            {
                await connection.SendLock.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

                if (connection.Socket.State == WebSocketState.Open)
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                        await connection.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);

                    connection.UpdateActivity();
                }
            }
            catch (WebSocketException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                connection.SendLock.Release();
            }
        }

        public static void SendLog(string message, string plugin)
        {
            if (!AppInit.conf.weblog.enable || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(plugin) || message.Length > 4_000000)
                return;

            if (weblog_clients.IsEmpty)
                return;

            foreach (string connectionId in weblog_clients.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var client))
                    _ = SendAsync(client, "Receive", message, plugin).ConfigureAwait(false);
            }
        }

        public static Task SendEvents(string connectionId, string uid, string name, string data)
        {
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(name))
                return Task.CompletedTask;

            var targets = event_clients.Where(i => i.Value == uid && (connectionId == null || i.Key != connectionId)).Select(i => i.Key).ToArray();
            if (targets.Length == 0)
                return Task.CompletedTask;

            var tasks = new List<Task>(targets.Length);
            foreach (string targetId in targets)
            {
                if (_connections.TryGetValue(targetId, out var client))
                    tasks.Add(SendAsync(client, "event", uid, name, data ?? string.Empty));
            }

            if (tasks.Count == 0)
                return Task.CompletedTask;

            return Task.WhenAll(tasks);
        }

        static Task SendEventToConnection(string connectionId, string uid, string name, string data)
        {
            if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(name))
                return Task.CompletedTask;

            if (_connections.TryGetValue(connectionId, out var client))
                return SendAsync(client, "event", uid ?? string.Empty, name, data ?? string.Empty);

            return Task.CompletedTask;
        }

        public static Task SendRchRequestAsync(string connectionId, string rchId, string url, string data, Dictionary<string, string> headers, bool returnHeaders)
        {
            if (string.IsNullOrEmpty(connectionId))
                return Task.CompletedTask;

            if (_connections.TryGetValue(connectionId, out var client))
                return SendAsync(client, "RchClient", rchId, url, data, headers, returnHeaders);

            return Task.CompletedTask;
        }
        #endregion

        #region cleanup
        public static void Cleanup(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
                return;

            if (_connections.TryRemove(connectionId, out var connection))
            {
                connection.Cancel();
                connection.Dispose();
            }

            weblog_clients.TryRemove(connectionId, out _);
            event_clients.TryRemove(connectionId, out _);
            RchClient.OnDisconnected(connectionId);
        }
        #endregion

        #region ConnectionMonitorCallback
        static void ConnectionMonitorCallback(object state)
        {
            try
            {
                if (_connections.IsEmpty)
                    return;

                foreach (string connectionId in _connections.Select(kv => kv.Key).ToArray())
                {
                    if (_connections.TryGetValue(connectionId, out var connection))
                    {
                        if (DateTime.UtcNow.AddMinutes(-2) >= connection.LastActivityUtc)
                            connection.Cancel();
                    }
                }
            }
            catch
            {
            }
        }
        #endregion


        #region FullDispose
        public static void FullDispose()
        {
            ConnectionMonitorTimer.Dispose();
            foreach (var connection in _connections)
                connection.Value.Cancel();
        }
        #endregion
    }
}
