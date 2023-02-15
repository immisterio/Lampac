using System.Collections.Generic;

namespace Lampac.Models.LITE.HDVB
{
    public class Folder
    {
        public string id { get; set; }

        public string episode { get; set; }

        public List<Folder> folder { get; set; }

        public string title { get; set; }

        public string file { get; set; }
    }
}
