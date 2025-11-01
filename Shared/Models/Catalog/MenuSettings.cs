namespace Shared.Models.Catalog
{
    public class MenuSettings
    {
        public Dictionary<string, string> categories { get; set; }

        public Dictionary<string, string> sort { get; set; }

        public Dictionary<string, string> format { get; set; }

        public string defaultName { get; set; }

        public string catalog { get; set; }
    }
}
