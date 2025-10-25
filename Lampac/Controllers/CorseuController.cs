using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Base;
using Shared.PlaywrightCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Controllers
{
    public class CorseuController : BaseController
    {
        #region Routes
        [HttpGet]
        [Route("/corseu")]
        async public Task<IActionResult> Get()
        {
            var model = ParseQuery();
            return await ExecuteAsync(model);
        }

        [HttpPost]
        [Route("/corseu")]
        async public Task<IActionResult> Post()
        {
            try
            {
                using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    string body = await reader.ReadToEndAsync().ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(body))
                        return BadRequest("Empty body");

                    var model = JsonConvert.DeserializeObject<CorseuRequest>(body);
                    if (model == null)
                        return BadRequest("Invalid body");

                    return await ExecuteAsync(model);
                }
            }
            catch (JsonException)
            {
                return BadRequest("Invalid JSON");
            }
        }
        #endregion

        #region Execute
        async Task<IActionResult> ExecuteAsync(CorseuRequest model)
        {
            var init = AppInit.conf.сorseu;

            if (init?.tokens == null || init.tokens.Length == 0)
                return StatusCode((int)HttpStatusCode.Forbidden);

            if (string.IsNullOrEmpty(model?.auth_token) || !init.tokens.Contains(model.auth_token))
                return StatusCode((int)HttpStatusCode.Forbidden);

            if (string.IsNullOrWhiteSpace(model?.url))
                return BadRequest("url is empty");

            string method = string.IsNullOrWhiteSpace(model.method) ? "GET" : model.method.ToUpperInvariant();
            string browser = string.IsNullOrWhiteSpace(model.browser) ? "http" : model.browser.ToLowerInvariant();

            var headers = model.headers != null
                ? new Dictionary<string, string>(model.headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool useDefaultHeaders = model.usedefaultHeaders ?? true;
            bool autoRedirect = model.autoredirect ?? true;
            bool headersOnly = model.headersOnly ?? false;
            int timeout = model.timeout.HasValue && model.timeout.Value > 0 ? model.timeout.Value : 20;
            int httpVersion = model.httpversion ?? 1;

            string contentType = null;
            if (headers.TryGetValue("content-type", out string ct))
            {
                contentType = ct;
                headers.Remove("content-type");
            }

            if (headers.ContainsKey("content-length"))
                headers.Remove("content-length");

            switch (browser)
            {
                case "chromium":
                    return await SendWithChromiumAsync(method, model.url, model.data, headers, contentType, timeout, autoRedirect, headersOnly, model.proxy, model.proxy_name);

                default:
                    return await SendWithHttpClientAsync(method, model.url, model.data, headers, contentType, timeout, httpVersion, useDefaultHeaders, autoRedirect, headersOnly, model.proxy, model.proxy_name, model.encoding);
            }
        }
        #endregion

        #region HttpClient
        async Task<IActionResult> SendWithHttpClientAsync(
            string method,
            string url,
            string data,
            Dictionary<string, string> headers,
            string contentType,
            int timeout,
            int httpVersion,
            bool useDefaultHeaders,
            bool autoRedirect,
            bool headersOnly,
            string proxyValue,
            string proxyName,
            string encodingName)
        {
            try
            {
                using (var handler = CreateHandler(url, autoRedirect, proxyValue, proxyName))
                {
                    var client = FrendlyHttp.HttpMessageClient(httpVersion == 2 ? "http2" : "base", handler);

                    using (var request = new HttpRequestMessage(new HttpMethod(method), url))
                    {
                        request.Version = httpVersion == 2 ? HttpVersion.Version20 : HttpVersion.Version11;

                        if (!string.IsNullOrEmpty(data))
                        {
                            var encoding = ResolveEncoding(encodingName);
                            var content = new StringContent(data, encoding);

                            if (!string.IsNullOrEmpty(contentType))
                                content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                            request.Content = content;
                        }

                        var headersModel = headers.Count > 0 ? HeadersModel.Init(headers) : null;
                        string log = string.Empty;
                        Http.DefaultRequestHeaders(url, request, null, null, headersModel, ref log, useDefaultHeaders);

                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted))
                        {
                            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, timeout)));

                            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false))
                            {
                                await CopyResponseAsync(response, headersOnly).ConfigureAwait(false);
                                return new EmptyResult();
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode((int)HttpStatusCode.RequestTimeout);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.BadGateway, ex.Message);
            }
        }

        HttpClientHandler CreateHandler(string url, bool autoRedirect, string proxyValue, string proxyName)
        {
            if (string.IsNullOrWhiteSpace(proxyValue) && string.IsNullOrWhiteSpace(proxyName))
            {
                var handler = Http.Handler(url, null);
                handler.AllowAutoRedirect = autoRedirect;
                return handler;
            }

            var customHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = autoRedirect
            };

            customHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            var proxy = CreateProxy(proxyValue, proxyName);
            if (proxy != null)
            {
                customHandler.UseProxy = true;
                customHandler.Proxy = proxy;
            }
            else
            {
                customHandler.UseProxy = false;
            }

            return customHandler;
        }
        #endregion

        #region Chromium
        async Task<IActionResult> SendWithChromiumAsync(
            string method,
            string url,
            string data,
            Dictionary<string, string> headers,
            string contentType,
            int timeout,
            bool autoRedirect,
            bool headersOnly,
            string proxyValue,
            string proxyName)
        {
            try
            {
                if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                    return StatusCode((int)HttpStatusCode.BadGateway, "PlaywrightStatus disabled");

                var contextHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
                var requestHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);

                var contextOptions = new APIRequestNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    ExtraHTTPHeaders = contextHeaders,
                    Timeout = timeout * 1000
                };

                var requestOptions = new APIRequestContextOptions
                {
                    Method = method,
                    Headers = requestHeaders,
                    Timeout = timeout * 1000
                };

                if (!string.IsNullOrEmpty(contentType))
                {
                    if (!requestHeaders.ContainsKey("content-type"))
                    {
                        requestHeaders["content-type"] = contentType;
                        contextHeaders["content-type"] = contentType;
                    }
                }

                if (!string.IsNullOrEmpty(data))
                    requestOptions.DataString = data;

                if (!autoRedirect)
                    requestOptions.MaxRedirects = 0;

                var proxy = CreateProxy(proxyValue, proxyName);
                if (proxy != null && proxy.Address != null)
                {
                    var credentials = proxy.Credentials as NetworkCredential;
                    contextOptions.Proxy = new Proxy
                    {
                        Server = proxy.Address.ToString(),
                        Username = credentials?.UserName,
                        Password = credentials?.Password,
                        Bypass = proxy.BypassProxyOnLocal ? "127.0.0.1" : null
                    };
                }

                await using (var requestContext = await Chromium.playwright.APIRequest.NewContextAsync(contextOptions).ConfigureAwait(false))
                {
                    var response = await requestContext.FetchAsync(url, requestOptions).ConfigureAwait(false);

                    try
                    {
                        HttpContext.Response.StatusCode = response.Status;

                        foreach (var header in response.HeadersArray)
                        {
                            var headerName = header.Name.ToLowerInvariant();

                            if (ShouldSkipHeader(headerName))
                                continue;

                            if (headerName == "content-type")
                                HttpContext.Response.ContentType = header.Value;

                            HttpContext.Response.Headers[header.Name] = header.Value;
                        }

                        if (headersOnly)
                        {
                            await HttpContext.Response.CompleteAsync().ConfigureAwait(false);
                            return new EmptyResult();
                        }

                        var body = await response.BodyAsync().ConfigureAwait(false);
                        if (body?.Length > 0)
                            await HttpContext.Response.Body.WriteAsync(body, 0, body.Length, HttpContext.RequestAborted).ConfigureAwait(false);

                        await HttpContext.Response.CompleteAsync().ConfigureAwait(false);

                        return new EmptyResult();
                    }
                    finally
                    {
                        await response.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode((int)HttpStatusCode.RequestTimeout);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.BadGateway, ex.Message);
            }
        }
        #endregion

        #region Helpers
        CorseuRequest ParseQuery()
        {
            var query = HttpContext.Request.Query;

            var model = new CorseuRequest
            {
                browser = query.TryGetValue("browser", out var browser) ? browser.ToString() : null,
                url = query.TryGetValue("url", out var url) ? url.ToString() : null,
                method = query.TryGetValue("method", out var method) ? method.ToString() : null,
                data = query.TryGetValue("data", out var data) ? data.ToString() : null,
                httpversion = TryParseInt(query, "httpversion"),
                timeout = TryParseInt(query, "timeout"),
                encoding = query.TryGetValue("encoding", out var enc) ? enc.ToString() : null,
                usedefaultHeaders = TryParseBool(query, "usedefaultHeaders") ?? TryParseBool(query, "useDefaultHeaders"),
                autoredirect = TryParseBool(query, "autoredirect"),
                proxy = query.TryGetValue("proxy", out var proxy) ? proxy.ToString() : null,
                proxy_name = query.TryGetValue("proxy_name", out var proxyName) ? proxyName.ToString() : null,
                headersOnly = TryParseBool(query, "headersOnly") ?? TryParseBool(query, "headers_only"),
                auth_token = query.TryGetValue("auth_token", out var token) ? token.ToString() : null,
                headers = ParseHeaders(query)
            };

            return model;
        }

        Dictionary<string, string> ParseHeaders(IQueryCollection query)
        {
            try
            {
                if (query.TryGetValue("headers", out StringValues headersValue))
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(headersValue.ToString());
            }
            catch { }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        int? TryParseInt(IQueryCollection query, string key)
        {
            if (!query.TryGetValue(key, out var value) || StringValues.IsNullOrEmpty(value))
                return null;

            if (int.TryParse(value.ToString(), out int result))
                return result;

            return null;
        }

        bool? TryParseBool(IQueryCollection query, string key)
        {
            if (!query.TryGetValue(key, out var value) || StringValues.IsNullOrEmpty(value))
                return null;

            var val = value.ToString();
            if (bool.TryParse(val, out bool result))
                return result;

            if (val == "1")
                return true;

            if (val == "0")
                return false;

            return null;
        }

        Encoding ResolveEncoding(string encodingName)
        {
            if (string.IsNullOrEmpty(encodingName))
                return Encoding.UTF8;

            var en = Encoding.GetEncoding(encodingName);
            if (en != null)
                return en;

            return Encoding.UTF8;
        }

        WebProxy CreateProxy(string proxyValue, string proxyName)
        {
            if (!string.IsNullOrWhiteSpace(proxyValue))
                return BuildProxy(proxyValue, null);

            if (!string.IsNullOrWhiteSpace(proxyName) && AppInit.conf.globalproxy != null)
            {
                var settings = AppInit.conf.globalproxy.FirstOrDefault(i => string.Equals(i.name, proxyName, StringComparison.OrdinalIgnoreCase));
                if (settings?.list != null && settings.list.Length > 0)
                {
                    string proxy = settings.list.OrderBy(_ => Guid.NewGuid()).First();
                    return BuildProxy(proxy, settings);
                }
            }

            return null;
        }

        WebProxy BuildProxy(string proxy, ProxySettings settings)
        {
            if (string.IsNullOrWhiteSpace(proxy))
                return null;

            string pattern = settings?.pattern_auth ?? "^(?<sheme>[^/]+//)?(?<username>[^:/]+):(?<password>[^@]+)@(?<host>.*)";
            NetworkCredential credentials = null;
            string address = proxy;

            if (proxy.Contains("@"))
            {
                var match = Regex.Match(proxy, pattern);
                if (match.Success)
                {
                    address = match.Groups["sheme"].Value + match.Groups["host"].Value;
                    credentials = new NetworkCredential(match.Groups["username"].Value, match.Groups["password"].Value);
                }
            }
            else if (settings?.useAuth == true)
            {
                credentials = new NetworkCredential(settings.username, settings.password);
            }

            return new WebProxy(address, settings?.BypassOnLocal ?? false, null, credentials);
        }

        async Task CopyResponseAsync(HttpResponseMessage response, bool headersOnly)
        {
            var httpResponse = HttpContext.Response;
            httpResponse.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
            {
                if (ShouldSkipHeader(header.Key))
                    continue;

                httpResponse.Headers[header.Key] = string.Join(", ", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    httpResponse.ContentType = response.Content.Headers.ContentType?.ToString();
                    continue;
                }

                if (ShouldSkipHeader(header.Key))
                    continue;

                httpResponse.Headers[header.Key] = string.Join(", ", header.Value);
            }

            if (headersOnly)
            {
                await httpResponse.CompleteAsync().ConfigureAwait(false);
                return;
            }

            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                await responseStream.CopyToAsync(httpResponse.Body, HttpContext.RequestAborted).ConfigureAwait(false);
        }

        bool ShouldSkipHeader(string header)
        {
            string key = header.ToLowerInvariant();

            return key switch
            {
                "content-length" => true,
                "transfer-encoding" => true,
                "connection" => true,
                "keep-alive" => true,
                "content-disposition" => true,
                "content-encoding" => true,
                "content-security-policy" => true,
                "vary" => true,
                "alt-svc" => true,
                _ when key.StartsWith("access-control") => true,
                _ when key.StartsWith("x-") => true,
                _ => false
            };
        }
        #endregion
    }
}