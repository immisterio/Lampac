using System;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Caching.Memory;

namespace Shared.Engine.CORE
{
    public class GeoIP2
    {
        static DatabaseReader cityReader = new DatabaseReader("MaxMind/GeoLite2-City.mmdb");

        /// <param name="IP">IP пользователя</param>
        /// <returns>Страна UA,RU,BY,KZ</returns>
        public static string Country(string IP)
        {
            if (string.IsNullOrWhiteSpace(IP))
                return null;

            string memKey = $"GeoIP2:Country:{IP}";
            if (Startup.memoryCache.TryGetValue(memKey, out string _country))
                return _country;

            try
            {
                _country = cityReader.City(IP).Country.IsoCode.ToUpper();
                if (!string.IsNullOrWhiteSpace(_country))
                    Startup.memoryCache.Set(memKey, _country, DateTime.Now.AddDays(20));

                return _country;
            }
            catch { return null; }
        }
    }
}
