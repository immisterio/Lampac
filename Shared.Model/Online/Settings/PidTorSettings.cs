using Shared.Model.Base;

namespace Shared.Model.Online.Settings
{
    public class PidTorSettings : Igroup
    {
        bool _enable;

        public bool enable
        {
            get
            {
                if (AppInit._defaultOn == "enabled")
                    return enabled;

                return _enable;
            }
            set
            {
                _enable = value;
            }
        }

        public bool enabled { get; set; }


        public string? displayname { get; set; }

        public int displayindex { get; set; }

        public string? redapi { get; set; }

        public string? apikey { get; set; }

        public int min_sid { get; set; }

        public long max_size { get; set; }

        public long max_serial_size { get; set; }

        public string? filter { get; set; }

        public string? filter_ignore { get; set; }

        public string? sort { get; set; }

        public PidTorAuthTS? base_auth { get; set; }

        public string[]? torrs { get; set; }

        public List<PidTorAuthTS>? auth_torrs { get; set; }

        public int group { get; set; }

        public bool group_hide { get; set; } = true;
    }

    public class PidTorAuthTS
    {
        public bool enable { get; set; }

        public string host { get; set; }

        public string? login { get; set; }

        public string? passwd { get; set; }

        public string? country { get; set; }

        public string? no_country { get; set; }

        public Dictionary<string, string>? headers { get; set; }
    }
}
