using System.Collections.Generic;

namespace Lampac.Models.LITE.KinoPub
{
    public class Season
    {
        public int number { get; set; }

        public List<Episode> episodes { get; set; }
    }
}
