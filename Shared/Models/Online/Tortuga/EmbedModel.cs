namespace Lampac.Models.LITE.Tortuga
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public string content { get; set; } = null!;

        public List<Voice> serial { get; set; }
    }
}
