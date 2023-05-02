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

namespace Lampac.Controllers
{
    public class TorrServerController : BaseController
    {
        #region ts.js
        [HttpGet]
        [Route("ts.js")]
        async public Task<ActionResult> Plugin()
        {
            if (!ModInit.enable)
                return Content(string.Empty);

            if (!memoryCache.TryGetValue("ApiController:ts.js", out string file))
            {
                file = await IO.File.ReadAllTextAsync("plugins/ts.js");
                memoryCache.Set("ApiController:ts.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file.Replace("{localhost}", Regex.Replace(host, "^https?://", "")), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("ts")]
        [Route("ts/{*suffix}")]
        async public Task Index()
        {
            if (!ModInit.enable || HttpContext.Request.Path.Value.StartsWith("/shutdown"))
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            if (AppInit.conf.accsdb.enable)
            {
                #region Обработка stream потока
                if (HttpContext.Request.Method == "GET" && Regex.IsMatch(HttpContext.Request.Path.Value, "^/ts/(stream|play)"))
                {
                    if (ModInit.clientIps.Contains(HttpContext.Connection.RemoteIpAddress.ToString()))
                    {
                        await TorAPI(HttpContext);
                        return;
                    }
                    else
                    {
                        HttpContext.Response.StatusCode = 404;
                        return;
                    }
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

                    string login = decodedString[0].ToLower();
                    string passwd = decodedString[1];

                    if (AppInit.conf.accsdb.accounts.Contains(login) && passwd == "ts")
                    {
                        await TorAPI(HttpContext);
                        return;
                    }
                }

                if (HttpContext.Request.Path.Value.StartsWith("/ts/echo"))
                {
                    await HttpContext.Response.WriteAsync("MatriX.API");
                    return;
                }

                HttpContext.Response.StatusCode = 401;
                HttpContext.Response.Headers.Add("Www-Authenticate", "Basic realm=Authorization Required");
                return;
            }
            else
            {
                await TorAPI(HttpContext);
                return;
            }
        }


        async public Task TorAPI(HttpContext httpContext)
        {
            if (ModInit.tsprocess == null || await CheckPort(ModInit.tsport, httpContext) == false)
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
                if (await CheckPort(ModInit.tsport, httpContext) == false)
                {
                    ModInit.tsprocess?.Dispose();
                    ModInit.tsprocess = null;
                    return;
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

                            var response = await client.PostAsync($"http://127.0.0.1:{ModInit.tsport}/settings", new StringContent("{\"action\":\"get\"}", Encoding.UTF8, "application/json"));
                            string settingsJson = await response.Content.ReadAsStringAsync();

                            if (!string.IsNullOrWhiteSpace(settingsJson))
                            {
                                string requestJson = IO.File.ReadAllText("torrserver/settings.json");
                                requestJson = Regex.Replace(requestJson, "[\n\r\t ]+", "");

                                if (requestJson != settingsJson)
                                {
                                    requestJson = "{\"action\":\"set\",\"sets\":" + requestJson + "}";
                                    await client.PostAsync($"http://127.0.0.1:{ModInit.tsport}/settings", new StringContent(requestJson, Encoding.UTF8, "application/json"));
                                }
                            }
                        }
                    }
                }
                catch { }
                #endregion
            }

            if (ModInit.tsprocess == null)
                return;

            ModInit.clientIps.Add(httpContext.Connection.RemoteIpAddress.ToString());

            #region settings
            if (httpContext.Request.Path.Value.StartsWith("/settings"))
            {
                if (httpContext.Request.Method != "POST")
                {
                    httpContext.Response.StatusCode = 404;
                    await httpContext.Response.WriteAsync("404 page not found");
                    return;
                }

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    #region Данные запроса
                    MemoryStream mem = new MemoryStream();
                    await httpContext.Request.Body.CopyToAsync(mem);
                    string requestJson = Encoding.UTF8.GetString(mem.ToArray());
                    #endregion

                    var response = await client.PostAsync($"http://127.0.0.1:{ModInit.tsport}/settings", new StringContent("{\"action\":\"get\"}", Encoding.UTF8, "application/json"));
                    string settingsJson = await response.Content.ReadAsStringAsync();

                    if (requestJson.Trim() == "{\"action\":\"get\"}")
                    {
                        await httpContext.Response.WriteAsync(settingsJson);
                        return;
                    }

                    await httpContext.Response.WriteAsync(string.Empty);
                    return;
                }
            }
            #endregion

            #region Отправляем запрос в torrserver
            string pathRequest = Regex.Replace(httpContext.Request.Path.Value, "^/ts", "");
            string servUri = $"http://127.0.0.1:{ModInit.tsport}{pathRequest + httpContext.Request.QueryString.Value}";

            using (var client = new HttpClient())
            {
                var request = CreateProxyHttpRequest(httpContext, new Uri(servUri));
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);

                await CopyProxyHttpResponse(httpContext, response);
            }
            #endregion
        }


        #region CheckPort
        async public ValueTask<bool> CheckPort(int port, HttpContext httpContext)
        {
            try
            {
                bool servIsWork = false;
                DateTime endTimeCheckort = DateTime.Now.AddSeconds(5);

                while (true)
                {
                    if (DateTime.Now > endTimeCheckort)
                        break;

                    await Task.Delay(200);

                    try
                    {
                        using (var client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(2);

                            var response = await client.GetAsync($"http://127.0.0.1:{port}/echo", httpContext.RequestAborted);
                            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                string echo = await response.Content.ReadAsStringAsync();
                                if (echo.StartsWith("MatriX."))
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
            requestMessage.Headers.Host = context.Request.Host.Value;// uri.Authority;
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

            byte[] buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                ModInit.lastActve = DateTime.Now;
            }
        }
        #endregion
    }
}
