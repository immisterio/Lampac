namespace Shared.Models.SISI.NextHUB
{
    public class ContentParseSettings
    {
        public string nodes { get; set; }

        public SingleNodeSettings name { get; set; }

        public SingleNodeSettings href { get; set; }

        public SingleNodeSettings img { get; set; }

        public SingleNodeSettings duration { get; set; }

        public SingleNodeSettings quality { get; set; }

        public SingleNodeSettings preview { get; set; }

        public ModelParse model { get; set; }

        public List<ContentParseArg> args { get; set; }

        public bool json { get; set; } = true;

        public string eval { get; set; }
    }
}
