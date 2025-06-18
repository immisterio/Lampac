using Lampac.Models.SISI;

namespace Shared.Model.SISI
{
    public struct OnStreamResult
    {
        public OnStreamResult(Dictionary<string, string> qualitys, Dictionary<string, string>? qualitys_proxy, Dictionary<string, string>? headers_stream, in IList<OnResultPlaylistItem>? recomends)
        {
            this.qualitys = qualitys;
            this.qualitys_proxy = qualitys_proxy;
            this.headers_stream = headers_stream;
            this.recomends = recomends;
        }

        public Dictionary<string, string> qualitys { get; set; }

        public Dictionary<string, string>? qualitys_proxy { get; set; }

        public Dictionary<string, string>? headers_stream { get; set; }

        public IList<OnResultPlaylistItem>? recomends { get; set; }
    }
}
