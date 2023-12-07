using MaxMind.GeoIP2;

namespace Shared.Engine.CORE
{
    public class GeoIP2
    {
        static DatabaseReader cityReader = new DatabaseReader("GeoLite2-Country.mmdb");

        /// <param name="IP">IP пользователя</param>
        /// <returns>Страна UA,RU,BY,KZ</returns>
        public static string Country(string IP)
        {
            if (string.IsNullOrWhiteSpace(IP))
                return null;

            try
            {
                return cityReader.Country(IP).Country.IsoCode.ToUpper();
            }
            catch { return null; }
        }
    }
}
