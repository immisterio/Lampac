namespace Shared.Models.Online.KinoPub
{
    public class Video
    {
        public long id { get; set; }

        public string title { get; set; }

        public Subtitle[] subtitles { get; set; }

        public File[] files { get; set; }

        public Audio[] audios { get; set; }
    }
}
