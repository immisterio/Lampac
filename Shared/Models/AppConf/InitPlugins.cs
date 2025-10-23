namespace Shared.Models.AppConf
{
    public class InitPlugins
    {
        public bool dlna { get; set; } = true;

        public bool tracks { get; set; } = true;

        public bool transcoding { get; set; }

        public bool tmdbProxy { get; set; } = true;

        public bool online { get; set; } = true;

        public bool catalog { get; set; } = true;

        public bool sisi { get; set; } = true;

        public bool torrserver { get; set; } = true;

        public bool backup { get; set; } = true;


        public bool sync { get; set; } = true;

        public bool bookmark { get; set; }

        public bool timecode { get; set; }
    }
}
