namespace Shared.Models.Online.Tortuga
{
    public struct Voice
    {
        public string title { get; set; }

        public string season { get; set; }

        public Season[] folder { get; set; }
    }
}
