using Shared.Model.Online;

namespace Shared.Model.Base
{
    public class BaseSettings : Iproxy, Istreamproxy, Icors
    {
        public bool enable { get; set; }

        public bool rhub { get; set; }

        public bool rip { get; set; }

        public int cache_time { get; set; }

        public string? displayname { get; set; }

        public int displayindex { get; set; }

        public string? overridehost { get; set; }

        public string? host { get; set; }

        public string? apihost { get; set; }

        public string? scheme { get; set; }

        public List<HeadersModel>? headers { get; set; }


        #region proxy
        public bool useproxy { get; set; }

        public string? globalnameproxy { get; set; }

        public ProxySettings? proxy { get; set; }

        public bool useproxystream { get; set; }

        public bool streamproxy { get; set; }

        public bool apnstream { get; set; }

        public List<string>? geostreamproxy { get; set; }

        public ApnConf? apn { get; set; }

        public bool qualitys_proxy { get; set; } = true;
        #endregion

        #region cors
        public bool corseu { get; set; }

        public string? webcorshost { get; set; }

        public string corsHost()
        {
            string? crhost = !string.IsNullOrWhiteSpace(webcorshost) ? webcorshost : corseu ? AppInit.corseuhost : null;
            if (string.IsNullOrWhiteSpace(crhost))
                return host;

            return $"{crhost}/{host}";
        }

        public string cors(string uri)
        {
            string? crhost = !string.IsNullOrWhiteSpace(webcorshost) ? webcorshost : corseu ? AppInit.corseuhost : null;
            if (string.IsNullOrWhiteSpace(crhost) || string.IsNullOrWhiteSpace(uri) || uri.Contains(crhost))
                return uri;

            return $"{crhost}/{uri}";
        }
        #endregion
    }
}
