namespace Shared.Model.SISI.NextHUB
{
    public class ContentParseSettings
    {
        public string nodes { get; set; }

        public SingleNodeSettings name { get; set; }

        public SingleNodeSettings href { get; set; }

        public SingleNodeSettings? img { get; set; }

        public SingleNodeSettings? duration { get; set; }

        public SingleNodeSettings? quality { get; set; }

        public SingleNodeSettings? myarg { get; set; }

        public string eval { get; set; }
    }
}
