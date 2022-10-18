using System.Collections.Generic;

namespace Lampac.Models.LITE.Collaps
{
    public class Episode
    {
        public string episode { get; set; }
        
        public string hls { get; set; }

        public List<Cc> cc { get; set; }
    }
}
