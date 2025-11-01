using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using Shared.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyCub
    {
        #region ProxyCub
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, byte> cacheFiles = new ConcurrentDictionary<string, byte>();

        static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreLocks = new();

        static Timer cleanupTimer;

        static ProxyCub() 
        {
            Directory.CreateDirectory("cache/cub");

            foreach (var item in Directory.EnumerateFiles("cache/cub", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), 0);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/cub",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };

            cleanupTimer = new Timer(cleanup, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        static void cleanup(object state)
        {
            try
            {
                var files = Directory.GetFiles("cache/cub", "*").Select(f => Path.GetFileName(f)).ToHashSet();

                foreach (string md5fileName in cacheFiles.Keys.ToArray())
                {
                    if (!files.Contains(md5fileName))
                        cacheFiles.TryRemove(md5fileName, out _);
                }
            }
            catch { }
        }

        public ProxyCub(RequestDelegate next) { }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache)
        {
            using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted))
            {
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(90));

                var hybridCache = new HybridCache();
                var requestInfo = httpContext.Features.Get<RequestModel>();

                var init = AppInit.conf.cub;
                string domain = init.domain;
                string path = httpContext.Request.Path.Value.Replace("/cub/", "");
                string query = httpContext.Request.QueryString.Value;
                string uri = Regex.Match(path, "^[^/]+/(.*)").Groups[1].Value + query;

                if (!init.enable || domain == "ws")
                {
                    httpContext.Response.Redirect($"https://{path}/{query}");
                    return;
                }

                if (path.Split(".")[0] is "geo" or "tmdb" or "tmapi" or "apitmdb" or "imagetmdb" or "cdn" or "ad" or "ws")
                    domain = $"{path.Split(".")[0]}.{domain}";

                if (domain.StartsWith("geo"))
                {
                    string country = requestInfo.Country;
                    if (string.IsNullOrEmpty(country))
                    {
                        var ipify = await Http.Get<JObject>("https://api.ipify.org/?format=json");
                        if (ipify != null || !string.IsNullOrEmpty(ipify.Value<string>("ip")))
                            country = GeoIP2.Country(ipify.Value<string>("ip"));
                    }

                    await httpContext.Response.WriteAsync(country ?? "", ctsHttp.Token);
                    return;
                }

                #region checker
                if (path.StartsWith("api/checker") || uri.StartsWith("api/checker"))
                {
                    if (HttpMethods.IsPost(httpContext.Request.Method))
                    {
                        if (httpContext.Request.ContentType != null &&
                            httpContext.Request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true))
                            {
                                string form = await reader.ReadToEndAsync();

                                var match = Regex.Match(form, @"(?:^|&)data=([^&]+)");
                                if (match.Success)
                                {
                                    string dataValue = Uri.UnescapeDataString(match.Groups[1].Value);
                                    await httpContext.Response.WriteAsync(dataValue, ctsHttp.Token);
                                    return;
                                }
                            }
                        }
                    }

                    await httpContext.Response.WriteAsync("ok", ctsHttp.Token);
                    return;
                }
                #endregion

                #region blacklist
                if (uri.StartsWith("api/plugins/blacklist"))
                {
                    httpContext.Response.ContentType = "application/json; charset=utf-8";
                    await httpContext.Response.WriteAsync("[]", ctsHttp.Token);
                    return;
                }
                #endregion

                #region ads/log/metric
                if (uri.StartsWith("api/metric/") || uri.StartsWith("api/ad/stat"))
                {
                    await httpContext.Response.WriteAsJsonAsync(new { secuses = true });
                    return;
                }

                if (uri.StartsWith("api/ad/vast"))
                {
                    await httpContext.Response.WriteAsJsonAsync(new 
                    { 
                        secuses = true,
                        ad = new string[] { },
                        day_of_month = DateTime.Now.Day,
                        days_in_month =  31,
                        month = DateTime.Now.Month
                    });
                    return;
                }
                #endregion

                var proxyManager = new ProxyManager("cub_api", init);
                var proxy = proxyManager.Get();

                bool isMedia = Regex.IsMatch(uri, "\\.(jpe?g|png|gif|webp|ico|svg|mp4|js|css)");

                if (0 >= init.cache_api || !HttpMethods.IsGet(httpContext.Request.Method) || isMedia ||
                    (path.Split(".")[0] is "imagetmdb" or "cdn" or "ad") ||
                    httpContext.Request.Headers.ContainsKey("token") || httpContext.Request.Headers.ContainsKey("profile"))
                {
                    #region bypass
                    string md5key = CrypTo.md5($"{domain}:{uri}");
                    string outFile = Path.Combine("cache", "cub", md5key);

                    if (cacheFiles.ContainsKey(md5key))
                    {
                        httpContext.Response.Headers["X-Cache-Status"] = "HIT";
                        httpContext.Response.ContentType = getContentType(uri);
                        await httpContext.Response.SendFileAsync(outFile);
                        return;
                    }

                    var handler = new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        AllowAutoRedirect = false
                    };

                    handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                    if (proxy != null)
                    {
                        handler.UseProxy = true;
                        handler.Proxy = proxy;
                    }
                    else { handler.UseProxy = false; }

                    var client = FrendlyHttp.HttpMessageClient("proxy", handler);
                    var request = CreateProxyHttpRequest(httpContext, new Uri($"{init.scheme}://{domain}/{uri}"), requestInfo, init.viewru && path.Split(".")[0] == "tmdb");

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    {
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                        {
                            if (init.cache_img > 0 && isMedia && HttpMethods.IsGet(httpContext.Request.Method) && AppInit.conf.mikrotik == false && response.StatusCode == HttpStatusCode.OK)
                            {
                                #region cache
                                httpContext.Response.ContentType = getContentType(uri);
                                httpContext.Response.Headers["X-Cache-Status"] = "MISS";

                                int initialCapacity = response.Content.Headers.ContentLength.HasValue ?
                                    (int)response.Content.Headers.ContentLength.Value :
                                    20_000; // 20kB

                                using (var memoryStream = new MemoryStream(initialCapacity))
                                {
                                    try
                                    {
                                        bool saveCache = true;

                                        using (var responseStream = await response.Content.ReadAsStreamAsync())
                                        {
                                            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                                            try
                                            {
                                                int bytesRead;

                                                while ((bytesRead = await responseStream.ReadAsync(buffer, ctsHttp.Token)) > 0)
                                                {
                                                    memoryStream.Write(buffer, 0, bytesRead);
                                                    await httpContext.Response.Body.WriteAsync(buffer, 0, bytesRead, ctsHttp.Token);
                                                }
                                            }
                                            catch
                                            {
                                                saveCache = false;
                                            }
                                            finally
                                            {
                                                ArrayPool<byte>.Shared.Return(buffer);
                                            }
                                        }

                                        if (saveCache && memoryStream.Length > 1000)
                                        {
                                            try
                                            {
                                                if (!cacheFiles.ContainsKey(md5key))
                                                {
                                                    File.WriteAllBytes(outFile, memoryStream.ToArray());
                                                    cacheFiles.TryAdd(md5key, 0);
                                                }
                                            }
                                            catch { File.Delete(outFile); }
                                        }
                                    }
                                    catch { }
                                }
                                #endregion
                            }
                            else
                            {
                                httpContext.Response.Headers["X-Cache-Status"] = "bypass";
                                await CopyProxyHttpResponse(httpContext, response);
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    #region cache
                    string memkey = $"cubproxy:{domain}:{uri}";
                    (string content, int statusCode, string contentType) cache = default;

                    var semaphore = _semaphoreLocks.GetOrAdd(memkey, _ => new SemaphoreSlim(1, 1));

                    try
                    {
                        await semaphore.WaitAsync(TimeSpan.FromSeconds(20));

                        if (!hybridCache.TryGetValue(memkey, out cache, inmemory: false))
                        {
                            var headers = HeadersModel.Init();

                            if (!string.IsNullOrEmpty(requestInfo.Country))
                                headers.Add(new HeadersModel("cf-connecting-ip", requestInfo.IP));

                            if (path.Split(".")[0] == "tmdb")
                            {
                                if (init.viewru)
                                    headers.Add(new HeadersModel("cookie", "viewru=1"));

                                headers.Add(new HeadersModel("user-agent", httpContext.Request.Headers.UserAgent.ToString()));
                            }
                            else
                            {
                                foreach (var header in httpContext.Request.Headers)
                                {
                                    if (header.Key.ToLower() is "cookie" or "user-agent")
                                        headers.Add(new HeadersModel(header.Key, header.Value.ToString()));
                                }
                            }

                            var result = await Http.BaseGetAsync($"{init.scheme}://{domain}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: headers, statusCodeOK: false, useDefaultHeaders: false);
                            if (string.IsNullOrEmpty(result.content))
                            {
                                proxyManager.Refresh();
                                httpContext.Response.StatusCode = (int)result.response.StatusCode;
                                return;
                            }

                            cache.content = result.content;
                            cache.statusCode = (int)result.response.StatusCode;
                            cache.contentType = result.response.Content.Headers.ContentType.ToString();

                            if (domain.StartsWith("tmdb") || domain.StartsWith("tmapi") || domain.StartsWith("apitmdb"))
                            {
                                if (cache.content == "{\"blocked\":true}")
                                {
                                    var header = HeadersModel.Init(("localrequest", AppInit.rootPasswd));
                                    string json = await Http.Get($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/{uri}", timeoutSeconds: 5, headers: header);
                                    if (!string.IsNullOrEmpty(json))
                                    {
                                        cache.statusCode = 200;
                                        cache.contentType = "application/json; charset=utf-8";
                                        cache.content = json;
                                    }
                                }
                            }

                            httpContext.Response.Headers["X-Cache-Status"] = "MISS";

                            if (cache.statusCode == 200)
                            {
                                proxyManager.Success();
                                hybridCache.Set(memkey, cache, DateTime.Now.AddMinutes(init.cache_api), inmemory: false);
                            }
                        }
                        else
                        {
                            httpContext.Response.Headers["X-Cache-Status"] = "HIT";
                        }
                    }
                    finally
                    {
                        try
                        {
                            semaphore.Release();
                        }
                        finally
                        {
                            if (semaphore.CurrentCount == 1)
                                _semaphoreLocks.TryRemove(memkey, out _);
                        }
                    }

                    httpContext.Response.StatusCode = cache.statusCode;
                    httpContext.Response.ContentType = cache.contentType;
                    await httpContext.Response.WriteAsync(cache.content, ctsHttp.Token);
                    #endregion
                }
            }
        }


        #region getContentType
        static string getContentType(string uri)
        {
            return Path.GetExtension(uri).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".js" => "application/javascript",
                ".css" => "text/css",
                _ => "application/octet-stream"
            };
        }
        #endregion

        #region CreateProxyHttpRequest
        HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri, RequestModel requestInfo, bool viewru)
        {
            var request = context.Request;

            var requestMessage = new HttpRequestMessage();

            var requestMethod = request.Method;
            if (HttpMethods.IsPost(requestMethod))
            {
                var streamContent = new StreamContent(request.Body);
                requestMessage.Content = streamContent;
            }

            if (viewru)
                request.Headers["cookie"] = "viewru=1";

            #region Headers
            foreach (var header in request.Headers)
            {
                if (header.Key.ToLower() is "host" or "origin" or "content-disposition" or "accept-encoding")
                    continue;

                if (viewru && header.Key.ToLower() == "cookie")
                    continue;

                if (header.Key.ToLower().StartsWith("x-"))
                    continue;

                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
            #endregion

            if (!string.IsNullOrEmpty(requestInfo.Country))
                requestMessage.Headers.Add("cf-connecting-ip", requestInfo.IP);

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);
            //requestMessage.Version = new Version(2, 0);

            return requestMessage;
        }
        #endregion

        #region CopyProxyHttpResponse
        async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
        {
            var response = context.Response;
            response.StatusCode = (int)responseMessage.StatusCode;
            response.ContentLength = responseMessage.Content.Headers.ContentLength;

            #region UpdateHeaders
            void UpdateHeaders(HttpHeaders headers)
            {
                foreach (var header in headers)
                {
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection" or "content-security-policy" or "content-disposition")
                        continue;

                    if (header.Key.ToLower().StartsWith("x-"))
                        continue;

                    if (header.Key.ToLower().Contains("access-control"))
                        continue;

                    string value = string.Empty;
                    foreach (var val in header.Value)
                        value += $"; {val}";

                    response.Headers[header.Key] = Regex.Replace(value, "^; ", "");
                }
            }
            #endregion

            UpdateHeaders(responseMessage.Headers);
            UpdateHeaders(responseMessage.Content.Headers);

            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync())
            {
                if (response.Body == null)
                    throw new ArgumentNullException("destination");

                if (!responseStream.CanRead && !responseStream.CanWrite)
                    throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

                if (!response.Body.CanRead && !response.Body.CanWrite)
                    throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

                if (!responseStream.CanRead)
                    throw new NotSupportedException("NotSupported_UnreadableStream");

                if (!response.Body.CanWrite)
                    throw new NotSupportedException("NotSupported_UnwritableStream");

                using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
                {
                    ctsHttp.CancelAfter(TimeSpan.FromSeconds(90));
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                    try
                    {
                        int bytesRead;

                        while ((bytesRead = await responseStream.ReadAsync(buffer, ctsHttp.Token)) != 0)
                            await response.Body.WriteAsync(buffer, 0, bytesRead, ctsHttp.Token);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }
        #endregion
    }
}
