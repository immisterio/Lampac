using Shared.Models.SISI.Base;

namespace Shared.Models.SISI.OnResult
{
    public readonly struct OnListResult
    {
        public OnListResult(int listCount, int total_pages, IList<MenuItem> menu)
        {
            list = new OnResultPlaylistItem[listCount];
            this.total_pages = total_pages;
            this.menu = menu;
        }

        public IList<MenuItem> menu { get; }

        public int total_pages { get; }

        public OnResultPlaylistItem[] list { get; }
    }
}
