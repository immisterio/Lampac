using System.Collections.Generic;

namespace Lampac.Models.LITE.Filmix
{
    public class Season
    {
        public int season { get; set; }
        public Dictionary<string, Episode> episodes { get; set; }
    }

    public class Episode
    {
        public int episode { get; set; }
        public List<File> files { get; set; }
    }

    public class File
    {
        public string url { get; set; }
        public int quality { get; set; }
    }
}