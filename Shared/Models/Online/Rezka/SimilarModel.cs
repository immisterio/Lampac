namespace Shared.Models.Online.Rezka
{
    public class SimilarModel
    {
        public SimilarModel(string title, string year, string href, string img)
        {
            this.title = title;
            this.year = year;
            this.href = href;
            this.img = img;
        }

        public string title { get; set; }

        public string year { get; set; }

        public string href { get; set; }

        public string img { get; set; }
    }
}
