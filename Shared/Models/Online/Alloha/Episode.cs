namespace Shared.Models.Online.Alloha
{
    public class Episode
    {
        public int episode { get; set; }

        public Dictionary<string, Translation> translation { get; set; }
    }
}