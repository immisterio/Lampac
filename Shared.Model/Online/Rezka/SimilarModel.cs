namespace Shared.Model.Online.Rezka
{
    public class SimilarModel
    {
        public SimilarModel(string title, string href)
        {
            this.title = title;
            this.href = href;
        }

        public string title { get; set; }

        public string href { get; set; }
    }
}
