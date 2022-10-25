using Lampac.Models.SISI;
using Lampac.Models.JAC;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using Lampac.Models.LITE.VideoCDN;
using Lampac.Models.LITE.Collaps;
using Lampac.Models.LITE.Filmix;
using Lampac.Models.LITE;

namespace Lampac
{
    public class AppInit
    {
        public static AppInit conf = JsonConvert.DeserializeObject<AppInit>(File.ReadAllText("init.conf"));

        public static string Host(HttpContext httpContext) => $"http://{httpContext.Request.Host.Value}";


        public int listenport = 9118;

        public int timeoutSeconds = 5;

        public int htmlCacheToMinutes = 1;

        public int magnetCacheToMinutes = 2;

        public string apikey = null;

        public TrackerSettings Rutor = new TrackerSettings("http://rutor.info", true, false);

        public TrackerSettings Megapeer = new TrackerSettings("http://megapeer.vip", true, false);

        public TrackerSettings TorrentBy = new TrackerSettings("http://torrent.by", true, false);

        public TrackerSettings Kinozal = new TrackerSettings("http://kinozal.tv", true, false);

        public TrackerSettings NNMClub = new TrackerSettings("https://nnmclub.to", true, false);

        public TrackerSettings Bitru = new TrackerSettings("https://bitru.org", true, false);

        public TrackerSettings Toloka = new TrackerSettings("https://toloka.to", false, false);

        public TrackerSettings Rutracker = new TrackerSettings("https://rutracker.net", false, false);

        public TrackerSettings Underverse = new TrackerSettings("https://underver.se", false, false);

        public TrackerSettings Selezen = new TrackerSettings("https://selezen.net", false, false);

        public TrackerSettings Anilibria = new TrackerSettings("https://www.anilibria.tv", true, false);

        public TrackerSettings Animelayer = new TrackerSettings("http://animelayer.ru", false, false);


        public bool xdb = false;

        public SisiSettings BongaCams = new SisiSettings("https://rt.bongacams.com", true, false);

        public SisiSettings Chaturbate = new SisiSettings("https://chaturbate.com", true, false);

        public SisiSettings Ebalovo = new SisiSettings("https://www.ebalovo.pro", true, false);

        public SisiSettings Eporner = new SisiSettings("https://www.eporner.com", true, false);

        public SisiSettings HQporner = new SisiSettings("https://hqporner.com", true, false);

        public SisiSettings Porntrex = new SisiSettings("https://www.porntrex.com", true, false);

        public SisiSettings Spankbang = new SisiSettings("https://ru.spankbang.com", true, false);

        public SisiSettings Xhamster = new SisiSettings("https://ru.xhamster.com", true, false);

        public SisiSettings Xnxx = new SisiSettings("https://www.xnxx.com", true, false);

        public SisiSettings Xvideos = new SisiSettings("https://www.xvideos.com", true, false);


        public VCDNSettings VCDN = new VCDNSettings("https://videocdn.tv", "3i40G5TSECmLF77oAqnEgbx61ZWaOYaE", "http://58.svetacdn.in", false);

        public CollapsSettings Collaps = new CollapsSettings("https://api.delivembd.ws", false);

        public FilmixSettings Filmix = new FilmixSettings("http://filmixapp.cyou", false);

        public BazonSettings Bazon = new BazonSettings("https://bazon.cc", "", true);

        public AllohaSettings Alloha = new AllohaSettings("https://api.alloha.tv", "https://torso.as.alloeclub.com", "", "", true);


        public ProxySettings proxy = new ProxySettings();
    }
}
