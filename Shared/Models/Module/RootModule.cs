using System.Collections.Generic;
using System.Reflection;

namespace Lampac.Models.Module
{
    public class RootModule
    {
        public bool enable { get; set; }

        public string dll { get; set; }

        public string initspace { get; set; }

        public string middlewares { get; set; }

        public string online { get; set; }

        public string sisi { get; set; }

        public Assembly assembly { get; set; }

        //public OnlineMod online { get; set; } = new OnlineMod();

        //public List<SisiMod> sisi { get; set; } = new List<SisiMod>();

        public List<JacMod> jac { get; set; } = new List<JacMod>();
    }
}
