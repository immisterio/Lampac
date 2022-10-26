namespace Lampac.Models.LITE
{
    public class OnlinesSettings
    {
        public OnlinesSettings(string host, bool useproxy)
        {
            this.host = host;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public bool useproxy { get; set; }
    }
}
