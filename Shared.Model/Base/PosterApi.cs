﻿using System.Text.RegularExpressions;

namespace Shared.Model.Base
{
    public static class PosterApi
    {
        static string? omdbapi_key;
        static PosterApiConf? init;
        static IProxyLink? iproxy;

        public static void Initialization(string omdbkey, PosterApiConf conf, IProxyLink _iproxy)
        {
            omdbapi_key = omdbkey;
            init = conf;
            iproxy = _iproxy;
        }

        public static string? Find(long? kpid, string? imdb)
        {
            string? imdb_img = null, kp_img = null;

            if (!string.IsNullOrEmpty(omdbapi_key) && !string.IsNullOrEmpty(imdb))
                imdb_img = $"https://img.omdbapi.com/?apikey={omdbapi_key}&i={imdb}";

            if (kpid > 0)
                kp_img = $"https://st.kp.yandex.net/images/film_iphone/iphone360_{kpid}.jpg";

            if (imdb_img != null && kp_img != null)
                return Size($"{imdb_img} or {kp_img}");

            return Size(imdb_img ?? kp_img);
        }

        public static string? Size(string? uri)
        {
            if (string.IsNullOrEmpty(uri) || iproxy == null || init == null || !init.rsize || (init.width == 0 && init.height == 0))
                return uri?.Split(" or ")?[0];

            if (!string.IsNullOrEmpty(init.disable_rsize) && Regex.IsMatch(uri, init.disable_rsize, RegexOptions.IgnoreCase))
                return uri?.Split(" or ")?[0];

            if (!string.IsNullOrEmpty(init.bypass) && Regex.IsMatch(uri, init.bypass, RegexOptions.IgnoreCase))
                return $"{init.host}/proxyimg/{iproxy.Encrypt(uri, "posterapi", DateTime.Now.AddHours(1))}";

            return $"{init.host}/proxyimg:{init.width}:{init.height}/{iproxy.Encrypt(uri, "posterapi", DateTime.Now.AddHours(1))}";
        }
    }
}
