namespace Shared.Models.SISI.NextHUB
{
    public class RegexMatchSettings
    {
        public string pattern { get; set; }

        public int index { get; set; } = 1;

        public string format { get; set; }

        public string[] matches { get; set; }
    }
}
