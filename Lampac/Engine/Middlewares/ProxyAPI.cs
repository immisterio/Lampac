﻿using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Buffers;
using Shared.Models;
using Shared.Model.Online;
using Shared.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Engine.Middlewares
{
    public class ProxyAPI
    {
        #region ProxyAPI
        private readonly RequestDelegate _next;

        private readonly IHttpClientFactory _httpClientFactory;

        IMemoryCache memoryCache;

        public ProxyAPI(RequestDelegate next, IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
        {
            _next = next;
            _httpClientFactory = httpClientFactory;
            this.memoryCache = memoryCache;
        }

        static ProxyAPI()
        {
            Directory.CreateDirectory("cache/hls");
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            var init = AppInit.conf.serverproxy;
            var requestInfo = httpContext.Features.Get<RequestModel>();
            string reqip = requestInfo.IP;
            string servUri = httpContext.Request.Path.Value.Replace("/proxy/", "").Replace("/proxy-dash/", "").Replace("://", ":/_/").Replace("//", "/").Replace(":/_/", "://") + httpContext.Request.QueryString.Value;

            if (httpContext.Request.Path.Value.StartsWith("/proxy-dash/"))
            {
                string dashkey = Regex.Match(servUri, "^([^/]+)").Groups[1].Value;
                if (!memoryCache.TryGetValue($"proxy-dash:{dashkey}:{(init.verifyip ? reqip : "")}", out string baseURL)) {
                    httpContext.Response.StatusCode = 403;
                    return;
                }

                servUri = servUri.Replace($"{dashkey}/", baseURL);

                if (init.showOrigUri)
                    httpContext.Response.Headers.Add("PX-Orig", servUri);

                using (var client = _httpClientFactory.CreateClient("proxy"))
                {
                    var collaps_header = HeadersModel.Init(("Origin", "https://api.ninsel.ws"), ("Referer", $"https://api.ninsel.ws/"), ("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1"));

                    var request = CreateProxyHttpRequest(httpContext, collaps_header, new Uri(servUri), false);
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);

                    httpContext.Response.Headers.Add("PX-Cache", "BYPASS");
                    await CopyProxyHttpResponse(httpContext, response);
                }
            }
            else if (httpContext.Request.Path.Value.StartsWith("/proxy/"))
            {
                #region tmdb proxy
                if (servUri.Contains(".themoviedb.org"))
                {
                    httpContext.Response.Redirect($"/tmdb/api/{Regex.Match(servUri, "https?://[^/]+/(.*)").Groups[1].Value}");
                    return;
                }
                else if (servUri.Contains(".tmdb.org"))
                {
                    httpContext.Response.Redirect($"/tmdb/img/{Regex.Match(servUri, "https?://[^/]+/(.*)").Groups[1].Value}");
                    return;
                }
                #endregion

                #region decryptLink
                ProxyLinkModel decryptLink = CORE.ProxyLink.Decrypt(Regex.Replace(servUri, "(\\?|&).*", ""), reqip);

                if (init.encrypt)
                {
                    servUri = decryptLink?.uri;
                }
                else
                {
                    if (!init.enable)
                    {
                        httpContext.Response.StatusCode = 403;
                        return;
                    }

                    if (decryptLink?.uri != null)
                        servUri = decryptLink.uri;
                }

                if (string.IsNullOrWhiteSpace(servUri) || !servUri.StartsWith("http"))
                {
                    httpContext.Response.StatusCode = 404;
                    return;
                }

                if (decryptLink == null)
                    decryptLink = new ProxyLinkModel(reqip, null, null, servUri);
                #endregion

                if (init.showOrigUri)
                    httpContext.Response.Headers.Add("PX-Orig", decryptLink.uri);

                #region Кеш файла
                string md5file = httpContext.Request.Path.Value.Replace("/proxy/", "");
                bool ists = md5file.EndsWith(".ts") || md5file.EndsWith(".m4s");

                string md5key = ists ? fixuri(decryptLink) : CORE.CrypTo.md5(decryptLink.uri);
                bool cache_stream = ists && !string.IsNullOrEmpty(md5key) && md5key.Length > 3;

                string foldercache = cache_stream ? $"cache/hls/{md5key.Substring(0, 3)}" : string.Empty;
                string cachefile = cache_stream ? ($"{foldercache}/{md5key.Substring(3)}" + Path.GetExtension(md5file)) : string.Empty;

                if (cache_stream && File.Exists(cachefile))
                {
                    using (var fileStream = new FileStream(cachefile, FileMode.Open, FileAccess.Read))
                    {
                        httpContext.Response.Headers.Add("PX-Cache", "HIT");
                        httpContext.Response.ContentType = md5file.EndsWith(".m4s") ? "video/mp4" : "video/mp2t";
                        httpContext.Response.ContentLength = fileStream.Length;
                        await fileStream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
                    }

                    return;
                }
                #endregion

                #region handler
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    AllowAutoRedirect = false
                };

                handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                if (decryptLink.proxy != null)
                {
                    handler.UseProxy = true;
                    handler.Proxy = decryptLink.proxy;
                }
                #endregion

                using (var client = decryptLink.proxy != null ? new HttpClient(handler) : _httpClientFactory.CreateClient("proxy"))
                {
                    var request = CreateProxyHttpRequest(httpContext, decryptLink.headers, new Uri(servUri), Regex.IsMatch(httpContext.Request.Path.Value, "\\.(m3u|ts|m4s|mp4|mkv|aacp|srt|vtt)", RegexOptions.IgnoreCase));
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);

                    if ((int)response.StatusCode is 301 or 302 or 303 or 0 || response.Headers.Location != null)
                    {
                        httpContext.Response.Redirect(validArgs($"{AppInit.Host(httpContext)}/proxy/{CORE.ProxyLink.Encrypt(response.Headers.Location.AbsoluteUri, decryptLink)}", httpContext));
                        return;
                    }

                    response.Content.Headers.TryGetValues("Content-Type", out var contentType);
                    if (httpContext.Request.Path.Value.Contains(".m3u") || (contentType != null && contentType.First().ToLower() is "application/x-mpegurl" or "application/vnd.apple.mpegurl" or "text/plain"))
                    {
                        #region m3u8/txt
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (response.Content.Headers.ContentLength > init.maxlength_m3u)
                                {
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                string m3u8 = Encoding.UTF8.GetString(await content.ReadAsByteArrayAsync(httpContext.RequestAborted));
                                string hls = editm3u(m3u8, httpContext, decryptLink);

                                httpContext.Response.ContentType = contentType == null ? "application/vnd.apple.mpegurl" : contentType.First();
                                httpContext.Response.ContentLength = hls.Length;
                                await httpContext.Response.WriteAsync(hls, httpContext.RequestAborted).ConfigureAwait(false);
                            }
                            else
                            {
                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                await httpContext.Response.WriteAsync("error proxy m3u8", httpContext.RequestAborted).ConfigureAwait(false);
                            }
                        }
                        #endregion
                    }
                    else if (httpContext.Request.Path.Value.Contains(".mpd") || (contentType != null && contentType.First().ToLower() is "application/dash+xml"))
                    {
                        #region dash
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (response.Content.Headers.ContentLength > init.maxlength_m3u)
                                {
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                string mpd = Encoding.UTF8.GetString(await content.ReadAsByteArrayAsync(httpContext.RequestAborted));
                                string baseURL = Regex.Match(mpd, "<BaseURL>([^<]+)</BaseURL>").Groups[1].Value;

                                string dashkey = CORE.CrypTo.md5(DateTime.Now.ToBinary().ToString());
                                memoryCache.Set($"proxy-dash:{dashkey}:{(init.verifyip ? reqip : "")}", baseURL, DateTime.Now.AddHours(8));

                                mpd = Regex.Replace(mpd, baseURL, $"{AppInit.Host(httpContext)}/proxy-dash/{dashkey}/");

                                httpContext.Response.ContentType = contentType == null ? "application/dash+xml" : contentType.First();
                                httpContext.Response.ContentLength = mpd.Length;
                                await httpContext.Response.WriteAsync(mpd, httpContext.RequestAborted).ConfigureAwait(false);
                            }
                            else
                            {
                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                await httpContext.Response.WriteAsync("error proxy", httpContext.RequestAborted).ConfigureAwait(false);
                            }
                        }
                        #endregion
                    }
                    else if (ists && cache_stream)
                    {
                        #region ts
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (response.Content.Headers.ContentLength > init.maxlength_ts)
                                {
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                byte[] buffer = await content.ReadAsByteArrayAsync(httpContext.RequestAborted).ConfigureAwait(false);

                                if (!File.Exists(cachefile))
                                {
                                    _ = Task.Factory.StartNew(async () =>
                                    {
                                        try
                                        {
                                            Directory.CreateDirectory(foldercache);
                                            await File.WriteAllBytesAsync(cachefile, buffer).ConfigureAwait(false);
                                        }
                                        catch { try { File.Delete(cachefile); } catch { } }

                                    }, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                                }

                                httpContext.Response.Headers.Add("PX-Cache", "MISS");
                                httpContext.Response.ContentType = md5file.EndsWith(".m4s") ? "video/mp4" : "video/mp2t";
                                httpContext.Response.ContentLength = buffer.Length;
                                await httpContext.Response.Body.WriteAsync(buffer, httpContext.RequestAborted).ConfigureAwait(false);
                            }
                            else
                            {
                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                await httpContext.Response.WriteAsync("error proxy ts", httpContext.RequestAborted).ConfigureAwait(false);
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        httpContext.Response.Headers.Add("PX-Cache", "BYPASS");
                        await CopyProxyHttpResponse(httpContext, response);
                    }
                }
            }
            else
            {
                await _next(httpContext);
            }
        }


        #region validArgs
        string validArgs(string uri, HttpContext httpContext)
        {
            if (!AppInit.conf.accsdb.enable)
                return uri;

            return AccsDbInvk.Args(uri, httpContext);
        }
        #endregion

        #region editm3u
        string editm3u(string _m3u8, HttpContext httpContext, ProxyLinkModel decryptLink)
        {
            string proxyhost = $"{AppInit.Host(httpContext)}/proxy";
            string m3u8 = Regex.Replace(_m3u8, "(https?://[^\n\r\"\\# ]+)", m =>
            {
                return validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(m.Groups[1].Value, decryptLink)}", httpContext);
            });

            string hlshost = Regex.Match(decryptLink.uri, "(https?://[^/]+)/").Groups[1].Value;
            string hlspatch = Regex.Match(decryptLink.uri, "(https?://[^\n\r]+/)([^/]+)$").Groups[1].Value;
            if (string.IsNullOrEmpty(hlspatch) && decryptLink.uri.EndsWith("/"))
                hlspatch = decryptLink.uri;

            m3u8 = Regex.Replace(m3u8, "([\n\r])([^\n\r]+)", m =>
            {
                string uri = m.Groups[2].Value;

                if (uri.Contains("#") || uri.Contains("\"") || uri.StartsWith("http"))
                    return m.Groups[0].Value;

                if (uri.StartsWith("//"))
                {
                    uri = "https:" + uri;
                }
                else if (uri.StartsWith("/"))
                {
                    uri = hlshost + uri;
                }
                else if (uri.StartsWith("./"))
                {
                    uri = hlspatch + uri.Substring(2);
                }
                else
                {
                    uri = hlspatch + uri;
                }

                return m.Groups[1].Value + validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(uri, decryptLink)}", httpContext);
            });

            m3u8 = Regex.Replace(m3u8, "(URI=\")([^\"]+)", m =>
            {
                string uri = m.Groups[2].Value;

                if (uri.Contains("\"") || uri.StartsWith("http"))
                    return m.Groups[0].Value;

                if (uri.StartsWith("//"))
                {
                    uri = "https:" + uri;
                }
                else if (uri.StartsWith("/"))
                {
                    uri = hlshost + uri;
                }
                else if (uri.StartsWith("./"))
                {
                    uri = hlspatch + uri.Substring(2);
                }
                else
                {
                    uri = hlspatch + uri;
                }

                return m.Groups[1].Value + validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(uri, decryptLink)}", httpContext);
            });

            return m3u8;
        }
        #endregion

        #region fixuri
        string fixuri(ProxyLinkModel decryptLink)
        {
            string uri = decryptLink.uri;
            var confs = AppInit.conf.serverproxy?.cache?.hls;

            if (confs != null && confs.Count >= 0)
            {
                foreach (var conf in confs)
                {
                    if (!conf.enable || decryptLink.plugin != conf.plugin || conf.tasks == null)
                        continue;

                    string key = uri;
                    foreach (var task in conf.tasks)
                    {
                        if (task.type == "match")
                            key = Regex.Match(key, task.pattern, RegexOptions.IgnoreCase).Groups[task.index].Value;
                        else
                            key = Regex.Replace(key, task.pattern, task.replacement, RegexOptions.IgnoreCase);
                    }

                    if (string.IsNullOrEmpty(key) || uri == key)
                        continue;

                    return CORE.CrypTo.md5($"{decryptLink.plugin}:{key}");
                }
            }

            return null;
        }
        #endregion


        #region CreateProxyHttpRequest
        HttpRequestMessage CreateProxyHttpRequest(HttpContext context, List<HeadersModel> headers, Uri uri, bool ismedia)
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
            if (headers != null && headers.Count > 0)
            {
                foreach (var item in headers)
                    requestMessage.Headers.TryAddWithoutValidation(item.name, item.val);
            }

            if (ismedia)
            {
                foreach (var header in request.Headers)
                {
                    if (header.Key.ToLower() is "range")
                    {
                        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                            requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }
            }
            else
            {
                foreach (var header in request.Headers)
                {
                    if (header.Key.ToLower() is "host" or "origin" or "user-agent" or "referer" or "content-disposition")
                        continue;

                    if (header.Key.ToLower().StartsWith("x-"))
                        continue;

                    if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                    {
                        //Console.WriteLine(header.Key + ": " + String.Join(" ", header.Value.ToArray()));
                        requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }
            }

            if (!requestMessage.Headers.Contains("User-Agent"))
                requestMessage.Headers.TryAddWithoutValidation("User-Agent", CORE.HttpClient.UserAgent);
            #endregion

            requestMessage.Headers.ConnectionClose = false;
            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);
            requestMessage.Version = new Version(2, 0);

            return requestMessage;
        }
        #endregion

        #region CopyProxyHttpResponse
        async ValueTask CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
        {
            var response = context.Response;
            response.StatusCode = (int)responseMessage.StatusCode;
            response.ContentLength = responseMessage.Content.Headers.ContentLength;

            #region UpdateHeaders
            void UpdateHeaders(HttpHeaders headers)
            {
                foreach (var header in headers)
                {
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection" or "content-security-policy")
                        continue;

                    if (header.Key.ToLower().StartsWith("x-"))
                        continue;

                    if (header.Key.ToLower().Contains("access-control"))
                        continue;

                    string value = string.Empty;
                    foreach (var val in header.Value)
                        value += $"; {val}";

                    response.Headers[header.Key] = Regex.Replace(value, "^; ", "");
                    //response.Headers[header.Key] = header.Value.ToArray();
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


                if (AppInit.conf.serverproxy?.buffering?.enable == true && (context.Request.Path.Value.EndsWith(".mp4") || context.Request.Path.Value.EndsWith(".mkv") || responseMessage.Content.Headers.ContentLength > 10_000000))
                {
                    var bunit = AppInit.conf.serverproxy.buffering;
                    byte[] array = ArrayPool<byte>.Shared.Rent(Math.Max(bunit.rent, 4096));

                    try
                    {
                        bool readFinished = false;
                        var writeFinished = new TaskCompletionSource<bool>();
                        var locker = new AsyncManualResetEvent();

                        Queue<byte[]> byteQueue = new Queue<byte[]>();

                        #region read task
                        _ = Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                int bytesRead;
                                while (!context.RequestAborted.IsCancellationRequested && (bytesRead = await responseStream.ReadAsync(new Memory<byte>(array), context.RequestAborted)) != 0)
                                {
                                    byte[] byteCopy = new byte[bytesRead];
                                    Array.Copy(array, byteCopy, bytesRead);

                                    byteQueue.Enqueue(byteCopy);
                                    locker.Set();

                                    if (context.RequestAborted.IsCancellationRequested)
                                        break;

                                    while (byteQueue.Count > bunit.length && !context.RequestAborted.IsCancellationRequested)
                                        await locker.WaitAsync(Math.Max(bunit.millisecondsTimeout, 1), context.RequestAborted).ConfigureAwait(false);
                                }
                            }
                            finally
                            {
                                readFinished = true;
                                locker.Set();
                            }

                        }, context.RequestAborted, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        #endregion

                        #region write task
                        _ = Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                while (true)
                                {
                                    if (context.RequestAborted.IsCancellationRequested)
                                        break;

                                    if (byteQueue.Count > 0)
                                    {
                                        byte[] bytesToSend = byteQueue.Dequeue();
                                        locker.Set();

                                        await response.Body.WriteAsync(new ReadOnlyMemory<byte>(bytesToSend), context.RequestAborted).ConfigureAwait(false);
                                    }
                                    else if (readFinished)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        await locker.WaitAsync(Math.Max(bunit.millisecondsTimeout, 1), context.RequestAborted).ConfigureAwait(false);
                                    }
                                }
                            }
                            finally
                            {
                                locker.Set();
                                writeFinished.SetResult(true);
                            }

                        }, context.RequestAborted, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        #endregion

                        await writeFinished.Task;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(array);
                    }
                }
                else
                {
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
        }
        #endregion
    }
}
