using DnsClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using NetVips;
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

        static ConcurrentDictionary<string, byte> cacheFiles = new ConcurrentDictionary<string, byte>();

        static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreLocks = new();

        static Timer cleanupTimer;

        static ProxyTmdb()
        {
            if (AppInit.conf.multiaccess == false)
                return;

            Directory.CreateDirectory("cache/tmdb");

            foreach (var item in Directory.EnumerateFiles("cache/tmdb", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), 0);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/tmdb",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
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
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(90));
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

                    var _spredns = _semaphoreLocks.GetOrAdd(dnskey, _ => new SemaphoreSlim(1, 1));

                    try
                    {
                        await _spredns.WaitAsync(TimeSpan.FromMinutes(1));

                        if (!Startup.memoryCache.TryGetValue(dnskey, out string dns_ip))
                        {
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50)))
                            {
                                var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                                var queryType = await lookup.QueryAsync("api.themoviedb.org", QueryType.A, cancellationToken: cts.Token);
                                dns_ip = queryType?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                                if (!string.IsNullOrEmpty(dns_ip))
                                    Startup.memoryCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                                else
                                    Startup.memoryCache.Set(dnskey, string.Empty, DateTime.Now.AddMinutes(5));
                            }
                        }

                        if (!string.IsNullOrEmpty(dns_ip))
                            tmdb_ip = dns_ip;
                    }
                    catch { }
                    finally
                    {
                        try
                        {
                            _spredns.Release();
                        }
                        finally
                        {
                            if (_spredns.CurrentCount == 1)
                                _semaphoreLocks.TryRemove(dnskey, out _);
                        }
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
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(90));

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

                httpContex.Response.ContentType = path.Contains(".png") ? "image/png" : path.Contains(".svg") ? "image/svg+xml" : "image/jpeg";

                if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                {
                    httpContex.Response.Headers["X-Cache-Status"] = "HIT";
                    await httpContex.Response.SendFileAsync(outFile);
                    return;
                }

                string tmdb_ip = init.IMG_IP;

                #region DNS QueryType.A
                if (string.IsNullOrEmpty(tmdb_ip) && string.IsNullOrEmpty(init.IMG_Minor) && !string.IsNullOrEmpty(init.DNS))
                {
                    string dnskey = $"tmdb/img:dns:{init.DNS}";

                    var _spredns = _semaphoreLocks.GetOrAdd(dnskey, _ => new SemaphoreSlim(1, 1));

                    try
                    {
                        await _spredns.WaitAsync(TimeSpan.FromMinutes(1));

                        if (!Startup.memoryCache.TryGetValue(dnskey, out string dns_ip))
                        {
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50)))
                            {
                                var lookup = new LookupClient(IPAddress.Parse(init.DNS));
                                var result = await lookup.QueryAsync("image.tmdb.org", QueryType.A);
                                dns_ip = result?.Answers?.ARecords()?.FirstOrDefault()?.Address?.ToString();

                                if (!string.IsNullOrEmpty(dns_ip))
                                    Startup.memoryCache.Set(dnskey, dns_ip, DateTime.Now.AddMinutes(Math.Max(init.DNS_TTL, 5)));
                                else
                                    Startup.memoryCache.Set(dnskey, string.Empty, DateTime.Now.AddMinutes(5));
                            }
                        }

                        if (!string.IsNullOrEmpty(dns_ip))
                            tmdb_ip = dns_ip;
                    }
                    catch { }
                    finally
                    {
                        try
                        {
                            _spredns.Release();
                        }
                        finally
                        {
                            if (_spredns.CurrentCount == 1)
                                _semaphoreLocks.TryRemove(dnskey, out _);
                        }
                    }
                }
                #endregion

                #region headers
                var headers = new List<HeadersModel>()
                {
                    // используем старый ua что-бы гарантировать image/jpeg вместо image/webp
                    new HeadersModel("Accept", "image/jpeg,image/png,image/*;q=0.8,*/*;q=0.5"),
                    new HeadersModel("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/534.57.2 (KHTML, like Gecko) Version/5.1.7 Safari/534.57.2"),
                    new HeadersModel("Cache-Control", "max-age=0")
                };

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

                bool cacheimg = init.cache_img > 0 && AppInit.conf.mikrotik == false;
                var semaphore = cacheimg ? _semaphoreLocks.GetOrAdd(uri, _ => new SemaphoreSlim(1, 1)) : null;

                try
                {
                    if (semaphore != null)
                        await semaphore.WaitAsync(TimeSpan.FromMinutes(1));

                    if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                    {
                        httpContex.Response.Headers["X-Cache-Status"] = "HIT";
                        await httpContex.Response.SendFileAsync(outFile);
                        return;
                    }

                    var handler = Http.Handler(uri, proxyManager.Get());

                    var client = FrendlyHttp.HttpMessageClient(init.httpversion == 2 ? "http2" : "base", handler);

                    var req = new HttpRequestMessage(HttpMethod.Get, uri)
                    {
                        Version = init.httpversion == 1 ? HttpVersion.Version11 : new Version(init.httpversion, 0)
                    };

                    Http.DefaultRequestHeaders(uri, req, null, null, headers);

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                    {
                        using (HttpResponseMessage response = await client.SendAsync(req, cts.Token))
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                                proxyManager.Success();
                            else
                                proxyManager.Refresh();

                            if (response.StatusCode == HttpStatusCode.OK && cacheimg)
                            {
                                #region cache
                                httpContex.Response.Headers["X-Cache-Status"] = "MISS";

                                int initialCapacity = response.Content.Headers.ContentLength.HasValue ?
                                    (int)response.Content.Headers.ContentLength.Value :
                                    50_000; // 50kB

                                using (var memoryStream = new MemoryStream(initialCapacity))
                                {
                                    try
                                    {
                                        bool saveCache = true;

                                        using (var responseStream = await response.Content.ReadAsStreamAsync())
                                        {
                                            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                                            try
                                            {
                                                int bytesRead;

                                                while ((bytesRead = await responseStream.ReadAsync(buffer, ctsHttp.Token)) > 0)
                                                {
                                                    memoryStream.Write(buffer, 0, bytesRead);
                                                    await httpContex.Response.Body.WriteAsync(buffer, 0, bytesRead, ctsHttp.Token);
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
                                                if (cacheFiles.ContainsKey(md5key) == false || (AppInit.conf.multiaccess == false && File.Exists(outFile) == false))
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

                                                    if (AppInit.conf.multiaccess)
                                                        cacheFiles.TryAdd(md5key, 0);
                                                }
                                            }
                                            catch { File.Delete(outFile); }
                                        }
                                    }
                                    catch { }
                                }
                                #endregion
                            }
                            else
                            {
                                httpContex.Response.StatusCode = (int)response.StatusCode;
                                httpContex.Response.Headers["X-Cache-Status"] = "bypass";
                                await response.Content.CopyToAsync(httpContex.Response.Body, ctsHttp.Token);
                            }
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
                    {
                        try
                        {
                            semaphore.Release();
                        }
                        finally
                        {
                            if (semaphore.CurrentCount == 1)
                                _semaphoreLocks.TryRemove(uri, out _);
                        }
                    }
                }
            }
        }
        #endregion
    }
}
