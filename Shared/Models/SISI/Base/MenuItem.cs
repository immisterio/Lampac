namespace Shared.Models.SISI.Base
{
    public struct MenuItem
    {
        public MenuItem(string title, string playlist_url) 
        {
            this.title = title;
            this.playlist_url = playlist_url;
        }

        public string title { get; set; }

        public string search_on { get; set; }

        public string logo_30x30 { get; set; }

        public string playlist_url { get; set; }

        public List<MenuItem> submenu { get; set; }
    }
}
