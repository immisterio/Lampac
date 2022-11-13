using Lampac.Models.SISI;
using Lampac.Models.JAC;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using Lampac.Models.LITE.VideoCDN;
using Lampac.Models.LITE.Filmix;
using Lampac.Models.LITE;
using Lampac.Models.LITE.HDVB;
using System.Collections.Generic;
using Lampac.Models;

namespace Lampac
{
    public class AppInit
    {
        public static AppInit conf = JsonConvert.DeserializeObject<AppInit>(File.ReadAllText("init.conf"));

        public static string Host(HttpContext httpContext) => $"http://{httpContext.Request.Host.Value}";


        public int listenport = 9118;

        public int timeoutSeconds = 6;

        public string cachetype = "file";

        public int htmlCacheToMinutes = 1;

        public int fileCacheInactiveDay = 20;

        public int magnetCacheToMinutes = 2;

        public bool emptycache = false;

        public string apikey = null;

        public string ffprobe = "linux";

        public bool disableserverproxy = false;

        public bool multiaccess = false;

        public bool proxytoproxyimg = false;

        public bool dlna = false;


        public WebConf LampaWeb = new WebConf() { autoupdate = true, autoindex = true };


        public TrackerSettings Rutor = new TrackerSettings("http://rutor.info");

        public TrackerSettings Megapeer = new TrackerSettings("http://megapeer.vip");

        public TrackerSettings TorrentBy = new TrackerSettings("http://torrent.by");

        public TrackerSettings Kinozal = new TrackerSettings("http://kinozal.tv");

        public TrackerSettings NNMClub = new TrackerSettings("https://nnmclub.to");

        public TrackerSettings Bitru = new TrackerSettings("https://bitru.org");

        public TrackerSettings Toloka = new TrackerSettings("https://toloka.to", enable: false);

        public TrackerSettings Rutracker = new TrackerSettings("https://rutracker.net", enable: false);

        public TrackerSettings Underverse = new TrackerSettings("https://underver.se", enable: false);

        public TrackerSettings Selezen = new TrackerSettings("https://selezen.net", enable: false);

        public TrackerSettings Anilibria = new TrackerSettings("https://www.anilibria.tv");

        public TrackerSettings Animelayer = new TrackerSettings("http://animelayer.ru", enable: false);


        public bool xdb = false;

        public int SisiHeightPicture = 200;

        public SisiSettings BongaCams = new SisiSettings("https://rt.bongacams.com");

        public SisiSettings Chaturbate = new SisiSettings("https://chaturbate.com");

        public SisiSettings Ebalovo = new SisiSettings("https://www.ebalovo.pro");

        public SisiSettings Eporner = new SisiSettings("https://www.eporner.com");

        public SisiSettings HQporner = new SisiSettings("https://hqporner.com");

        public SisiSettings Porntrex = new SisiSettings("https://www.porntrex.com");

        public SisiSettings Spankbang = new SisiSettings("https://ru.spankbang.com");

        public SisiSettings Xhamster = new SisiSettings("https://ru.xhamster.com");

        public SisiSettings Xnxx = new SisiSettings("https://www.xnxx.com");

        public SisiSettings Xvideos = new SisiSettings("https://www.xvideos.com");

        public SisiSettings PornHub = new SisiSettings("https://rt.pornhub.com");


        public OnlinesSettings Kinobase = new OnlinesSettings("https://kinobase.org");

        public OnlinesSettings Rezka = new OnlinesSettings("https://voidboost.net");

        public OnlinesSettings Collaps = new OnlinesSettings("https://api.delivembd.ws");

        public OnlinesSettings Ashdi = new OnlinesSettings("https://base.ashdi.vip");

        public OnlinesSettings Eneyida = new OnlinesSettings("https://eneyida.tv");

        public OnlinesSettings Kinokrad = new OnlinesSettings("https://kinokrad.cc");

        public OnlinesSettings Kinotochka = new OnlinesSettings("https://kinotochka.co");

        public OnlinesSettings Redheadsound = new OnlinesSettings("https://redheadsound.ru");

        public OnlinesSettings Kinoprofi = new OnlinesSettings("https://kinoprofi.vip", apihost: "https://api.kinoprofi.vip");

        public OnlinesSettings Lostfilmhd = new OnlinesSettings("http://www.lostfilmhd.ru");

        public FilmixSettings Filmix = new FilmixSettings("http://filmixapp.cyou");

        public OnlinesSettings Zetflix = new OnlinesSettings("https://8nov.zetfix.online");


        public VCDNSettings VCDN = new VCDNSettings("https://videocdn.tv", "3i40G5TSECmLF77oAqnEgbx61ZWaOYaE", "http://58.svetacdn.in", false);

        public OnlinesSettings VideoAPI = new OnlinesSettings("http://5100.svetacdn.in", token: "qR0taraBKvEZULgjoIRj69AJ7O6Pgl9O");

        public IframeVideoSettings IframeVideo = new IframeVideoSettings("https://iframe.video", "https://videoframe.space");

        public HDVBSettings HDVB = new HDVBSettings("https://apivb.info", "");

        public OnlinesSettings Seasonvar = new OnlinesSettings("http://seasonvar.ru", apihost: "http://api.seasonvar.ru");

        public OnlinesSettings KinoPub = new OnlinesSettings(null, apihost: "https://api.service-kp.com");

        public BazonSettings Bazon = new BazonSettings("https://bazon.cc", "", true);

        public AllohaSettings Alloha = new AllohaSettings("https://api.alloha.tv", "https://torso.as.alloeclub.com", "", "", true);

        public KodikSettings Kodik = new KodikSettings("https://kodikapi.com", "http://kodik.biz", "b7cc4293ed475c4ad1fd599d114f4435", "", true);


        public ProxySettings proxy = new ProxySettings();

        public List<ProxySettings> globalproxy = new List<ProxySettings>();
    }
}
