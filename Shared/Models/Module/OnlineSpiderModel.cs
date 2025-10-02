namespace Shared.Models.Module
{
    public class OnlineSpiderModel
    {
        public OnlineSpiderModel(string title, bool isanime)
        {
            this.title = title;
            this.isanime = isanime;
        }

        public string title { get; set; }
        public bool isanime { get; set; }
        public bool requireRhub { get; set; }
    }
}
