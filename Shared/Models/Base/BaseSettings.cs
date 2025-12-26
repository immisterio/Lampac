using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Models.Base
{
    public class BaseSettings : Iproxy, Istreamproxy, Icors, Igroup, ICloneable
    {
        bool _enable;

        public bool enable 
        {
            get 
            {
                if (AppInit.conf.defaultOn == "enabled")
                    return enabled;

                return _enable;
            }
            set
            {
                _enable = value;
            }
        }

        public bool enabled { get; set; }

        public bool spider { get; set; } = true;


        public bool kit { get; set; } = true;

        public bool IsKitConf { get; set; }

        public bool IsCloneable { get; set; }

        public string plugin { get; set; }

        public int group { get; set; }

        public bool group_hide { get; set; } = true;

        public bool rhub { get; set; }

        public bool rhub_streamproxy { get; set; }

        public bool rhub_fallback { get; set; }

        public string[] rhub_geo_disable { get; set; }

        public string[] geo_hide { get; set; }

        /// <summary>
        /// Список устройств которым выводить источник не зависимво от rhub
        /// </summary>
        public string client_type { get; set; }

        /// <summary>
        /// Список устройств которым выводить источник при включеном rhub
        /// </summary>
        public string rch_access { get; set; }

        public string RchAccessNotSupport(bool nocheck = false)
        {
            if (string.IsNullOrWhiteSpace(rch_access))
                return null;

            if (nocheck == false)
            {
                // rch выключен
                // разрешен fallback
                // указан webcorshost или включен corseu
                if (!rhub || rhub_fallback || !string.IsNullOrWhiteSpace(webcorshost) || corseu)
                    return null;
            }

            var noAccess = new List<string>(3);

            if (!rch_access.Contains("apk"))
                noAccess.Add("apk");

            if (!rch_access.Contains("cors"))
                noAccess.Add("cors");

            if (!rch_access.Contains("web"))
                noAccess.Add("web");

            return noAccess.Count > 0 ? string.Join(",", noAccess) : null;
        }

        public bool rip { get; set; }

        public int cache_time { get; set; }

        public string displayname { get; set; }

        public int displayindex { get; set; }

        public string overridehost { get; set; }

        public string[] overridehosts { get; set; }

        public string overridepasswd { get; set; }

        public string host { get; set; }

        public string apihost { get; set; }

        public string scheme { get; set; }

        public bool hls { get; set; }

        public string cookie { get; set; }

        public string token { get; set; }

        [JsonProperty("headers",
            ObjectCreationHandling = ObjectCreationHandling.Replace,   // ← заменить, а не дополнять
            NullValueHandling = NullValueHandling.Ignore               // ← не затирать null-ом
        )]
        public Dictionary<string, string> headers { get; set; }

        [JsonProperty("headers_stream",
            ObjectCreationHandling = ObjectCreationHandling.Replace,   // ← заменить, а не дополнять
            NullValueHandling = NullValueHandling.Ignore               // ← не затирать null-ом
        )]
        public Dictionary<string, string> headers_stream { get; set; }

        [JsonProperty("headers_image",
            ObjectCreationHandling = ObjectCreationHandling.Replace,   // ← заменить, а не дополнять
            NullValueHandling = NullValueHandling.Ignore               // ← не затирать null-ом
        )]
        public Dictionary<string, string> headers_image { get; set; }

        public VastConf vast { get; set; }

        public string priorityBrowser { get; set; }

        public int httptimeout { get; set; } = 8;

        public int httpversion { get; set; } = 1;


        #region proxy
        public bool useproxy { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }

        public bool useproxystream { get; set; }

        public bool streamproxy { get; set; }

        public bool streamproxy_preview { get; set; }

        public bool apnstream { get; set; }

        public string[] geostreamproxy { get; set; }

        public ApnConf apn { get; set; }

        public bool qualitys_proxy { get; set; }

        public bool url_reserve { get; set; }

        public string stream_access { get; set; }

        public string StreamAccessNotSupport(bool nocheck = false)
        {
            if (string.IsNullOrWhiteSpace(stream_access))
                return null;

            if (nocheck == false)
            {
                if (AppInit.conf.serverproxy.forced_apn && !string.IsNullOrWhiteSpace(AppInit.conf?.apn?.host))
                    return null;

                if (rhub && !rhub_streamproxy && !rhub_fallback && rhub_geo_disable == null) { }
                else
                {
                    if (streamproxy || apnstream || qualitys_proxy || geostreamproxy != null)
                        return null;
                }
            }

            var noAccess = new List<string>(3);

            if (!stream_access.Contains("apk"))
                noAccess.Add("apk");

            if (!stream_access.Contains("cors"))
                noAccess.Add("cors");

            if (!stream_access.Contains("web"))
                noAccess.Add("web");

            return noAccess.Count > 0 ? string.Join(",", noAccess) : null;
        }
        #endregion

        #region cors
        public bool corseu { get; set; }

        public string webcorshost { get; set; }

        public string corsHost()
        {
            string crhost = !string.IsNullOrWhiteSpace(webcorshost) ? webcorshost : corseu ? AppInit.conf.corsehost : null;
            if (string.IsNullOrWhiteSpace(crhost))
                return host;

            if (crhost.Contains("{encode_uri}") || crhost.Contains("{uri}"))
                return crhost.Replace("{encode_uri}", HttpUtility.UrlEncode(host)).Replace("{uri}", host);

            return $"{crhost}/{host}";
        }

        public string cors(string uri)
        {
            string crhost = !string.IsNullOrWhiteSpace(webcorshost) ? webcorshost : corseu ? AppInit.conf.corsehost : null;
            if (string.IsNullOrWhiteSpace(crhost) || string.IsNullOrWhiteSpace(uri))
                return uri;

            crhost = crhost.Trim();

            if (uri.Contains(Regex.Match(crhost, "https?://([^/]+)", RegexOptions.IgnoreCase).Groups[1].Value))
                return uri;

            if (crhost.Contains("{encode_uri}") || crhost.Contains("{uri}"))
                return crhost.Replace("{encode_uri}", HttpUtility.UrlEncode(uri)).Replace("{uri}", uri);

            return $"{crhost}/{uri}";
        }
        #endregion


        public string Decrypt(string data)
        {
            try
            {
                if (data == null)
                    return data;

                char[] buffer = data.ToCharArray();
                for (int i = 0; i < buffer.Length; i++)
                {
                    char letter = buffer[i];
                    letter = (char)(letter - 3);
                    buffer[i] = letter;
                }

                return new string(buffer);
            }
            catch { return null; }
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
