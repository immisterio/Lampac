using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Services;
using Shared.Services.Pools;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;

namespace TmdbProxy;

public class TmdbProxyController : BaseController
{
    #region static
    static readonly HttpClient http2ApiClient = FriendlyHttp.CreateHttp2Client();
    static readonly HttpClient http2ImgClient = FriendlyHttp.CreateHttp2Client();

    const string tmdbApiHost = "api.themoviedb.org";
    const string tmdbImgHost = "image.tmdb.org";

    static readonly Version httpVersion = ModInit.conf.httpversion switch
    {
        2 => HttpVersion.Version20,
        3 => HttpVersion.Version30,
        _ => HttpVersion.Version11
    };

    static readonly JsonWriterOptions jsonWriterOptions = new JsonWriterOptions
    {
        Indented = false,
        SkipValidation = true
    };

    static readonly IReadOnlyList<HeadersModel> headersImg = HeadersModel.Init(
        // используем старый ua что-бы гарантировать image/jpeg вместо image/webp
        ("Accept", "image/jpeg,image/png,image/*;q=0.8,*/*;q=0.5"),
        ("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/534.57.2 (KHTML, like Gecko) Version/5.1.7 Safari/534.57.2"),
        ("Cache-Control", "max-age=0")
    );
    #endregion

    #region tmdbproxy.js
    [HttpGet]
    [AllowAnonymous]
    [Route("tmdbproxy.js")]
    [Route("tmdbproxy/js/{token}")]
    public ActionResult TmdbProxy(string token)
    {
        SetHeadersNoCache();

        string plugin = FileCache.ReadAllText($"{ModInit.modpath}/plugin.js", "tmdbproxy.js")
            .Replace("{localhost}", host)
            .Replace("{token}", HttpUtility.UrlEncode(token));

        return Content(plugin, "application/javascript; charset=utf-8");
    }
    #endregion

    #region API
    [HttpGet]
    [Staticache(
        always: true,
        setHeadersNoCache: true,
        skipUids: true,
        queryKeys = [".*"]
    )]
    [Route("tmdb/api/{*suffix}")]
    async public Task TmdbAPI()
    {
        IBufferWriter<byte> bodyWriter = StaticacheOrBodyWriter();
        HttpContext.Response.ContentType = "application/json; charset=utf-8";

        var proxyManager = ModInit.conf.proxyapi?.useproxy == true
            ? new ProxyManager("tmdb_api", ModInit.conf.proxyapi)
            : null;

        ReadOnlySpan<char> path = RequestPath(HttpContext.Request.Path.Value, "/tmdb/api/");

        var result = await Http.BaseGetAsync<JsonObject>(
            RequestUri(tmdbApiHost, path, HttpContext.Request.Query),
            textJson: true,
            timeoutSeconds: 15,
            httpversion: ModInit.conf.httpversion,
            proxy: proxyManager?.Get(),
            statusCodeOK: false,
            httpClient: ModInit.conf.httpversion == 2
                ? http2ApiClient
                : null
        ).ConfigureAwait(false);

        if (result.content == null)
        {
            proxyManager?.Refresh();
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            bodyWriter.Write("{\"error\":true,\"msg\":\"json null\"}"u8);
            return;
        }

        HttpContext.Response.StatusCode = result.content.ContainsKey("status_message")
            ? StatusCodes.Status400BadRequest
            : (int)result.response.StatusCode;

        if (HttpContext.Response.StatusCode == 200 && ModInit.conf.cache_api > 0)
            HttpContext.Features.Set(new StatiCacheEntry(DateTimeOffset.Now.AddMinutes(ModInit.conf.cache_api)));

        using (var writer = new Utf8JsonWriter(bodyWriter, jsonWriterOptions))
            JsonSerializer.Serialize(writer, result.content);
    }
    #endregion

    #region IMG
    [HttpGet]
    [Staticache(always: true)]
    [Route("tmdb/img/{*suffix}")]
    async public Task TmdbIMG()
    {
        IBufferWriter<byte> bodyWriter = StaticacheOrBodyWriter();

        ReadOnlySpan<char> path = RequestPath(HttpContext.Request.Path.Value, "/tmdb/img/");
        string uri = RequestUri(tmdbImgHost, path, HttpContext.Request.Query);

        HttpContext.Response.Headers[HeaderNames.CacheControl] = "public,max-age=86400,immutable";
        HttpContext.Response.ContentType = path.Contains(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : path.Contains(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/jpeg";

        var proxyManager = ModInit.conf.proxyimg?.useproxy == true
                ? new ProxyManager("tmdb_img", ModInit.conf.proxyimg)
                : null;

        var client = FriendlyHttp.MessageClient(
            "proxyimg",
            Http.HandlerOrNull(uri, proxyManager?.Get()),
            out bool disposeHttpClient,
            findNoRedirectClient: false,
            httpClient: ModInit.conf.httpversion == 2
                ? http2ImgClient
                : null
        );

        using (var req = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Version = httpVersion
        })
        {
            foreach (var h in headersImg)
                req.Headers.TryAddWithoutValidation(h.name, h.val);

            try
            {
                using (HttpResponseMessage response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    HttpContext.Response.StatusCode = (int)response.StatusCode;

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        proxyManager?.Success();

                        if (ModInit.conf.cache_img > 0)
                            HttpContext.Features.Set(new StatiCacheEntry(DateTimeOffset.Now.AddMinutes(ModInit.conf.cache_img)));
                    }
                    else
                        proxyManager?.Refresh();

                    if (response.Content.Headers.ContentLength.HasValue)
                        HttpContext.Response.ContentLength = response.Content.Headers.ContentLength.Value;

                    await using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        using (var nbuf = new BufferPool())
                        {
                            int bytesRead;
                            var memBuf = nbuf.Memory;

                            while ((bytesRead = await responseStream.ReadAsync(memBuf).ConfigureAwait(false)) > 0)
                                bodyWriter.Write(memBuf.Span.Slice(0, bytesRead));
                        }
                    }
                }
            }
            finally
            {
                if (disposeHttpClient)
                    client.Dispose();
            }
        }
    }
    #endregion


    #region Helpers
    static ReadOnlySpan<char> RequestPath(string pathString, string route)
    {
        ReadOnlySpan<char> path = pathString.AsSpan();

        if (path.StartsWith(route))
            path = path.Slice(9);

        if (path.StartsWith("//"))
            path = path.Slice(1);

        if (path.EndsWith('/'))
            path = path[..^1];

        return path;
    }

    static string RequestUri(ReadOnlySpan<char> host, ReadOnlySpan<char> path, IQueryCollection query)
    {
        var uri = StringBuilderPool.ThreadInstance;

        uri.Append("https://")
           .Append(host)
           .Append(path);

        var firstArg = true;

        foreach (var q in query)
        {
            if (CoreInit.SkipQueryKeys.Contains(q.Key))
                continue;

            var values = q.Value;
            if (values.Count == 0)
                continue;

            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (string.IsNullOrEmpty(value))
                    continue;

                uri.Append(firstArg ? '?' : '&');
                uri.Append(q.Key).Append('=').Append(value);
                firstArg = false;
            }
        }

        return uri.ToString();
    }
    #endregion
}