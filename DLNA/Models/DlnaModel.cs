using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace DLNA.Models
{
    public class DlnaModel
    {
        public string name { get; set; }

        public string uri { get; set; }

        public string img { get; set; }

        public string preview { get; set; }

        public List<Subtitle> subtitles { get; set; }

        public string path { get; set; }

        public string type { get; set; }

        public long length { get; set; }

        public DateTime creationTime { get; set; }

        public int s { get; set; }

        public int e { get; set; }

        public JObject tmdb { get; set; }

        public JObject episode { get; set; }
    }
}
