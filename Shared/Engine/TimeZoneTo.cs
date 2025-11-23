namespace Shared.Engine
{
    public static class TimeZoneTo
    {
        public static bool ByIds(string[] zones, out DateTime zoneTime)
        {
            zoneTime = DateTime.MinValue;

            foreach (var zone in zones)
            {
                if (ById(zone, out zoneTime))
                    return true;
            }

            return false;
        }

        public static bool ById(string zone, out DateTime zoneTime)
        {
            zoneTime = DateTime.MinValue;

            try
            {
                TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
                zoneTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                return true;
            }
            catch 
            {
                return false;
            }
        }
    }
}
