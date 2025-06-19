namespace Shared.Model.Base
{
    public interface IProxyLink
    {
        public string Encrypt(in string uri, in string plugin, DateTime ex = default);
    }
}
