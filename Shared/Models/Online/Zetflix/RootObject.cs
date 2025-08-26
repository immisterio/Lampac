namespace Shared.Models.Online.Zetflix
{
    public struct RootObject
    {
        public string title { get; set; }

        public string file { get; set; }

        public Folder[] folder { get; set; }
    }
}
