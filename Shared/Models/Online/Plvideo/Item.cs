namespace Shared.Models.Online.Plvideo
{
    public struct Item
    {
        public string id { get; set; }

        public string title { get; set; }

        public ItemuploadFile uploadFile { get; set; }

        public string visible { get; set; }
    }

    public struct ItemuploadFile
    {
        public long videoDuration { get; set; }
    }
}
