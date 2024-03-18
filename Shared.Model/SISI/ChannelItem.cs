namespace Lampac.Models.SISI
{
    public class ChannelItem
    {
        public ChannelItem(string title, string playlist_url)
        {
            this.title = title;
            this.playlist_url = playlist_url;
        }

        public string title { get; set; }

        public string playlist_url { get; set; }
    }
}
