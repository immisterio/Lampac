using Lampac.Models.SISI;

namespace JinEnergy.Model
{
    public class ResultModel
    {
        public List<MenuItem>? menu { get; set; }

        public int total_pages { get; set; }

        public List<PlaylistItem>? list { get; set; }


        public Dictionary<string, string>? qualitys { get; set; }

        public Dictionary<string, string>? qualitys_proxy { get; set; }

        public List<PlaylistItem>? recomends { get; set; }


        public string? error { get; set; }
    }
}
