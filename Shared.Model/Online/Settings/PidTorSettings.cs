namespace Shared.Model.Online.Settings
{
    public class PidTorSettings
    {
        public bool enable { get; set; }

        public string? displayname { get; set; }

        public int displayindex { get; set; }

        public string? redapi { get; set; }

        public int min_sid { get; set; }

        public int max_size { get; set; }

        public string? filter { get; set; }

        public string? filter_ignore { get; set; }

        public string[]? torrs { get; set; }

        public List<PidTorAuthTS>? auth_torrs { get; set; }
    }

    public class PidTorAuthTS
    {
        public bool enable { get; set; }

        public string host { get; set; }

        public string? login { get; set; }

        public string? passwd { get; set; }
    }
}
