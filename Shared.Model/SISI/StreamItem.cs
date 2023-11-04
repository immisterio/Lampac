namespace Lampac.Models.SISI
{
    public class StreamItem
    {
        public Dictionary<string, string> qualitys { get; set; }

        public Dictionary<string, string>? qualitys_proxy { get; set; }

        public List<PlaylistItem>? recomends { get; set; }
    }
}
