namespace Shared.Models.SISI.OnResult
{
    public struct OnStreamResult
    {
        public OnStreamResult(int recomendsCount) 
        {
            recomends = new OnResultPlaylistItem[recomendsCount];
        }

        public Dictionary<string, string> qualitys { get; set; }

        public Dictionary<string, string> qualitys_proxy { get; set; }

        public Dictionary<string, string> headers_stream { get; set; }

        public OnResultPlaylistItem[] recomends { get; set; }
    }
}
