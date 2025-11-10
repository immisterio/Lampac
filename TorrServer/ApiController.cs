using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using Shared.Models.Base;
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
using System.Web;

namespace TorrServer.Controllers
{
    public class TorrServerController : BaseController
    {
        #region ts.js
        [HttpGet]
        [AllowAnonymous]
        [Route("ts.js")]
        [Route("ts/js/{token}")]
        public ActionResult Plugin(string token)
        {
            string file = FileCache.ReadAllText("plugins/ts.js").Replace("{localhost}", Regex.Replace(host, "^https?://", ""));

            if (!string.IsNullOrEmpty(token))
                file = Regex.Replace(file, "Lampa.Storage.set\\('torrserver_login'[^\n\r]+", $"Lampa.Storage.set('torrserver_login','{HttpUtility.UrlEncode(token)}');");

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion

        #region HttpClient
        private static readonly HttpClient httpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None,
            SslOptions = { RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true },
            MaxConnectionsPerServer = 100
        })
        {
            BaseAddress = new Uri($"http://{AppInit.conf.listen.localhost}:{ModInit.tsport}"),
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue("Basic", CrypTo.Base64($"ts:{ModInit.tspass}")),
            },
            Timeout = TimeSpan.FromSeconds(30)
        };
        #endregion


        #region Main
        [Route("ts")]
        [Route("ts/static/js/{suffix}")]
        async public Task<ActionResult> Main()
        {
            string html = null;
            string pathRequest = Regex.Replace(HttpContext.Request.Path.Value, "^/ts", "");

            try
            {
                var responseMessage = await httpClient.GetAsync(pathRequest + HttpContext.Request.QueryString.Value).ConfigureAwait(false);
                html = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch { }

            if (html == null)
                return StatusCode(500);

            if (pathRequest.Contains(".js"))
            {
                string key = Regex.Match(html, "\\.concat\\(([^,]+),\"/echo\"").Groups[1].Value;
                html = html.Replace($".concat({key},\"/", $".concat({key},\"/ts/");
                return Content(html, "application/javascript; charset=utf-8");
            }
            else
            {
                html = html.Replace("href=\"/", "href=\"/ts/").Replace("src=\"/", "src=\"/ts/");
                html = html.Replace("src=\"./", "src=\"/ts/");
                return Content(html, "text/html; charset=utf-8");
            }
        }
        #endregion

        #region TorAPI
        [Route("ts/{*suffix}")]
        async public Task Index()
        {
            if (HttpContext.Request.Path.Value.StartsWith("/shutdown"))
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            if (AppInit.conf.accsdb.enable)
            {
                #region Обработка stream потока
                if (HttpContext.Request.Method == "GET" && Regex.IsMatch(HttpContext.Request.Path.Value, "^/ts/(stream|play)"))
                {
                    await TorAPI().ConfigureAwait(false);
                    return;

                    //if (ModInit.clientIps.Contains(HttpContext.Connection.RemoteIpAddress.ToString()))
                    //{
                    //    await TorAPI();
                    //    return;
                    //}
                    //else
                    //{
                    //    HttpContext.Response.StatusCode = 404;
                    //    return;
                    //}
                }
                #endregion

                #region Access-Control-Request-Headers
                if (HttpContext.Request.Method == "OPTIONS" && HttpContext.Request.Headers.TryGetValue("Access-Control-Request-Headers", out var AccessControl) && AccessControl == "authorization")
                {
                    HttpContext.Response.StatusCode = 204;
                    return;
                }
                #endregion

                if (HttpContext.Request.Headers.TryGetValue("Authorization", out var Authorization))
                {
                    byte[] data = Convert.FromBase64String(Authorization.ToString().Replace("Basic ", ""));
                    string[] decodedString = Encoding.UTF8.GetString(data).Split(":");

                    string login = decodedString[0].ToLower().Trim();
                    string passwd = decodedString[1];

                    if (AppInit.conf.accsdb.findUser(login) is AccsUser user && !user.ban && user.expires > DateTime.UtcNow && passwd == ModInit.conf.defaultPasswd)
                    {
                        if (ModInit.conf.group > user.group)
                        {
                            await HttpContext.Response.WriteAsync("NoAccessGroup", HttpContext.RequestAborted).ConfigureAwait(false);
                            return;
                        }

                        await TorAPI(user).ConfigureAwait(false);
                        return;
                    }
                }

                if (HttpContext.Request.Path.Value.StartsWith("/ts/echo"))
                {
                    await HttpContext.Response.WriteAsync("MatriX.API", HttpContext.RequestAborted).ConfigureAwait(false);
                    return;
                }

                HttpContext.Response.StatusCode = 401;
                HttpContext.Response.Headers["Www-Authenticate"] = "Basic realm=Authorization Required";
                return;
            }
            else
            {
                await TorAPI().ConfigureAwait(false);
                return;
            }
        }

        async public Task TorAPI(AccsUser user = null)
        {
            string pathRequest = Regex.Replace(HttpContext.Request.Path.Value, "^/ts", "");
            string servUri = $"http://{AppInit.conf.listen.localhost}:{ModInit.tsport}{pathRequest + HttpContext.Request.QueryString.Value}";

            #region settings
            if (pathRequest.StartsWith("/settings"))
            {
                if (HttpContext.Request.Method != "POST")
                {
                    HttpContext.Response.StatusCode = 404;
                    await HttpContext.Response.WriteAsync("404 page not found", HttpContext.RequestAborted).ConfigureAwait(false);
                    return;
                }

                using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    string requestJson = await reader.ReadToEndAsync().ConfigureAwait(false);

                    if (requestJson.Contains("\"get\""))
                    {
                        var rs = await httpClient.PostAsync("/settings", new StringContent("{\"action\":\"get\"}", Encoding.UTF8, "application/json")).ConfigureAwait(false);
                        await rs.Content.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted).ConfigureAwait(false);
                        return;
                    }
                    else if (!ModInit.conf.rdb || requestInfo.IP == "127.0.0.1" || requestInfo.IP.StartsWith("192.168."))
                    {
                        await httpClient.PostAsync("/settings", new StringContent(requestJson, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    }

                    await HttpContext.Response.WriteAsync(string.Empty, HttpContext.RequestAborted).ConfigureAwait(false);
                    return;
                }
            }
            #endregion

            #region playlist
            if (pathRequest.StartsWith("/stream/") && HttpContext.Request.QueryString.Value.Contains("&m3u"))
            {
                string m3u = await httpClient.GetStringAsync(servUri).ConfigureAwait(false);
                HttpContext.Response.ContentType = "audio/x-mpegurl; charset=utf-8";
                await HttpContext.Response.WriteAsync((m3u ?? string.Empty).Replace("/stream/", "/ts/stream/"), HttpContext.RequestAborted).ConfigureAwait(false);
                return;
            }
            #endregion

            #region multiaccess
            if (ModInit.conf.multiaccess == "full" || (ModInit.conf.multiaccess == "auth" && user != null))
            {
                if (HttpContext.Request.Method == "POST" && pathRequest == "/torrents" && user?.group != 666)
                {
                    HttpContext.Request.EnableBuffering();
                    using (var readerBody = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true)) // Оставляем поток открытым
                    {
                        string requestJson = await readerBody.ReadToEndAsync().ConfigureAwait(false);

                        if (requestJson.Contains("\"action\":\"add\"") || requestJson.Contains("\"action\":\"list\""))
                        {
                            try
                            {
                                var rs = await httpClient.PostAsync(pathRequest, new StringContent(requestJson, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                                string json = await rs.Content.ReadAsStringAsync().ConfigureAwait(false);

                                string uid = user?.id ?? user?.ids?.FirstOrDefault();
                                HttpContext.Response.ContentType = "application/json; charset=utf-8";

                                if (requestJson.Contains("\"action\":\"add\""))
                                {
                                    #region add
                                    string hash = Regex.Match(json, "\"hash\":\"([^\"]+)\"").Groups[1].Value;
                                    if (!string.IsNullOrEmpty(hash))
                                    {
                                        var doc = ModInit.whosehash.FindById(hash);

                                        if (doc != null)
                                        {
                                            doc.ip = requestInfo.IP;
                                            doc.uid = uid;
                                            ModInit.whosehash.Update(doc);
                                        }
                                        else
                                        {
                                            ModInit.whosehash.Insert(new WhoseHashModel
                                            {
                                                id = hash,
                                                ip = requestInfo.IP,
                                                uid = uid
                                            });
                                        }
                                    }

                                    await HttpContext.Response.WriteAsync(json, HttpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                    #endregion
                                }
                                else
                                {
                                    #region list
                                    var torrents = JArray.Parse(json);

                                    for (int i = torrents.Count - 1; i >= 0; i--)
                                    {
                                        var hash = torrents[i]["hash"]?.ToString();

                                        if (!string.IsNullOrEmpty(hash))
                                        {
                                            var doc = ModInit.whosehash.FindById(hash);

                                            if (doc != null)
                                            {
                                                if (doc.ip == requestInfo.IP || (doc.uid != null && doc.uid == uid)) { }
                                                else
                                                    torrents.RemoveAt(i);
                                            }
                                        }
                                    }

                                    await HttpContext.Response.WriteAsync(torrents.ToString(), HttpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                    #endregion
                                }
                            }
                            catch { }

                            HttpContext.Response.StatusCode = 500;
                            await HttpContext.Response.WriteAsync(string.Empty, HttpContext.RequestAborted).ConfigureAwait(false);
                            return;
                        }
                    }

                    // Сбрасываем позицию
                    HttpContext.Request.Body.Position = 0;
                }
            }
            #endregion

            var request = CreateProxyHttpRequest(HttpContext, new Uri(servUri));

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            await CopyProxyHttpResponse(HttpContext, response).ConfigureAwait(false);
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

            foreach (var header in request.Headers)
            {
                if (header.Key.ToLower() is "authorization")
                    continue;

                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            requestMessage.Headers.Host = string.IsNullOrEmpty(AppInit.conf.listen.host) ? context.Request.Host.Value : AppInit.conf.listen.host;
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

            #region UpdateHeaders
            void UpdateHeaders(HttpHeaders headers)
            {
                foreach (var header in headers)
                {
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection" or "content-security-policy" or "content-disposition")
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

            var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.Body == null)
                throw new ArgumentNullException("destination");

            if (!responseStream.CanRead && !responseStream.CanWrite)
                throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

            if (!response.Body.CanRead && !response.Body.CanWrite)
                throw new ObjectDisposedException("ObjectDisposed_StreamClosed");

            if (!responseStream.CanRead || !response.Body.CanWrite)
                throw new NotSupportedException("NotSupported_UnreadableStream");

            int rent = responseMessage.Content?.Headers?.ContentLength > 100000000 ? 81920 : 4096;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(rent);

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
        }
        #endregion
    }
}
