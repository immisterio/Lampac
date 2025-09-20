using Microsoft.AspNetCore.Http;
using Shared;
using Shared.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using Shared.Models.Proxy;
using Shared.Engine;

namespace Lampac.Engine.Middlewares
{
    public class ProxyAPI
    {
        #region ProxyAPI
        public ProxyAPI(RequestDelegate next) { }

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
            string servUri = httpContext.Request.Path.Value.Replace("/proxy/", "").Replace("/proxy-dash/", "") + httpContext.Request.QueryString.Value;

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
            string _servUri = ProxyLink.IsAes(servUri) ? servUri : servUri.Split("/")[0];
            var decryptLink = ProxyLink.Decrypt(Regex.Replace(_servUri, "(\\?|&).*", ""), reqip);

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
            using var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = false
            };

            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            if (decryptLink.proxy != null)
            {
                handler.UseProxy = true;
                handler.Proxy = decryptLink.proxy;
            }
            #endregion

            if (httpContext.Request.Path.Value.StartsWith("/proxy-dash/"))
            {
                #region DASH
                servUri += Regex.Replace(httpContext.Request.Path.Value, "/[^/]+/[^/]+/", "") + httpContext.Request.QueryString.Value;

                var client = FrendlyHttp.CreateClient("ProxyAPI:DASH", handler, "proxy", timeoutSeconds: 20);

                using (var request = CreateProxyHttpRequest(httpContext, decryptLink.headers, new Uri(servUri), true))
                {
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted).ConfigureAwait(false))
                    {
                        httpContext.Response.Headers["PX-Cache"] = "BYPASS";
                        await CopyProxyHttpResponse(httpContext, response).ConfigureAwait(false);
                    }
                }
                #endregion
            }
            else
            {
                #region Кеш файла
                string md5file = httpContext.Request.Path.Value.Replace("/proxy/", "");
                bool ists = md5file.EndsWith(".ts") || md5file.EndsWith(".m4s");

                string md5key = ists ? fixuri(decryptLink) : CrypTo.md5(decryptLink.uri);
                bool cache_stream = ists && !string.IsNullOrEmpty(md5key) && md5key.Length > 3;

                string foldercache = cache_stream ? $"cache/hls/{md5key.Substring(0, 3)}" : string.Empty;
                string cachefile = cache_stream ? ($"{foldercache}/{md5key.Substring(3)}" + Path.GetExtension(md5file)) : string.Empty;

                if (cache_stream && File.Exists(cachefile))
                {
                    httpContext.Response.Headers["PX-Cache"] = "HIT";
                    httpContext.Response.ContentType = md5file.EndsWith(".m4s") ? "video/mp4" : "video/mp2t";
                    await httpContext.Response.SendFileAsync(cachefile).ConfigureAwait(false);
                    return;
                }
                #endregion

                #region Video OR
                if (servUri.Contains(" or "))
                {
                    var hdlr = new HttpClientHandler()
                    {
                        AllowAutoRedirect = true,
                        AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
                    };

                    hdlr.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                    if (decryptLink.proxy != null)
                    {
                        hdlr.UseProxy = true;
                        hdlr.Proxy = decryptLink.proxy;
                    }

                    string[] links = servUri.Split(" or ");
                    servUri = links[0].Trim();

                    try
                    {
                        // base => AllowAutoRedirect = true
                        var clientor = FrendlyHttp.CreateClient("ProxyAPI:or", hdlr, "base", timeoutSeconds: 7);

                        using (var requestor = CreateProxyHttpRequest(httpContext, decryptLink.headers, new Uri(servUri), true))
                        {
                            using (var response = await clientor.SendAsync(requestor, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted).ConfigureAwait(false))
                            {
                                if ((int)response.StatusCode != 200)
                                    servUri = links[1].Trim();
                            }
                        }
                    }
                    catch
                    {
                        servUri = links[1].Trim();
                    }

                    servUri = servUri.Split(" ")[0].Trim();
                    decryptLink.uri = servUri;
                }
                #endregion

                var client = FrendlyHttp.CreateClient("ProxyAPI", handler, "proxy", timeoutSeconds: 20);

                using var request = CreateProxyHttpRequest(httpContext, decryptLink.headers, new Uri(servUri), Regex.IsMatch(httpContext.Request.Path.Value, "\\.(m3u|ts|m4s|mp4|mkv|aacp|srt|vtt)", RegexOptions.IgnoreCase));

                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted).ConfigureAwait(false))
                {
                    if ((int)response.StatusCode is 301 or 302 or 303 or 0 || response.Headers.Location != null)
                    {
                        httpContext.Response.Redirect(validArgs($"{AppInit.Host(httpContext)}/proxy/{ProxyLink.Encrypt(response.Headers.Location.AbsoluteUri, decryptLink)}", httpContext));
                        return;
                    }

                    response.Content.Headers.TryGetValues("Content-Type", out var contentType);
                    if (!ists && (httpContext.Request.Path.Value.Contains(".m3u") || (contentType != null && contentType.First().ToLower() is "application/x-mpegurl" or "application/vnd.apple.mpegurl" or "text/plain")))
                    {
                        #region m3u8/txt
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent)
                            {
                                if (response.Content.Headers.ContentLength > init.maxlength_m3u)
                                {
                                    httpContext.Response.StatusCode = 502;
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                var array = await content.ReadAsByteArrayAsync(httpContext.RequestAborted).ConfigureAwait(false);
                                if (array == null)
                                {
                                    httpContext.Response.StatusCode = 502;
                                    await httpContext.Response.WriteAsync("error proxy m3u8", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                string hls = editm3u(Encoding.UTF8.GetString(array), httpContext, decryptLink);

                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                httpContext.Response.ContentType = contentType == null ? "application/vnd.apple.mpegurl" : contentType.First();
                                //httpContext.Response.ContentLength = hls.Length;
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

                                var array = await content.ReadAsByteArrayAsync(httpContext.RequestAborted).ConfigureAwait(false);
                                if (array == null)
                                {
                                    httpContext.Response.StatusCode = 502;
                                    await httpContext.Response.WriteAsync("error proxy mpd", httpContext.RequestAborted).ConfigureAwait(false);
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

                                httpContext.Response.ContentType = contentType == null ? "application/dash+xml" : contentType.First();
                                //httpContext.Response.ContentLength = mpd.Length;
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
                            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent)
                            {
                                if (response.Content.Headers.ContentLength > init.maxlength_ts)
                                {
                                    httpContext.Response.StatusCode = 502;
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                byte[] buffer = await content.ReadAsByteArrayAsync(httpContext.RequestAborted).ConfigureAwait(false);

                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                httpContext.Response.Headers["PX-Cache"] = "MISS";
                                httpContext.Response.ContentType = md5file.EndsWith(".m4s") ? "video/mp4" : "video/mp2t";
                                //httpContext.Response.ContentLength = buffer.Length;
                                await httpContext.Response.Body.WriteAsync(buffer, httpContext.RequestAborted).ConfigureAwait(false);

                                try
                                {
                                    if (!File.Exists(cachefile))
                                    {
                                        Directory.CreateDirectory(foldercache);

                                        using (var fileStream = new FileStream(cachefile, FileMode.Create, FileAccess.Write, FileShare.None))
                                            await fileStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                                    }
                                }
                                catch { try { File.Delete(cachefile); } catch { } }
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
                        httpContext.Response.Headers["PX-Cache"] = "BYPASS";
                        await CopyProxyHttpResponse(httpContext, response).ConfigureAwait(false);
                    }
                }
            }
        }


        #region validArgs
        static string validArgs(in string uri, HttpContext httpContext)
        {
            if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                return AccsDbInvk.Args(uri, httpContext);

            return uri;
        }
        #endregion

        #region editm3u
        static string editm3u(in string _m3u8, HttpContext httpContext, ProxyLinkModel decryptLink)
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

            return m3u8;
        }
        #endregion

        #region fixuri
        static string fixuri(ProxyLinkModel decryptLink)
        {
            string uri = decryptLink.uri;
            var confs = AppInit.conf.serverproxy?.cache_hls;

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

                    return CrypTo.md5($"{decryptLink.plugin}:{key}");
                }
            }

            return null;
        }
        #endregion


        #region CreateProxyHttpRequest
        static HttpRequestMessage CreateProxyHttpRequest(HttpContext context, List<HeadersModel> headers, Uri uri, bool ismedia)
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

            if (ismedia || headers != null)
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
                    if (header.Key.ToLower() is "host" or "origin" or "user-agent" or "referer" or "content-disposition" or "accept-encoding")
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
                requestMessage.Headers.TryAddWithoutValidation("User-Agent", Http.UserAgent);
            #endregion

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

            //requestMessage.Version = new Version(2, 0);
            //Console.WriteLine(JsonConvert.SerializeObject(requestMessage.Headers, Formatting.Indented));

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

                var bunit = AppInit.conf.serverproxy?.buffering;

                if (bunit?.enable == true && 
                   ((!string.IsNullOrEmpty(bunit.pattern) && Regex.IsMatch(context.Request.Path.Value, bunit.pattern, RegexOptions.IgnoreCase)) || 
                   context.Request.Path.Value.EndsWith(".mp4") || context.Request.Path.Value.EndsWith(".mkv") || responseMessage.Content.Headers.ContentLength > 40_000000))
                {
                    #region buffering
                    var channel = Channel.CreateBounded<(byte[] Buffer, int Length)>(new BoundedChannelOptions(capacity: bunit.length)
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
                                    byte[] chunkBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(bunit.rent, 4096));
                                    int bytesRead = await responseStream.ReadAsync(chunkBuffer, 0, chunkBuffer.Length, context.RequestAborted);

                                    if (bytesRead == 0) 
                                        break;

                                    await channel.Writer.WriteAsync((chunkBuffer, bytesRead), context.RequestAborted);
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
                            await foreach (var (chunkBuffer, length) in channel.Reader.ReadAllAsync(context.RequestAborted))
                            {
                                try
                                {
                                    await response.Body.WriteAsync(chunkBuffer, 0, length, context.RequestAborted);
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
                    #region bypass
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
                    #endregion
                }
            }
        }
        #endregion
    }
}
