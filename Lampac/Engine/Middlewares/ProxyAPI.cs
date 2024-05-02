using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Buffers;
using Shared.Models;
using Shared.Model.Online;

namespace Lampac.Engine.Middlewares
{
    public class ProxyAPI
    {
        #region ProxyAPI
        private readonly RequestDelegate _next;

        private readonly IHttpClientFactory _httpClientFactory;

        public ProxyAPI(RequestDelegate next, IHttpClientFactory httpClientFactory)
        {
            _next = next;
            _httpClientFactory = httpClientFactory;
        }

        static ProxyAPI()
        {
            Directory.CreateDirectory("cache/hls");
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxy/"))
            {
                #region decryptLink
                ProxyLinkModel decryptLink = null;
                string reqip = httpContext.Connection.RemoteIpAddress.ToString();
                string servUri = httpContext.Request.Path.Value.Replace("/proxy/", "") + httpContext.Request.QueryString.Value;
                string account_email = Regex.Match(httpContext.Request.QueryString.Value, "account_email=([^&]+)").Groups[1].Value;

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
                    decryptLink = new ProxyLinkModel(reqip, null, null, servUri);
                #endregion

                if (AppInit.conf.serverproxy.showOrigUri)
                    httpContext.Response.Headers.Add("PX-Orig", decryptLink.uri);

                #region Кеш файла
                string md5file = httpContext.Request.Path.Value.Replace("/proxy/", "");
                bool ists = md5file.EndsWith(".ts") || md5file.EndsWith(".m4s");

                string md5key = CORE.CrypTo.md5(ists ? fixuri(decryptLink) : decryptLink.uri);
                bool cache_stream = !string.IsNullOrEmpty(md5key) && md5key.Length > 3 && AppInit.conf.serverproxy.encrypt && AppInit.conf.serverproxy.cache.hls;

                string foldercache = cache_stream ? $"cache/hls/{md5key.Substring(0, 3)}" : string.Empty;
                string cachefile = cache_stream ? ($"{foldercache}/{md5key.Substring(3)}" + Path.GetExtension(md5file)) : string.Empty;

                if (cache_stream && File.Exists(cachefile))
                {
                    httpContext.Response.Headers.Add("PX-Cache", "HIT");

                    if (md5file.EndsWith(".m3u8"))
                    {
                        string hls = editm3u(File.ReadAllText(cachefile), httpContext, account_email, decryptLink);

                        httpContext.Response.ContentType = "application/vnd.apple.mpegurl";
                        httpContext.Response.ContentLength = hls.Length;
                        await httpContext.Response.WriteAsync(hls, httpContext.RequestAborted).ConfigureAwait(false);
                    }
                    else
                    {
                        using (var fileStream = new FileStream(cachefile, FileMode.Open, FileAccess.Read))
                        {
                            httpContext.Response.ContentType = ists ? (md5file.EndsWith(".m4s") ? "video/mp4" : "video/mp2t") : "text/plain";
                            httpContext.Response.ContentLength = fileStream.Length;
                            await fileStream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
                        }
                    }

                    return;
                }
                #endregion

                #region handler
                HttpClientHandler handler = new HttpClientHandler()
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
                #endregion

                using (var client = decryptLink.proxy != null ? new HttpClient(handler) : _httpClientFactory.CreateClient("proxy"))
                {
                    var request = CreateProxyHttpRequest(httpContext, decryptLink.headers, new Uri(servUri), httpContext.Request.Path.Value.Contains(".m3u") || httpContext.Request.Path.Value.Contains(".ts"));
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);

                    if ((int)response.StatusCode is 301 or 302 or 303 or 0 || response.Headers.Location != null)
                    {
                        httpContext.Response.Redirect(validArgs($"{AppInit.Host(httpContext)}/proxy/{CORE.ProxyLink.Encrypt(response.Headers.Location.AbsoluteUri, decryptLink)}", account_email));
                        return;
                    }

                    response.Content.Headers.TryGetValues("Content-Type", out var contentType);
                    if (httpContext.Request.Path.Value.Contains(".m3u") || (contentType != null && contentType.First().ToLower() is "application/x-mpegurl" or "application/vnd.apple.mpegurl" or "text/plain"))
                    {
                        #region m3u8/txt
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (response.Content.Headers.ContentLength > 625000)
                                {
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                string m3u8 = Encoding.UTF8.GetString(await content.ReadAsByteArrayAsync(httpContext.RequestAborted));
                                string hls = editm3u(m3u8, httpContext, account_email, decryptLink);

                                if (cache_stream && !File.Exists(cachefile))
                                {
                                    try
                                    {
                                        Directory.CreateDirectory(foldercache);
                                        File.WriteAllText(cachefile, m3u8);
                                    }
                                    catch { try { File.Delete(cachefile); } catch { } }
                                }

                                httpContext.Response.Headers.Add("PX-Cache", cache_stream ? "MISS" : "BYPASS");
                                httpContext.Response.ContentType = contentType == null ? "application/vnd.apple.mpegurl" : contentType.First();
                                httpContext.Response.ContentLength = hls.Length;
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
                    else if (ists && cache_stream)
                    {
                        #region ts
                        using (HttpContent content = response.Content)
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                if (response.Content.Headers.ContentLength > 10_000000)
                                {
                                    httpContext.Response.ContentType = "text/plain";
                                    await httpContext.Response.WriteAsync("bigfile", httpContext.RequestAborted).ConfigureAwait(false);
                                    return;
                                }

                                byte[] buffer = await content.ReadAsByteArrayAsync(httpContext.RequestAborted).ConfigureAwait(false);

                                if (!File.Exists(cachefile))
                                {
                                    _ = Task.Factory.StartNew(() =>
                                    {
                                        try
                                        {
                                            Directory.CreateDirectory(foldercache);
                                            File.WriteAllBytes(cachefile, buffer);
                                        }
                                        catch { try { File.Delete(cachefile); } catch { } }

                                    }, httpContext.RequestAborted, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                                }

                                httpContext.Response.Headers.Add("PX-Cache", "MISS");
                                httpContext.Response.ContentType = md5file.EndsWith(".m4s") ? "video/mp4" : "video/mp2t";
                                httpContext.Response.ContentLength = buffer.Length;
                                await httpContext.Response.Body.WriteAsync(buffer, httpContext.RequestAborted).ConfigureAwait(false);
                            }
                            else
                            {
                                httpContext.Response.StatusCode = (int)response.StatusCode;
                                await httpContext.Response.WriteAsync("error proxy ts", httpContext.RequestAborted);
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        httpContext.Response.Headers.Add("PX-Cache", "BYPASS");
                        await CopyProxyHttpResponse(httpContext, response);
                    }
                }
            }
            else
            {
                await _next(httpContext);
            }
        }


        #region validArgs
        string validArgs(string uri, string account_email)
        {
            if (!AppInit.conf.accsdb.enable || string.IsNullOrWhiteSpace(account_email))
                return uri;

            return uri + (uri.Contains("?") ? "&" : "?") + $"account_email={account_email}";
        }
        #endregion

        #region editm3u
        string editm3u(string _m3u8, HttpContext httpContext, string account_email, ProxyLinkModel decryptLink)
        {
            string proxyhost = $"{AppInit.Host(httpContext)}/proxy";
            string m3u8 = Regex.Replace(_m3u8, "(https?://[^\n\r\"\\# ]+)", m =>
            {
                return validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(m.Groups[1].Value, decryptLink)}", account_email);
            });

            string hlshost = Regex.Match(decryptLink.uri, "(https?://[^/]+)/").Groups[1].Value;
            string hlspatch = Regex.Match(decryptLink.uri, "(https?://[^\n\r]+/)([^/]+)$").Groups[1].Value;

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

                return m.Groups[1].Value + validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(uri, decryptLink)}", account_email);
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

                return m.Groups[1].Value + validArgs($"{proxyhost}/{CORE.ProxyLink.Encrypt(uri, decryptLink)}", account_email);
            });

