using Microsoft.JSInterop;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JinEnergy.Engine
{
    public static class JsHttpClient
    {
        #region headers
        static string headers(List<(string name, string val)>? addHeaders)
        {
            string hed = string.Empty;
            if (addHeaders != null && addHeaders.Count > 0)
            {
                foreach (var h in addHeaders)
                    hed += $"'{h.name}':'{h.val}',";
            }

            return "headers:{" + Regex.Replace(hed, ",$", "") + "}";
        }
        #endregion


        #region Get
        public static ValueTask<string?> Get(string url, Encoding? encoding = null, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null)
        {
            return BaseGetAsync(url, encoding: encoding, timeoutSeconds: timeoutSeconds, addHeaders: addHeaders);
        }
        #endregion

        #region Get<T>
        async public static ValueTask<T?> Get<T>(string url, Encoding? encoding = null, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null)
        {
            try
            {
                string? html = await BaseGetAsync(url, encoding: encoding, timeoutSeconds: timeoutSeconds, addHeaders: addHeaders);
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
        async static ValueTask<string?> BaseGetAsync(string url, Encoding? encoding = null, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null)
        {
            try
            {
                if (AppInit.IsAndrod)
                {
                    if (AppInit.JSRuntime == null)
                        return default;

                    return await AppInit.JSRuntime.InvokeAsync<string?>("eval", "httpReq('" + url + "', false, {dataType: 'text', " + headers(addHeaders) + "})");
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    client.MaxResponseContentBufferSize = 4_000_000; // 4MB

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
        public static ValueTask<string?> Post(string url, string data, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null)
        {
            return Post(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), timeoutSeconds: timeoutSeconds, addHeaders: addHeaders);
        }

        async public static ValueTask<string?> Post(string url, HttpContent data, Encoding? encoding = null, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null)
        {
            try
            {
                if (AppInit.IsAndrod)
                {
                    if (AppInit.JSRuntime == null)
                        return default;

                    return await AppInit.JSRuntime.InvokeAsync<string?>("eval", "httpReq('" + url + "','" + data.ReadAsStringAsync().Result + "',{dataType: 'text', "+ headers(addHeaders) + "})");
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    client.MaxResponseContentBufferSize = 4_000_000; // 4MB

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
        public static ValueTask<T?> Post<T>(string url, string data, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null, Encoding? encoding = null)
        {
            return Post<T>(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"), timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, encoding: encoding);
        }

        async public static ValueTask<T?> Post<T>(string url, HttpContent data, int timeoutSeconds = 8, List<(string name, string val)>? addHeaders = null, Encoding? encoding = null)
        {
            try
            {
                string? json = await Post(url, data, timeoutSeconds: timeoutSeconds, addHeaders: addHeaders, encoding: encoding);
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
    }
}
