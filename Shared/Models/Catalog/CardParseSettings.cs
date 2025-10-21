namespace Shared.Models.Catalog
{
    public class CardParseSettings
    {
        public SingleNodeSettings name { get; set; }

        public SingleNodeSettings original_name { get; set; }

        public SingleNodeSettings image { get; set; }

        public SingleNodeSettings year { get; set; }

        public SingleNodeSettings description { get; set; }

        public List<SingleNodeSettings> args { get; set; }

        public string eval { get; set; }
    }
}
