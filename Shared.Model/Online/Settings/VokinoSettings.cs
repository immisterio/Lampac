﻿using Shared.Model.Base;
using Shared.Model.Online.VoKino;

namespace Lampac.Models.LITE
{
    public class VokinoSettings : BaseSettings, ICloneable
    {
        public VokinoSettings(string plugin, string host, bool streamproxy, bool rip = false)
        {
            this.streamproxy = streamproxy;
            this.plugin = plugin;
            this.rip = rip;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public bool onlyBalancerName { get; set; }

        public ViewOnline online { get; set; } = new ViewOnline();


        public VokinoSettings Clone()
        {
            return (VokinoSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
