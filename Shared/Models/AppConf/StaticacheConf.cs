namespace Shared.Models.AppConf
{
    public class StaticacheConf
    {
        public bool enable { get; set; }

        public List<StaticacheRoute> routes { get; set; } = new List<StaticacheRoute>();
    }


    public class StaticacheRoute
    {
        public string pathRex { get; set; }

        public int cacheMinutes { get; set; }

        public string contentType { get; set; }

        public string[] queryKeys { get; set; }
    }
}
