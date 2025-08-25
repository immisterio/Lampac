namespace Shared.Models.JacRed.Tracks
{
    public class ffStream
    {
        public int index { get; set; }

        public string codec_name { get; set; }

        public string codec_long_name { get; set; }

        public string codec_type { get; set; }

        public int? width { get; set; }

        public int? height { get; set; }

        public int? coded_width { get; set; }

        public int? coded_height { get; set; }

        public string sample_fmt { get; set; }

        public string sample_rate { get; set; }

        public int? channels { get; set; }

        public string channel_layout { get; set; }

        public string bit_rate { get; set; }

        public ffTags tags { get; set; }
    }
}
