using JinEnergy.Engine;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JinEnergy
{
    public static class AppInit
    {
        /// <param name="type">
        /// apk  - android
        /// web  - msx, браузер, etc
        /// cors - виджет или браузер с отключеным cors
        /// </param>
        /// <param name="conf">
        /// url   - ссылка на json с настройками 
        /// proxy - включает прокси через cors.eu.org там где он поддерживается
        /// </param>
        [JSInvokable("oninit")]
        async public static Task OnInit(string type, string urlconf)
        {
            if (type == "apk")
                IsAndrod = true;

            Chaturbate.host = "https://m.chaturbate.com";

            if (type == "web" || urlconf == "proxy")
            {
                string corshost = "https://cors.eu.org";

                PornHub.webcorshost = corshost;
                HQporner.webcorshost = corshost;
                Spankbang.webcorshost = corshost;
                Eporner.webcorshost = corshost;
                Porntrex.webcorshost = corshost;
                Xhamster.webcorshost = corshost;
                BongaCams.webcorshost = corshost;
                Chaturbate.webcorshost = corshost;
            }

            if (type == "web")
            {
                Xnxx.enable = false;
                Xvideos.enable = false;
                Ebalovo.enable = false;
            }

            if (urlconf != null && urlconf.StartsWith("http"))
            {
                var setings = await JsHttpClient.Get<Shared.Model.AppInit>(urlconf);
                if (setings != null)
                    conf = setings;
            }
        }


        static Shared.Model.AppInit conf = new Shared.Model.AppInit();

        public static IJSRuntime? JSRuntime;

        public static bool IsAndrod { get; private set; }

        public static SisiSettings BongaCams => conf.BongaCams;

        public static SisiSettings Chaturbate => conf.Chaturbate;

        public static SisiSettings Eporner => conf.Eporner;

        public static SisiSettings HQporner => conf.HQporner;

        public static SisiSettings Porntrex => conf.Porntrex;

        public static SisiSettings Xhamster => conf.Xhamster;

        public static SisiSettings Xnxx => conf.Xnxx;

        public static SisiSettings Xvideos => conf.Xvideos;

        public static SisiSettings PornHub => conf.PornHub;

        public static SisiSettings Ebalovo => conf.Ebalovo;

        public static SisiSettings Spankbang => conf.Spankbang;

    }
}
