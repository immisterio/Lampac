namespace Shared.Models.Online.VoKino
{
    public class Сhannel
    {
        public string title { get; set; }

        public string ident { get; set; }

        public string playlist_url { get; set; }

        public bool selected { get; set; }

        public Сhannel[] submenu { get; set; }


        public string stream_url { get; set; }

        public string quality_full { get; set; }

        public Dictionary<string, string> extra { get; set; }

        public Details details { get; set; }
    }
}
