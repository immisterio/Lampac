﻿using JinEnergy.Engine;
using Lampac.Models.AppConf;
using Lampac.Models.LITE;
using Lampac.Models.SISI;
using Microsoft.JSInterop;
using Shared.Model.Base;
using Shared.Model.Online.Settings;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JinEnergy
{
    public static class AppInit
    {
        #region OnInit
        [JSInvokable("initial")]
        public static bool IsInitial() { return true; }

        public static string KitUid = "null";


        /// <param name="type">
        /// apk  - android
        /// web  - msx, браузер, etc
        /// cors - виджет или браузер с отключеным cors
        /// </param>
        /// <param name="conf">
        /// url   - ссылка на json с настройками
        /// </param>
        [JSInvokable("oninit")]
        async public static Task OnInit(string type, string urlconf)
        {
            if (type == "apk")
                IsAndrod = true;

            typeConf = type ?? "web";

            await LoadOrUpdateConfig(urlconf);

            var timer = new System.Timers.Timer(TimeSpan.FromMinutes(20));

            timer.Elapsed += async (s, e) => await LoadOrUpdateConfig(urlconf);

            timer.AutoReset = true;
            timer.Enabled = true;

            try
            {
                if (JSRuntime != null)
                {
                    string? result = await JSRuntime.InvokeAsync<string?>("httpReq", "https://github.com/", false, new
                    {
                        dataType = "text",
                        timeout = 5 * 1000,
                        returnHeaders = true
                    });

                    IsWorkReturnHeaders = result != null && result.Contains("currentUrl");
                }
            }
            catch { }
        }
        #endregion

        #region LoadOrUpdateConfig
        async static Task LoadOrUpdateConfig(string urlconf)
        {
            try
            {
                string? geo = await JsHttpClient.Get("https://rc.bwa.to/geo?select=country");
                if (geo != null)
                    Country = geo;

                if (!string.IsNullOrEmpty(urlconf))
                {
                    string? json = urlconf;
                    Shared.Model.AppInit? setings = null;

                    if (urlconf.StartsWith("http"))
                        json = await JsHttpClient.Get(urlconf + (urlconf.Contains("?") ? "&" : "?") + $"v={DateTime.Now.ToBinary()}");

                    if (json != null)
                    {
                        IsDefaultConf = Regex.IsMatch(urlconf, "/settings/(web|cors|apk)\\.json");
                        if (!IsDefaultConf)
                            KitUid = Regex.Match(urlconf, "&uid=([^&]+)").Groups[1].Value;

                        setings = JsonSerializer.Deserialize<Shared.Model.AppInit>(json);
                        if (setings != null)
                        {
                            conf = setings;

                            if (setings.corsehost != null)
                                Shared.Model.AppInit.corseuhost = setings.corsehost;

                            if (conf.Kodik.token != null)
                                conf.Kodik.token = conf.Kodik.token.Contains(":") ? conf.Kodik.Decrypt(conf.Kodik.token)! : conf.Kodik.token;

                            if (conf.VCDN.token != null)
                                conf.VCDN.token = conf.VCDN.token.Contains(":") ? conf.VCDN.Decrypt(conf.VCDN.token)! : conf.VCDN.token;

                            if (IsDefaultConf && geo == "RU")
                            {
                                conf.BongaCams.enable = false;
                                conf.Xvideos.overridehost = "https://rc.bwa.to/xds";
                                conf.Xnxx.overridehost = "https://rc.bwa.to/xnx";
                                conf.Ebalovo.overridehost = "https://rc.bwa.to/elo";
                                conf.HQporner.overridehost = "https://rc.bwa.to/hqr";
                                conf.Spankbang.overridehost = "https://rc.bwa.to/sbg";
                                conf.Xhamster.corseu = true;
                                conf.Porntrex.corseu = true;
                                conf.Eporner.corseu = true;
                                conf.Chaturbate.corseu = true;
                                conf.Kinotochka.corseu = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { log(ex.ToString()); }
        }
        #endregion


        static Shared.Model.AppInit conf = new Shared.Model.AppInit();

        public static IJSRuntime? JSRuntime;

        public static string log(string msg)
        {
            JSRuntime?.InvokeVoidAsync("console.log", "BWA", msg);
            return string.Empty;
        }

        public static Random random = new Random();


        public static bool IsAndrod { get; private set; }

        public static bool IsWorkReturnHeaders { get; private set; }

        public static bool IsDefaultConf { get; private set; } = true;

        public static string typeConf { get; private set; }

        public static string? Country { get; private set; }

        public static SisiConf sisi => conf.sisi;

        public static ApnConf? apn => conf.apn;

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


        public static OnlinesSettings Kinobase => conf.Kinobase;

        public static RezkaSettings Rezka => conf.Rezka;

        public static RezkaSettings Voidboost => conf.Voidboost;

        public static CollapsSettings Collaps => conf.Collaps;

        public static OnlinesSettings Ashdi => conf.Ashdi;

        public static OnlinesSettings Eneyida => conf.Eneyida;

        public static OnlinesSettings Kinoukr => conf.Kinoukr;

        public static OnlinesSettings Kinotochka => conf.Kinotochka;

        public static OnlinesSettings Redheadsound => conf.Redheadsound;

        public static OnlinesSettings iRemux => conf.iRemux;

        public static FilmixSettings Filmix => conf.Filmix;

        public static FilmixSettings FilmixPartner => conf.FilmixPartner;

        public static ZetflixSettings Zetflix => conf.Zetflix;

        public static OnlinesSettings VideoDB => conf.VideoDB;

        public static OnlinesSettings CDNmovies => conf.CDNmovies;

        public static OnlinesSettings CDNvideohub => conf.CDNvideohub;

        public static OnlinesSettings VDBmovies => conf.VDBmovies;


        public static OnlinesSettings VCDN => conf.VCDN;

        public static VokinoSettings VoKino => conf.VoKino;

        public static KinoPubSettings KinoPub => conf.KinoPub;

        public static KodikSettings Kodik => conf.Kodik;

        public static OnlinesSettings AnilibriaOnline => conf.AnilibriaOnline;

        public static OnlinesSettings Animevost => conf.Animevost;

        public static OnlinesSettings AniMedia => conf.AniMedia;

        public static OnlinesSettings Animebesst => conf.Animebesst;

        public static OnlinesSettings AnimeLib => conf.AnimeLib;

        public static OnlinesSettings MoonAnime => conf.MoonAnime;

        public static OnlinesSettings HDVB => conf.HDVB;
    }
}
