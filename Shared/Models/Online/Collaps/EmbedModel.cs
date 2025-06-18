using Lampac.Models.LITE.Collaps;

namespace Shared.Model.Online.Collaps
{
    public class EmbedModel
    {
        public string content { get; set; } = null!;

        public List<RootObject> serial { get; set; }
    }
}
