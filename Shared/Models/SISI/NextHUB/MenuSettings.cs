namespace Shared.Models.SISI.NextHUB
{
    public class MenuSettings
    {
        public bool bind { get; set; } = true;

        public Dictionary<string, string> route { get; set; }

        public Dictionary<string, string> sort { get; set; }

        public Dictionary<string, string> categories { get; set; }

        public string formatcat(string cat)
        {
            if (categories != null && categories.TryGetValue("format", out string format))
                return format.Replace("{cat}", cat);

            return cat;
        }

        public List<CustomCategories> customs { get; set; }
    }
}
