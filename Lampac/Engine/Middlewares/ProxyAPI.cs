using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Threading;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Buffers;

namespace Lampac.Engine.Middlewares
{
    public class ProxyAPI
    {
        #region ProxyAPI
        private readonly RequestDelegate _next;

        public ProxyAPI(RequestDelegate next)
        {
            _next = next;
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxy/"))
            {
                Shared.Models.ProxyLinkModel decryptLink = null;
                string reqip = httpContext.Connection.RemoteIpAddress.ToString();
                string servUri = httpContext.Request.Path.Value.Replace("/proxy/", "") + httpContext.Request.QueryString.Value;
                string account_email = Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)account_email=([^&]+)").Groups[2].Value;

                if (AppInit.conf.serverproxy.encrypt)
                {
                    if (servUri.Contains(".themoviedb.org") || servUri.Contains(".tmdb.org"))
                    {
                        if (!AppInit.conf.serverproxy.allow_tmdb)
                        {
                            httpContext.Response.StatusCode = 403;
                            return;
                        }
                    }
                    else
                    {
                        decryptLink = CORE.ProxyLink.Decrypt(Regex.Replace(servUri, "(\\?|&).*", ""), reqip);
                        servUri = decryptLink?.uri;
                    }
                }
                else
                {
                    if (!AppInit.conf.serverproxy.enable)
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
                    decryptLink = new Shared.Models.ProxyLinkModel(reqip, null, null, null);

                string validArgs(string uri)
                {
                    if (!AppInit.conf.accsdb.enable || string.IsNullOrWhiteSpace(account_email))
                        return uri;

                    return uri + (uri.Contains("?") ? "&" : "?") + $"account_email={account_email}";
                }

                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    AllowAutoRedirect = false
                };

                handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                using (var client = new HttpClient(handler))
                {
                    if (decryptLink.proxy != null)
                    {
                        handler.UseProxy = true;
                        handler.Proxy = decryptLink.proxy;
                    }

                    var request = CreateProxyHttpRequest(httpContext, decryptLink.headers, new Uri(servUri), httpContext.Request.Path.Value.Contains(".m3u") || httpContext.Request.Path.Value.Contains(".ts"));
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);

                    if ((int)response.StatusCode is 301 or 302 or 303 || response.Headers.Location != null)
                    {
                        httpContext.Response.Redirect(validArgs($"{AppInit.Host(httpContext)}/proxy/{CORE.ProxyLink.Encrypt(response.Headers.Location.AbsoluteUri, decryptLink)}"));
                        return;
                    }

                    response.Content.Headers.TryGetValues("Content-Type", out var contentType);
                    if (httpContext.Request.Path.Value.Contains(".m3u") || (contentType != null && contentType.First().ToLower() is "application/x-mpegurl" or "application/vnd.apple.mpegurl" or "text/plain"))
                    {
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (response.Content.Headers.ContentLength > 625000)
                                {
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("ContentLength > 5MB", httpContext.RequestAborted);
                                    return;
                                }

                                string proxyhost = $"{AppInit.Host(httpContext)}/proxy";
                                string m3u8 = Regex.Replace(Encoding.UTF8.GetString(await content.ReadAsByteArrayAsync(httpContext.RequestAborted)), "(https?://[^\n\r\"\\# ]+)", m =>
                                {
                                    return validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(m.Groups[1].Value, decryptLink)}");
                                });

                                string hlshost = Regex.Match(servUri, "(https?://[^/]+)/").Groups[1].Value;
                                string hlspatch = Regex.Match(servUri, "(https?://[^\n\r]+/)([^/]+)$").Groups[1].Value;

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
                                    else
                                    {
                                        uri = hlspatch + uri;
                                    }

                                    return m.Groups[1].Value + validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(uri, decryptLink)}");
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
                                    else
                                    {
                                        uri = hlspatch + uri;
                                    }

                                    return m.Groups[1].Value + validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(uri, decryptLink)}");
                                });

                                httpContext.Response.ContentLength = m3u8.Length;
                                httpContext.Response.ContentType = contentType == null ? "application/vnd.apple.mpegurl" : contentType.First();
                                await httpContext.Response.WriteAsync(m3u8, httpContext.RequestAborted);
                            }
                            else
                            {
                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                await httpContext.Response.WriteAsync("error proxy m3u8", httpContext.RequestAborted);
                            }
                        }
                    }
                    else
                    {
                        if (httpContext.Request.Path.Value.Contains(".ts"))
                        {
                            if (response.Content.Headers.ContentLength > 20000000)
                            {
                                httpContext.Response.ContentType = "text/plain";
                                await httpContext.Response.WriteAsync("ContentLength > 20MB", httpContext.RequestAborted);
                                return;
                            }
                        }

                        await CopyProxyHttpResponse(httpContext, response);
                    }
                }
            }
            else
            {
                await _next(httpContext);
            }
        }


        #region CreateProxyHttpRequest
        HttpRequestMessage CreateProxyHttpRequest(HttpContext context, List<(string name, string val)> headers, Uri uri, bool ishls)
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
            if (!ishls)
            {
                foreach (var header in request.Headers)
                {
                    if (header.Key.ToLower() is "origin" or "user-agent" or "referer" or "content-disposition")
                        continue;

                    if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                    {
                        //Console.WriteLine(header.Key + ": " + String.Join(" ", header.Value.ToArray()));
                        requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }
            }

            if (headers != null && headers.Count > 0)
            {
                foreach (var item in headers)
                    requestMessage.Headers.TryAddWithoutValidation(item.name, item.val);
            }

            requestMessage.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36");
            #endregion

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

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
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection")
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
                await CopyToAsyncInternal(response.Body, responseStream, context.RequestAborted);
                //await responseStream.CopyToAsync(response.Body, context.RequestAborted);
            }
        }
        #endregion


        #region CopyToAsyncInternal
        async Task CopyToAsyncInternal(Stream destination, Stream responseStream, CancellationToken cancellationToken)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");

            if (!responseStream.CanRead && !responseStream.CanWrite)
                throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

            if (!destination.CanRead && !destination.CanWrite)
                throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

            if (!responseStream.CanRead)
                throw new NotSupportedException("NotSupported_UnreadableStream");

            if (!destination.CanWrite)
                throw new NotSupportedException("NotSupported_UnwritableStream");


            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

            try
            {
                int bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false)) != 0)
                    await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            //await responseStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        #endregion
    }
}
