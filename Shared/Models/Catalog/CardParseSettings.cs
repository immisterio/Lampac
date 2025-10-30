namespace Shared.Models.Catalog
{
    public class CardParseSettings
    {
        public bool? jsonPath { get; set; }

        public string initUrl { get; set; }

        public string postData { get; set; }

        public string initHeader { get; set; }


        public string node { get; set; }

        public SingleNodeSettings name { get; set; }

        public SingleNodeSettings original_name { get; set; }

        public SingleNodeSettings image { get; set; }

        public SingleNodeSettings year { get; set; }

        public SingleNodeSettings description { get; set; }
    }
}
