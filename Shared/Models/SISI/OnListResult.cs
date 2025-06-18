using Lampac.Models.SISI;

namespace Shared.Model.SISI
{
    public struct OnListResult
    {
        public OnListResult(in OnResultPlaylistItem[] list, in int total_pages, in IList<MenuItem>? menu)
        {
            this.list = list;
            this.total_pages = total_pages;
            this.menu = menu;
        }

        public IList<MenuItem>? menu { get; set; }

        public int total_pages { get; set; }

        public OnResultPlaylistItem[] list { get; set; }
    }
}
