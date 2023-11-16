using System;
using System.Collections.Generic;

namespace Lampac.Models.AppConf
{
    public class AccsConf
    {
        public bool enable { get; set; }

        public string whitepattern { get; set; }

        public int maxiptohour { get; set; }

        public string authMesage { get; set; }

        public string denyMesage { get; set; }

        public string expiresMesage { get; set; }

        public Dictionary<string, DateTime> accounts { get; set; } = new Dictionary<string, DateTime>();
    }
}
