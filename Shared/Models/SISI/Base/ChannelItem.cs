namespace Shared.Models.SISI.Base
{
    public struct ChannelItem
    {
        public ChannelItem(string title, string playlist_url, int displayindex)
        {
            this.title = title;
            this.playlist_url = playlist_url;
            this.displayindex = displayindex;
        }

        public string title { get; set; }

        public string playlist_url { get; set; }

        public int displayindex { get; set; }
    }
}
