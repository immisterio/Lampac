using System;
using System.Collections.Concurrent;

namespace Lampac.Models.AppConf
{
    public class AccsConf
    {
        public bool enable { get; set; }

        public string whitepattern { get; set; }

        public int maxiptohour { get; set; }

        public string cubMesage { get; set; }

        public string denyMesage { get; set; }

        public ConcurrentDictionary<string, DateTime> accounts { get; set; } = new ConcurrentDictionary<string, DateTime>();
    }
}
