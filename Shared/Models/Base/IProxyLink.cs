namespace Shared.Models.Base
{
    public interface IProxyLink
    {
        public string Encrypt(string uri, string plugin, DateTimeOffset ex = default);
    }
}
