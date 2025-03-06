﻿using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class FilmixSettings : BaseSettings, ICloneable
    {
        public FilmixSettings(string plugin, string host, bool enable = true)
        {
            this.enable = enable;
            this.plugin = plugin;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public string[]? tokens { get; set; }

        public bool pro { get; set; }

        public bool livehash { get; set; }

        public string? token_apitv { get; set; }

        public string? user_apitv { get; set; }

        public string? passwd_apitv { get; set; }


        public string? APIKEY { get; set; }

        public string? APISECRET { get; set; }

        public string? user_name { get; set; }

        public string? user_passw { get; set; }

        public string? lowlevel_api_passw { get; set; }


        public FilmixSettings Clone()
        {
            return (FilmixSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
