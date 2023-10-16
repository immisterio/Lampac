namespace Shared.Model.Online.Rezka
{
    public class SimilarModel
    {
        public SimilarModel(string title, string year, string href)
        {
            this.title = title;
            this.year = year;
            this.href = href;
        }

        public string title { get; set; }

        public string year { get; set; }

        public string href { get; set; }
    }
}
