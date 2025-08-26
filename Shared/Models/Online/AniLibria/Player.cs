namespace Shared.Models.Online.AniLibria
{
    public struct Player
    {
        public string host { get; set; }

        public Dictionary<string, Series> playlist { get; set; }
    }
}
