using Newtonsoft.Json;
using Shared.Models;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public static class Http
    {
        public static string UserAgent => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";

        public static IHttpClientFactory httpClientFactory;

        #region Handler
        public static HttpClientHandler Handler(string url, WebProxy proxy, CookieContainer cookieContainer = null)
        {
            string log = string.Empty;
            return Handler(url, proxy, ref log, cookieContainer);
        }

        public static HttpClientHandler Handler(string url, WebProxy proxy, ref string loglines, CookieContainer cookieContainer = null)
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
                loglines += $"proxy: {proxy.Address.ToString()}\n";
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
                        loglines += $"globalproxy: {proxyip} {(p.useAuth ? $" - {p.username}:{p.password}" : "")}\n";
                        break;
                    }
                }
            }

            return handler;
        }
        #endregion

        #region DefaultRequestHeaders
        public static void DefaultRequestHeaders(System.Net.Http.HttpClient client, int timeoutSeconds, long MaxResponseContentBufferSize, string cookie, string referer, List<HeadersModel> headers, bool useDefaultHeaders = true)
        {
            string loglines = string.Empty;
            DefaultRequestHeaders(client, timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, headers, ref loglines, useDefaultHeaders);
        }

        public static void DefaultRequestHeaders(System.Net.Http.HttpClient client, int timeoutSeconds, long MaxResponseContentBufferSize, string cookie, string referer, List<HeadersModel> headers, ref string loglines, bool useDefaultHeaders = true)
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            if (MaxResponseContentBufferSize != -1)
                client.MaxResponseContentBufferSize = MaxResponseContentBufferSize == 0 ? 10_000_000 : MaxResponseContentBufferSize; // 10MB

            if (useDefaultHeaders)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5");
                loglines += $"Accept-Language: {client.DefaultRequestHeaders.AcceptLanguage}\n";
            }

            if (cookie != null)
            {
                client.DefaultRequestHeaders.Add("cookie", cookie);
                loglines += $"cookie: {cookie}\n";
            }

            if (referer != null)
            {
                client.DefaultRequestHeaders.Add("referer", referer);
                loglines += $"referer: {referer}\n";
            }

            bool setDefaultUseragent = true;

            if (headers != null)
            {
                foreach (var item in headers)
                {
                    if (item.name.ToLower() == "user-agent")
                        setDefaultUseragent = false;

                    if (!client.DefaultRequestHeaders.Contains(item.name))
                    {
                        client.DefaultRequestHeaders.Add(item.name, item.val);
                        loglines += $"{item.name}: {item.val}\n";
                    }
                }
            }

            if (useDefaultHeaders && setDefaultUseragent)
            {
                client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                loglines += $"User-Agent: {client.DefaultRequestHeaders.UserAgent}\n";
            }
        }
        #endregion


        #region GetLocation
        async public static Task<string> GetLocation(string url, string referer = null, int timeoutSeconds = 8, List<HeadersModel> headers = null, int httpversion = 1, bool allowAutoRedirect = false, WebProxy proxy = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = allowAutoRedirect;

                using (var client = handler.UseProxy || allowAutoRedirect == false ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient(httpversion == 2 ? "http2" : "base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, 2000000, null, referer, headers);

                    var req = new HttpRequestMessage(HttpMethod.Get, url)
                    {
                        Version = new Version(httpversion, 0)
                    };

                    using (HttpResponseMessage response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
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

                using (var client = handler.UseProxy || allowAutoRedirect == false ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient(httpversion == 2 ? "http2" : "base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, 2000000, null, null, headers);

                    var req = new HttpRequestMessage(HttpMethod.Get, url)
                    {
                        Version = new Version(httpversion, 0)
                    };

                    using (HttpResponseMessage response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                        return response;
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion


        #region Get
        async public static ValueTask<string> Get(string url, Encoding encoding = default, string cookie = null, string referer = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, long MaxResponseContentBufferSize = 0, WebProxy proxy = null, int httpversion = 1, bool statusCodeOK = true, bool weblog = true, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null)
        {
            return (await BaseGetAsync(url, encoding, cookie: cookie, referer: referer, timeoutSeconds: timeoutSeconds, headers: headers, MaxResponseContentBufferSize: MaxResponseContentBufferSize, proxy: proxy, httpversion: httpversion, statusCodeOK: statusCodeOK, weblog: weblog, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, body: body).ConfigureAwait(false)).content;
        }
        #endregion

        #region Get<T>
        async public static ValueTask<T> Get<T>(string url, Encoding encoding = default, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool weblog = true, HttpContent body = null)
        {
            try
            {
                string html = (await BaseGetAsync(url, encoding, cookie: cookie, referer: referer, MaxResponseContentBufferSize: MaxResponseContentBufferSize, timeoutSeconds: timeoutSeconds, headers: headers, proxy: proxy, httpversion: httpversion, statusCodeOK: statusCodeOK, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, weblog: weblog, body: body).ConfigureAwait(false)).content;
                if (html == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(html, new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } });

                return JsonConvert.DeserializeObject<T>(html);
            }
            catch
            {
                return default;
            }
        }
        #endregion


        #region BaseGetAsync<T>
        async public static Task<(T content, HttpResponseMessage response)> BaseGetAsync<T>(string url, Encoding encoding = default, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null)
        {
            try
            {
                var result = await BaseGetAsync(url, encoding, cookie: cookie, referer: referer, MaxResponseContentBufferSize: MaxResponseContentBufferSize, timeoutSeconds: timeoutSeconds, headers: headers, proxy: proxy, httpversion: httpversion, statusCodeOK: statusCodeOK, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, body: body).ConfigureAwait(false);
                if (result.content == null)
                    return default;

                JsonSerializerSettings settings = null;

                if (IgnoreDeserializeObject)
                    settings = new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };

                return (JsonConvert.DeserializeObject<T>(result.content, settings), result.response);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region BaseGetAsync
        async public static Task<(string content, HttpResponseMessage response)> BaseGetAsync(string url, Encoding encoding = default, string cookie = null, string referer = null, int timeoutSeconds = 15, long MaxResponseContentBufferSize = 0, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, bool statusCodeOK = true, bool weblog = true, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, HttpContent body = null)
        {
            string loglines = string.Empty;

            try
            {
                var handler = Handler(url, proxy, ref loglines, cookieContainer);

                var client = FrendlyHttp.CreateClient("http:BaseGetAsync", handler, httpversion == 2 ? "http2" : "base", headers?.ToDictionary(), timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, useDefaultHeaders, uclient =>
                {
                    DefaultRequestHeaders(uclient, timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, headers, ref loglines, useDefaultHeaders);
                });

                if (cookieContainer != null)
                {
                    var cookiesString = new StringBuilder();
                    foreach (Cookie c in cookieContainer.GetCookies(new Uri(url)))
                        cookiesString.Append($"{c.Name}={c.Value}; ");

                    if (!string.IsNullOrEmpty(cookiesString.ToString()))
                        loglines += $"Cookie: {cookiesString.ToString().TrimEnd(' ', ';')}\n";
                }

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = new Version(httpversion, 0),
                    Content = body
                };

                using (HttpResponseMessage response = await client.SendAsync(req).ConfigureAwait(false))
                {
                    loglines += $"\n\nStatusCode: {(int)response.StatusCode}\n";
                    foreach (var h in response.Headers)
                    {
                        if (h.Key == "Set-Cookie")
                        {
                            foreach (string v in h.Value)
                                loglines += $"{h.Key}: {v}\n";
                        }
                        else
                            loglines += $"{h.Key}: {string.Join("", h.Value)}\n";
                    }

                    using (HttpContent content = response.Content)
                    {
                        if (encoding != default)
                        {
                            string res = encoding.GetString(await content.ReadAsByteArrayAsync().ConfigureAwait(false));
                            if (string.IsNullOrWhiteSpace(res))
                                return (null, response);

                            loglines += "\n" + res;
                            if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                return (null, response);

                            return (res, response);
                        }
                        else
                        {
                            string res = await content.ReadAsStringAsync().ConfigureAwait(false);
                            if (string.IsNullOrWhiteSpace(res))
                                return (null, response);

                            loglines += "\n" + res;
                            if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                return (null, response);

                            return (res, response);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                loglines = ex.ToString();

                return (null, new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    RequestMessage = new HttpRequestMessage()
                });
            }
            finally
            {
                if (weblog)
                    WriteLog(url, "GET", body == null ? null : body.ReadAsStringAsync().Result, loglines);
            }
        }
        #endregion


        #region Post
        public static ValueTask<string> Post(string url, in string data, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool removeContentType = false)
        {
            return Post(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), cookie: cookie, MaxResponseContentBufferSize: MaxResponseContentBufferSize, timeoutSeconds: timeoutSeconds, headers: headers, proxy: proxy, httpversion: httpversion, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, removeContentType: removeContentType);
        }

        async public static ValueTask<string> Post(string url, HttpContent data, Encoding encoding = default, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool removeContentType = false, bool statusCodeOK = true)
        {
            return (await BasePost(url, data, encoding, cookie, MaxResponseContentBufferSize, timeoutSeconds, headers, proxy, httpversion, cookieContainer, useDefaultHeaders, removeContentType, statusCodeOK).ConfigureAwait(false)).content;
        }
        #endregion

        #region Post<T>
        public static Task<T> Post<T>(string url, in string data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, int httpversion = 1)
        {
            return Post<T>(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), cookie: cookie, timeoutSeconds: timeoutSeconds, headers: headers, encoding: encoding, proxy: proxy, IgnoreDeserializeObject: IgnoreDeserializeObject, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, httpversion: httpversion);
        }

        async public static Task<T> Post<T>(string url, HttpContent data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, int httpversion = 1)
        {
            try
            {
                string json = await Post(url, data, cookie: cookie, timeoutSeconds: timeoutSeconds, headers: headers, encoding: encoding, proxy: proxy, cookieContainer: cookieContainer, useDefaultHeaders: useDefaultHeaders, httpversion: httpversion).ConfigureAwait(false);
                if (json == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } });

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region BasePost
        async public static Task<(string content, HttpResponseMessage response)> BasePost(string url, HttpContent data, Encoding encoding = default, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, CookieContainer cookieContainer = null, bool useDefaultHeaders = true, bool removeContentType = false, bool statusCodeOK = true)
        {
            string loglines = string.Empty;

            try
            {
                var handler = Handler(url, proxy, ref loglines, cookieContainer);

                var client = FrendlyHttp.CreateClient("http:BasePost", handler, httpversion == 2 ? "http2" : "base", headers?.ToDictionary(), timeoutSeconds, MaxResponseContentBufferSize, cookie, null, useDefaultHeaders, uclient =>
                {
                    DefaultRequestHeaders(uclient, timeoutSeconds, MaxResponseContentBufferSize, cookie, null, headers, ref loglines, useDefaultHeaders);
                });

                if (cookieContainer != null)
                {
                    var cookiesString = new StringBuilder();
                    foreach (Cookie c in cookieContainer.GetCookies(new Uri(url)))
                        cookiesString.Append($"{c.Name}={c.Value}; ");

                    if (!string.IsNullOrEmpty(cookiesString.ToString()))
                        loglines += $"Cookie: {cookiesString.ToString().TrimEnd(' ', ';')}\n";
                }

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Version = new Version(httpversion, 0),
                    Content = data
                };

                if (removeContentType)
                    req.Content.Headers.Remove("Content-Type");

                using (HttpResponseMessage response = await client.SendAsync(req).ConfigureAwait(false))
                {
                    loglines += $"\n\nStatusCode: {(int)response.StatusCode}\n";
                    foreach (var h in response.Headers)
                    {
                        if (h.Key == "Set-Cookie")
                        {
                            foreach (string v in h.Value)
                                loglines += $"{h.Key}: {v}\n";
                        }
                        else
                            loglines += $"{h.Key}: {string.Join("", h.Value)}\n";
                    }

                    using (HttpContent content = response.Content)
                    {
                        if (encoding != default)
                        {
                            string res = encoding.GetString(await content.ReadAsByteArrayAsync().ConfigureAwait(false));
                            if (string.IsNullOrWhiteSpace(res))
                                return (null, response);

                            loglines += "\n" + res;
                            if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                return (null, response);

                            return (res, response);
                        }
                        else
                        {
                            string res = await content.ReadAsStringAsync().ConfigureAwait(false);
                            if (string.IsNullOrWhiteSpace(res))
                                return (null, response);

                            loglines += "\n" + res;
                            if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                return (null, response);

                            return (res, response);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                loglines = ex.ToString();
                return (null, new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    RequestMessage = new HttpRequestMessage()
                });
            }
            finally
            {
                WriteLog(url, "POST", data.ReadAsStringAsync().Result, loglines);
            }
        }
        #endregion


        #region Download
        async public static Task<byte[]> Download(string url, string cookie = null, string referer = null, int timeoutSeconds = 20, long MaxResponseContentBufferSize = 0, List<HeadersModel> headers = null, WebProxy proxy = null, bool statusCodeOK = true, bool useDefaultHeaders = true, string factoryClient = null)
        {
            return (await BaseDownload(url, cookie, referer, timeoutSeconds, MaxResponseContentBufferSize, headers, proxy, statusCodeOK, useDefaultHeaders, factoryClient).ConfigureAwait(false)).array;
        }
        #endregion

        #region BaseDownload
        async public static Task<(byte[] array, HttpResponseMessage response)> BaseDownload(string url, string cookie = null, string referer = null, int timeoutSeconds = 20, long MaxResponseContentBufferSize = 0, List<HeadersModel> headers = null, WebProxy proxy = null, bool statusCodeOK = true, bool useDefaultHeaders = true, string factoryClient = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                var client = FrendlyHttp.CreateClient("http:BaseDownload", handler, factoryClient ?? "base", headers?.ToDictionary(), timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, useDefaultHeaders, uclient =>
                {
                    DefaultRequestHeaders(uclient, timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, headers, useDefaultHeaders: useDefaultHeaders);
                });

                using (HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false))
                {
                    if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                        return (null, response);

                    using (HttpContent content = response.Content)
                    {
                        byte[] res = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (res == null || res.Length == 0)
                            return (null, response);

                        return (res, response);
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
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                using (var client = new System.Net.Http.HttpClient(handler))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, -1, null, null, headers);

                    using (var stream = await client.GetStreamAsync(url))
                    {
                        using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await stream.CopyToAsync(fileStream);
                            return true;
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


        #region WriteLog
        static FileStream logFileStream = null;

        public static EventHandler<string> onlog = null;

        static void WriteLog(string url, string method, in string postdata, in string result)
        {
            if (url.Contains("127.0.0.1"))
                return;

            if (!AppInit.conf.filelog && !AppInit.conf.weblog.enable)
                return;

            var log = new StringBuilder();

            log.Append($"{DateTime.Now}\n{method}: {url}\n");

            if (!string.IsNullOrEmpty(postdata))
                log.Append($"{postdata}\n\n");

            log.Append(result);

            onlog?.Invoke(null, log.ToString());

            if (!AppInit.conf.filelog || log.Length > 700_000)
                return;

            string dateLog = DateTime.Today.ToString("dd.MM.yy");
            string patchlog = $"cache/logs/HttpClient_{dateLog}.log";

            if (logFileStream == null || !File.Exists(patchlog))
                logFileStream = new FileStream(patchlog, FileMode.Append, FileAccess.Write, FileShare.Read);

            var buffer = Encoding.UTF8.GetBytes($"\n\n\n################################################################\n\n{log.ToString()}");
            logFileStream.Write(buffer, 0, buffer.Length);
            logFileStream.Flush();
        }
        #endregion
    }
}
