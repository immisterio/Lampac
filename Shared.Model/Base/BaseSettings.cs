using Shared.Model.Online;

namespace Shared.Model.Base
{
    public class BaseSettings : Iproxy, Istreamproxy, Icors, Igroup
    {
        public bool enable { get; set; }

        public int group { get; set; }

        public bool group_hide { get; set; } = true;

        public bool rhub { get; set; }

        public bool rhub_fallback { get; set; }

        public string[]? rhub_geo_disable { get; set; }

        public string[]? geo_hide { get; set; }

        public bool rip { get; set; }

        public int cache_time { get; set; }

        public string? displayname { get; set; }

        public int displayindex { get; set; }

        public string? overridehost { get; set; }

        public string[]? overridehosts { get; set; }

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


        public string? Decrypt(string? data)
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
    }
}
