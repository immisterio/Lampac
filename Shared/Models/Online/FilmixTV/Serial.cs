namespace Shared.Models.Online.FilmixTV
{
    public struct Season
    {
        public int season { get; set; }
        public Dictionary<string, Episode> episodes { get; set; }
    }

    public struct Episode
    {
        public int episode { get; set; }
        public File[] files { get; set; }
    }

    public struct File
    {
        public string url { get; set; }
        public int quality { get; set; }
    }
}
