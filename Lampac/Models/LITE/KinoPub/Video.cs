using System.Collections.Generic;

namespace Lampac.Models.LITE.KinoPub
{
    public class Video
    {
        public string title { get; set; }

        public List<Subtitle> subtitles { get; set; }

        public List<File> files { get; set; }

        public List<Audio> audios { get; set; }
    }
}
