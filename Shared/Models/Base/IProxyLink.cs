namespace Shared.Models.Base
{
    public interface IProxyLink
    {
        public string Encrypt(string uri, string plugin, DateTime ex = default);
    }
}
