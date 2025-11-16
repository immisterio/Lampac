using Microsoft.AspNetCore.Http;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Proxy;
using Shared.Models.Events;
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
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

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
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
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

            if (httpContext.Request.Path.Value.StartsWith("/proxy-dash/"))
            {
                #region DASH
                servUri += Regex.Replace(httpContext.Request.Path.Value, "/[^/]+/[^/]+/", "") + httpContext.Request.QueryString.Value;

                var client = FrendlyHttp.HttpMessageClient("proxy", handler);

                using (var request = await CreateProxyHttpRequest(decryptLink.plugin, httpContext, decryptLink.headers, new Uri(servUri), true))
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                    {
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false))
                        {
                            httpContext.Response.Headers["PX-Cache"] = "BYPASS";
                            await CopyProxyHttpResponse(httpContext, response).ConfigureAwait(false);
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
                    else { hdlr.UseProxy = false; }

                    string[] links = servUri.Split(" or ");
                    servUri = links[0].Trim();

                    try
                    {
                        // base => AllowAutoRedirect = true
                        var clientor = FrendlyHttp.HttpMessageClient("base", hdlr);

                        using (var requestor = await CreateProxyHttpRequest(decryptLink.plugin, httpContext, decryptLink.headers, new Uri(servUri), true))
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

                using (var request = await CreateProxyHttpRequest(decryptLink.plugin, httpContext, decryptLink.headers, new Uri(servUri), Regex.IsMatch(httpContext.Request.Path.Value, "\\.(m3u|ts|m4s|mp4|mkv|aacp|srt|vtt)", RegexOptions.IgnoreCase)))
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                    {
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false))
                        {
                            if ((int)response.StatusCode is 301 or 302 or 303 or 0 || response.Headers.Location != null)
                            {
                                httpContext.Response.Redirect(validArgs($"{AppInit.Host(httpContext)}/proxy/{ProxyLink.Encrypt(response.Headers.Location.AbsoluteUri, decryptLink)}", httpContext));
                                return;
                            }

                            response.Content.Headers.TryGetValues("Content-Type", out var contentType);

                            bool ists = httpContext.Request.Path.Value.EndsWith(".ts") || httpContext.Request.Path.Value.EndsWith(".m4s");

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

                                        httpContext.Response.ContentType = contentType == null ? "application/vnd.apple.mpegurl" : contentType.First();
                                        httpContext.Response.StatusCode = (int)response.StatusCode;
                                        //httpContext.Response.ContentLength = hls.Length;

                                        if (response.Headers.AcceptRanges != null)
                                            httpContext.Response.Headers["accept-ranges"] = "bytes";

                                        if (httpContext.Response.StatusCode == 206)
                                            httpContext.Response.Headers["content-range"] = $"bytes 0-{hls.Length - 1}/{hls.Length}";

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
                            else
                            {
                                httpContext.Response.Headers["PX-Cache"] = "BYPASS";
                                await CopyProxyHttpResponse(httpContext, response).ConfigureAwait(false);
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
        static string editm3u(string _m3u8, HttpContext httpContext, ProxyLinkModel decryptLink)
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

            await InvkEvent.ProxyApi(new EventProxyApiCreateHttpRequest(plugin, request, headers, uri, ismedia, requestMessage));

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
                    #region bypass
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                    try
                    {
                        int bytesRead;

                        while ((bytesRead = await responseStream.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) != 0)
                            await response.Body.WriteAsync(buffer, 0, bytesRead, context.RequestAborted).ConfigureAwait(false);
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
