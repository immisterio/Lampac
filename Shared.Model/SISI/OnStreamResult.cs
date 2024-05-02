using Lampac.Models.SISI;

namespace Shared.Model.SISI
{
    public class OnStreamResult
    {
        public Dictionary<string, string> qualitys { get; set; }

        public Dictionary<string, string>? qualitys_proxy { get; set; }

        public IEnumerable<PlaylistItem>? recomends { get; set; }
    }
}
