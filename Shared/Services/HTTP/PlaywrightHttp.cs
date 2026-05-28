using Microsoft.Playwright;
using Shared.Models.Events;
using Shared.PlaywrightCore;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Shared.Services.HTTP;

public static class PlaywrightHttp
{
    #region EventListener
    async static Task SendPlaywrightHttpResponseEvent(EventPlaywrightHttpResponse eventData)
    {
        foreach (Func<EventPlaywrightHttpResponse, Task> handler in EventListener.PlaywrightHttpResponse.GetInvocationList())
            await handler.Invoke(eventData).ConfigureAwait(false);
    }
    #endregion

    #region Get
    async public static Task<string> Get(BaseSettings init, string url, IReadOnlyList<HeadersModel> headers = null, (string ip, string username, string password) proxy = default, List<Cookie> cookies = null, bool viewsource = true)
    {
        IResponse response = default;
        string result = null;

        try
        {
            using (var browser = new PlaywrightBrowser(init?.priorityBrowser))
            {
                var page = await browser.NewPageAsync(init?.plugin, headers?.ToDictionary(), proxy).ConfigureAwait(false);
                if (page == null)
                    return null;

                if (cookies != null)
                    await page.Context.AddCookiesAsync(cookies).ConfigureAwait(false);

                if (browser.firefox != null)
                {
                    response = await page.GotoAsync(url, new PageGotoOptions()
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    }).ConfigureAwait(false);
                }
                else
                {
                    response = await page.GotoAsync(viewsource ? $"view-source:{url}" : url, new PageGotoOptions()
                    {
                        Timeout = 10_000,
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    }).ConfigureAwait(false);
                }

                if (response != null)
                    result = await response.TextAsync().ConfigureAwait(false);
            }

            if (EventListener.PlaywrightHttpResponse != null)
            {
                await SendPlaywrightHttpResponseEvent(
                    new EventPlaywrightHttpResponse(
                        url: url,
                        method: response?.Request?.Method,
                        status: response?.Status ?? 0,
                        requestHeaders: response?.Request?.Headers,
                        responseHeaders: response?.Headers,
                        result: result,
                        error: null
                    )
                ).ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            if (EventListener.PlaywrightHttpResponse != null)
            {
                await SendPlaywrightHttpResponseEvent(
                    new EventPlaywrightHttpResponse(
                        url: url,
                        method: response?.Request?.Method,
                        status: response?.Status ?? 0,
                        requestHeaders: response?.Request?.Headers,
                        responseHeaders: response?.Headers,
                        result: result,
                        error: ex.ToString()
                    )
                ).ConfigureAwait(false);
            }
        }

        return null;
    }
    #endregion

    #region GetSpan
    static readonly List<Dictionary<string, object>> _responseHeaders = new();

    static readonly Dictionary<string, object> _fetchPatterns = new Dictionary<string, object>()
    {
        ["patterns"] = new[]
        {
            new Dictionary<string, object>
            {
                ["urlPattern"] = "*",
                ["requestStage"] = "Response"
            }
        }
    };

