namespace Lampac.Models.LITE.KinoPub
{
    public class Episode
    {
        public long id { get; set; }

        public int number { get; set; }

        public string title { get; set; }

        public List<Subtitle> subtitles { get; set; }

        public List<File> files { get; set; }

        public List<Audio> audios { get; set; }
    }
}
