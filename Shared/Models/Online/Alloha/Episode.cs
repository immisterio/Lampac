namespace Shared.Models.Online.Alloha
{
    public struct Episode
    {
        public int episode { get; set; }

        public Dictionary<string, Translation> translation { get; set; }
    }
}