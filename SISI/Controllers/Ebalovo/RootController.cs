using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using System.Text.RegularExpressions;
using System;

namespace Lampac.Controllers.Ebalovo
{
    public static class RootController
    {
        async public static Task<string> goHost(string host)
        {
            if (!Regex.IsMatch(host, "^https?://www\\."))
                return host;

            var hybridCache = new HybridCache();
            string backhost = CrypTo.DecodeBase64("aHR0cHM6Ly93ZXQuZWJhbG92by5wb3Ju");

            string memkey = $"ebalovo:gohost:{host}";
            if (hybridCache.TryGetValue(memkey, out string _host))
            {
                if (string.IsNullOrEmpty(_host))
                    return backhost;

                return _host;
            }

            _host = await HttpClient.GetLocation(host, timeoutSeconds: 5, allowAutoRedirect: true);
            if (_host != null)
            {
                _host = Regex.Replace(_host, "/$", "");
                hybridCache.Set(memkey, _host, DateTime.Now.AddMinutes(20));
                return _host;
            }
            else
            {
                hybridCache.Set(memkey, string.Empty, DateTime.Now.AddMinutes(1));
            }

            return backhost;
        }
    }
}
