using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Shared.Engine;
using System.Web;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using DnsClient;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System;
using Shared.Model.Online;
using System.Collections.Generic;
using Shared.Engine.CORE;
using Lampac.Engine.CORE;
using IO = System.IO;
using NetVips;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Lampac.Controllers
{
    public class TmdbController : BaseController
    {
        #region tmdbproxy.js
        [HttpGet]
        [Route("tmdbproxy.js")]
        [Route("tmdbproxy/js/{token}")]
        public ActionResult TmdbProxy(string token)
        {
            string file = FileCache.ReadAllText("plugins/tmdbproxy.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("/tmdb/{*suffix:regex(^https?://.*$)}")]
        [Route("/tmdb/image.tmdb.org/{*suffix}")]
        [Route("/tmdb/api.themoviedb.org/{*suffix}")]
        public Task Auto()
        {
            string path = Regex.Replace(HttpContext.Request.Path.Value, "^/tmdb/https?://", "").Replace("/tmdb/", "");
            string uri = Regex.Match(path, "^[^/]+/(.*)").Groups[1].Value + HttpContext.Request.QueryString.Value;

            if (path.Contains("api.themoviedb.org"))
            {
                HttpContext.Request.Path = $"/tmdb/api/{uri}";
                return API();
            }
            else if (path.Contains("image.tmdb.org"))
            {
                HttpContext.Request.Path = $"/tmdb/img/{uri}";
                return IMG();
            }

            HttpContext.Response.StatusCode = 403;
            return Task.CompletedTask;
        }

        #region API
        [Route("/tmdb/api/{*suffix}")]
        async public Task API()
        {
            HttpContext.Response.ContentType = "application/json; charset=utf-8";

            var init = AppInit.conf.tmdb;
            if (!init.enable && !requestInfo.IsLocalRequest)
            {
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(new { error = true, msg = "disable" }, HttpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            string path = HttpContext.Request.Path.Value.Replace("/tmdb/api", "");
            path = Regex.Replace(path, "/$", "");

            string query = Regex.Replace(HttpContext.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = "https://api.themoviedb.org" + path + query;

            string mkey = $"tmdb/api:{path}:{query}";
            if (hybridCache.TryGetValue(mkey, out JObject cache))
            {
                HttpContext.Response.Headers.Add("X-Cache-Status", "HIT");
                await HttpContext.Response.WriteAsync(JsonConvert.SerializeObject(cache), HttpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            HttpContext.Response.Headers.Add("X-Cache-Status", "MISS");

            string tmdb_ip = init.API_IP;

            #region DNS QueryType.A
            if (string.IsNullOrEmpty(tmdb_ip) && !string.IsNullOrEmpty(init.DNS))
            {
                string dnskey = $"tmdb/api:dns:{init.DNS}";
                if (!hybridCache.TryGetValue(dnskey, out string dns_ip))
                {
                    var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                    var queryType = await lookup.QueryAsync("api.themoviedb.org", QueryType.A);
                    dns_ip = queryType?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                    if (!string.IsNullOrEmpty(dns_ip))
                        hybridCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                }

                if (!string.IsNullOrEmpty(dns_ip))
                    tmdb_ip = dns_ip;
            }
            #endregion

            var headers = new List<HeadersModel>();
            var proxyManager = new ProxyManager("tmdb_api", init);

            if (!string.IsNullOrEmpty(tmdb_ip))
            {
                headers.Add(new HeadersModel("Host", "api.themoviedb.org"));
                uri = uri.Replace("api.themoviedb.org", tmdb_ip);
            }

            var result = await HttpClient.BaseGetAsync<JObject>(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers, statusCodeOK: false);
            if (result.content == null)
            {
                proxyManager.Refresh();
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(new { error = true, msg = "json null" }, HttpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (result.content.ContainsKey("status_message") || result.response.StatusCode != HttpStatusCode.OK)
            {
                proxyManager.Refresh();
                HttpContext.Response.StatusCode = (int)result.response.StatusCode;
                await HttpContext.Response.WriteAsync(JsonConvert.SerializeObject(result.content), HttpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (init.cache_api > 0)
                hybridCache.Set(mkey, result.content, DateTime.Now.AddMinutes(init.cache_api));

            proxyManager.Success();
            await HttpContext.Response.WriteAsync(JsonConvert.SerializeObject(result.content), HttpContext.RequestAborted).ConfigureAwait(false);
        }
        #endregion

        #region IMG
        [Route("/tmdb/img/{*suffix}")]
        async public Task IMG()
        {
            var init = AppInit.conf.tmdb;
            if (!init.enable)
            {
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(new { error = true, msg = "disable" }, HttpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            string path = HttpContext.Request.Path.Value.Replace("/tmdb/img", "");
            string contentType = path.Contains(".png") ? "image/png" : path.Contains(".svg") ? "image/svg+xml" : "image/jpeg";
            string query = Regex.Replace(HttpContext.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = "https://image.tmdb.org" + path + query;

            string md5key = CrypTo.md5($"{path}:{query}");
            string outFile = $"cache/tmdb/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";

            if (IO.File.Exists(outFile))
            {
                HttpContext.Response.ContentType = contentType;
                HttpContext.Response.Headers.Add("X-Cache-Status", "HIT");

                using (var fs = new FileStream(outFile, FileMode.Open, FileAccess.Read))
                    await fs.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted).ConfigureAwait(false);

                return;
            }

            string tmdb_ip = init.IMG_IP;

            #region DNS QueryType.A
            if (string.IsNullOrEmpty(tmdb_ip) && !string.IsNullOrEmpty(init.DNS))
            {
                string dnskey = $"tmdb/img:dns:{init.DNS}";
                if (!hybridCache.TryGetValue(dnskey, out string dns_ip))
                {
                    var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                    var result = await lookup.QueryAsync("image.tmdb.org", QueryType.A);
                    dns_ip = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                    if (!string.IsNullOrEmpty(dns_ip))
                        hybridCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                }

                if (!string.IsNullOrEmpty(dns_ip))
                    tmdb_ip = dns_ip;
            }
            #endregion

            var headers = new List<HeadersModel>();
            var proxyManager = new ProxyManager("tmdb_img", init);

            if (!string.IsNullOrEmpty(tmdb_ip))
            {
                headers.Add(new HeadersModel("Host", "image.tmdb.org"));
                uri = uri.Replace("image.tmdb.org", tmdb_ip);
            }

            if (init.cache_img > 0)
            {
                #region cache
                var array = await HttpClient.Download(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers).ConfigureAwait(false);
                if (array == null || array.Length == 0)
                {
                    proxyManager.Refresh();
                    HttpContext.Response.StatusCode = 502;
                    return;
                }

                #region check_img
                if (init.check_img)
                {
                    using (var image = Image.NewFromBuffer(array))
                    {
                        try
                        {
                            if (!path.Contains(".svg"))
                            {
                                // тестируем jpg/png на целостность
                                byte[] temp = image.JpegsaveBuffer();
                                if (temp == null || temp.Length == 0)
                                {
                                    HttpContext.Response.StatusCode = 502;
                                    return;
                                }
                            }
                        }
                        catch 
                        {
                            HttpContext.Response.StatusCode = 502;
                            return;
                        }
                    }
                }
                #endregion

                proxyManager.Success();
                HttpContext.Response.ContentType = contentType;
                HttpContext.Response.Headers.Add("X-Cache-Status", "MISS");
                await HttpContext.Response.Body.WriteAsync(array, HttpContext.RequestAborted).ConfigureAwait(false);

                try
                {
                    if (!IO.File.Exists(outFile))
                    {
                        Directory.CreateDirectory($"cache/tmdb/{md5key.Substring(0, 2)}");

                        using (var fileStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                            await fileStream.WriteAsync(array, 0, array.Length).ConfigureAwait(false);
                    }
                }
                catch { try { IO.File.Delete(outFile); } catch { } }
                #endregion
            }
            else
            {
                #region bypass
                var handler = HttpClient.Handler(uri, proxyManager.Get());
                handler.AllowAutoRedirect = true;

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : HttpClient.httpClientFactory.CreateClient("base"))
                {
                    HttpClient.DefaultRequestHeaders(client, 10, 0, null, null, headers);

                    if (!handler.UseProxy)
                        client.DefaultRequestHeaders.ConnectionClose = false;

                    using (var response = await client.GetAsync(uri).ConfigureAwait(false))
                    {
                        HttpContext.Response.StatusCode = (int)response.StatusCode;
                        HttpContext.Response.Headers.Add("X-Cache-Status", "bypass");

                        proxyManager.Success();
                        await response.Content.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted).ConfigureAwait(false);
                    }
                }
                #endregion
            }
        }
        #endregion
    }
}