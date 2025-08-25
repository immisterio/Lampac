using Shared.Models.JacRed.Tracks;

namespace Shared.Models.JacRed
{
    public class TorrentDetails : ICloneable
    {
        public string trackerName { get; set; }

        public string[] types { get; set; }

        public string url { get; set; }

        public HashSet<string> urls { get; set; }


        public string title { get; set; }

        public int sid { get; set; }

        public int pir { get; set; }

        public string sizeName { get; set; }

        public DateTime createTime { get; set; } = DateTime.UtcNow;

        public DateTime updateTime { get; set; } = DateTime.UtcNow;

        public DateTime checkTime { get; set; } = DateTime.Now;

        public string magnet { get; set; }

        public string parselink { get; set; }


        public string name { get; set; }

        public string originalname { get; set; }

        public int relased { get; set; }

        public double size { get; set; }

        public int quality { get; set; }

        public string videotype { get; set; }

        public HashSet<string> voices { get; set; } = new HashSet<string>();

        public HashSet<int> seasons { get; set; } = new HashSet<int>();


        public HashSet<string> languages { get; set; }

        public List<ffStream> ffprobe { get; set; }


        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
