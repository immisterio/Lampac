namespace Shared.Models.Online.KinoPub
{
    public struct Item
    {
        public bool advert { get; set; }

        public int quality { get; set; }

        public string voice { get; set; }

        public Video[] videos { get; set; }

        public Season[] seasons { get; set; }
    }
}