            return m3u8;
        }
        #endregion

        #region fixuri
        string fixuri(ProxyLinkModel decryptLink)
        {
            string uri = decryptLink.uri;

            if (decryptLink.plugin == "fph" && uri.Contains("media="))
            {
                // https://ip222728867.ahcdn.com/key=Tr8UEBCyKFNoqh9X4exL1A,s=,end=1696892400/data=5.61.39.226/state=ZSROBT0n/reftag=187656082/media=hls4/58/21/4/321917114.mp4/seg-3-v1-a1.ts
                string uts = Regex.Match(uri, "media=([^\\?&]+\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (decryptLink.plugin == "xmr")
            {
                if (uri.Contains("media="))
                {
                    // https://video-lm-b.xhcdn.com/token=nva=1698775200~dirs=5~hash=02580a8e66a351d21a21d/media=hls4/multi=256x144:144p,426x240:240p,854x480:480p,1280x720:720p,1920x1080:1080p/023/904/044/144p.h264.mp4/seg-1-v1-a1.ts
                    string uts = Regex.Match(uri, "media=([^\\?&]+\\.ts)").Groups[1].Value;
                    if (!string.IsNullOrEmpty(uts))
                        return $"{decryptLink.plugin}:{uts}";
                }
                else
                {
                    // https://1-1427-19-18.b.cdn13.com/hls/bsd/4000/sd/4000/023/940/081/1080p.h264.mp4/seg-15-v1-a1.ts?cdn_hash=db6e0bf5c899702777e0852f5a5c8dba&cdn_creation_time=1698762516&cdn_ttl=14400&cdn_cv_data=2a06%3A98c0%3A3600%3A%3A103-dvp
                    uri = Regex.Replace(uri, "^https?://[^/]+", "");
                    uri = Regex.Replace(uri, "\\?.*", "");
                    return $"{decryptLink.plugin}:{uri}";
                }
            }

            if (decryptLink.plugin is "phubprem" or "phub")
            {
                // https://dv-h.phprcdn.com/hls/videos/202301/27/424213491/,1080P_4000K,720P_4000K,480P_2000K,240P_1000K,_424213491.mp4.urlset/seg-5-f2-v1-a1.ts?ttl=1697014995&l=0&ipa=5.61.39.226&hash=e75580dd8920bc61ccbe9c311612771d
                uri = Regex.Replace(uri, "^https?://[^/]+", "");
                uri = Regex.Replace(uri, "\\?.*", "");
                return $"{decryptLink.plugin}:{uri}";
            }

            if ((decryptLink.plugin is "xdsred" or "xds" or "xnx") && uri.Contains(","))
            {
                // https://video-cdn77-premium.xvideos-cdn.com/U9-cvOnM-E9JjyZymkikpg==,1699185782/videos/hls/60/60/ea/6060eaea4f92b9ffdab6120b826199d4/hls-1080p-f138b3.ts
                // https://vid-egc.xvideos-cdn.com/Y7mtJbqRjQbmCC-3LaxkjA==,1698832019/videos/hls/45/8f/f7/458ff790edbf5c0b6265d171286b270f/hls-250p-87b140.ts
                string uts = Regex.Match(uri, ",([0-9]+/videos/[^\\?&]+\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (decryptLink.plugin == "kodik")
            {
                // https://glory.cloud.kodik-storage.com/useruploads/29e5fcf9-a27b-4a32-81c6-5f971e574bf3/adafe955bbed673178a73f76cbb9578f:2023120514/./720.mp4:hls:seg-19-v1-a1.ts
                // https://glory.cloud.kodik-storage.com/useruploads/29e5fcf9-a27b-4a32-81c6-5f971e574bf3/adafe955bbed673178a73f76cbb9578f:2023120514/720.mp4:hls:seg-1-v1-a1.ts
                var g = Regex.Match(uri, "/useruploads/([^/]+)/[^/]+/(.*\\.ts)").Groups;
                if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(g[2].Value))
                    return $"{decryptLink.plugin}:{g[1].Value}:{g[2].Value}";
            }

            if (decryptLink.plugin == "vcdn")
            {
                // https://mystic.cloud.cdnland.in/5fd0c7ccf5c248e2f17632e5ee7ed2a0:2023120611/animetvseries/6cfb3c44adf0705fe1997aa5b44f0872671a169d/720.mp4:hls:seg-1-v1-a1.ts
                string uts = Regex.Match(uri, "https?://[^/]+/[^/]+/(.*\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (decryptLink.plugin == "videodb")
            {
                // https://glory.videokinoplay1.online/animetvseries/f3b391fb97f442eeb760ea1e961d0aff6f2e6190/e88b91a3a4103e1548ebffa4053b75e7:2023120623/1080.mp4:hls:seg-1-v1-a1.ts
                // https://iridium.videokinoplay1.online/movies/71416328961755e50401b511ebc3a5ff0399b013/34b7292ade3dccdaaa9514c7229f158e:2023120714/1080.mp4:hls:seg-1-v1-a1.ts
                var g = Regex.Match(uri, "https?://[^/]+/([^/]+/[^/]+)/[^/]+/(.*\\.ts)").Groups;
                if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(g[2].Value))
                    return $"{decryptLink.plugin}:{g[1].Value}:{g[2].Value}";
            }

            if (decryptLink.plugin is "rezka" or "voidboost")
            {
                // https://broadway.stream.voidboost.cc/ae6235dd062c2bc5e80bec676c9c86ab:2023120807:dmoxckFDWWR2eTJPWFhFcTE0TkZobFprVEtXMnUrdnN5QThmUWoxRFc4TEpZcjZjeXZETXdRNklsdnF5MTdqSDRGRlNud2pnNlpXSjZsdTQybWROUk1RVEc1bUpTSXBIOC8wWkYvMlFVRjQ9/9/6/4/3/9/0/lmrw7.mp4:hls:seg-2-v1-a1.ts
                string uts = Regex.Match(uri, "https?://[^/]+/[^/]+/(.*\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (decryptLink.plugin == "zetflix")
            {
                // https://glory.prosto.hdvideobox.me/f89eb821ed9784dd4e22516075e35cbd:2023120623/animetvseries/28eba2e8ccb33547c20eacdc7d51c5e81ce447be/1080.mp4:hls:seg-1-v1-a1.ts
                string uts = Regex.Match(uri, "https?://[^/]+/[^/]+/(.*\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (decryptLink.plugin == "anilibria")
            {
                // https://cache.libria.fun/videos/media/ts/9261/1/1080/521c1f3960f35ce521b2a41b3ac9d381_00033.ts
                // https://cache-cloud21.libria.fun/videos/media/ts/9261/1/1080/521c1f3960f35ce521b2a41b3ac9d381_00033.ts?expires=1701782505&extra=Lu6IAwHxxH22omYfHVFHhA
                uri = Regex.Replace(uri, "^https?://[^/]+", "");
                uri = Regex.Replace(uri, "\\?.*", "");
                return $"{decryptLink.plugin}:{uri}";
            }

            if (decryptLink.plugin is "ashdi" or "eneyida" or "animebesst" or"animedia" or "animego" or "redheadsound")
            {
                // https://s1.ashdi.vip/content/stream/serials/chainsaw_man_gweanmaslinka/chainsaw_man__01_webdl_1080p_hevc_aac_ukr_dvo_76967/hls/1080/segment73.ts
                // https://kraken.tortuga.wtf/content/stream/films/fall_2022_bdrip_1080p_82657/hls/1080/segment1.ts
                // https://tv.anime1.best/content/vod/serials/wan_jie_du_zun/s01/wan_jie_du_zun__01_tv_1_150/hls/480/segment1084.ts
                // https://hls.animedia.tv/dir220/1601680190ba8319ddd61df944691352b601445e2576d1601d/3_0000.ts
                // https://sophia.yagami-light.com/qv/QVWMBPZgdxD/mEkqvLgk5aeAgwqUyUbvqE3hjxriFY_chunk_6_00005.m4s
                // https://redheadsound.video/storage/2433a07a/hls/stream_0/data329.ts
                uri = Regex.Replace(uri, "^https?://[^/]+", "");
                return $"{decryptLink.plugin}:{uri}";
            }

            if (decryptLink.plugin == "alloha")
            {
                // https://9bc-a3e-2200g0.v.plground.live:10402/hs/48/1702570110/UbO54o740twlR1ghiUWJxQ/67/669067/4/seg-45-f1-v1-sa4-a1.ts
                string uts = Regex.Match(uri, "https?://[^/]+/[^/]+/[^/]+/[^/]+/[^/]+/(.*\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (decryptLink.plugin is "collaps" or "hdvb")
            {
                // https://fazhzcddzec.takedwn.ws/10_12_22/10/12/11/EOAUKCA7/BXWSCUZU.mp4/seg-38-a1.ts?x-cdn=10551403
                // https://cdn4571.vb17123filippaaniketos.pw/vod/3e484719f3134616e92025dd5bae8c30/1080/segment80.ts?md5=qlrvhGkM2s0eTM6wiaee_g&expires=1702558288
                string uts = Regex.Match(uri, "https?://[^/]+/(.*\\.ts)").Groups[1].Value;
                if (!string.IsNullOrEmpty(uts))
                    return $"{decryptLink.plugin}:{uts}";
            }

            if (!string.IsNullOrEmpty(AppInit.conf.serverproxy.cache.hls_pattern) && Regex.IsMatch(uri, AppInit.conf.serverproxy.cache.hls_pattern, RegexOptions.IgnoreCase))
                return $"{decryptLink.plugin}:{Regex.Replace(uri, "^https?://[^/]+", "")}";

            return null;
        }
        #endregion


        #region CreateProxyHttpRequest
        HttpRequestMessage CreateProxyHttpRequest(HttpContext context, List<HeadersModel> headers, Uri uri, bool ishls)
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

            requestMessage.Headers.ConnectionClose = false;
            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

            return requestMessage;
        }
        #endregion

        #region CopyProxyHttpResponse
        async ValueTask CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
        {
            var response = context.Response;
            response.StatusCode = (int)responseMessage.StatusCode;
            response.ContentLength = responseMessage.Content.Headers.ContentLength;

            #region UpdateHeaders
            void UpdateHeaders(HttpHeaders headers)
            {
                foreach (var header in headers)
                {
                    if (header.Key.ToLower() is "transfer-encoding" or "etag" or "connection" or "content-security-policy")
                        continue;

                    if (header.Key.ToLower().StartsWith("x-"))
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


                if (AppInit.conf.serverproxy?.buffering?.enable == true && (context.Request.Path.Value.EndsWith(".mp4") || context.Request.Path.Value.EndsWith(".mkv") || responseMessage.Content.Headers.ContentLength > 10_000000))
                {
                    var bunit = AppInit.conf.serverproxy.buffering;
                    byte[] array = ArrayPool<byte>.Shared.Rent(Math.Max(bunit.rent, 4096));

                    try
                    {
                        bool readFinished = false;
                        var writeFinished = new TaskCompletionSource<bool>();
                        var locker = new AsyncManualResetEvent();

                        Queue<byte[]> byteQueue = new Queue<byte[]>();

                        #region read task
                        _ = Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                int bytesRead;
                                while (!context.RequestAborted.IsCancellationRequested && (bytesRead = await responseStream.ReadAsync(new Memory<byte>(array), context.RequestAborted)) != 0)
                                {
                                    byte[] byteCopy = new byte[bytesRead];
                                    Array.Copy(array, byteCopy, bytesRead);

                                    byteQueue.Enqueue(byteCopy);
                                    locker.Set();

                                    if (context.RequestAborted.IsCancellationRequested)
                                        break;

                                    while (byteQueue.Count > bunit.length && !context.RequestAborted.IsCancellationRequested)
                                        await locker.WaitAsync(Math.Max(bunit.millisecondsTimeout, 1), context.RequestAborted).ConfigureAwait(false);
                                }
                            }
                            finally
                            {
                                readFinished = true;
                                locker.Set();
                            }

                        }, context.RequestAborted, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        #endregion

                        #region write task
                        _ = Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                while (true)
                                {
                                    if (context.RequestAborted.IsCancellationRequested)
                                        break;

                                    if (byteQueue.Count > 0)
                                    {
                                        byte[] bytesToSend = byteQueue.Dequeue();
                                        locker.Set();

                                        await response.Body.WriteAsync(new ReadOnlyMemory<byte>(bytesToSend), context.RequestAborted).ConfigureAwait(false);
                                    }
                                    else if (readFinished)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        await locker.WaitAsync(Math.Max(bunit.millisecondsTimeout, 1), context.RequestAborted).ConfigureAwait(false);
                                    }
                                }
                            }
                            finally
                            {
                                locker.Set();
                                writeFinished.SetResult(true);
                            }

                        }, context.RequestAborted, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        #endregion

                        await writeFinished.Task;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(array);
                    }
                }
                else
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                    try
                    {
                        int bytesRead;
                        while ((bytesRead = await responseStream.ReadAsync(new Memory<byte>(buffer), context.RequestAborted).ConfigureAwait(false)) != 0)
                            await response.Body.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), context.RequestAborted).ConfigureAwait(false);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }
        #endregion
    }
}
