using System.Collections.Generic;

namespace Shared.Models.ServerProxy
{
    public class ServerproxyCacheConf
    {
        public bool img { get; set; }

        public bool img_rsize { get; set; }

        public List<HlsCacheConf> hls { get; set; }
    }
}
