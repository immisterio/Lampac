namespace Lampac.Models.AppConf
{
    public class InitPlugins
    {
        public bool dlna { get; set; } = true;

        public bool tracks { get; set; } = true;

        public bool tmdbProxy { get; set; } = true;

        public bool cubProxy { get; set; } = true;

        public bool online { get; set; } = true;

        public bool sisi { get; set; } = true;

        public bool timecode { get; set; } = false;

        public bool torrserver { get; set; } = true;

        public bool backup { get; set; } = true;

        public bool sync { get; set; } = true;
    }
}
