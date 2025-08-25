namespace JacRed.Models.Sync
{
    public class Value
    {
        public DateTime time { get; set; }

        public long fileTime { get; set; }

        public Dictionary<string, TorrentDetails> torrents { get; set; }
    }
}
