using System.Collections.Generic;

namespace Lampac.Models.SISI
{
    public class MenuItem
    {
        public string title { get; set; }

        public string search_on { get; set; }

        public string playlist_url { get; set; }

        public List<MenuItem> submenu { get; set; }
    }
}
