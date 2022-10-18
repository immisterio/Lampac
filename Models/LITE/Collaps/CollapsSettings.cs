namespace Lampac.Models.LITE.Collaps
{
    public class CollapsSettings
    {
        public CollapsSettings(string host, bool useproxy)
        {
            this.host = host;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public bool useproxy { get; set; }
    }
}
