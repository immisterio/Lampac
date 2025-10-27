namespace Shared.Models.DLNA
{
    public class DLNASettings
    {
        public bool enable { get; set; }

        public string path { get; set; }

        public string mediaPattern { get; set; }

        public bool autoupdatetrackers { get; set; }

        public bool addTrackersToMagnet { get; set; }

        public int intervalUpdateTrackers { get; set; }

        public string mode { get; set; }

        public int downloadSpeed { get; set; }

        public int uploadSpeed { get; set; }

        public int maximumDiskReadRate { get; set; }

        public int maximumDiskWriteRate { get; set; }

        public CoverSettings cover { get; set; }
    }
}
