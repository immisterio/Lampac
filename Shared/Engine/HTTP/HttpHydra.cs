using Shared.Models;
using Shared.Models.Base;
using System.Net;
using System.Text;

namespace Shared.Engine
{
    public class HttpHydra
    {
        BaseSettings init;
        RchClient rch;
        WebProxy proxy;
        List<HeadersModel> basehaders;

        public HttpHydra(BaseSettings init, List<HeadersModel> basehaders, RchClient rch, WebProxy proxy)
        {
            this.init = init;
            this.rch = rch;
            this.proxy = proxy;
            this.basehaders = basehaders;
        }

        #region Get
        public Task<T> Get<T>(string url, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool IgnoreDeserializeObject = false, bool safety = false)
        {
            var headers = JsonHeaders(addheaders, newheaders);

            return IsRchEnable(safety)
                ? rch.Get<T>(init.cors(url), headers, IgnoreDeserializeObject, useDefaultHeaders)
                : Http.Get<T>(init.cors(url), timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }

        public Task<string> Get(string url, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool safety = false)
        {
            var headers = JsonHeaders(addheaders, newheaders);

            return IsRchEnable(safety)
                ? rch.Get(init.cors(url), headers, useDefaultHeaders)
                : Http.Get(init.cors(url), encoding, timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }
        #endregion

        #region GetSpan
        public Task GetSpan(string url, Action<ReadOnlySpan<char>> spanAction, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, bool safety = false)
        {
            var headers = JsonHeaders(addheaders, newheaders);

            return IsRchEnable(safety)
                ? rch.GetSpan(spanAction, init.cors(url), headers, useDefaultHeaders)
                : Http.GetSpan(spanAction, init.cors(url), timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }
        #endregion

        #region Post
        public Task<T> Post<T>(string url, string data, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool IgnoreDeserializeObject = false, bool safety = false)
        {
            var headers = JsonHeaders(addheaders, newheaders);

            return IsRchEnable(safety)
                ? rch.Post<T>(init.cors(url), data, headers, IgnoreDeserializeObject, useDefaultHeaders)
                : Http.Post<T>(init.cors(url), data, encoding: encoding, timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }

        public Task<string> Post(string url, string data, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool safety = false)
        {
            var headers = JsonHeaders(addheaders, newheaders);

            return IsRchEnable(safety)
                ? rch.Post(init.cors(url), data, headers, useDefaultHeaders)
                : Http.Post(init.cors(url), data, encoding: encoding, timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }
        #endregion

        #region PostSpan
        public Task PostSpan(string url, string data, Action<ReadOnlySpan<char>> spanAction, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool safety = false)
        {
            var headers = JsonHeaders(addheaders, newheaders);

            return IsRchEnable(safety)
                ? rch.PostSpan(spanAction, init.cors(url), data, headers, useDefaultHeaders)
                : Http.PostSpan(spanAction, init.cors(url), data, encoding: encoding, timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }
        #endregion


        #region JsonHeaders / IsRchEnable
        List<HeadersModel> JsonHeaders(List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null)
        {
            var headers = HeadersModel.Init(newheaders ?? basehaders);

            if (addheaders != null && addheaders.Count > 0)
                headers = HeadersModel.Join(headers, addheaders);

            return headers;
        }

        bool IsRchEnable(bool safety)
        {
            bool rch_enable = rch != null && rch.enable;
            if (rch_enable)
            {
                if (safety && init.rhub_safety)
                    rch_enable = false;
            }

            return rch_enable;
        }
        #endregion
    }
}
