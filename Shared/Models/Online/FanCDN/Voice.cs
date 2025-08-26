namespace Shared.Models.Online.FanCDN
{
    public struct Voice
    {
        public int id { get; set; }

        public string title { get; set; }

        public Dictionary<string, Episode> folder { get; set; }

        public int seasons { get; set; }
    }
}
