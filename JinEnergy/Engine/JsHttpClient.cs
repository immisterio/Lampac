﻿using Microsoft.JSInterop;
using Shared.Model.Online;
using System.Net;
using System.Text;
using System.Text.Json;

namespace JinEnergy.Engine
{
    public static class JsHttpClient
    {
        #region httpReqHeaders
        public static Dictionary<string, string> httpReqHeaders(List<HeadersModel>? addHeaders, bool useDefaultHeaders = true)
        {
            var hed = new Dictionary<string, string>();

            if (addHeaders != null && addHeaders.Count > 0)
            {
                foreach (var h in addHeaders)
                    hed.TryAdd(h.name, h.val);
            }

            if (useDefaultHeaders && !hed.ContainsKey("User-Agent"))
                hed.TryAdd("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

            return hed;
        }
        #endregion


        #region Get
        public static ValueTask<string?> Get(string url, List<HeadersModel>? headers, int timeoutSeconds = 8, bool useDefaultHeaders = true)
        {
            return BaseGetAsync(url, encoding: default, timeoutSeconds: timeoutSeconds, addHeaders: headers, useDefaultHeaders: useDefaultHeaders);
        }

        public static ValueTask<string?> Get(string url, Encoding? encoding = null, int timeoutSeconds = 8, List<HeadersModel>? addHeaders = null, bool androidHttpReq = true, bool useDefaultHeaders = true)
        {
            return BaseGetAsync(url, encoding: encoding, timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, androidHttpReq: androidHttpReq, useDefaultHeaders: useDefaultHeaders);
        }
        #endregion

        #region Get<T>
        async public static ValueTask<T?> Get<T>(string url, List<HeadersModel>? addHeaders = null, int timeoutSeconds = 8, Encoding? encoding = null, bool androidHttpReq = true, bool useDefaultHeaders = true)
        {
            try
            {
                string? html = await BaseGetAsync(url, encoding: encoding, timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, androidHttpReq: androidHttpReq, useDefaultHeaders: useDefaultHeaders);
                if (html == null)
                    return default;

                return JsonSerializer.Deserialize<T>(html);
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region BaseGetAsync
        async static ValueTask<string?> BaseGetAsync(string url, Encoding? encoding = null, int timeoutSeconds = 8, List<HeadersModel>? addHeaders = null, bool androidHttpReq = true, bool useDefaultHeaders = true)
        {
            try
            {
                if (androidHttpReq && AppInit.IsAndrod && AppInit.JSRuntime != null)
                    return await AppInit.JSRuntime.InvokeAsync<string?>("httpReq", url, false, new { dataType = "text", timeout = timeoutSeconds * 1000, headers = httpReqHeaders(addHeaders, useDefaultHeaders) });

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    client.MaxResponseContentBufferSize = 3_000_000; // 3MB

                    if (addHeaders != null)
                    {
                        foreach (var item in addHeaders)
                        {
                            if (!string.IsNullOrEmpty(item.val))
                                client.DefaultRequestHeaders.Add(item.name, item.val);
                        }
                    }

                    using (HttpResponseMessage response = await client.GetAsync(url))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            return null;

                        using (HttpContent content = response.Content)
                        {
                            if (encoding != default)
                            {
                                string res = encoding.GetString(await content.ReadAsByteArrayAsync());
                                if (string.IsNullOrWhiteSpace(res))
                                    return null;

                                return res;
                            }
                            else
                            {
                                string res = await content.ReadAsStringAsync();
                                if (string.IsNullOrWhiteSpace(res))
                                    return null;

                                return res;
                            }
                        }
                    }
                }
            }
            catch
            {
                return default;
            }
        }
        #endregion


        #region Post
        public static ValueTask<string?> Post(string url, string data, List<HeadersModel>? headers, int timeoutSeconds = 8, bool useDefaultHeaders = true, bool androidHttpReq = true)
        {
            return Post(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), timeoutSeconds: timeoutSeconds, addHeaders: headers, useDefaultHeaders: useDefaultHeaders, androidHttpReq: androidHttpReq);
        }

        public static ValueTask<string?> Post(string url, string data, int timeoutSeconds = 8, List<HeadersModel>? addHeaders = null, bool useDefaultHeaders = true, bool androidHttpReq = true)
        {
            return Post(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, useDefaultHeaders: useDefaultHeaders, androidHttpReq: androidHttpReq);
        }

        async public static ValueTask<string?> Post(string url, HttpContent data, Encoding? encoding = null, int timeoutSeconds = 8, List<HeadersModel>? addHeaders = null, bool useDefaultHeaders = true, bool androidHttpReq = true)
        {
            try
            {
                if (androidHttpReq && AppInit.IsAndrod && AppInit.JSRuntime != null)
                    return await AppInit.JSRuntime.InvokeAsync<string?>("httpReq", url, data.ReadAsStringAsync().Result, new { dataType = "text", timeout = timeoutSeconds * 1000, headers = httpReqHeaders(addHeaders, useDefaultHeaders) });

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    client.MaxResponseContentBufferSize = 3_000_000; // 3MB

                    if (addHeaders != null)
                    {
                        foreach (var item in addHeaders)
                        {
                            if (!string.IsNullOrEmpty(item.val))
                                client.DefaultRequestHeaders.Add(item.name, item.val);
                        }
                    }

                    using (HttpResponseMessage response = await client.PostAsync(url, data))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            return null;

                        using (HttpContent content = response.Content)
                        {
                            if (encoding != default)
                            {
                                string res = encoding.GetString(await content.ReadAsByteArrayAsync());
                                if (string.IsNullOrWhiteSpace(res))
                                    return null;

                                return res;
                            }
                            else
                            {
                                string res = await content.ReadAsStringAsync();
                                if (string.IsNullOrWhiteSpace(res))
                                    return null;

                                return res;
                            }
                        }
                    }
                }
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region Post<T>
        public static ValueTask<T?> Post<T>(string url, string data, int timeoutSeconds = 8, List<HeadersModel>? addHeaders = null, Encoding? encoding = null, bool useDefaultHeaders = true)
        {
            return Post<T>(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, encoding: encoding, useDefaultHeaders: useDefaultHeaders);
        }

        async public static ValueTask<T?> Post<T>(string url, HttpContent data, int timeoutSeconds = 8, List<HeadersModel>? addHeaders = null, Encoding? encoding = null, bool useDefaultHeaders = true)
        {
            try
            {
                string? json = await Post(url, data, timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, encoding: encoding, useDefaultHeaders: useDefaultHeaders);
                if (json == null)
                    return default;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }
        #endregion


        #region StatusCode
        async public static ValueTask<int> StatusCode(string? url, int timeout = 2)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                    return -1;

                using (var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }))
                {
                    client.Timeout = TimeSpan.FromSeconds(timeout);
                    client.MaxResponseContentBufferSize = 1_000_000;

                    using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                        return (int)response.StatusCode;
                }
            }
            catch
            {
                return -1;
            }
        }
        #endregion
    }
}
