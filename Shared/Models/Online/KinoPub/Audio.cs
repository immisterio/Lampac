namespace Shared.Models.Online.KinoPub
{
    public class Audio
    {
        public int index { get; set; }

        public string lang { get; set; }

        public string codec { get; set; }

        public Author author { get; set; }

        public Author type { get; set; }
    }
}
