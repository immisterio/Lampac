namespace Shared.Models.Online.Filmix
{
    public class SearchModel
    {
        public int id { get; set; }

        public string title { get; set; }

        public string original_title { get; set; }

        /// <summary>
        /// api.filmix.tv
        /// </summary>
        public string original_name { get; set; }

        public string poster { get; set; }

        public int year { get; set; }
    }
}
