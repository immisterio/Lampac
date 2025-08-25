namespace Shared.Models.Online.AniLibria
{
    public class Player
    {
        public string host { get; set; }

        public Dictionary<string, Series> playlist { get; set; }
    }
}
