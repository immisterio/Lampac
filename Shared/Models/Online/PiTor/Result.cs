namespace Shared.Models.Online.PiTor
{
    public struct Result
    {
        public string Tracker { get; set; }
        public string Title { get; set; }
        public long? Size { get; set; }
        public int Seeders { get; set; }
        public string MagnetUri { get; set; }
        public Info info { get; set; }

        public DateTime PublishDate { get; set; }
    }
}
