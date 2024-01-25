using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.IO;
using IO = System.IO;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Headers;
using TorrServer;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Buffers;

namespace Lampac.Controllers
{
    public class TorrServerController : BaseController
    {
        #region ts.js
        [HttpGet]
        [Route("ts.js")]
        async public Task<ActionResult> Plugin()
        {
            if (!memoryCache.TryGetValue("ApiController:ts.js", out string file))
            {
                file = await IO.File.ReadAllTextAsync("plugins/ts.js");
                memoryCache.Set("ApiController:ts.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file.Replace("{localhost}", Regex.Replace(host, "^https?://", "")), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region Main
        [Route("ts")]
        [Route("ts/static/js/main.{suffix}.chunk.js")]
        async public Task<ActionResult> Main()
        {
            string pathRequest = Regex.Replace(HttpContext.Request.Path.Value, "^/ts", "");
            string servUri = $"http://{AppInit.conf.localhost}:{ModInit.tsport}{pathRequest + HttpContext.Request.QueryString.Value}";

            if (!pathRequest.Contains(".js") && await Start() == false)
                return StatusCode(500);

            string html = await Engine.CORE.HttpClient.Get(servUri, timeoutSeconds: 5, addHeaders: new List<(string name, string val)>() 
            {
                ("Authorization", $"Basic {Engine.CORE.CrypTo.Base64($"ts:{ModInit.tspass}")}"),
            });

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
                    await TorAPI();
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

                    if (AppInit.conf.accsdb.accounts.TryGetValue(login, out DateTime ex) && ex > DateTime.UtcNow && passwd == "ts")
                    {
                        await TorAPI();
                        return;
                    }
                }

                if (HttpContext.Request.Path.Value.StartsWith("/ts/echo"))
                {
                    await HttpContext.Response.WriteAsync("MatriX.API", HttpContext.RequestAborted);
                    return;
                }

                HttpContext.Response.StatusCode = 401;
                HttpContext.Response.Headers.Add("Www-Authenticate", "Basic realm=Authorization Required");
                return;
            }
            else
            {
                await TorAPI();
                return;
            }
        }

        async public Task TorAPI()
        {
            if (await Start() == false)
                return;

            #region settings
            if (HttpContext.Request.Path.Value.StartsWith("/ts/settings"))
            {
                if (HttpContext.Request.Method != "POST")
                {
                    HttpContext.Response.StatusCode = 404;
                    await HttpContext.Response.WriteAsync("404 page not found", HttpContext.RequestAborted);
                    return;
                }

                MemoryStream mem = new MemoryStream();
                await HttpContext.Request.Body.CopyToAsync(mem, HttpContext.RequestAborted);
                string requestJson = Encoding.UTF8.GetString(mem.ToArray());

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Basic {Engine.CORE.CrypTo.Base64($"ts:{ModInit.tspass}")}");

                    if (requestJson.Contains("\"get\""))
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var response = await client.PostAsync($"http://{AppInit.conf.localhost}:{ModInit.tsport}/settings", new StringContent("{\"action\":\"get\"}", Encoding.UTF8, "application/json"), HttpContext.RequestAborted);
                        await response.Content.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted);
                        return;
                    }
                    else if (HttpContext.Connection.RemoteIpAddress.ToString() == "127.0.0.1")
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        await client.PostAsync($"http://{AppInit.conf.localhost}:{ModInit.tsport}/settings", new StringContent(requestJson, Encoding.UTF8, "application/json"), HttpContext.RequestAborted);
                        IO.File.WriteAllText("torrserver/settings.json", requestJson);
                        return;
                    }
                }

