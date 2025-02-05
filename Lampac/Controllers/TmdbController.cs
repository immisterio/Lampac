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

        #region API
        [Route("/tmdb/api/{*suffix}")]
        async public Task<ActionResult> API()
        {
            var init = AppInit.conf.tmdb;
            if (!init.enable)
                return Json(new { error = true, msg = "disable"});

            string path = HttpContext.Request.Path.Value.Replace("/tmdb/api", "");
            string query = Regex.Replace(HttpContext.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = "https://api.themoviedb.org" + path + query;

            string mkey = $"tmdb/api:{path}:{query}";
            if (hybridCache.TryGetValue(mkey, out JObject cache))
                return ContentTo(JsonConvert.SerializeObject(cache));

            string tmdb_ip = init.API_IP;

            #region DNS QueryType.A
            if (string.IsNullOrEmpty(tmdb_ip) && !string.IsNullOrEmpty(init.DNS))
            {
                string dnskey = $"tmdb/api:dns:{init.DNS}";
                if (!hybridCache.TryGetValue(dnskey, out string dns_ip))
                {
                    var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                    var result = await lookup.QueryAsync("api.themoviedb.org", QueryType.A);
                    dns_ip = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

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

            JObject json = await HttpClient.Get<JObject>(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers);
            if (json == null)
            {
                proxyManager.Refresh();
                return Json(new { error = true, msg = "json null" });
            }

            if (json.ContainsKey("status_message"))
                return ContentTo(JsonConvert.SerializeObject(json));

            if (init.cache_api > 0)
                hybridCache.Set(mkey, json, DateTime.Now.AddMinutes(init.cache_api));

            proxyManager.Success();
            return ContentTo(JsonConvert.SerializeObject(json));
        }
        #endregion

        #region IMG
        [Route("/tmdb/img/{*suffix}")]
        async public Task<ActionResult> IMG()
        {
            var init = AppInit.conf.tmdb;
            if (!init.enable)
                return Json(new { error = true, msg = "disable" });

            string path = HttpContext.Request.Path.Value.Replace("/tmdb/img", "");
            string contentType = path.Contains(".png") ? "image/png" : path.Contains(".svg") ? "image/svg+xml" : "image/jpeg";
            string query = Regex.Replace(HttpContext.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = "https://image.tmdb.org" + path + query;

            string md5key = CrypTo.md5($"{path}:{query}");
            string outFile = $"cache/tmdb/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";

            if (IO.File.Exists(outFile))
                return File(IO.File.OpenRead(outFile), contentType);

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

            var array = await HttpClient.Download(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers);
            if (array == null || array.Length == 0)
            {
                proxyManager.Refresh();
                return StatusCode(502);
            }

            if (init.cache_img > 0)
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
                                return StatusCode(502);
                        }

                        Directory.CreateDirectory($"cache/tmdb/{md5key.Substring(0, 2)}");
                        await IO.File.WriteAllBytesAsync(outFile, array).ConfigureAwait(false);
                    }
                    catch 
                    {
                        try 
                        { 
                            if (IO.File.Exists(outFile))
                                IO.File.Delete(outFile); 
                        } 
                        catch { } 
                    }
                }
            }

            proxyManager.Success();
            return File(array, contentType);
        }
        #endregion
    }
}