namespace Lampac.Models.LITE.AniLibria
{
    public class Player
    {
        public string host { get; set; }

        public Dictionary<string, Series> playlist { get; set; }
    }
}
