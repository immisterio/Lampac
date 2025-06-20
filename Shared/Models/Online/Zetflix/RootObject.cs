namespace Shared.Model.Online.Zetflix
{
    public class RootObject
    {
        public string title { get; set; }

        public string file { get; set; }

        public List<Folder> folder { get; set; }
    }
}
