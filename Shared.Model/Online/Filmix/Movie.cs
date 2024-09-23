using System.Collections.Generic;

namespace Lampac.Models.LITE.Filmix
{
    public class Movie
    {
        public string link { get; set; }

        public string translation { get; set; }

        public List<int> qualities { get; set; }
    }

    public class MovieTV
    {
        public List<File> files { get; set; }
        public string voiceover { get; set; }
    }
}