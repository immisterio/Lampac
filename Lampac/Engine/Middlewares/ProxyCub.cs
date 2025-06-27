using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using Shared.Model.Online;
using Shared.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyCub
    {
        #region ProxyCub
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, byte> cacheFiles = new ConcurrentDictionary<string, byte>();

        static ProxyCub() 
        {
            Directory.CreateDirectory("cache/cub");

            foreach (var item in Directory.GetFiles("cache/cub", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), 0);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/cub",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };
        }

        public ProxyCub(RequestDelegate next) { }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache)
        {
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

            if (domain.StartsWith("geo") && !string.IsNullOrEmpty(requestInfo.Country))
            {
                await httpContext.Response.WriteAsync(requestInfo.Country, httpContext.RequestAborted).ConfigureAwait(false);
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
                        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
                        string form = await reader.ReadToEndAsync().ConfigureAwait(false);

                        var match = Regex.Match(form, @"(?:^|&)data=([^&]+)");
                        if (match.Success)
                        {
                            string dataValue = Uri.UnescapeDataString(match.Groups[1].Value);
                            await httpContext.Response.WriteAsync(dataValue, httpContext.RequestAborted).ConfigureAwait(false);
                            return;
                        }
                    }
                }

                await httpContext.Response.WriteAsync("ok", httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }
            #endregion

            if (uri.StartsWith("api/plugins/blacklist"))
            {
                httpContext.Response.ContentType = "application/json; charset=utf-8";
                await httpContext.Response.WriteAsync("[]", httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

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
                bool isCacheRequest = init.cache_img > 0 && isMedia && HttpMethods.IsGet(httpContext.Request.Method) && AppInit.conf.mikrotik == false;

                if (isCacheRequest && cacheFiles.ContainsKey(md5key))
                {
                    httpContext.Response.Headers.Add("X-Cache-Status", "HIT");
                    httpContext.Response.ContentType = getContentType(uri);
                    await httpContext.Response.SendFileAsync(outFile).ConfigureAwait(false);
                    return;
                }

                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    AllowAutoRedirect = false
                };

                handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                if (proxy != null)
                {
                    handler.UseProxy = true;
                    handler.Proxy = proxy;
                }

                var client = FrendlyHttp.CreateClient("cubproxy", handler, "proxy");
                var request = CreateProxyHttpRequest(httpContext, new Uri($"{init.scheme}://{domain}/{uri}"));

                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted).ConfigureAwait(false))
                {
                    if (isCacheRequest && response.StatusCode == HttpStatusCode.OK)
                    {
                        #region cache
                        httpContext.Response.ContentType = getContentType(uri);
                        httpContext.Response.Headers.Add("X-Cache-Status", "MISS");

                        int initialCapacity = response.Content.Headers.ContentLength.HasValue ?
                            (int)response.Content.Headers.ContentLength.Value :
                            20_000; // 20kB

                        using (var memoryStream = new MemoryStream(initialCapacity))
                        {
                            bool saveCache = true;

                            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                                try
                                {
                                    int bytesRead;
                                    Memory<byte> memoryBuffer = buffer.AsMemory();

                                    while ((bytesRead = await responseStream.ReadAsync(memoryBuffer, httpContext.RequestAborted).ConfigureAwait(false)) > 0)
                                    {
                                        memoryStream.Write(memoryBuffer.Slice(0, bytesRead).Span);
                                        await httpContext.Response.Body.WriteAsync(memoryBuffer.Slice(0, bytesRead), httpContext.RequestAborted).ConfigureAwait(false);
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
                                        File.WriteAllBytes(outFile, memoryStream.ToArray());
                                }
                                catch { try { File.Delete(outFile); } catch { } }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        httpContext.Response.Headers.Add("X-Cache-Status", "bypass");
                        await CopyProxyHttpResponse(httpContext, response).ConfigureAwait(false);
                    }
                }
                #endregion
            }
            else
            {
                #region cache
                string memkey = $"cubproxy:{domain}:{uri}";
                if (!hybridCache.TryGetValue(memkey, out (string content, int statusCode, string contentType) cache))
                {
                    var headers = HeadersModel.Init();
                    foreach (var header in httpContext.Request.Headers)
                    {
                        if (header.Key.ToLower() is "cookie" or "user-agent")
                            headers.Add(new HeadersModel(header.Key, header.Value.ToString()));
                    }

                    var result = await CORE.HttpClient.BaseGetAsync($"{init.scheme}://{domain}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: headers, statusCodeOK: false, useDefaultHeaders: false).ConfigureAwait(false);
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
                            string json = await CORE.HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/tmdb/api/{uri}", timeoutSeconds: 5, headers: header).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(json))
                            {
                                cache.statusCode = 200;
                                cache.contentType = "application/json; charset=utf-8";
                                cache.content = json;
                            }
                        }
                    }

                    httpContext.Response.Headers.Add("X-Cache-Status", "MISS");

                    if (cache.statusCode == 200)
                    {
                        proxyManager.Success();
                        hybridCache.Set(memkey, cache, DateTime.Now.AddMinutes(init.cache_api));
                    }
                }
                else
                {
                    httpContext.Response.Headers.Add("X-Cache-Status", "HIT");
                }

                httpContext.Response.StatusCode = cache.statusCode;
                httpContext.Response.ContentType = cache.contentType;
                await httpContext.Response.WriteAsync(cache.content, httpContext.RequestAborted).ConfigureAwait(false);
                #endregion
            }
        }


        #region getContentType
        static string getContentType(in string uri)
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
        HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri)
        {
            var request = context.Request;

            var requestMessage = new HttpRequestMessage();

            var requestMethod = request.Method;
            if (HttpMethods.IsPost(requestMethod))
            {
                var streamContent = new StreamContent(request.Body);
                requestMessage.Content = streamContent;
            }

            #region Headers
            foreach (var header in request.Headers)
            {
                if (header.Key.ToLower() is "host" or "origin" or "user-agent" or "referer" or "content-disposition" or "accept-encoding")
                    continue;

                if (header.Key.ToLower().StartsWith("x-"))
                    continue;

                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
            #endregion

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

            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
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

                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                try
                {
                    int bytesRead;
                    Memory<byte> memoryBuffer = buffer.AsMemory();

                    while ((bytesRead = await responseStream.ReadAsync(memoryBuffer, context.RequestAborted).ConfigureAwait(false)) != 0)
                        await response.Body.WriteAsync(memoryBuffer.Slice(0, bytesRead), context.RequestAborted).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        #endregion
    }
}
