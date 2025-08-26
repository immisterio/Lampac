namespace Shared.Models.Online.VideoDB
{
    public struct RootObject
    {
        public string title { get; set; }

        public string file { get; set; }

        public string subtitle { get; set; }

        public Folder[] folder { get; set; }
    }
}
