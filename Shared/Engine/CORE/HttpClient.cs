using Newtonsoft.Json;
using Shared.Model.Online;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class HttpClient
    {
        public static string UserAgent => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        public static IHttpClientFactory httpClientFactory;

        #region Handler
        public static HttpClientHandler Handler(string url, WebProxy proxy)
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
            }

            if (AppInit.conf.globalproxy != null && AppInit.conf.globalproxy.Count > 0)
            {
                foreach (var p in AppInit.conf.globalproxy)
                {
                    if (p.list == null || p.list.Count == 0 || p.pattern == null)
                        continue;

                    if (Regex.IsMatch(url, p.pattern, RegexOptions.IgnoreCase))
                    {
                        ICredentials credentials = null;

                        if (p.useAuth)
                            credentials = new NetworkCredential(p.username, p.password);

                        handler.UseProxy = true;
                        handler.Proxy = new WebProxy(p.list.OrderBy(a => Guid.NewGuid()).First(), p.BypassOnLocal, null, credentials);
                        break;
                    }
                }
            }

            return handler;
        }
        #endregion

        #region DefaultRequestHeaders
        public static void DefaultRequestHeaders(System.Net.Http.HttpClient client, int timeoutSeconds, long MaxResponseContentBufferSize, string cookie, string referer, List<HeadersModel> headers)
        {
            string loglines = string.Empty;
            DefaultRequestHeaders(client, timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, headers, ref loglines);
        }

        public static void DefaultRequestHeaders(System.Net.Http.HttpClient client, int timeoutSeconds, long MaxResponseContentBufferSize, string cookie, string referer, List<HeadersModel> headers, ref string loglines)
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            if (MaxResponseContentBufferSize != -1)
                client.MaxResponseContentBufferSize = MaxResponseContentBufferSize == 0 ? 10_000_000 : MaxResponseContentBufferSize; // 10MB

            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.6,en;q=0.5");

            loglines += "Accept-Encoding: gzip, deflate, br\n";
            loglines += "Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.6,en;q=0.5\n";

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

                    client.DefaultRequestHeaders.Add(item.name, item.val);
                    loglines += $"{item.name}: {item.val}\n";
                }
            }

            if (setDefaultUseragent)
            {
                client.DefaultRequestHeaders.Add("User-Agent", "UserAgent");
                loglines += $"User-Agent: {UserAgent}\n";
            }
        }
        #endregion


        #region GetLocation
        async public static ValueTask<string> GetLocation(string url, string referer = null, int timeoutSeconds = 8, List<HeadersModel> headers = null, int httpversion = 1, bool allowAutoRedirect = false, WebProxy proxy = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = allowAutoRedirect;

                using (var client = handler.UseProxy || allowAutoRedirect == false ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient("base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, 2000000, null, referer, headers);

                    var req = new HttpRequestMessage(HttpMethod.Get, url)
                    {
                        Version = new Version(httpversion, 0)
                    };

                    using (HttpResponseMessage response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
                    {
                        string location = ((int)response.StatusCode == 301 || (int)response.StatusCode == 302 || (int)response.StatusCode == 307) ? response.Headers.Location?.ToString() : response.RequestMessage.RequestUri?.ToString();
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


        #region Get
        async public static ValueTask<string> Get(string url, Encoding encoding = default, string cookie = null, string referer = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, long MaxResponseContentBufferSize = 0, WebProxy proxy = null, int httpversion = 1, bool statusCodeOK = true)
        {
            return (await BaseGetAsync(url, encoding, cookie: cookie, referer: referer, timeoutSeconds: timeoutSeconds, headers: headers, MaxResponseContentBufferSize: MaxResponseContentBufferSize, proxy: proxy, httpversion: httpversion, statusCodeOK: statusCodeOK)).content;
        }
        #endregion

        #region Get<T>
        async public static ValueTask<T> Get<T>(string url, Encoding encoding = default, string cookie = null, string referer = null, long MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, bool IgnoreDeserializeObject = false, WebProxy proxy = null, bool statusCodeOK = true, int httpversion = 1)
        {
            try
            {
                string html = (await BaseGetAsync(url, encoding, cookie: cookie, referer: referer, MaxResponseContentBufferSize: MaxResponseContentBufferSize, timeoutSeconds: timeoutSeconds, headers: headers, proxy: proxy, httpversion: httpversion, statusCodeOK: statusCodeOK)).content;
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

        #region BaseGetAsync
        async public static ValueTask<(string content, HttpResponseMessage response)> BaseGetAsync(string url, Encoding encoding = default, string cookie = null, string referer = null, int timeoutSeconds = 15, long MaxResponseContentBufferSize = 0, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1, bool statusCodeOK = true)
        {
            string loglines = string.Empty;

            try
            {
                var handler = Handler(url, proxy);

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient("base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, headers, ref loglines);

                    var req = new HttpRequestMessage(HttpMethod.Get, url)
                    {
                        Version = new Version(httpversion, 0)
                    };

                    using (HttpResponseMessage response = await client.SendAsync(req))
                    {
                        loglines += $"\n\nStatusCode: {(int)response.StatusCode}\n";
                        foreach (var h in response.Headers)
                            loglines += $"{h.Key}: {string.Join("", h.Value)}\n";

                        using (HttpContent content = response.Content)
                        {
                            if (encoding != default)
                            {
                                string res = encoding.GetString(await content.ReadAsByteArrayAsync());
                                if (string.IsNullOrWhiteSpace(res))
                                    return (null, response);

                                loglines += "\n" + res;
                                if (statusCodeOK && response.StatusCode != HttpStatusCode.OK)
                                    return (null, response);

                                return (res, response);
                            }
                            else
                            {
                                string res = await content.ReadAsStringAsync();
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
                await WriteLog(url, "GET", null, loglines);
            }
        }
        #endregion


        #region Post
        public static ValueTask<string> Post(string url, string data, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1)
        {
            return Post(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), cookie: cookie, MaxResponseContentBufferSize: MaxResponseContentBufferSize, timeoutSeconds: timeoutSeconds, headers: headers, proxy: proxy, httpversion: httpversion);
        }

        async public static ValueTask<string> Post(string url, HttpContent data, Encoding encoding = default, string cookie = null, int MaxResponseContentBufferSize = 0, int timeoutSeconds = 15, List<HeadersModel> headers = null, WebProxy proxy = null, int httpversion = 1)
        {
            string loglines = string.Empty;

            try
            {
                var handler = Handler(url, proxy);

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient("base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, MaxResponseContentBufferSize, cookie, null, headers, ref loglines);

                    var req = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Version = new Version(httpversion, 0),
                        Content = data
                    };

                    using (HttpResponseMessage response = await client.SendAsync(req))
                    {
                        loglines += $"\n\nStatusCode: {(int)response.StatusCode}\n";
                        foreach (var h in response.Headers)
                            loglines += $"{h.Key}: {string.Join("", h.Value)}\n";

                        using (HttpContent content = response.Content)
                        {
                            if (encoding != default)
                            {
                                string res = encoding.GetString(await content.ReadAsByteArrayAsync());
                                if (string.IsNullOrWhiteSpace(res))
                                    return null;

                                loglines += "\n" + res;
                                if (response.StatusCode != HttpStatusCode.OK)
                                    return null;

                                return res;
                            }
                            else
                            {
                                string res = await content.ReadAsStringAsync();
                                if (string.IsNullOrWhiteSpace(res))
                                    return null;

                                loglines += "\n" + res;
                                if (response.StatusCode != HttpStatusCode.OK)
                                    return null;

                                return res;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                loglines = ex.ToString();
                return null;
            }
            finally
            {
                await WriteLog(url, "POST", data.ReadAsStringAsync().Result, loglines);
            }
        }
        #endregion

        #region Post<T>
        async public static ValueTask<T> Post<T>(string url, string data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false)
        {
            return await Post<T>(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), cookie: cookie, timeoutSeconds: timeoutSeconds, headers: headers, encoding: encoding, proxy: proxy, IgnoreDeserializeObject: IgnoreDeserializeObject);
        }

        async public static ValueTask<T> Post<T>(string url, HttpContent data, string cookie = null, int timeoutSeconds = 15, List<HeadersModel> headers = null, Encoding encoding = default, WebProxy proxy = null, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await Post(url, data, cookie: cookie, timeoutSeconds: timeoutSeconds, headers: headers, encoding: encoding, proxy: proxy);
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


        #region Download
        async public static ValueTask<byte[]> Download(string url, string cookie = null, string referer = null, int timeoutSeconds = 20, long MaxResponseContentBufferSize = 0, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient("base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, MaxResponseContentBufferSize, cookie, referer, headers);

                    using (HttpResponseMessage response = await client.GetAsync(url))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            return null;

                        using (HttpContent content = response.Content)
                        {
                            byte[] res = await content.ReadAsByteArrayAsync();
                            if (res.Length == 0)
                                return null;

                            return res;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region DownloadFile
        async public static ValueTask<bool> DownloadFile(string url, string path, int timeoutSeconds = 20, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : httpClientFactory.CreateClient("base"))
                {
                    DefaultRequestHeaders(client, timeoutSeconds, -1, null, null, headers);

                    using (var stream = await client.GetStreamAsync(url))
                    {
                        using (var fileStream = new FileStream(path, FileMode.OpenOrCreate))
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

        async static Task WriteLog(string url, string method, string postdata, string result)
        {
            if (url.Contains("127.0.0.1"))
                return;

            string log = $"{DateTime.Now}\n{method}: {url}\n";

            if (!string.IsNullOrEmpty(postdata))
                log += $"{postdata}\n\n";

            log += result;

            onlog?.Invoke(null, log);

            if (!AppInit.conf.filelog || log.Length > 700_000)
                return;

            string dateLog = DateTime.Today.ToString("dd.MM.yy");
            string patchlog = $"cache/logs/HttpClient_{dateLog}.log";

            if (logFileStream == null || !File.Exists(patchlog))
                logFileStream = new FileStream(patchlog, FileMode.Append, FileAccess.Write);

            var buffer = Encoding.UTF8.GetBytes($"\n\n\n################################################################\n\n{log}");
            await logFileStream.WriteAsync(buffer, 0, buffer.Length);
            await logFileStream.FlushAsync();
        }
        #endregion
    }
}
