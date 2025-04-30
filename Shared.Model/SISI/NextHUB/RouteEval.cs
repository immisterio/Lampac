namespace Shared.Model.SISI.NextHUB
{
    public class RouteEval
    {
        public RouteEval(string type, string data)
        {
            this.type = type;
            this.data = data;
        }

        public string type { get; set; }

        public string data { get; set; }
    }
}
