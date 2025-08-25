namespace Shared.Models.Online.Collaps
{
    public class Episode
    {
        public string episode { get; set; }
        
        public string hls { get; set; }

        public string dasha { get; set; }
        public string dash { get; set; }

        public List<Cc> cc { get; set; }

        public Audio audio { get; set; }
    }
}
