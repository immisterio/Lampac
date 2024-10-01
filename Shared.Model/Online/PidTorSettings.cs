namespace Shared.Model.Online
{
    public class PidTorSettings
    {
        public bool enable { get; set; }

        public string? displayname { get; set; }

        public int displayindex { get; set; }

        public string? redapi { get; set; }

        public int min_sid { get; set; }

        public string[]? torrs { get; set; }
    }
}
