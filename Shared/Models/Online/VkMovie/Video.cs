namespace Shared.Models.Online.VkMovie
{
    public class Video
    {
        public long id { get; set; }
        public long owner_id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public long duration { get; set; }
        public VideoFiles files { get; set; }
        public VideoSubtitle[] subtitles { get; set; }
    }
}
