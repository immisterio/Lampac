namespace Lampac.Models.LITE.Ashdi
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public string content { get; set; } = null!;

        public List<Voice> serial { get; set; }
    }
}
