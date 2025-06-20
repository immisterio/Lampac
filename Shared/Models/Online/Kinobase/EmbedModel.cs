using Lampac.Models.LITE.Kinobase;

namespace Shared.Model.Online.Kinobase
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public string? content { get; set; }

        public List<Season>? serial { get; set; }

        public string quality { get; set; }
    }
}
