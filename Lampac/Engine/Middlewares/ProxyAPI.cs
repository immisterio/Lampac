using Microsoft.AspNetCore.Http;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Proxy;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyAPI
    {
        #region ProxyAPI
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, long> cacheFiles = new();

        static ProxyAPI()
        {
            Directory.CreateDirectory("cache/hls");

            foreach (var item in Directory.EnumerateFiles("cache/hls", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), new FileInfo(item).Length);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/hls",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };
        }

        public ProxyAPI(RequestDelegate next) { }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            var init = AppInit.conf.serverproxy;
            var requestInfo = httpContext.Features.Get<RequestModel>();
            string reqip = requestInfo.IP;
            string servPath = httpContext.Request.Path.Value.Replace("/proxy/", "").Replace("/proxy-dash/", "");
            string servUri = servPath + httpContext.Request.QueryString.Value;

            #region tmdb proxy
            if (servUri.Contains(".themoviedb.org"))
            {
                httpContext.Response.Redirect($"/tmdb/api/{Regex.Match(servUri.Replace("://", ":/_/").Replace("//", "/").Replace(":/_/", "://"), "https?://[^/]+/(.*)").Groups[1].Value}");
                return;
            }
            else if (servUri.Contains(".tmdb.org"))
            {
                httpContext.Response.Redirect($"/tmdb/img/{Regex.Match(servUri.Replace("://", ":/_/").Replace("//", "/").Replace(":/_/", "://"), "https?://[^/]+/(.*)").Groups[1].Value}");
                return;
            }
            #endregion

            #region decryptLink
            var decryptLink = ProxyLink.Decrypt(httpContext.Request.Path.Value.StartsWith("/proxy-dash/") ? servPath.Split("/")[0] : servPath, reqip);

            if (init.encrypt || decryptLink?.uri != null || httpContext.Request.Path.Value.StartsWith("/proxy-dash/"))
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
            {
                //Console.WriteLine("PX-Orig: " + decryptLink.uri);
                httpContext.Response.Headers["PX-Orig"] = decryptLink.uri;
            }

            #region handler
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.None,
                AllowAutoRedirect = false
            };

            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            if (decryptLink.proxy != null)
            {
                handler.UseProxy = true;
                handler.Proxy = decryptLink.proxy;
            }
            else { handler.UseProxy = false; }
            #endregion

            #region cacheFiles
            (string uriKey, string contentType) cacheStream = InvkEvent.ProxyApiCacheStream(httpContext, decryptLink);

            if (cacheStream.uriKey != null && init.showOrigUri)
                httpContext.Response.Headers["PX-CacheStream"] = cacheStream.uriKey;

            if (cacheStream.uriKey != null && cacheFiles.ContainsKey(CrypTo.md5(cacheStream.uriKey)))
            {
                string md5key = CrypTo.md5(cacheStream.uriKey);

                httpContext.Response.Headers["PX-Cache"] = "HIT";
                httpContext.Response.Headers["accept-ranges"] = "bytes";
                httpContext.Response.ContentType = cacheStream.contentType ?? "application/octet-stream";

                long cacheLength = cacheFiles[md5key];
                string cachePath = $"cache/hls/{md5key}";

                if (RangeHeaderValue.TryParse(httpContext.Request.Headers["Range"], out var range))
                {
                    var rangeItem = range.Ranges.FirstOrDefault();
                    if (rangeItem != null)
                    {
                        long start = rangeItem.From ?? 0;
                        long end = rangeItem.To ?? (cacheLength - 1);

                        if (start >= cacheLength)
                        {
                            httpContext.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                            httpContext.Response.Headers["content-range"] = $"bytes */{cacheLength}";
                            return;
                        }

                        if (end >= cacheLength)
                            end = cacheLength - 1;

                        long length = end - start + 1;

                        httpContext.Response.StatusCode = StatusCodes.Status206PartialContent;
                        httpContext.Response.Headers["content-range"] = $"bytes {start}-{end}/{cacheLength}";

                        if (init.responseContentLength)
                            httpContext.Response.ContentLength = length;

                        await httpContext.Response.SendFileAsync(cachePath, start, length, httpContext.RequestAborted).ConfigureAwait(false);
                        return;
                    }
                }

                if (init.responseContentLength)
                    httpContext.Response.ContentLength = cacheLength;

                await httpContext.Response.SendFileAsync(cachePath, httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }
            #endregion

            if (httpContext.Request.Path.Value.StartsWith("/proxy-dash/"))
            {
                #region DASH
                servUri += Regex.Replace(httpContext.Request.Path.Value, "/[^/]+/[^/]+/", "") + httpContext.Request.QueryString.Value;

                var client = FrendlyHttp.HttpMessageClient("proxy", handler);

                using (var request = await CreateProxyHttpRequest(decryptLink.plugin, httpContext, decryptLink.headers, new Uri(servUri), true).ConfigureAwait(false))
                {
                    using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted))
                    {
                        ctsHttp.CancelAfter(TimeSpan.FromSeconds(30));

                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctsHttp.Token).ConfigureAwait(false))
                        {
                            httpContext.Response.Headers["PX-Cache"] = "BYPASS";
                            await CopyProxyHttpResponse(httpContext, response, cacheStream.uriKey).ConfigureAwait(false);
                        }
                    }
                }
                #endregion
            }
            else
            {
                #region Video OR
                if (servUri.Contains(" or "))
                {
                    string[] links = servUri.Split(" or ");
                    servUri = links[0].Trim();

                    try
                    {
                        var hdlr = new HttpClientHandler()
                        {
                            AllowAutoRedirect = true
                        };

                        hdlr.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                        if (decryptLink.proxy != null)
                        {
                            hdlr.UseProxy = true;
                            hdlr.Proxy = decryptLink.proxy;
                        }
                        else { hdlr.UseProxy = false; }

                        var clientor = FrendlyHttp.HttpMessageClient("base", hdlr);

                        using (var requestor = await CreateProxyHttpRequest(decryptLink.plugin, httpContext, decryptLink.headers, new Uri(servUri), true).ConfigureAwait(false))
                        {
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7)))
                            {
                                using (var response = await clientor.SendAsync(requestor, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false))
                                {
                                    if ((int)response.StatusCode is 200 or 206) { }
                                    else
                                        servUri = links[1].Trim();
                                }
                            }
                        }
                    }
                    catch
                    {
                        servUri = links[1].Trim();
                    }

                    servUri = servUri.Split(" ")[0].Trim();
                    decryptLink.uri = servUri;

                    if (init.showOrigUri)
                        httpContext.Response.Headers["PX-Set-Orig"] = decryptLink.uri;
                }
                #endregion

                var client = FrendlyHttp.HttpMessageClient("proxy", handler);

                using (var request = await CreateProxyHttpRequest(decryptLink.plugin, httpContext, decryptLink.headers, new Uri(servUri), Regex.IsMatch(httpContext.Request.Path.Value, "\\.(m3u|ts|m4s|mp4|mkv|aacp|srt|vtt)", RegexOptions.IgnoreCase)).ConfigureAwait(false))
                {
                    using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted))
                    {
                        ctsHttp.CancelAfter(TimeSpan.FromSeconds(30));

                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctsHttp.Token).ConfigureAwait(false))
                        {
                            if ((int)response.StatusCode is 301 or 302 or 303 or 0 || response.Headers.Location != null)
                            {
                                httpContext.Response.Redirect(validArgs($"{AppInit.Host(httpContext)}/proxy/{ProxyLink.Encrypt(response.Headers.Location.AbsoluteUri, decryptLink)}", httpContext));
                                return;
                            }

                            IEnumerable<string> _contentType = null;
                            if (response.Content?.Headers != null)
                                response.Content.Headers.TryGetValues("Content-Type", out _contentType);

                            string contentType = _contentType?.FirstOrDefault()?.ToLower();

                            bool ists = httpContext.Request.Path.Value.EndsWith(".ts") || httpContext.Request.Path.Value.EndsWith(".m4s");

                            if (!ists && (httpContext.Request.Path.Value.Contains(".m3u") || (contentType != null && contentType is "application/x-mpegurl" or "application/vnd.apple.mpegurl" or "text/plain")))
                            {
                                #region m3u8/txt
                                using (HttpContent content = response.Content)
                                {
                                    if (response.StatusCode == HttpStatusCode.OK || 
                                        response.StatusCode == HttpStatusCode.PartialContent || 
                                        response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                                    {
                                        if (response.Content?.Headers?.ContentLength > init.maxlength_m3u)
                                        {
                                            httpContext.Response.StatusCode = 503;
                                            httpContext.Response.ContentType = "text/plain";
                                            await httpContext.Response.WriteAsync("bigfile", ctsHttp.Token).ConfigureAwait(false);
                                            return;
                                        }

                                        var array = await content.ReadAsByteArrayAsync(ctsHttp.Token).ConfigureAwait(false);
                                        if (array == null)
                                        {
                                            httpContext.Response.StatusCode = 503;
                                            await httpContext.Response.WriteAsync("error array m3u8", ctsHttp.Token).ConfigureAwait(false);
                                            return;
                                        }

                                        byte[] hlsArray = editm3u(Encoding.UTF8.GetString(array), httpContext, decryptLink);

                                        httpContext.Response.ContentType = contentType ?? "application/vnd.apple.mpegurl";
                                        httpContext.Response.StatusCode = (int)response.StatusCode;

                                        if (response.Headers.AcceptRanges != null)
                                            httpContext.Response.Headers["accept-ranges"] = "bytes";

                                        if (httpContext.Response.StatusCode is 206 or 416)
                                        {
                                            var contentRange = response.Content?.Headers?.ContentRange;
                                            if (contentRange != null)
                                            {
                                                httpContext.Response.Headers["content-range"] = contentRange.ToString();
                                            }
                                            else
                                            {
                                                if (httpContext.Response.StatusCode == 206)
                                                    httpContext.Response.Headers["content-range"] = $"bytes 0-{hlsArray.Length - 1}/{hlsArray.Length}";

                                                if (httpContext.Response.StatusCode == 416)
                                                    httpContext.Response.Headers["content-range"] = $"bytes */{hlsArray.Length}";
                                            }
                                        }
                                        else
                                        {
                                            if (init.responseContentLength && !AppInit.CompressionMimeTypes.Contains(httpContext.Response.ContentType))
                                                httpContext.Response.ContentLength = hlsArray.Length;
                                        }

                                        await httpContext.Response.Body.WriteAsync(hlsArray, ctsHttp.Token).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        // проксируем ошибку 
                                        await CopyProxyHttpResponse(httpContext, response, null).ConfigureAwait(false);
                                    }
                                }
                                #endregion
                            }
                            else if (httpContext.Request.Path.Value.Contains(".mpd") || (contentType != null && contentType == "application/dash+xml"))
                            {
                                #region dash
                                using (HttpContent content = response.Content)
                                {
                                    if (response.StatusCode == HttpStatusCode.OK || 
                                        response.StatusCode == HttpStatusCode.PartialContent ||
                                        response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                                    {
                                        if (response.Content?.Headers?.ContentLength > init.maxlength_m3u)
                                        {
                                            httpContext.Response.StatusCode = 503;
                                            httpContext.Response.ContentType = "text/plain";
                                            await httpContext.Response.WriteAsync("bigfile", ctsHttp.Token).ConfigureAwait(false);
                                            return;
                                        }

                                        var array = await content.ReadAsByteArrayAsync(ctsHttp.Token).ConfigureAwait(false);
                                        if (array == null)
                                        {
                                            httpContext.Response.StatusCode = 503;
                                            await httpContext.Response.WriteAsync("error array mpd", ctsHttp.Token).ConfigureAwait(false);
                                            return;
                                        }

                                        string mpd = Encoding.UTF8.GetString(array);

                                        var m = Regex.Match(mpd, "<BaseURL>([^<]+)</BaseURL>");
                                        while (m.Success)
                                        {
                                            string baseURL = m.Groups[1].Value;
                                            mpd = Regex.Replace(mpd, baseURL, $"{AppInit.Host(httpContext)}/proxy-dash/{ProxyLink.Encrypt(baseURL, decryptLink, forceMd5: true)}/");
                                            m = m.NextMatch();
                                        }

                                        byte[] mpdArray = Encoding.UTF8.GetBytes(mpd);

                                        httpContext.Response.ContentType = contentType ?? "application/dash+xml";
                                        httpContext.Response.StatusCode = (int)response.StatusCode;

                                        if (response.Headers.AcceptRanges != null)
                                            httpContext.Response.Headers["accept-ranges"] = "bytes";

                                        if (httpContext.Response.StatusCode is 206 or 416)
                                        {
                                            var contentRange = response.Content.Headers.ContentRange;
                                            if (contentRange != null)
                                            {
                                                httpContext.Response.Headers["content-range"] = contentRange.ToString();
                                            }
                                            else
                                            {
                                                if (httpContext.Response.StatusCode == 206)
                                                    httpContext.Response.Headers["content-range"] = $"bytes 0-{mpdArray.Length - 1}/{mpdArray.Length}";

                                                if (httpContext.Response.StatusCode == 416)
                                                    httpContext.Response.Headers["content-range"] = $"bytes */{mpdArray.Length}";
                                            }
                                        }
                                        else
                                        {
                                            if (init.responseContentLength && !AppInit.CompressionMimeTypes.Contains(httpContext.Response.ContentType))
                                                httpContext.Response.ContentLength = mpdArray.Length;
                                        }

                                        await httpContext.Response.Body.WriteAsync(mpdArray, ctsHttp.Token).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        // проксируем ошибку 
                                        await CopyProxyHttpResponse(httpContext, response, null).ConfigureAwait(false);
                                    }
                                }
                                #endregion
                            }
                            else
                            {
                                httpContext.Response.Headers["PX-Cache"] = cacheStream.uriKey != null ? "MISS" : "BYPASS";
                                await CopyProxyHttpResponse(httpContext, response, cacheStream.uriKey).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }


        #region validArgs
        static string validArgs(string uri, HttpContext httpContext)
        {
            if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                return AccsDbInvk.Args(uri, httpContext);

            return uri;
        }
        #endregion

        #region editm3u
        static byte[] editm3u(string _m3u8, HttpContext httpContext, ProxyLinkModel decryptLink)
        {
            string proxyhost = $"{AppInit.Host(httpContext)}/proxy";
            string m3u8 = Regex.Replace(_m3u8, "(https?://[^\n\r\"\\# ]+)", m =>
            {
                return validArgs($"{proxyhost}/{ProxyLink.Encrypt(m.Groups[1].Value, decryptLink)}", httpContext);
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

                return m.Groups[1].Value + validArgs($"{proxyhost}/{ProxyLink.Encrypt(uri, decryptLink)}", httpContext);
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

                return m.Groups[1].Value + validArgs($"{proxyhost}/{ProxyLink.Encrypt(uri, decryptLink)}", httpContext);
            });

            return Encoding.UTF8.GetBytes(m3u8);
        }
        #endregion


        #region CreateProxyHttpRequest
        async static Task<HttpRequestMessage> CreateProxyHttpRequest(string plugin, HttpContext context, List<HeadersModel> headers, Uri uri, bool ismedia)
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
            {
                var addHeaders = new Dictionary<string, string[]>() 
                {
                    ["accept"] = ["*/*"],
                    ["accept-language"] = ["ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"]
                };

                if (headers != null && headers.Count > 0)
                {
                    foreach (var h in headers)
                        addHeaders[h.name.ToLower().Trim()] = [h.val];
                }

                if (ismedia || headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        if (header.Key.ToLower() == "range")
                            addHeaders[header.Key.ToLower().Trim()] = header.Value.ToArray();
                    }
                }
                else
                {
                    foreach (var header in request.Headers)
                    {
                        if (header.Key.ToLower() is "host" or "origin" or "user-agent" or "referer" or "content-disposition" or "accept-encoding")
                            continue;

                        if (header.Key.ToLower().StartsWith("x-"))
                            continue;

                        if (header.Key.ToLower() == "range")
                        {
                            addHeaders[header.Key.ToLower().Trim()] = header.Value.ToArray();
                            continue;
                        }

                        addHeaders.TryAdd(header.Key.ToLower().Trim(), header.Value.ToArray());
                    }
                }

                foreach (var h in Http.defaultFullHeaders)
                    addHeaders[h.Key.ToLower().Trim()] = [h.Value];

                foreach (var h in Http.NormalizeHeaders(addHeaders))
                {
                    if (!requestMessage.Headers.TryAddWithoutValidation(h.Key, h.Value))
                    {
                        if (requestMessage.Content?.Headers != null)
                            requestMessage.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                    }
                }
            }
            #endregion

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

            //requestMessage.Version = new Version(2, 0);
            //Console.WriteLine(JsonConvert.SerializeObject(requestMessage.Headers, Formatting.Indented));

            await InvkEvent.ProxyApiCreateHttpRequest(plugin, request, headers, uri, ismedia, requestMessage).ConfigureAwait(false);

            return requestMessage;
        }
        #endregion

        #region CopyProxyHttpResponse
        async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage, string uriKeyFileCache)
        {
            var response = context.Response;
            response.StatusCode = (int)responseMessage.StatusCode;

            #region responseContentLength
            if (AppInit.conf.serverproxy.responseContentLength && responseMessage.Content?.Headers?.ContentLength > 0)
            {
                IEnumerable<string> contentType = null;
                if (responseMessage.Content?.Headers != null)
                    responseMessage.Content.Headers.TryGetValues("Content-Type", out contentType);

                string type = contentType?.FirstOrDefault()?.ToLower();

                if (string.IsNullOrEmpty(type) || !AppInit.CompressionMimeTypes.Contains(type))
                    response.ContentLength = responseMessage.Content.Headers.ContentLength;
            }
            #endregion

            #region UpdateHeaders
            void UpdateHeaders(HttpHeaders headers)
            {
                foreach (var header in headers)
                {
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection" or "content-security-policy" or "content-disposition" or "content-length")
                        continue;

                    if (header.Key.ToLower().StartsWith("x-") || header.Key.ToLower().StartsWith("alt-"))
                        continue;

                    if (header.Key.ToLower().StartsWith("access-control"))
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

            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync(context.RequestAborted).ConfigureAwait(false))
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

                var buffering = AppInit.conf.serverproxy?.buffering;

                if (buffering?.enable == true && 
                   ((!string.IsNullOrEmpty(buffering.pattern) && Regex.IsMatch(context.Request.Path.Value, buffering.pattern, RegexOptions.IgnoreCase)) || 
                   context.Request.Path.Value.EndsWith(".mp4") || context.Request.Path.Value.EndsWith(".mkv") || responseMessage.Content?.Headers?.ContentLength > 40_000000))
                {
                    #region buffering
                    var channel = Channel.CreateBounded<(byte[] Buffer, int Length)>(new BoundedChannelOptions(capacity: buffering.length)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleWriter = true,
                        SingleReader = true
                    });

                    var readTask = Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                while (!context.RequestAborted.IsCancellationRequested)
                                {
                                    byte[] chunkBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(buffering.rent, 4096));

                                    try
                                    {
                                        int bytesRead = await responseStream.ReadAsync(chunkBuffer, 0, chunkBuffer.Length, context.RequestAborted);

                                        if (bytesRead == 0)
                                        {
                                            ArrayPool<byte>.Shared.Return(chunkBuffer);
                                            break;
                                        }

                                        await channel.Writer.WriteAsync((chunkBuffer, bytesRead), context.RequestAborted);
                                    }
                                    catch
                                    {
                                        ArrayPool<byte>.Shared.Return(chunkBuffer);
                                        break;
                                    }
                                }
                            }
                            finally
                            {
                                channel.Writer.Complete();
                            }
                        },
                        context.RequestAborted, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default
                    ).Unwrap();

                    var writeTask = Task.Factory.StartNew(async () =>
                        {
                            bool reqAborted = false;

                            await foreach (var (chunkBuffer, length) in channel.Reader.ReadAllAsync(context.RequestAborted))
                            {
                                try
                                {
                                    if (reqAborted == false)
                                        await response.Body.WriteAsync(chunkBuffer, 0, length, context.RequestAborted);
                                }
                                catch 
                                {
                                    reqAborted = true;
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(chunkBuffer);
                                }
                            }
                        },
                        context.RequestAborted, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default
                    ).Unwrap();

                    await Task.WhenAll(readTask, writeTask).ConfigureAwait(false);
                    #endregion
                }
                else
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                    try
                    {
                        if (uriKeyFileCache != null && 
                            responseMessage.Content.Headers.ContentLength.HasValue && 
                            AppInit.conf.serverproxy.maxlength_ts >= responseMessage.Content.Headers.ContentLength)
                        {
                            #region cache
                            string md5key = CrypTo.md5(uriKeyFileCache);
                            string targetFile = $"cache/hls/{md5key}";
                            var semaphore = new SemaphorManager(targetFile, context.RequestAborted);

                            try
                            {
                                await semaphore.WaitAsync().ConfigureAwait(false);

                                int cacheLength = 0;

                                using (var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096*20))
                                {
                                    int bytesRead;
                                    while ((bytesRead = await responseStream.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) != 0)
                                    {
                                        cacheLength += bytesRead;
                                        await fileStream.WriteAsync(buffer, 0, bytesRead, context.RequestAborted).ConfigureAwait(false);
                                        await response.Body.WriteAsync(buffer, 0, bytesRead, context.RequestAborted).ConfigureAwait(false);
                                    }
                                }

                                if (!responseMessage.Content.Headers.ContentLength.HasValue || responseMessage.Content.Headers.ContentLength.Value == cacheLength)
                                {
                                    cacheFiles[md5key] = cacheLength;
                                }
                                else
                                {
                                    File.Delete(targetFile);
                                }
                            }
                            catch
                            {
                                File.Delete(targetFile);
                                throw;
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                            #endregion
                        }
                        else
                        {
                            #region bypass
                            int bytesRead;

                            while ((bytesRead = await responseStream.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) != 0)
                                await response.Body.WriteAsync(buffer, 0, bytesRead, context.RequestAborted).ConfigureAwait(false);
                            #endregion
                        }
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
