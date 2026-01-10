namespace Shared.Models.SISI.Base
{
    public class Channel
    {
        public IList<MenuItem> menu { get; set; }

        public IList<PlaylistItem> list { get; set; }

        public int total_pages { get; set; }
    }
}
