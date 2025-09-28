namespace Shared.Models.Online.AnimeLib
{
    public struct Player
    {
        public string player { get; set; }

        public PlayerTeam team { get; set; }

        public Video video { get; set; }
    }

    public struct PlayerTeam
    {
        public string name { get; set; }
    }
}
