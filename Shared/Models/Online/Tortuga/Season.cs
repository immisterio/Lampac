namespace Shared.Models.Online.Tortuga
{
    public struct Season
    {
        public string title { get; set; }

        public string number { get; set; }

        public Series[] folder { get; set; }
    }
}
