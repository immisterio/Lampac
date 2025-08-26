namespace Shared.Models.Online.VideoDB
{
    public struct Folder
    {
        public string title { get; set; }

        public Folder[] folder { get; set; }

        public string file { get; set; }
    }
}
