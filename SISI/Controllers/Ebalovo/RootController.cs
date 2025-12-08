using System.Net;

namespace SISI.Controllers.Ebalovo
{
    public static class RootController
    {
        async public static ValueTask<string> goHost(string host, WebProxy proxy = null)
        {
            if (!Regex.IsMatch(host, "^https?://www\\."))
                return host;

            var hybridCache = new HybridCache();
            string backhost = "https://web.epalovo.com";

            string memkey = $"ebalovo:gohost:{host}";
            if (hybridCache.TryGetValue(memkey, out string _host, inmemory: true))
                return _host;

            _host = await Http.GetLocation(host, timeoutSeconds: 5, proxy: proxy, allowAutoRedirect: true);
            if (_host != null && !Regex.IsMatch(_host, "^https?://www\\."))
            {
                _host = Regex.Replace(_host, "/$", "");
                hybridCache.Set(memkey, _host, DateTime.Now.AddHours(1), inmemory: true);
                return _host;
            }
            else
            {
                hybridCache.Set(memkey, backhost, DateTime.Now.AddMinutes(20), inmemory: true);
                return backhost;
            }
        }
    }
}
