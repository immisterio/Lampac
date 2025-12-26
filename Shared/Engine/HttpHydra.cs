using Newtonsoft.Json;
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
        async public Task<T> Get<T>(string url, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await Get(url, addheaders, newheaders, useDefaultHeaders, statusCodeOK, encoding).ConfigureAwait(false);
                if (json == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } });

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default;
            }
        }

        public Task<string> Get(string url, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default)
        {
            var headers = HeadersModel.Init(newheaders ?? basehaders);

            if (addheaders != null)
                headers = HeadersModel.Join(headers, addheaders);

            return rch.enable
                ? rch.Get(init.cors(url), headers, useDefaultHeaders)
                : Http.Get(init.cors(url), encoding, timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }
        #endregion

        #region Post
        async public Task<T> Post<T>(string url, string data, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default, bool IgnoreDeserializeObject = false)
        {
            try
            {
                string json = await Post(url, data, addheaders, newheaders, useDefaultHeaders, statusCodeOK, encoding).ConfigureAwait(false);
                if (json == null)
                    return default;

                if (IgnoreDeserializeObject)
                    return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } });

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default;
            }
        }

        public Task<string> Post(string url, string data, List<HeadersModel> addheaders = null, List<HeadersModel> newheaders = null, bool useDefaultHeaders = true, bool statusCodeOK = true, Encoding encoding = default)
        {
            var headers = HeadersModel.Init(newheaders ?? basehaders);

            if (addheaders != null)
                headers = HeadersModel.Join(headers, addheaders);

            return rch.enable
                ? rch.Post(init.cors(url), data, headers, useDefaultHeaders)
                : Http.Post(init.cors(url), data, encoding: encoding, timeoutSeconds: init.httptimeout, httpversion: init.httpversion, proxy: proxy, headers: headers, useDefaultHeaders: useDefaultHeaders, statusCodeOK: statusCodeOK);
        }
        #endregion
    }
}
