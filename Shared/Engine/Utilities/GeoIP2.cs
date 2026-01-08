using MaxMind.GeoIP2;

namespace Shared.Engine
{
    public class GeoIP2
    {
        static DatabaseReader cityReader = null;

        static GeoIP2()
        {
            if (File.Exists("data/GeoLite2-Country.mmdb"))
                cityReader = new DatabaseReader("data/GeoLite2-Country.mmdb");
        }

        /// <param name="IP">IP пользователя</param>
        /// <returns>Страна UA,RU,BY,KZ</returns>
        public static string Country(string IP)
        {
            if (string.IsNullOrEmpty(IP) || cityReader == null)
                return null;

            try
            {
                return cityReader.Country(IP).Country.IsoCode.ToUpper();
            }
            catch { return null; }
        }
    }
}
