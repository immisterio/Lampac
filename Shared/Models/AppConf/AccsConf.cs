using System;
using System.Collections.Generic;

namespace Lampac.Models.AppConf
{
    public class AccsConf
    {
        public bool enable { get; set; }

        public string whitepattern { get; set; }

        public string premium_pattern { get; set; }

        public int maxip_hour { get; set; }

        public int maxrequest_hour { get; set; }

        public int maxlock_day { get; set; }

        public int blocked_hour { get; set; }

        public string authMesage { get; set; }

        public string denyMesage { get; set; }

        public string expiresMesage { get; set; }

        public Dictionary<string, DateTime> accounts { get; set; } = new Dictionary<string, DateTime>();
    }
}
