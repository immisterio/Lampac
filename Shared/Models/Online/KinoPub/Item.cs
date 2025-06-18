namespace Lampac.Models.LITE.KinoPub
{
    public class Item
    {
        public bool advert { get; set; }

        public int quality { get; set; }

        public string voice { get; set; }

        public List<Video> videos { get; set; }

        public List<Season> seasons { get; set; }
    }
}
