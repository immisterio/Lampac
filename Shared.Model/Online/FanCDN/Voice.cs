namespace Shared.Model.Online.FanCDN
{
    public class Voice
    {
        public int id { get; set; }

        public string title { get; set; }

        public Dictionary<string, Episode> folder { get; set; }

        public int seasons { get; set; }
    }
}
