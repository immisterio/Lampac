namespace Shared.Models.Online.KinoPub
{
    public struct Episode
    {
        public long id { get; set; }

        public int number { get; set; }

        public string title { get; set; }

        public Subtitle[] subtitles { get; set; }

        public File[] files { get; set; }

        public Audio[] audios { get; set; }
    }
}
