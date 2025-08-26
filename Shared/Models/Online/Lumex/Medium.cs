namespace Shared.Models.Online.Lumex
{
    public struct Medium
    {
        public int translation_id { get; set; }

        public string translation_name { get; set; }

        public int? max_quality { get; set; }

        public string playlist { get; set; }

        public string[] subtitles { get; set; }

        public Track[] tracks { get; set; }



        public int season_id { get; set; }

        public Episode[] episodes { get; set; }
    }
}
