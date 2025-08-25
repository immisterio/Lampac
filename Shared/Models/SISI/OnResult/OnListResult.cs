using Shared.Models.SISI.Base;

namespace Shared.Models.SISI.OnResult
{
    public struct OnListResult
    {
        public OnListResult(in OnResultPlaylistItem[] list, int total_pages, IList<MenuItem> menu)
        {
            this.list = list;
            this.total_pages = total_pages;
            this.menu = menu;
        }

        public IList<MenuItem> menu { get; set; }

        public int total_pages { get; set; }

        public OnResultPlaylistItem[] list { get; set; }
    }
}
