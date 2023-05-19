namespace Shared.Model.Online.VoKino
{
    public class Сhannel
    {
        public string stream_url { get; set; }

        public string quality_full { get; set; }

        public Dictionary<string, string> extra { get; set; }

        public Details details { get; set; }
    }
}
