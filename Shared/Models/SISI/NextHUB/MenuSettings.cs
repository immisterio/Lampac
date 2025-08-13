namespace Shared.Model.SISI.NextHUB
{
    public class MenuSettings
    {
        public string eval { get; set; }

        public Dictionary<string, string> sort { get; set; }


        public Dictionary<string, string> categories { get; set; }

        public string formatcat(string cat)
        {
            if (categories != null && categories.TryGetValue("format", out string format))
                return format.Replace("{cat}", cat);

            return cat;
        }
    }
}