                await HttpContext.Response.WriteAsync(string.Empty, HttpContext.RequestAborted);
                return;
            }
            #endregion

            #region Отправляем запрос в torrserver
            string pathRequest = Regex.Replace(HttpContext.Request.Path.Value, "^/ts", "");
            string servUri = $"http://{AppInit.conf.localhost}:{ModInit.tsport}{pathRequest + HttpContext.Request.QueryString.Value}";

            using (var client = new HttpClient())
            {
                var request = CreateProxyHttpRequest(HttpContext, new Uri(servUri));
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);

                await CopyProxyHttpResponse(HttpContext, response);
            }
            #endregion
        }
        #endregion


        #region Start
        async public ValueTask<bool> Start()
        {
            if (ModInit.tsprocess == null /*|| await CheckPort(ModInit.tsport, HttpContext) == false*/)
            {
                #region Запускаем TorrServer
                var thread = new Thread(() =>
                {
                    try
                    {
                        ModInit.tsprocess = new System.Diagnostics.Process();
                        ModInit.tsprocess.StartInfo.UseShellExecute = false;
                        ModInit.tsprocess.StartInfo.RedirectStandardOutput = true;
                        ModInit.tsprocess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                        ModInit.tsprocess.StartInfo.FileName = ModInit.tspath;
                        ModInit.tsprocess.StartInfo.Arguments = $"--httpauth -p {ModInit.tsport} -d {ModInit.homedir}";
                        ModInit.tsprocess.Start();
                        ModInit.tsprocess.WaitForExit();
                    }
                    catch { }

                    ModInit.tsprocess?.Dispose();
                    ModInit.tsprocess = null;
                });

                thread.Start();
                #endregion

                #region Проверяем доступность сервера
                if (await CheckPort(ModInit.tsport, HttpContext) == false)
                {
                    ModInit.tsprocess?.Dispose();
                    ModInit.tsprocess = null;
                    return false;
                }
                #endregion

                #region Обновляем настройки по умолчанию
                try
                {
                    if (IO.File.Exists("torrserver/settings.json"))
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(10);
                            client.DefaultRequestHeaders.Add("Authorization", $"Basic {Engine.CORE.CrypTo.Base64($"ts:{ModInit.tspass}")}");

                            var response = await client.PostAsync($"http://{AppInit.conf.localhost}:{ModInit.tsport}/settings", new StringContent("{\"action\":\"get\"}", Encoding.UTF8, "application/json"));
                            string settingsJson = await response.Content.ReadAsStringAsync();

                            if (!string.IsNullOrWhiteSpace(settingsJson))
                            {
                                string requestJson = IO.File.ReadAllText("torrserver/settings.json");

                                if (requestJson != settingsJson)
                                {
                                    if (!requestJson.Contains("\"action\""))
                                        requestJson = "{\"action\":\"set\",\"sets\":" + Regex.Replace(requestJson, "[\n\r\t ]+", "") + "}";

                                    await client.PostAsync($"http://{AppInit.conf.localhost}:{ModInit.tsport}/settings", new StringContent(requestJson, Encoding.UTF8, "application/json"));
                                }
                            }
                        }
                    }
                }
                catch { }
                #endregion
            }

            if (ModInit.tsprocess == null)
                return false;

            ModInit.clientIps.Add(HttpContext.Connection.RemoteIpAddress.ToString());

            return true;
        }
        #endregion

        #region CheckPort
        async public ValueTask<bool> CheckPort(int port, HttpContext httpContext)
        {
            try
            {
                bool servIsWork = false;

                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(200);

                    try
                    {
                        using (var client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(i == 0 ? 4 : 3);

                            var response = await client.GetAsync($"http://{AppInit.conf.localhost}:{port}/echo", httpContext.RequestAborted);
                            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                string echo = await response.Content.ReadAsStringAsync();
                                if (echo != null && echo.StartsWith("MatriX."))
                                {
                                    servIsWork = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                return servIsWork;
            }
            catch
            {
                return false;
            }
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

            requestMessage.Headers.Add("Authorization", $"Basic {Engine.CORE.CrypTo.Base64($"ts:{ModInit.tspass}")}");
            requestMessage.Headers.Host = string.IsNullOrEmpty(AppInit.conf.listenhost) ? context.Request.Host.Value : AppInit.conf.listenhost;
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
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection" or "content-disposition")
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
                {
                    ModInit.lastActve = DateTime.Now;
                    await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            //byte[] buffer = new byte[81920];
            //int bytesRead;
            //while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            //{
            //    await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            //    ModInit.lastActve = DateTime.Now;
            //}
        }
        #endregion
    }
}
