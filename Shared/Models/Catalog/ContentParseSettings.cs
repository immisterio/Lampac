namespace Shared.Models.Catalog
{
    public class ContentParseSettings
    {
        public string serial_regex { get; set; }

        public SingleNodeSettings serial_key { get; set; }

        public bool? jsonPath { get; set; }


        public string nodes { get; set; }

        public SingleNodeSettings name { get; set; }

        public SingleNodeSettings original_name { get; set; }

        public SingleNodeSettings href { get; set; }

        public SingleNodeSettings image { get; set; }

        public SingleNodeSettings year { get; set; }

        public List<SingleNodeSettings> args { get; set; }

        public SingleNodeSettings total_pages { get; set; }

        public string eval { get; set; }
    }
}
