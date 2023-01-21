using System.Collections.Generic;

namespace Lampac.Models.AppConf
{
    public class AccsConf
    {
        public bool enable { get; set; }

        public string cubMesage { get; set; }

        public string denyMesage { get; set; }

        public HashSet<string> accounts { get; set; } = new HashSet<string>();
    }
}
