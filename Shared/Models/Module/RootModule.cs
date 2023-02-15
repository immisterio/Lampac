using System.Reflection;

namespace Lampac.Models.Module
{
    public class RootModule
    {
        public bool enable { get; set; }

        public string dll { get; set; }

        public Assembly assembly { get; set; }

        public OnlineMod online { get; set; } = new OnlineMod();

        public SisiMod sisi { get; set; } = new SisiMod();

        public JacMod jac { get; set; } = new JacMod();
    }
}
