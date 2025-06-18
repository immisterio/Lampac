using Lampac.Models.LITE.Ashdi;

namespace Shared.Model.Online.Eneyida
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public string? content { get; set; }

        public string? quel { get; set; }

        public List<Voice> serial { get; set; }

        public List<Similar>? similars { get; set; }
    }
}
