namespace Shared.Models.Online.HDVB
{
    public struct Folder
    {
        public string id { get; set; }

        public string episode { get; set; }

        public Folder[] folder { get; set; }

        public string title { get; set; }

        public string file { get; set; }
    }
}
