using System;
using System.Collections.Generic;

namespace Lampac.Models.DLNA
{
    public class DlnaModel
    {
        public string name { get; set; }

        public string uri { get; set; }

        public string img { get; set; }

        public List<Subtitle> subtitles { get; set; }

        public string path { get; set; }

        public string type { get; set; }

        public long length { get; set; }

        public DateTime creationTime { get; set; }
    }
}
