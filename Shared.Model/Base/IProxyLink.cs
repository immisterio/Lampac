namespace Shared.Model.Base
{
    public interface IProxyLink
    {
        public string Encrypt(string uri, DateTime ex = default);
    }
}
