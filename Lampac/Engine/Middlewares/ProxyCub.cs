using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using Shared.Model.Online;
using Shared.Models;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyCub
    {
        #region ProxyCub
        private readonly RequestDelegate _next;

        public ProxyCub(RequestDelegate next)
        {
            _next = next;
        }
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

            if (path.StartsWith("api/checker") || uri.StartsWith("api/checker"))
            {
                await httpContext.Response.WriteAsync("ok", httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (uri.StartsWith("api/plugins/blacklist"))
            {
                httpContext.Response.ContentType = "application/json; charset=utf-8";
                await httpContext.Response.WriteAsync("[]", httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            var proxyManager = new ProxyManager("cub_api", init);
            var proxy = proxyManager.Get();

            if (HttpMethods.IsGet(httpContext.Request.Method))
            {
                string memkey = $"cubproxy:{domain}:{uri}";
                if (!hybridCache.TryGetValue(memkey, out (byte[] array, int statusCode, string contentType) cache))
                {
                    bool isMedia = Regex.IsMatch(uri, "\\.(jpe?g|png|ico|svg)$");
                    bool setCache = path.Split(".")[0] is "tmdb" or "tmapi" or "apitmdb" or "imagetmdb" or "cdn" or "ad";
                    if (!setCache && isMedia)
                        setCache = true;

                    if (uri.StartsWith("api/reactions/get/") || uri.StartsWith("api/discuss/get/") ||
                        uri.StartsWith("api/collections/") || uri.StartsWith("api/trailers/") || uri.StartsWith("api/feed/all"))
                        setCache = true;

                    var headers = HeadersModel.Init();
                    foreach (var header in httpContext.Request.Headers)
                    {
                        if (header.Key.ToLower() is "cookie" or "user-agent")
                            headers.Add(new HeadersModel(header.Key, header.Value.ToString()));

                        if (header.Key.ToLower() is "token" or "profile")
                        {
                            setCache = false;
                            headers.Add(new HeadersModel(header.Key, header.Value.ToString()));
                        }
                    }

                    var result = await CORE.HttpClient.BaseDownload($"{init.scheme}://{domain}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: headers, statusCodeOK: false, useDefaultHeaders: false).ConfigureAwait(false);
                    if (result.array == null || result.array.Length == 0)
                    {
                        proxyManager.Refresh();
                        httpContext.Response.StatusCode = (int)result.response.StatusCode;
                        return;
                    }

                    if (result.response.Headers.TryGetValues("Set-Cookie", out var cookies))
                    {
                        foreach (var cookie in cookies)
                        {
                            setCache = false;
                            httpContext.Response.Headers.Append("Set-Cookie", cookie);
                        }
                    }

                    cache.array = result.array;
                    cache.statusCode = (int)result.response.StatusCode;
                    cache.contentType = result.response.Content.Headers.ContentType.ToString();

                    httpContext.Response.Headers.Add("X-Cache-Status", setCache ? "MISS" : "bypass");

                    if (setCache && cache.statusCode == 200)
                    {
                        if (isMedia)
                        {
                            if (AppInit.conf.mikrotik == false)
                                hybridCache.Set(memkey, cache, DateTime.Now.AddMinutes(init.cache_img));
                        }
                        else
                        {
                            hybridCache.Set(memkey, cache, DateTime.Now.AddMinutes(init.cache_api));
                        }
                    }
                }
                else
                {
                    httpContext.Response.Headers.Add("X-Cache-Status", "HIT");
                }

                if ((domain.StartsWith("tmdb") || domain.StartsWith("tmapi") || domain.StartsWith("apitmdb")) && cache.array.Length == 16)
                {
                    if (Encoding.UTF8.GetString(cache.array) == "{\"blocked\":true}")
                    {
                        var header = HeadersModel.Init(("localrequest", System.IO.File.ReadAllText("passwd")));
                        string json = await Engine.CORE.HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/tmdb/api/{uri}", timeoutSeconds: 5, headers: header).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(json))
                        {
                            httpContext.Response.ContentType = "application/json; charset=utf-8";
                            await httpContext.Response.WriteAsync(json, httpContext.RequestAborted).ConfigureAwait(false);
                            return;
                        }
                    }
                }

                proxyManager.Success();
                httpContext.Response.StatusCode = cache.statusCode;
                httpContext.Response.ContentType = cache.contentType;
                await httpContext.Response.BodyWriter.WriteAsync(cache.array, httpContext.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                #region bypass
                httpContext.Response.Headers.Add("X-Cache-Status", "bypass");

                HttpClientHandler handler = new HttpClientHandler()
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

                var client = FrendlyHttp.CreateClient("cubproxy", handler, "proxy");
                var request = CreateProxyHttpRequest(httpContext, new Uri($"{init.scheme}://{domain}/{uri}"));

                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted).ConfigureAwait(false))
                {
                    await CopyProxyHttpResponse(httpContext, response).ConfigureAwait(false);
                }
                #endregion
            }
        }


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

                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                try
                {
                    int bytesRead;
                    while ((bytesRead = await responseStream.ReadAsync(new Memory<byte>(buffer), context.RequestAborted).ConfigureAwait(false)) != 0)
                        await response.Body.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), context.RequestAborted).ConfigureAwait(false);
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
