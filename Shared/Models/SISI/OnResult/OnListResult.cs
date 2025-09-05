using Shared.Models.SISI.Base;

namespace Shared.Models.SISI.OnResult
{
    public struct OnListResult
    {
        public OnListResult(int listCount, int total_pages, IList<MenuItem> menu)
        {
            list = new OnResultPlaylistItem[listCount];
            this.total_pages = total_pages;
            this.menu = menu;
        }

        public IList<MenuItem> menu { get; set; }

        public int total_pages { get; set; }

        public OnResultPlaylistItem[] list { get; set; }
    }
}
