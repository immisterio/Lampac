using Lampac.Models.SISI;

namespace Shared.Model.SISI
{
    public class OnListResult
    {
        public List<MenuItem>? menu { get; set; }

        public int total_pages { get; set; }

        public PlaylistItem[] list { get; set; }
    }
}
