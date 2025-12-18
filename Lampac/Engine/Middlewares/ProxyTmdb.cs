using DnsClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using Shared.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyTmdb
    {
        #region ProxyTmdb
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, int> cacheFiles = new ();

        static Timer cleanupTimer;

        static ProxyTmdb()
        {
            if (AppInit.conf.multiaccess == false)
                return;

            Directory.CreateDirectory("cache/tmdb");

            foreach (var item in Directory.EnumerateFiles("cache/tmdb", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), (int)new FileInfo(item).Length);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/tmdb",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            //fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };

            cleanupTimer = new Timer(cleanup, null, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));
        }

        static void cleanup(object state)
        {
            try
            {
                var files = Directory.GetFiles("cache/tmdb", "*").Select(f => Path.GetFileName(f)).ToHashSet();

                foreach (string md5fileName in cacheFiles.Keys.ToArray())
                {
                    if (!files.Contains(md5fileName))
                        cacheFiles.TryRemove(md5fileName, out _);
                }
            }
            catch { }
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
                return IMG(httpContext, requestInfo);

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
                return IMG(httpContext, requestInfo);
            }

            httpContext.Response.StatusCode = 403;
            return Task.CompletedTask;
        }


        #region API
        async public Task API(HttpContext httpContex, HybridCache hybridCache, RequestModel requestInfo)
        {
            using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(httpContex.RequestAborted))
            {
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(30));
                httpContex.Response.ContentType = "application/json; charset=utf-8";

                var init = AppInit.conf.tmdb;
                if (!init.enable && !requestInfo.IsLocalRequest)
                {
                    httpContex.Response.StatusCode = 401;
                    await httpContex.Response.WriteAsJsonAsync(new { error = true, msg = "disable" }, ctsHttp.Token);
                    return;
                }

                string path = httpContex.Request.Path.Value.Replace("/tmdb/api", "");
                path = Regex.Replace(path, "^/https?://api.themoviedb.org", "");
                path = Regex.Replace(path, "/$", "");

                string query = Regex.Replace(httpContex.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
                string uri = "https://api.themoviedb.org" + path + query;

                string mkey = $"tmdb/api:{path}:{query}";

                if (hybridCache.TryGetValue(mkey, out (string json, int statusCode) cache, inmemory: false))
                {
                    httpContex.Response.Headers["X-Cache-Status"] = "HIT";
                    httpContex.Response.StatusCode = cache.statusCode;
                    httpContex.Response.ContentType = "application/json; charset=utf-8";
                    await httpContex.Response.WriteAsync(cache.json, ctsHttp.Token);
                    return;
                }

                httpContex.Response.Headers["X-Cache-Status"] = "MISS";

                string tmdb_ip = init.API_IP;

                #region DNS QueryType.A
                if (string.IsNullOrEmpty(tmdb_ip) && string.IsNullOrEmpty(init.API_Minor) && !string.IsNullOrEmpty(init.DNS))
                {
                    string dnskey = $"tmdb/api:dns:{init.DNS}";

                    var _spredns = new SemaphorManager(dnskey, ctsHttp.Token);

                    try
                    {
                        await _spredns.WaitAsync();

                        if (!Startup.memoryCache.TryGetValue(dnskey, out string dns_ip))
                        {
                            var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                            var queryType = await lookup.QueryAsync("api.themoviedb.org", QueryType.A, cancellationToken: ctsHttp.Token);
                            dns_ip = queryType?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                            if (!string.IsNullOrEmpty(dns_ip))
                                Startup.memoryCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                            else
                                Startup.memoryCache.Set(dnskey, string.Empty, DateTime.Now.AddMinutes(5));
                        }

                        if (!string.IsNullOrEmpty(dns_ip))
                            tmdb_ip = dns_ip;
                    }
                    catch { }
                    finally
                    {
                        _spredns.Release();
                    }
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

                var result = await Http.BaseGetAsync<JObject>(uri, timeoutSeconds: 20, proxy: proxyManager.Get(), httpversion: init.httpversion, headers: headers, statusCodeOK: false);
                if (result.content == null)
                {
                    proxyManager.Refresh();
                    httpContex.Response.StatusCode = 401;
                    await httpContex.Response.WriteAsJsonAsync(new { error = true, msg = "json null" }, ctsHttp.Token);
                    return;
                }

                cache.statusCode = (int)result.response.StatusCode;
                httpContex.Response.StatusCode = cache.statusCode;

                if (result.content.ContainsKey("status_message") || result.response.StatusCode != HttpStatusCode.OK)
                {
                    proxyManager.Refresh();
                    cache.json = JsonConvert.SerializeObject(result.content);

                    if (init.cache_api > 0 && !string.IsNullOrEmpty(cache.json))
                        hybridCache.Set(mkey, cache, DateTime.Now.AddMinutes(1), inmemory: true);

                    await httpContex.Response.WriteAsync(cache.json, ctsHttp.Token);
                    return;
                }

                cache.json = JsonConvert.SerializeObject(result.content);

                if (init.cache_api > 0 && !string.IsNullOrEmpty(cache.json))
                    hybridCache.Set(mkey, cache, DateTime.Now.AddMinutes(init.cache_api), inmemory: false);

                proxyManager.Success();
                httpContex.Response.ContentType = "application/json; charset=utf-8";
                await httpContex.Response.WriteAsync(cache.json, ctsHttp.Token);
            }
        }
        #endregion

        #region IMG
        async public Task IMG(HttpContext httpContex, RequestModel requestInfo)
        {
            using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(httpContex.RequestAborted))
            {
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(30));

                var init = AppInit.conf.tmdb;
                if (!init.enable)
                {
                    httpContex.Response.StatusCode = 401;
                    await httpContex.Response.WriteAsJsonAsync(new { error = true, msg = "disable" }, ctsHttp.Token);
                    return;
                }

                string path = httpContex.Request.Path.Value.Replace("/tmdb/img", "");
                path = Regex.Replace(path, "^/https?://image.tmdb.org", "");

                string query = Regex.Replace(httpContex.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
                string uri = "https://image.tmdb.org" + path + query;

                string md5key = CrypTo.md5($"{path}:{query}");
                string outFile = Path.Combine("cache", "tmdb", md5key);

                bool cacheimg = init.cache_img > 0 && AppInit.conf.mikrotik == false;

                httpContex.Response.ContentType = path.Contains(".png") ? "image/png" : path.Contains(".svg") ? "image/svg+xml" : "image/jpeg";

                #region cacheFiles
                if (cacheimg)
                {
                    if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                    {
                        httpContex.Response.Headers["X-Cache-Status"] = "HIT";

                        if (init.responseContentLength && cacheFiles.ContainsKey(md5key))
                            httpContex.Response.ContentLength = cacheFiles[md5key];

                        await httpContex.Response.SendFileAsync(outFile, ctsHttp.Token).ConfigureAwait(false);
                        return;
                    }
                }
                #endregion

                string tmdb_ip = init.IMG_IP;

                #region DNS QueryType.A
                if (string.IsNullOrEmpty(tmdb_ip) && string.IsNullOrEmpty(init.IMG_Minor) && !string.IsNullOrEmpty(init.DNS))
                {
                    string dnskey = $"tmdb/img:dns:{init.DNS}";

                    var _spredns = new SemaphorManager(dnskey, ctsHttp.Token);

                    try
                    {
                        await _spredns.WaitAsync();

                        if (!Startup.memoryCache.TryGetValue(dnskey, out string dns_ip))
                        {
                            var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                            var result = await lookup.QueryAsync("image.tmdb.org", QueryType.A, cancellationToken: ctsHttp.Token);
                            dns_ip = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                            if (!string.IsNullOrEmpty(dns_ip))
                                Startup.memoryCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                            else
                                Startup.memoryCache.Set(dnskey, string.Empty, DateTime.Now.AddMinutes(5));
                        }

                        if (!string.IsNullOrEmpty(dns_ip))
                            tmdb_ip = dns_ip;
                    }
                    catch { }
                    finally
                    {
                        _spredns.Release();
                    }
                }
                #endregion

                #region headers
                var headers = HeadersModel.Init(
                    // используем старый ua что-бы гарантировать image/jpeg вместо image/webp
                    ("Accept", "image/jpeg,image/png,image/*;q=0.8,*/*;q=0.5"),
                    ("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/534.57.2 (KHTML, like Gecko) Version/5.1.7 Safari/534.57.2"),
                    ("Cache-Control", "max-age=0")
                );

                if (!string.IsNullOrEmpty(init.IMG_Minor))
                {
                    uri = uri.Replace("image.tmdb.org", init.IMG_Minor);
                }
                else if (!string.IsNullOrEmpty(tmdb_ip))
                {
                    headers.Add(new HeadersModel("Host", "image.tmdb.org"));
                    uri = uri.Replace("image.tmdb.org", tmdb_ip);
                }
                #endregion

                var proxyManager = new ProxyManager("tmdb_img", init);

                var semaphore = cacheimg ? new SemaphorManager(outFile, ctsHttp.Token) : null;

                try
                {
                    if (semaphore != null)
                        await semaphore.WaitAsync().ConfigureAwait(false);

                    #region cacheFiles
                    if (cacheimg)
                    {
                        if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                        {
                            httpContex.Response.Headers["X-Cache-Status"] = "HIT";

                            if (init.responseContentLength && cacheFiles.ContainsKey(md5key))
                                httpContex.Response.ContentLength = cacheFiles[md5key];

                            await httpContex.Response.SendFileAsync(outFile, ctsHttp.Token).ConfigureAwait(false);
                            return;
                        }
                    }
                    #endregion

                    var handler = Http.Handler(uri, proxyManager.Get());

                    var client = FrendlyHttp.HttpMessageClient(init.httpversion == 2 ? "http2proxyimg" : "proxyimg", handler);

                    var req = new HttpRequestMessage(HttpMethod.Get, uri)
                    {
                        Version = init.httpversion == 1 ? HttpVersion.Version11 : new Version(init.httpversion, 0)
                    };

                    foreach (var h in headers)
                    {
                        if (!req.Headers.TryAddWithoutValidation(h.name, h.val))
                        {
                            if (req.Content?.Headers != null)
                                req.Content.Headers.TryAddWithoutValidation(h.name, h.val);
                        }
                    }

                    using (HttpResponseMessage response = await client.SendAsync(req, ctsHttp.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                            proxyManager.Success();
                        else
                            proxyManager.Refresh();

                        httpContex.Response.StatusCode = (int)response.StatusCode;

                        if (init.responseContentLength && response.Content?.Headers?.ContentLength > 0)
                            httpContex.Response.ContentLength = response.Content.Headers.ContentLength.Value;

                        if (response.StatusCode == HttpStatusCode.OK && cacheimg)
                        {
                            #region cache
                            httpContex.Response.Headers["X-Cache-Status"] = "MISS";

                            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                            try
                            {
                                int cacheLength = 0;

                                int bufferSize = response.Content.Headers.ContentLength.HasValue
                                    ? (int)response.Content.Headers.ContentLength.Value
                                    : 50_000; // 50kB

                                using (var cacheStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
                                {
                                    using (var responseStream = await response.Content.ReadAsStreamAsync(ctsHttp.Token).ConfigureAwait(false))
                                    {
                                        int bytesRead;

                                        while ((bytesRead = await responseStream.ReadAsync(buffer, ctsHttp.Token).ConfigureAwait(false)) > 0)
                                        {
                                            cacheLength += bytesRead;
                                            await cacheStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                                            await httpContex.Response.Body.WriteAsync(buffer, 0, bytesRead, ctsHttp.Token).ConfigureAwait(false);
                                        }
                                    }
                                }

                                if (!response.Content.Headers.ContentLength.HasValue || response.Content.Headers.ContentLength.Value == cacheLength)
                                {
                                    if (AppInit.conf.multiaccess)
                                        cacheFiles[md5key] = cacheLength;
                                }
                                else
                                {
                                    File.Delete(outFile);
                                }
                            }
                            catch
                            {
                                File.Delete(outFile);
                                throw;
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                            #endregion
                        }
                        else
                        {
                            httpContex.Response.Headers["X-Cache-Status"] = "bypass";
                            await response.Content.CopyToAsync(httpContex.Response.Body, ctsHttp.Token).ConfigureAwait(false);
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
                finally
                {
                    if (semaphore != null)
                        semaphore.Release();
                }
            }
        }
        #endregion
    }
}