    async public static Task GetSpan(string plugin, string url, Action<ReadOnlySpan<char>> spanAction, IReadOnlyList<HeadersModel> headers = null, (string ip, string username, string password) proxy = default, List<Cookie> cookies = null)
    {
        const int rawSize = 32 * 1024;
        const int timeout = 10_000;

        using (var browser = new PlaywrightBrowser())
        {
            var page = await browser.NewPageAsync(plugin, headers?.ToDictionary(), proxy).ConfigureAwait(false);
            if (page == null)
                return;

            if (cookies != null)
                await page.Context.AddCookiesAsync(cookies).ConfigureAwait(false);

            using (var msm = PoolInvk.msm.GetStream())
            {
                var cdp = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);

                using (var cts = new CancellationTokenSource(timeout))
                {
                    var ct = cts.Token;
                    var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    #region OnEvent
                    cdp.Event("Fetch.requestPaused").OnEvent += async (_, ev) =>
                    {
                        string handle = null, requestId = null;

                        try
                        {
                            JsonElement root = ev.Value;
                            requestId = root.GetProperty("requestId").GetString();
                            int statusCode = root.TryGetProperty("responseStatusCode", out JsonElement statusProp)
                                ? statusProp.GetInt32()
                                : 0;

                            if (statusCode >= 300 && statusCode < 400)
                            {
                                await cdp.SendAsync("Fetch.continueRequest", new()
                                {
                                    ["requestId"] = requestId
                                }).WaitAsync(ct).ConfigureAwait(false);

                                return;
                            }

                            if (statusCode < 200 || statusCode >= 300)
                            {
                                await cdp.SendAsync("Fetch.failRequest", new()
                                {
                                    ["requestId"] = requestId,
                                    ["errorReason"] = "Aborted"
                                }).WaitAsync(ct).ConfigureAwait(false);

                                done.TrySetResult(false);
                                return;
                            }

                            var streamResult = await cdp.SendAsync("Fetch.takeResponseBodyAsStream", new()
                            {
                                ["requestId"] = requestId
                            }).WaitAsync(ct).ConfigureAwait(false);

                            handle = streamResult.Value
                                .GetProperty("stream")
                                .GetString();

                            try
                            {
                                /// это не полноценный stream, у Microsoft.Playwright его нету
                                /// мы разбиваем html на чанки по ~49кб, это позволяет GC эффективней ебать мусор в Gen0 и не складывать в долгую LOH память
                                while (true)
                                {
                                    ct.ThrowIfCancellationRequested();

                                    var ioread = await cdp.SendAsync("IO.read", new()
                                    {
                                        ["handle"] = handle,
                                        ["size"] = rawSize
                                    }).WaitAsync(ct).ConfigureAwait(false);

                                    JsonElement readRoot = ioread.Value;

                                    if (readRoot.TryGetProperty("data", out JsonElement data))
                                    {
                                        if (readRoot.TryGetProperty("base64Encoded", out JsonElement base64Encoded) && base64Encoded.GetBoolean())
                                        {
                                            /// медиа файлы
                                            if (!data.TryGetBytesFromBase64(out byte[] bytes) || bytes == null)
                                                break;

                                            if (bytes.Length > 0)
                                                msm.Write(bytes);
                                        }
                                        else
                                        {
                                            /// html/json
                                            string res = data.GetString();
                                            if (string.IsNullOrEmpty(res))
                                                break;

                                            using (var nbuf = new BufferBytePool(Encoding.UTF8.GetMaxByteCount(res.Length)))
                                            {
                                                int bytesWritten = Encoding.UTF8.GetBytes(res, nbuf.Span);

                                                if (bytesWritten > 0)
                                                    msm.Write(nbuf.Span.Slice(0, bytesWritten));
                                            }
                                        }
                                    }

                                    if (readRoot.TryGetProperty("eof", out JsonElement eof) && eof.GetBoolean())
                                        break;
                                }
                            }
                            finally
                            {
                                if (handle != null)
                                {
                                    try
                                    {
                                        await cdp.SendAsync("IO.close", new()
                                        {
                                            ["handle"] = handle
                                        }).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                                    }
                                    catch { }
                                }
                            }

                            ct.ThrowIfCancellationRequested();

                            await cdp.SendAsync("Fetch.fulfillRequest", new()
                            {
                                ["requestId"] = requestId,
                                ["responseCode"] = statusCode,
                                ["responseHeaders"] = _responseHeaders,
                                ["body"] = "b2s="
                            }).WaitAsync(ct).ConfigureAwait(false);

                            done.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            if (requestId != null)
                            {
                                try
                                {
                                    await cdp.SendAsync("Fetch.failRequest", new()
                                    {
                                        ["requestId"] = requestId,
                                        ["errorReason"] = "Failed"
                                    }).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                                }
                                catch { }
                            }

                            done.TrySetException(ex);
                        }
                    };
                    #endregion

                    try
                    {
                        await cdp.SendAsync("Fetch.enable", _fetchPatterns)
                            .WaitAsync(ct)
                            .ConfigureAwait(false);

                        var gotoTask = page.GotoAsync(url, new PageGotoOptions
                        {
                            Timeout = timeout,
                            WaitUntil = WaitUntilState.Commit
                        });

                        var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, ct);

                        var completedTask = await Task.WhenAny(
                            done.Task,
                            timeoutTask
                        ).ConfigureAwait(false);

                        if (completedTask != done.Task)
                            return;

                        bool ok = await done.Task.ConfigureAwait(false);

                        if (!ok)
                            return;

                        try
                        {
                            await gotoTask.WaitAsync(ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch
                        {
                            // тело всё равно уже получено
                        }

                        msm.Position = 0;

                        OwnerTo.Span(msm, Encoding.UTF8, span =>
                        {
                            if (span.IsEmpty)
                                return;

                            spanAction.Invoke(span);
                        });
                    }
                    catch { }
                    finally
                    {
                        try
                        {
                            await cdp.SendAsync("Fetch.disable")
                                .WaitAsync(TimeSpan.FromSeconds(1))
                                .ConfigureAwait(false);
                        }
                        catch { }

                        try
                        {
                            await cdp.DetachAsync()
                                .WaitAsync(TimeSpan.FromSeconds(1))
                                .ConfigureAwait(false);
                        }
                        catch { }
                    }
                }
            }
        }
    }
    #endregion
}
