namespace Shared.Model.Online.Lumex
{
    public class Medium
    {
        public int translation_id { get; set; }

        public string translation_name { get; set; }

        public string max_quality { get; set; }

        public string playlist { get; set; }

        public List<string>? subtitles { get; set; }



        public int season_id { get; set; }

        public List<Episode> episodes { get; set; }
    }
}
