using DnsClient;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using NetVips;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Shared.Model.Online;
using Shared.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyTmdb
    {
        #region ProxyTmdb
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, byte> cacheFiles = new ConcurrentDictionary<string, byte>();

        static ProxyTmdb()
        {
            Directory.CreateDirectory("cache/tmdb");

            foreach (var item in Directory.GetFiles("cache/tmdb", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), 0);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/tmdb",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };
        }

        public ProxyTmdb(RequestDelegate next) { }
        #endregion

        public Task Invoke(HttpContext httpContext)
        {
            var hybridCache = new HybridCache();
            var requestInfo = httpContext.Features.Get<RequestModel>();

            if (httpContext.Request.Path.Value.StartsWith("/tmdb/api/"))
                return API(httpContext, hybridCache, requestInfo);

            if (httpContext.Request.Path.Value.StartsWith("/tmdb/img/"))
                return IMG(httpContext, hybridCache, requestInfo);

            string path = Regex.Replace(httpContext.Request.Path.Value, "^/tmdb/https?://", "").Replace("/tmdb/", "");
            string uri = Regex.Match(path, "^[^/]+/(.*)").Groups[1].Value + httpContext.Request.QueryString.Value;

            if (path.Contains("api.themoviedb.org"))
            {
                httpContext.Request.Path = $"/tmdb/api/{uri}";
                return API(httpContext, hybridCache, requestInfo);
            }
            else if (path.Contains("image.tmdb.org"))
            {
                httpContext.Request.Path = $"/tmdb/img/{uri}";
                return IMG(httpContext, hybridCache, requestInfo);
            }

            httpContext.Response.StatusCode = 403;
            return Task.CompletedTask;
        }


        #region API
        async public Task API(HttpContext httpContex, HybridCache hybridCache, RequestModel requestInfo)
        {
            httpContex.Response.ContentType = "application/json; charset=utf-8";

            var init = AppInit.conf.tmdb;
            if (!init.enable && !requestInfo.IsLocalRequest)
            {
                httpContex.Response.StatusCode = 401;
                await httpContex.Response.WriteAsJsonAsync(new { error = true, msg = "disable" }, httpContex.RequestAborted).ConfigureAwait(false);
                return;
            }

            string path = httpContex.Request.Path.Value.Replace("/tmdb/api", "");
            path = Regex.Replace(path, "/$", "");

            string query = Regex.Replace(httpContex.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = "https://api.themoviedb.org" + path + query;

            string mkey = $"tmdb/api:{path}:{query}";
            if (hybridCache.TryGetValue(mkey, out (string json, int statusCode) cache))
            {
                httpContex.Response.Headers.Add("X-Cache-Status", "HIT");
                httpContex.Response.StatusCode = cache.statusCode;
                httpContex.Response.ContentType = "application/json; charset=utf-8";
                await httpContex.Response.WriteAsync(cache.json, httpContex.RequestAborted).ConfigureAwait(false);
                return;
            }

            httpContex.Response.Headers.Add("X-Cache-Status", "MISS");

            string tmdb_ip = init.API_IP;

            #region DNS QueryType.A
            if (string.IsNullOrEmpty(tmdb_ip) && string.IsNullOrEmpty(init.API_Minor) && !string.IsNullOrEmpty(init.DNS))
            {
                string dnskey = $"tmdb/api:dns:{init.DNS}";
                if (!Startup.memoryCache.TryGetValue(dnskey, out string dns_ip))
                {
                    var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                    var queryType = await lookup.QueryAsync("api.themoviedb.org", QueryType.A);
                    dns_ip = queryType?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                    if (!string.IsNullOrEmpty(dns_ip))
                        Startup.memoryCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                    else
                        Startup.memoryCache.Set(dnskey, string.Empty, DateTime.Now.AddMinutes(5));
                }

                if (!string.IsNullOrEmpty(dns_ip))
                    tmdb_ip = dns_ip;
            }
            #endregion

            var headers = new List<HeadersModel>();
            var proxyManager = new ProxyManager("tmdb_api", init);

            if (!string.IsNullOrEmpty(init.API_Minor))
            {
                uri = uri.Replace("api.themoviedb.org", init.API_Minor);
            }
            else if (!string.IsNullOrEmpty(tmdb_ip))
            {
                headers.Add(new HeadersModel("Host", "api.themoviedb.org"));
                uri = uri.Replace("api.themoviedb.org", tmdb_ip);
            }

            var result = await HttpClient.BaseGetAsync<JObject>(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), httpversion: 2, headers: headers, statusCodeOK: false).ConfigureAwait(false);
            if (result.content == null)
            {
                proxyManager.Refresh();
                httpContex.Response.StatusCode = 401;
                await httpContex.Response.WriteAsJsonAsync(new { error = true, msg = "json null" }, httpContex.RequestAborted).ConfigureAwait(false);
                return;
            }

            cache.statusCode = (int)result.response.StatusCode;
            httpContex.Response.StatusCode = cache.statusCode;

            if (result.content.ContainsKey("status_message") || result.response.StatusCode != HttpStatusCode.OK)
            {
                proxyManager.Refresh();
                cache.json = JsonConvert.SerializeObject(result.content);

                if (init.cache_api > 0 && !string.IsNullOrEmpty(cache.json))
                    hybridCache.Set(mkey, cache, DateTime.Now.AddMinutes(1));

                await httpContex.Response.WriteAsync(cache.json, httpContex.RequestAborted).ConfigureAwait(false);
                return;
            }

            cache.json = JsonConvert.SerializeObject(result.content);

            if (init.cache_api > 0 && !string.IsNullOrEmpty(cache.json))
                hybridCache.Set(mkey, cache, DateTime.Now.AddMinutes(init.cache_api));

            proxyManager.Success();
            httpContex.Response.ContentType = "application/json; charset=utf-8";
            await httpContex.Response.WriteAsync(cache.json, httpContex.RequestAborted).ConfigureAwait(false);
        }
        #endregion

        #region IMG
        async public Task IMG(HttpContext httpContex, HybridCache hybridCache, RequestModel requestInfo)
        {
            var init = AppInit.conf.tmdb;
            if (!init.enable)
            {
                httpContex.Response.StatusCode = 401;
                await httpContex.Response.WriteAsJsonAsync(new { error = true, msg = "disable" }, httpContex.RequestAborted).ConfigureAwait(false);
                return;
            }

            string path = httpContex.Request.Path.Value.Replace("/tmdb/img", "");
            string query = Regex.Replace(httpContex.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = "https://image.tmdb.org" + path + query;

            string md5key = CrypTo.md5($"{path}:{query}");
            string outFile = Path.Combine("cache", "tmdb", md5key);

            httpContex.Response.ContentType = path.Contains(".png") ? "image/png" : path.Contains(".svg") ? "image/svg+xml" : "image/jpeg";

            if (cacheFiles.ContainsKey(md5key))
            {
                httpContex.Response.Headers.Add("X-Cache-Status", "HIT");
                await httpContex.Response.SendFileAsync(outFile).ConfigureAwait(false);
                return;
            }

            string tmdb_ip = init.IMG_IP;

            #region DNS QueryType.A
            if (string.IsNullOrEmpty(tmdb_ip) && string.IsNullOrEmpty(init.IMG_Minor) && !string.IsNullOrEmpty(init.DNS))
            {
                string dnskey = $"tmdb/img:dns:{init.DNS}";
                if (!Startup.memoryCache.TryGetValue(dnskey, out string dns_ip))
                {
                    var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                    var result = await lookup.QueryAsync("image.tmdb.org", QueryType.A);
                    dns_ip = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                    if (!string.IsNullOrEmpty(dns_ip))
                        Startup.memoryCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                    else
                        Startup.memoryCache.Set(dnskey, string.Empty, DateTime.Now.AddMinutes(5));
                }

                if (!string.IsNullOrEmpty(dns_ip))
                    tmdb_ip = dns_ip;
            }
            #endregion

            var headers = new List<HeadersModel>();
            var proxyManager = new ProxyManager("tmdb_img", init);

            if (!string.IsNullOrEmpty(init.IMG_Minor))
            {
                uri = uri.Replace("image.tmdb.org", init.IMG_Minor);
            }
            else if (!string.IsNullOrEmpty(tmdb_ip))
            {
                headers.Add(new HeadersModel("Host", "image.tmdb.org"));
                uri = uri.Replace("image.tmdb.org", tmdb_ip);
            }

            try
            {
                var handler = HttpClient.Handler(uri, proxyManager.Get());
                handler.AllowAutoRedirect = true;

                var client = FrendlyHttp.CreateClient("tmdbroxy:image", handler, "http2", headers?.ToDictionary(), timeoutSeconds: 10, updateClient: uclient =>
                {
                    HttpClient.DefaultRequestHeaders(uclient, 10, 0, null, null, headers);
                });

                using (var response = await client.GetAsync(uri).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                        proxyManager.Success();
                    else
                        proxyManager.Refresh();

                    if (response.StatusCode == HttpStatusCode.OK && init.cache_img > 0 && AppInit.conf.mikrotik == false)
                    {
                        #region cache
                        httpContex.Response.Headers.Add("X-Cache-Status", "MISS");

                        int initialCapacity = response.Content.Headers.ContentLength.HasValue ?
                            (int)response.Content.Headers.ContentLength.Value :
                            50_000; // 50kB

                        using (var memoryStream = new MemoryStream(initialCapacity))
                        {
                            bool saveCache = true;

                            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                                try
                                {
                                    int bytesRead;
                                    Memory<byte> memoryBuffer = buffer.AsMemory();

                                    while ((bytesRead = await responseStream.ReadAsync(memoryBuffer, httpContex.RequestAborted).ConfigureAwait(false)) > 0)
                                    {
                                        memoryStream.Write(memoryBuffer.Slice(0, bytesRead).Span);
                                        await httpContex.Response.Body.WriteAsync(memoryBuffer.Slice(0, bytesRead), httpContex.RequestAborted).ConfigureAwait(false);
                                    }
                                }
                                catch
                                {
                                    saveCache = false;
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(buffer);
                                }
                            }

                            if (saveCache && memoryStream.Length > 1000)
                            {
                                try
                                {
                                    if (!cacheFiles.ContainsKey(md5key))
                                    {
                                        #region check_img
                                        if (init.check_img && !path.Contains(".svg"))
                                        {
                                            using (var image = Image.NewFromBuffer(memoryStream.ToArray()))
                                            {
                                                try
                                                {
                                                    // тестируем jpg/png на целостность
                                                    byte[] temp = image.JpegsaveBuffer();
                                                    if (temp == null || temp.Length == 0)
                                                        return;
                                                }
                                                catch
                                                {
                                                    return;
                                                }
                                            }
                                        }
                                        #endregion

                                        File.WriteAllBytes(outFile, memoryStream.ToArray());
                                    }
                                }
                                catch { try { File.Delete(outFile); } catch { } }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        httpContex.Response.StatusCode = (int)response.StatusCode;
                        httpContex.Response.Headers.Add("X-Cache-Status", "bypass");
                        await response.Content.CopyToAsync(httpContex.Response.Body, httpContex.RequestAborted).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                proxyManager.Refresh();

                if (!string.IsNullOrEmpty(tmdb_ip))
                    httpContex.Response.Redirect(uri.Replace(tmdb_ip, "image.tmdb.org"));
                else
                    httpContex.Response.Redirect(uri);
            }
        }
        #endregion
    }
}
