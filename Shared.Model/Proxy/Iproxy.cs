using Lampac.Models;

namespace Shared.Model.Proxy
{
    public interface Iproxy
    {
        public bool useproxy { get; set; }

        public string? globalnameproxy { get; set; }

        public ProxySettings? proxy { get; set; }
    }
}
