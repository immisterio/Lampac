namespace SISI.Controllers.Ebalovo
{
    public static class RootController
    {
        async public static ValueTask<string> goHost(string host)
        {
            if (!Regex.IsMatch(host, "^https?://www\\."))
                return host;

            var hybridCache = new HybridCache();
            string backhost = CrypTo.DecodeBase64("aHR0cHM6Ly93ZXQuZWJhbG92by5wb3Ju");

            string memkey = $"ebalovo:gohost:{host}";
            if (hybridCache.TryGetValue(memkey, out string _host))
                return _host;

            _host = await Http.GetLocation(host, timeoutSeconds: 5, allowAutoRedirect: true);
            if (_host != null && !Regex.IsMatch(_host, "^https?://www\\."))
            {
                _host = Regex.Replace(_host, "/$", "");
                hybridCache.Set(memkey, _host, DateTime.Now.AddHours(1));
                return _host;
            }
            else
            {
                hybridCache.Set(memkey, backhost, DateTime.Now.AddMinutes(20));
                return backhost;
            }
        }
    }
}
