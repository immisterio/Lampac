namespace Shared.Models.Online.AnimeLib
{
    public class AnimeLibTokenState
    {
        public string init_tk { get; set; }
        public string token { get; set; }
        public string refresh_token { get; set; }
        public long refresh_time { get; set; }
    }
}
