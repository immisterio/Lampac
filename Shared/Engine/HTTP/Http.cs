using Newtonsoft.Json;
using Shared.Engine.Pools;
using Shared.Engine.Utilities;
using Shared.Models;
using Shared.Models.Events;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Shared.Engine
{
    public static class Http
    {
        static readonly ThreadLocal<JsonSerializer> _serializerDefault = new ThreadLocal<JsonSerializer>(JsonSerializer.CreateDefault);

        static readonly ThreadLocal<JsonSerializer> _serializerIgnoreDeserialize = new ThreadLocal<JsonSerializer>(() => JsonSerializer.Create(new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } }));

        public static IHttpClientFactory httpClientFactory;


        #region defaultHeaders / UserAgent
        public static readonly Dictionary<string, string> defaultUaHeaders = new Dictionary<string, string>()
        {
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["sec-ch-ua"] = "\"Chromium\";v=\"142\", \"Google Chrome\";v=\"142\", \"Not_A Brand\";v=\"99\"",
            ["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36"
        };

        public static readonly Dictionary<string, string> defaultCommonHeaders = new Dictionary<string, string>()
        {
            ["cache-control"] = "no-cache",
            ["dnt"] = "1",
            ["pragma"] = "no-cache",
            ["priority"] = "u=0, i"
        };

        public static readonly Dictionary<string, string> defaultFullHeaders = defaultUaHeaders.Concat(defaultCommonHeaders).ToDictionary(
            kv => kv.Key,
            kv => kv.Value
        );

        public static string UserAgent => defaultUaHeaders["user-agent"];
        #endregion

        #region Handler
        public static HttpClientHandler Handler(string url, WebProxy proxy, CookieContainer cookieContainer = null)
        {
            return Handler(url, proxy, null, cookieContainer);
        }

        static HttpClientHandler Handler(string url, WebProxy proxy, StringBuilder loglines, CookieContainer cookieContainer = null)
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            if (proxy != null)
            {
                handler.UseProxy = true;
                handler.Proxy = proxy;

                if (loglines != null && IsLogged)
                    loglines.Append($"proxy: {proxy.Address.ToString()}\n");
            }
            else
            {
                handler.UseProxy = false;
            }

            if (cookieContainer != null)
            {
                handler.CookieContainer = cookieContainer;
                handler.UseCookies = true; //<-- Enable the use of cookies.
            }

            if (AppInit.conf.globalproxy != null && AppInit.conf.globalproxy.Length > 0)
            {
                foreach (var p in AppInit.conf.globalproxy)
                {
                    if (p.list == null || p.list.Length == 0 || p.pattern == null)
                        continue;

                    if (Regex.IsMatch(url, p.pattern, RegexOptions.IgnoreCase))
                    {
                        string proxyip = p.list.OrderBy(a => Guid.NewGuid()).First();

                        NetworkCredential credentials = null;

                        if (proxyip.Contains("@"))
                        {
                            var g = Regex.Match(proxyip, p.pattern_auth).Groups;
                            proxyip = g["sheme"].Value + g["host"].Value;
                            credentials = new NetworkCredential(g["username"].Value, g["password"].Value);
                        }
                        else if (p.useAuth)
                            credentials = new NetworkCredential(p.username, p.password);

                        handler.UseProxy = true;
                        handler.Proxy = new WebProxy(proxyip, p.BypassOnLocal, null, credentials);

                        if (loglines != null && IsLogged)
                            loglines.Append($"globalproxy: {proxyip} {(p.useAuth ? $" - {p.username}:{p.password}" : "")}\n");

                        break;
                    }
                }
            }

            if (InvkEvent.IsHttpClientHandler())
                InvkEvent.HttpClientHandler(new EventHttpHandler(url, handler, proxy, cookieContainer, Startup.memoryCache));

            return handler;
        }
        #endregion


        #region DefaultRequestHeaders
        public static void DefaultRequestHeaders(string url, HttpRequestMessage client, string cookie, string referer, List<HeadersModel> headers, bool useDefaultHeaders = true)
        {
            DefaultRequestHeaders(url, client, cookie, referer, headers, null, useDefaultHeaders);
        }

        public static void DefaultRequestHeaders(string url, HttpRequestMessage client, string cookie, string referer, List<HeadersModel> headers, StringBuilder loglines, bool useDefaultHeaders = true)
        {
            var addHeaders = new Dictionary<string, string>();

            if (useDefaultHeaders)
            {
                addHeaders.TryAdd("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                addHeaders.TryAdd("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5");
            }

            if (cookie != null)
                addHeaders.TryAdd("cookie", cookie);

            if (referer != null)
                addHeaders.TryAdd("referer", referer);

            if (useDefaultHeaders)
            {
                if (headers != null && headers.FirstOrDefault(i => i.name.ToLower() == "user-agent") != null)
                {
                    foreach (var h in defaultCommonHeaders)
                        addHeaders.TryAdd(h.Key.ToLower().Trim(), h.Value);
                }
                else
                {
                    foreach (var h in defaultFullHeaders)
                        addHeaders.TryAdd(h.Key.ToLower().Trim(), h.Value);
                }
            }

            if (headers != null)
            {
                foreach (var h in headers)
                    addHeaders[h.name.ToLower().Trim()] = h.val;
            }

            if (NormalizeHeaders(addHeaders) is var normalizeHeaders && normalizeHeaders != null)
            {
                foreach (var h in normalizeHeaders)
                {
                    if (client.Headers.TryAddWithoutValidation(h.Key, h.Value))
                    {
                        if (IsLogged && loglines != null)
                            loglines.Append($"{h.Key}: {h.Value}\n");
                    }
                    else if (client.Content?.Headers != null)
                    {
                        if (client.Content.Headers.TryAddWithoutValidation(h.Key, h.Value))
                        {
                            if (IsLogged && loglines != null)
                                loglines.Append($"{h.Key}: {h.Value}\n");
                        }
                    }
                }
            }

            if (InvkEvent.IsHttpClientHeaders())
                InvkEvent.HttpClientHeaders(new EventHttpHeaders(url, client, cookie, referer, headers, useDefaultHeaders, Startup.memoryCache));
        }
        #endregion

        #region NormalizeHeaders
        public static Dictionary<string, T> NormalizeHeaders<T>(Dictionary<string, T> raw)
        {
            if (raw == null || raw.Count == 0)
                return null;

            var result = new Dictionary<string, T>(raw.Count, StringComparer.Ordinal);

            foreach (var kv in raw) 
                result[NormalizeHeaderName(kv.Key)] = kv.Value;

            return result;
        }

        public static Dictionary<string, string> NormalizeHeaders(List<HeadersModel> raw)
        {
            if (raw == null || raw.Count == 0)
                return null;

            var result = new Dictionary<string, string>(raw.Count, StringComparer.Ordinal);

            foreach (var kv in raw)
                result[NormalizeHeaderName(kv.name)] = kv.val;

            return result;
        }

        private static string NormalizeHeaderName(string key)
        {
            return string.Create(key.Length, key, static (span, src) =>
            {
                bool upper = true;
                for (int i = 0; i < src.Length; i++)
                {
                    char c = src[i];
                    if (c == '-')
                    {
                        span[i] = '-';
                        upper = true;
                        continue;
                    }

                    if (upper)
                    {
                        // Для заголовков обычно корректнее invariant
                        span[i] = char.ToUpperInvariant(c);
                        upper = false;
                    }
                    else
                    {
                        span[i] = c;
                    }
                }
            });
        }
        #endregion


        #region GetLocation
        async public static Task<string> GetLocation(string url, string referer = null, int timeoutSeconds = 8, List<HeadersModel> headers = null, int httpversion = 1, bool allowAutoRedirect = false, WebProxy proxy = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = allowAutoRedirect;

                var client = FrendlyHttp.MessageClient(httpversion == 2 ? "http2" : "base", handler);

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = httpversion == 1 ? HttpVersion.Version11 : new Version(httpversion, 0)
                };

                DefaultRequestHeaders(url, req, null, referer, headers);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))))
                {
                    using (HttpResponseMessage response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false))
                    {
                        string location = (int)response.StatusCode == 301 || (int)response.StatusCode == 302 || (int)response.StatusCode == 307 ? response.Headers.Location?.ToString() : response.RequestMessage.RequestUri?.ToString();
                        location = Uri.EscapeUriString(System.Web.HttpUtility.UrlDecode(location ?? ""));

                        return string.IsNullOrWhiteSpace(location) ? null : location;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region ResponseHeaders
        async public static Task<HttpResponseMessage> ResponseHeaders(string url, int timeoutSeconds = 8, List<HeadersModel> headers = null, int httpversion = 1, bool allowAutoRedirect = false, WebProxy proxy = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = allowAutoRedirect;

                var client = FrendlyHttp.MessageClient(httpversion == 2 ? "http2" : "base", handler);

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = httpversion == 1 ? HttpVersion.Version11 : new Version(httpversion, 0)
                };

                DefaultRequestHeaders(url, req, null, null, headers);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))))
                    using (HttpResponseMessage response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false))
                        return response;
            }
            catch
            {
                return null;
            }
        }
        #endregion


        #region Get<T>
        async public static Task<T> Get<T>(string url, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool weblog = true, HttpContent body = null)
        {
            return (await BaseGetAsync<T>(
                url, cookie, referer, MaxResponseContentBufferSize, timeoutSeconds, headers, IgnoreDeserializeObject, proxy,statusCodeOK, httpversion, cookieContainer, useDefaultHeaders, body, weblog
            ).ConfigureAwait(false)).content;
        }
        #endregion

        #region BaseGetAsync<T>
        async public static Task<(T content, HttpResponseMessage response)> BaseGetAsync<T>(string url, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null, bool weblog = true)
        {
            var ms = PoolInvk.msm.GetStream();

            try
            {
                T result = default;

                var req = await BaseGetReaderAsync(async e =>
                {
                    try
                    {
                        await e.stream.CopyToAsync(ms, PoolInvk.bufferSize, e.ct);
                        ms.Position = 0;

                        using (var streamReader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: PoolInvk.bufferSize, leaveOpen: true))
                        {
                            using (var jsonReader = new JsonTextReader(streamReader)
                            {
                                ArrayPool = NewtonsoftPool.Array
                            })
                            {
                                var serializer = IgnoreDeserializeObject
                                    ? _serializerIgnoreDeserialize.Value
                                    : _serializerDefault.Value;

                                result = serializer.Deserialize<T>(jsonReader);

                                if (e.loglines != null)
                                    e.loglines.Append($"\n{JsonConvert.SerializeObject(result, Formatting.Indented)}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (e.loglines != null)
                            e.loglines.Append($"\n{ex.Message}");
                    }
                },
                    url, cookie, referer, MaxResponseContentBufferSize, timeoutSeconds, headers, IgnoreDeserializeObject, proxy, statusCodeOK, httpversion, cookieContainer, useDefaultHeaders, body, weblog
                ).ConfigureAwait(false);

                return (result, req.response);
            }
            finally
            {
                ms.Dispose();
            }
        }
        #endregion

        #region BaseGetReaderAsync
        async public static Task<(bool success, HttpResponseMessage response)> BaseGetReaderAsync(Action<(Stream stream, CancellationToken ct, StringBuilder loglines)> action, string url, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null, bool weblog = true)
        {
            try
            {
                var loglines = IsLogged && weblog 
                    ? StringBuilderPool.Rent() 
                    : null;

                try
                {
                    var handler = Handler(url, proxy, loglines, cookieContainer);

                    var client = FrendlyHttp.MessageClient(httpversion == 1 ? "base" : $"http{httpversion}", handler, MaxResponseContentBufferSize);

                    if (cookieContainer != null && loglines != null)
                    {
                        var cookiesString = new StringBuilder(200);
                        foreach (Cookie c in cookieContainer.GetCookies(new Uri(url)))
                            cookiesString.Append($"{c.Name}={c.Value}; ");

                        if (cookiesString.Length > 0)
                            loglines.Append($"Cookie: {cookiesString.ToString().TrimEnd(' ', ';')}\n");
                    }

                    var req = new HttpRequestMessage(HttpMethod.Get, url)
                    {
                        Version = httpversion == 1 ? HttpVersion.Version11 : new Version(httpversion, 0),
                        Content = body
                    };

                    DefaultRequestHeaders(url, req, cookie, referer, headers, loglines, useDefaultHeaders);

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))))
                    {
                        using (HttpResponseMessage response = await client.SendAsync(req, cts.Token).ConfigureAwait(false))
                        {
                            if (loglines != null)
                            {
                                loglines.Append($"\n\nStatusCode: {(int)response.StatusCode}\n");

                                foreach (var h in response.Headers)
                                {
                                    if (h.Key == "Set-Cookie")
                                    {
                                        foreach (string v in h.Value)
                                            loglines.Append($"{h.Key}: {v}\n");
                                    }
                                    else
                                        loglines.Append($"{h.Key}: {string.Join("", h.Value)}\n");
                                }
                            }

                            using (HttpContent content = response.Content)
                            {
                                if (InvkEvent.IsHttpAsync())
                                    await InvkEvent.HttpAsync(new EventHttpResponse(url, null, client, "ReadAsStream", response, Startup.memoryCache));

                                if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                    return (false, response);

                                using (var stream = await content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false))
                                {
                                    action.Invoke((stream, cts.Token, loglines));
                                    return (true, response);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (loglines != null)
                        loglines.Append(ex.ToString());

                    if (InvkEvent.IsHttpAsync())
                    {
                        await InvkEvent.HttpAsync(new EventHttpResponse(url, null, null, ex.ToString(), new HttpResponseMessage()
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            RequestMessage = new HttpRequestMessage()
                        }, Startup.memoryCache));
                    }

                    return (false, new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        RequestMessage = new HttpRequestMessage()
                    });
                }
                finally
                {
                    if (!url.Contains("127.0.0.1") && loglines != null)
                        WriteLog(url, "GET", body == null ? null : body.ReadAsStringAsync().Result, loglines);

                    StringBuilderPool.Return(loglines);
                }
            }
            catch
            {
                return default;
            }
        }
        #endregion


        #region GetSpan
        async public static Task<bool> GetSpan(Action<ReadOnlySpan<char>> spanAction, string url, Encoding encoding = default, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null, bool weblog = true)
        {
            var ms = PoolInvk.msm.GetStream();

            try
            {
                var req = await BaseGetReaderAsync(async e =>
                {
                    try
                    {
                        await e.stream.CopyToAsync(ms, PoolInvk.bufferSize, e.ct);
                        ms.Position = 0;

                        OwnerTo.Span(ms, encoding != default ? encoding : Encoding.UTF8, span =>
                        {
                            if (span.IsEmpty)
                                return;

                            spanAction.Invoke(span);

                            if (e.loglines != null)
                                e.loglines.Append($"\n{span.ToString()}");
                        });
                    }
                    catch (Exception ex) 
                    {
                        if (e.loglines != null)
                            e.loglines.Append($"\n{ex.Message}");
                    }
                },
                    url, cookie, referer, MaxResponseContentBufferSize, timeoutSeconds, headers, IgnoreDeserializeObject, proxy, statusCodeOK, httpversion, cookieContainer, useDefaultHeaders, body, weblog
                ).ConfigureAwait(false);

                return req.success;
            }
            finally
            {
                ms.Dispose();
            }
        }
        #endregion

        #region PostSpan
        async public static Task<bool> PostSpan(Action<ReadOnlySpan<char>> spanAction, string url, string data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, int httpversion = 1, int MaxResponseContentBufferSize = 0, bool statusCodeOK = true)
        {
            var ms = PoolInvk.msm.GetStream();

            try
            {
                var req = await BasePostReaderAsync(async e =>
                {
                    try
                    {
                        await e.stream.CopyToAsync(ms, PoolInvk.bufferSize, e.ct);
                        ms.Position = 0;

                        OwnerTo.Span(ms, encoding != default ? encoding : Encoding.UTF8, span => 
                        {
                            if (span.IsEmpty)
                                return;

                            spanAction.Invoke(span);

                            if (e.loglines != null)
                                e.loglines.Append($"\n{span.ToString()}");
                        });
                    }
                    catch (Exception ex)
                    {
                        if (e.loglines != null)
                            e.loglines.Append($"\n{ex.Message}");
                    }
                },
                    url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"),
                    cookie, MaxResponseContentBufferSize, timeoutSeconds, headers, proxy, httpversion, cookieContainer, useDefaultHeaders, IgnoreDeserializeObject, statusCodeOK
                ).ConfigureAwait(false);

                return req.success;
            }
            finally
            {
                ms.Dispose();
            }
        }
        #endregion


        #region Get
        async public static Task<string> Get(string url, Encoding encoding = default, string cookie = null, string referer = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, long MaxResponseContentBufferSize = 0, WebProxy proxy = null, int httpversion = 1, bool statusCodeOK = true, bool weblog = true, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null)
        {
            return (await BaseGet(url, encoding, cookie: cookie, referer: referer, timeoutSeconds: timeoutSeconds, headers: headers, MaxResponseContentBufferSize: MaxResponseContentBufferSize, proxy: proxy, httpversion: httpversion, statusCodeOK: statusCodeOK, weblog: weblog, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, body: body).ConfigureAwait(false)).content;
        }
        #endregion

        #region BaseGet
        async public static Task<(string content, HttpResponseMessage response)> BaseGet(string url, Encoding encoding = default, string cookie = null, string referer = null, int timeoutSeconds = 15, long MaxResponseContentBufferSize = 0, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, bool statusCodeOK = true, bool weblog = true, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null)
        {
            var loglines = IsLogged && weblog
                ? StringBuilderPool.Rent()
                : null;

            try
            {
                var handler = Handler(url, proxy, loglines, cookieContainer);

                var client = FrendlyHttp.MessageClient(httpversion == 1 ? "base" : $"http{httpversion}", handler, MaxResponseContentBufferSize);

                if (cookieContainer != null && loglines != null)
                {
                    var cookiesString = new StringBuilder(200);
                    foreach (Cookie c in cookieContainer.GetCookies(new Uri(url)))
                        cookiesString.Append($"{c.Name}={c.Value}; ");

                    if (cookiesString.Length > 0)
                        loglines.Append($"Cookie: {cookiesString.ToString().TrimEnd(' ', ';')}\n");
                }

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = httpversion == 1 ? HttpVersion.Version11 : new Version(httpversion, 0),
                    Content = body
                };

                DefaultRequestHeaders(url, req, cookie, referer, headers, loglines, useDefaultHeaders);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))))
                {
                    using (HttpResponseMessage response = await client.SendAsync(req, cts.Token).ConfigureAwait(false))
                    {
                        if (loglines != null)
                        {
                            loglines.Append($"\n\nStatusCode: {(int)response.StatusCode}\n");

                            foreach (var h in response.Headers)
                            {
                                if (h.Key == "Set-Cookie")
                                {
                                    foreach (string v in h.Value)
                                        loglines.Append($"{h.Key}: {v}\n");
                                }
                                else
                                    loglines.Append($"{h.Key}: {string.Join("", h.Value)}\n");
                            }
                        }

                        using (HttpContent content = response.Content)
                        {
                            if (encoding != default)
                            {
                                string res = encoding.GetString(await content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false));

                                if (InvkEvent.IsHttpAsync())
                                    await InvkEvent.HttpAsync(new EventHttpResponse(url, null, client, res, response, Startup.memoryCache));

                                if (string.IsNullOrWhiteSpace(res))
                                    return (null, response);

                                if (loglines != null)
                                    loglines.Append($"\n{res}");

                                if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                    return (null, response);

                                return (res, response);
                            }
                            else
                            {
                                string res = await content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

                                if (InvkEvent.IsHttpAsync())
                                    await InvkEvent.HttpAsync(new EventHttpResponse(url, null, client, res, response, Startup.memoryCache));

                                if (string.IsNullOrWhiteSpace(res))
                                    return (null, response);

                                if (loglines != null)
                                    loglines.Append($"\n{res}");

                                if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                    return (null, response);

                                return (res, response);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (loglines != null)
                    loglines.Append(ex.ToString());

                if (InvkEvent.IsHttpAsync())
                {
                    await InvkEvent.HttpAsync(new EventHttpResponse(url, null, null, ex.ToString(), new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        RequestMessage = new HttpRequestMessage()
                    }, Startup.memoryCache));
                }

                return (null, new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    RequestMessage = new HttpRequestMessage()
                });
            }
            finally
            {
                if (!url.Contains("127.0.0.1") && loglines != null)
                    WriteLog(url, "GET", body == null ? null : body.ReadAsStringAsync().Result, loglines);

                StringBuilderPool.Return(loglines);
            }
        }
        #endregion


        #region Post
        public static Task<string> Post(string url, string data, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool removeContentType = false, Encoding encoding = default, bool statusCodeOK = true)
        {
            return Post(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), 
                encoding, cookie, MaxResponseContentBufferSize, timeoutSeconds, headers, proxy, httpversion, cookieContainer, useDefaultHeaders, removeContentType, statusCodeOK
            );
        }

        async public static Task<string> Post(string url, HttpContent data, Encoding encoding = default, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool removeContentType = false, bool statusCodeOK = true)
        {
            return (await BasePost(
                url, data, encoding, cookie, MaxResponseContentBufferSize, timeoutSeconds, headers, proxy, httpversion, cookieContainer, useDefaultHeaders, removeContentType, statusCodeOK
            ).ConfigureAwait(false)).content;
        }
        #endregion

        #region BasePost
        async public static Task<(string content, HttpResponseMessage response)> BasePost(string url, HttpContent data, Encoding encoding = default, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool removeContentType = false, bool statusCodeOK = true)
        {
            var loglines = IsLogged
                ? StringBuilderPool.Rent()
                 : null;

            try
            {
                var handler = Handler(url, proxy, loglines, cookieContainer);

                var client = FrendlyHttp.MessageClient(httpversion == 1 ? "base" : $"http{httpversion}", handler, MaxResponseContentBufferSize);

                if (cookieContainer != null && loglines != null)
                {
                    var cookiesString = new StringBuilder(200);
                    foreach (Cookie c in cookieContainer.GetCookies(new Uri(url)))
                        cookiesString.Append($"{c.Name}={c.Value}; ");

                    if (cookiesString.Length > 0)
                        loglines.Append($"Cookie: {cookiesString.ToString().TrimEnd(' ', ';')}\n");
                }

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Version = httpversion == 1 ? HttpVersion.Version11 : new Version(httpversion, 0),
                    Content = data
                };

                DefaultRequestHeaders(url, req, cookie, null, headers, loglines, useDefaultHeaders);

                if (removeContentType)
                    req.Content.Headers.Remove("Content-Type");

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))))
                {
                    using (HttpResponseMessage response = await client.SendAsync(req, cts.Token).ConfigureAwait(false))
                    {
                        if (loglines != null)
                        {
                            loglines.Append($"\n\nStatusCode: {(int)response.StatusCode}\n");

                            foreach (var h in response.Headers)
                            {
                                if (h.Key == "Set-Cookie")
                                {
                                    foreach (string v in h.Value)
                                        loglines.Append($"{h.Key}: {v}\n");
                                }
                                else
                                    loglines.Append($"{h.Key}: {string.Join("", h.Value)}\n");
                            }
                        }

                        using (HttpContent content = response.Content)
                        {
                            if (encoding != default)
                            {
                                string res = encoding.GetString(await content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false));

                                if (InvkEvent.IsHttpAsync())
                                    await InvkEvent.HttpAsync(new EventHttpResponse(url, data, client, res, response, Startup.memoryCache));

                                if (string.IsNullOrWhiteSpace(res))
                                    return (null, response);

                                if (loglines != null)
                                    loglines.Append($"\n{res}");

                                if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                    return (null, response);

                                return (res, response);
                            }
                            else
                            {
                                string res = await content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

                                if (InvkEvent.IsHttpAsync())
                                    await InvkEvent.HttpAsync(new EventHttpResponse(url, data, client, res, response, Startup.memoryCache));

                                if (string.IsNullOrWhiteSpace(res))
                                    return (null, response);

                                if (loglines != null)
                                    loglines.Append($"\n{res}");

                                if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                    return (null, response);

                                return (res, response);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (loglines != null)
                    loglines.Append(ex.ToString());

                if (InvkEvent.IsHttpAsync())
                {
                    await InvkEvent.HttpAsync(new EventHttpResponse(url, data, null, ex.ToString(), new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        RequestMessage = new HttpRequestMessage()
                    }, Startup.memoryCache));
                }

                return (null, new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    RequestMessage = new HttpRequestMessage()
                });
            }
            finally
            {
                if (!url.Contains("127.0.0.1") && loglines != null)
                    WriteLog(url, "POST", data.ReadAsStringAsync().Result, loglines);

                StringBuilderPool.Return(loglines);
            }
        }
        #endregion


        #region Post<T>
        public static Task<T> Post<T>(string url, string data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, int httpversion = 1, int MaxResponseContentBufferSize = 0, bool statusCodeOK = true)
        {
            return Post<T>(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"),
                cookie, timeoutSeconds, headers, encoding, proxy, IgnoreDeserializeObject, cookieContainer, useDefaultHeaders, httpversion, MaxResponseContentBufferSize, statusCodeOK
            );
        }

        async public static Task<T> Post<T>(string url, HttpContent data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, int httpversion = 1, int MaxResponseContentBufferSize = 0, bool statusCodeOK = true)
        {
            var ms = PoolInvk.msm.GetStream();

            try
            {
                T result = default;

                var req = await BasePostReaderAsync(async e =>
                {
                    try
                    {
                        await e.stream.CopyToAsync(ms, PoolInvk.bufferSize, e.ct);
                        ms.Position = 0;

                        var encdg = encoding != default ? encoding : Encoding.UTF8;

                        using (var streamReader = new StreamReader(ms, encdg, detectEncodingFromByteOrderMarks: false, bufferSize: PoolInvk.bufferSize, leaveOpen: true))
                        {
                            using (var jsonReader = new JsonTextReader(streamReader)
                            { 
                                ArrayPool = NewtonsoftPool.Array
                            })
                            {
                                var serializer = IgnoreDeserializeObject
                                    ? _serializerIgnoreDeserialize.Value
                                    : _serializerDefault.Value;

                                result = serializer.Deserialize<T>(jsonReader);

                                if (e.loglines != null)
                                    e.loglines.Append($"\n{JsonConvert.SerializeObject(result, Formatting.Indented)}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (e.loglines != null)
                            e.loglines.Append($"\n{ex.Message}");
                    }
                },
                    url, data, cookie, MaxResponseContentBufferSize, timeoutSeconds, headers, proxy, httpversion, cookieContainer, useDefaultHeaders, IgnoreDeserializeObject, statusCodeOK
                ).ConfigureAwait(false);

                return result;
            }
            finally
            {
                ms.Dispose();
            }
        }
        #endregion

        #region BasePostReaderAsync
        async public static Task<(bool success, HttpResponseMessage response)> BasePostReaderAsync(Action<(Stream stream, CancellationToken ct, StringBuilder loglines)> action, string url, HttpContent data, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool IgnoreDeserializeObject = false, bool statusCodeOK = true)
        {
            var loglines = IsLogged
                ? StringBuilderPool.Rent()
                : null;

            try
            {
                var handler = Handler(url, proxy, loglines, cookieContainer);

                var client = FrendlyHttp.MessageClient(httpversion == 1 ? "base" : $"http{httpversion}", handler, MaxResponseContentBufferSize);

                if (cookieContainer != null && loglines != null)
                {
                    var cookiesString = new StringBuilder(200);
                    foreach (Cookie c in cookieContainer.GetCookies(new Uri(url)))
                        cookiesString.Append($"{c.Name}={c.Value}; ");

                    if (cookiesString.Length > 0)
                        loglines.Append($"Cookie: {cookiesString.ToString().TrimEnd(' ', ';')}\n");
                }

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Version = httpversion == 1 ? HttpVersion.Version11 : new Version(httpversion, 0),
                    Content = data
                };

                DefaultRequestHeaders(url, req, cookie, null, headers, loglines, useDefaultHeaders);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))))
                {
                    using (HttpResponseMessage response = await client.SendAsync(req, cts.Token).ConfigureAwait(false))
                    {
                        if (loglines != null)
                        {
                            loglines.Append($"\n\nStatusCode: {(int)response.StatusCode}\n");

                            foreach (var h in response.Headers)
                            {
                                if (h.Key == "Set-Cookie")
                                {
                                    foreach (string v in h.Value)
                                        loglines.Append($"{h.Key}: {v}\n");
                                }
                                else
                                    loglines.Append($"{h.Key}: {string.Join("", h.Value)}\n");
                            }
                        }

                        using (HttpContent content = response.Content)
                        {
                            if (InvkEvent.IsHttpAsync())
                                await InvkEvent.HttpAsync(new EventHttpResponse(url, data, client, "ReadAsStream", response, Startup.memoryCache));

                            if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                return (false, response);

                            using (var stream = await content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false))
                            {
                                action.Invoke((stream, cts.Token, loglines));
                                return (true, response);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (loglines != null)
                    loglines.Append(ex.ToString());

                if (InvkEvent.IsHttpAsync())
                {
                    await InvkEvent.HttpAsync(new EventHttpResponse(url, data, null, ex.ToString(), new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        RequestMessage = new HttpRequestMessage()
                    }, Startup.memoryCache));
                }

                return (false, new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    RequestMessage = new HttpRequestMessage()
                });
            }
            finally
            {
                if (!url.Contains("127.0.0.1") && loglines != null)
                    WriteLog(url, "POST", data.ReadAsStringAsync().Result, loglines);

                StringBuilderPool.Return(loglines);
            }
        }
        #endregion


        #region Download
        async public static Task<byte[]> Download(string url, string cookie = null, string referer = null, int timeoutSeconds = 60, long MaxResponseContentBufferSize = 50_000_000, List<HeadersModel> headers = null, WebProxy proxy = null, bool statusCodeOK = true, bool useDefaultHeaders = true)
        {
            return (await BaseDownload(url, cookie, referer, timeoutSeconds, MaxResponseContentBufferSize, headers, proxy, statusCodeOK, useDefaultHeaders).ConfigureAwait(false)).array;
        }
        #endregion

        #region BaseDownload
        async public static Task<(byte[] array, HttpResponseMessage response)> BaseDownload(string url, string cookie = null, string referer = null, int timeoutSeconds = 60, long MaxResponseContentBufferSize = 50_000_000, List<HeadersModel> headers = null, WebProxy proxy = null, bool statusCodeOK = true, bool useDefaultHeaders = true)
        {
            try
            {
                var handler = Handler(url, proxy);

                var client = FrendlyHttp.MessageClient("base", handler, MaxResponseContentBufferSize);

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version11
                };

                DefaultRequestHeaders(url, req, cookie, referer, headers, useDefaultHeaders);

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(20, timeoutSeconds))))
                {
                    using (HttpResponseMessage response = await client.SendAsync(req, cts.Token).ConfigureAwait(false))
                    {
                        if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                            return (null, response);

                        using (HttpContent content = response.Content)
                        {
                            byte[] res = await content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                            if (res == null || res.Length == 0)
                                return (null, response);

                            return (res, response);
                        }
                    }
                }
            }
            catch
            {
                return (null, new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    RequestMessage = new HttpRequestMessage()
                });
            }
        }
        #endregion

        #region DownloadFile
        async public static Task<bool> DownloadFile(string url, string path, int timeoutSeconds = 20, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                using (var handler = Handler(url, proxy))
                {
                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                        bool setDefaultUseragent = true;

                        if (headers != null)
                        {
                            foreach (var item in headers)
                            {
                                if (item.name.ToLower() == "user-agent")
                                    setDefaultUseragent = false;

                                if (!client.DefaultRequestHeaders.Contains(item.name))
                                    client.DefaultRequestHeaders.Add(item.name, item.val);
                            }
                        }

                        if (setDefaultUseragent)
                            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

                        using (var stream = await client.GetStreamAsync(url))
                        {
                            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, PoolInvk.bufferSize))
                            {
                                await stream.CopyToAsync(fileStream, PoolInvk.bufferSize);
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion


        #region IsLogSend
        public static INws nws;

        public static ISoks ws;

        public static bool IsLogged
        {
            get
            {
                if (AppInit.conf.filelog)
                    return true;

                if (!AppInit.conf.weblog.enable || onlog == null)
                    return false;

                if (nws == null && ws == null)
                    return false;

                if (nws?.CountWeblogClients > 0 || ws?.CountWeblogClients > 0)
                    return true;

                return false;
            }
        }
        #endregion

        #region WriteLog
        static FileStream logFileStream = null;

        public static EventHandler<string> onlog = null;

        static void WriteLog(string url, string method, in string postdata, StringBuilder result)
        {
            if (!IsLogged || result == null)
                return;

            var log = new StringBuilder((result.Length + (postdata?.Length ?? 0)) *2);

            log.Append($"{DateTime.Now}\n{method}: {url}\n");

            if (!string.IsNullOrEmpty(postdata))
                log.Append($"{postdata}\n\n");

            log.Append(result);

            onlog?.Invoke(null, log.ToString());

            if (!AppInit.conf.filelog)
                return;

            string dateLog = DateTime.Today.ToString("dd.MM.yy");
            string patchlog = $"cache/logs/HttpClient_{dateLog}.log";

            if (logFileStream == null || !File.Exists(patchlog))
                logFileStream = new FileStream(patchlog, FileMode.Append, FileAccess.Write, FileShare.Read, PoolInvk.bufferSize);

            var buffer = Encoding.UTF8.GetBytes($"\n\n\n################################################################\n\n{log.ToString()}");
            logFileStream.Write(buffer, 0, buffer.Length);
            logFileStream.Flush();
        }
        #endregion
    }
}
