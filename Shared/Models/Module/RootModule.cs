using System.Reflection;

namespace Shared.Models.Module
{
    public class RootModule
    {
        public bool enable { get; set; }

        public int index { get; set; }

        public int version { get; set; }

        public string dll { get; set; }

        public string[] references { get; set; }

        public Assembly assembly { get; set; }


        public string @namespace { get; set; }

        public string initspace { get; set; }

        public string middlewares { get; set; }

        public string online { get; set; }

        public string sisi { get; set; }

        public string initialization { get; set; }

        public List<JacMod> jac { get; set; } = new List<JacMod>();


        public string NamespacePath(string val)
        {
            if (version >= 3 && !string.IsNullOrEmpty(@namespace))
                return $"{@namespace}.{val}";

            return val;
        }
    }
}
