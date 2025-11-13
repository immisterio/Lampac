namespace Shared.Models.SISI.Base
{
    public class PlaylistItem
    {
        public string video { get; set; }

        public string name { get; set; }

        public string picture { get; set; }

        public string preview { get; set; }

        public string quality { get; set; }

        public string time { get; set; }

        public string myarg { get; set; }

        public bool json { get; set; }

        public bool hide { get; set; }

        public bool related { get; set; }

        public ModelItem model { get; set; }

        public Dictionary<string, string> qualitys { get; set; }

        public Bookmark bookmark { get; set; }

        public string history_uid { get; set; }
    }
}
