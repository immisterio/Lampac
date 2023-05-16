using Lampac.Models.SISI;
using Lampac.Models.LITE;

namespace Shared.Model
{
    public class AppInit
    {
        public static string corseuhost => "https://cors.eu.org";


        public SisiSettings BongaCams = new SisiSettings("https://rus.bongacams.com");

        public SisiSettings Chaturbate = new SisiSettings("https://chaturbate.com");

        public SisiSettings Ebalovo = new SisiSettings("https://www.ebalovo.pro", streamproxy: true);

        public SisiSettings Eporner = new SisiSettings("https://www.eporner.com");

        public SisiSettings HQporner = new SisiSettings("https://m.hqporner.com");

        public SisiSettings Porntrex = new SisiSettings("https://www.porntrex.com");

        public SisiSettings Spankbang = new SisiSettings("https://ru.spankbang.com");

        public SisiSettings Xhamster = new SisiSettings("https://ru.xhamster.com");

        public SisiSettings Xnxx = new SisiSettings("https://www.xnxx.com");

        public SisiSettings Xvideos = new SisiSettings("https://www.xvideos.com");

        public SisiSettings PornHub = new SisiSettings("https://rt.pornhub.com", streamproxy: true);



        public OnlinesSettings Kinobase = new OnlinesSettings("https://kinobase.org", streamproxy: true);

        public OnlinesSettings Rezka = new OnlinesSettings("https://rezka.ag");

        public OnlinesSettings Voidboost = new OnlinesSettings("https://voidboost.net", streamproxy: true);

        public OnlinesSettings Collaps = new OnlinesSettings("https://api.delivembd.ws");

        public OnlinesSettings Ashdi = new OnlinesSettings("https://base.ashdi.vip");

        public OnlinesSettings Eneyida = new OnlinesSettings("https://eneyida.tv");

        public OnlinesSettings Kinokrad = new OnlinesSettings("https://kinokrad.cc", streamproxy: true);

        public OnlinesSettings Kinotochka = new OnlinesSettings("https://kinotochka.co", streamproxy: true);

        public OnlinesSettings Redheadsound = new OnlinesSettings("https://redheadsound.studio", streamproxy: true);

        public OnlinesSettings Kinoprofi = new OnlinesSettings("https://kinoprofi.io", apihost: "https://api.kinoprofi.io", streamproxy: true);

        public OnlinesSettings Lostfilmhd = new OnlinesSettings("http://www.disneylove.ru", streamproxy: true);

        public FilmixSettings Filmix = new FilmixSettings("http://filmixapp.cyou");

        public FilmixSettings FilmixPartner = new FilmixSettings("http://5.61.56.18/partner_api", enable: false);

        public OnlinesSettings Zetflix = new OnlinesSettings("https://zetfix.online");

        public OnlinesSettings VideoDB = new OnlinesSettings(string.Empty);

        public OnlinesSettings CDNmovies = new OnlinesSettings("https://cdnmovies.nl");


        public OnlinesSettings VCDN = new OnlinesSettings("https://89442664434375553.svetacdn.in/0HlZgU1l1mw5");

        public OnlinesSettings VoKino = new OnlinesSettings("http://api.vokino.tv", enable: false, streamproxy: true);

        public OnlinesSettings VideoAPI = new OnlinesSettings("https://videoapi.tv", token: "qR0taraBKvEZULgjoIRj69AJ7O6Pgl9O");

        public IframeVideoSettings IframeVideo = new IframeVideoSettings("https://iframe.video", "https://videoframe.space", enable: false);

        public OnlinesSettings HDVB = new OnlinesSettings("https://apivb.info", token: "5e2fe4c70bafd9a7414c4f170ee1b192");

        public OnlinesSettings Seasonvar = new OnlinesSettings("http://api.seasonvar.ru", enable: false);

        public KinoPubSettings KinoPub = new KinoPubSettings("https://api.service-kp.com") { uhd = true };

        public BazonSettings Bazon = new BazonSettings("https://bazon.cc", "", true);

        public AllohaSettings Alloha = new AllohaSettings("https://api.alloha.tv", "https://torso.as.alloeclub.com", "", "", true);

        public KodikSettings Kodik = new KodikSettings("https://kodikapi.com", "http://kodik.biz", "b7cc4293ed475c4ad1fd599d114f4435", "", true);


        public OnlinesSettings AnilibriaOnline = new OnlinesSettings("https://www.anilibria.tv", apihost: "https://api.anilibria.tv");

        public OnlinesSettings AniMedia = new OnlinesSettings("https://online.animedia.tv");

        public OnlinesSettings AnimeGo = new OnlinesSettings("https://animego.org", streamproxy: true);

        public OnlinesSettings Animevost = new OnlinesSettings("https://animevost.org", streamproxy: true);

        public OnlinesSettings Animebesst = new OnlinesSettings("https://anime1.animebesst.org", streamproxy: true);
    }
}
