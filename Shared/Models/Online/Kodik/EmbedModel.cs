using Shared.Models.Templates;

namespace Shared.Models.Online.Kodik
{
    public class EmbedModel
    {
        public SimilarTpl? stpl { get; set; }

        public List<Result> result { get; set; }
    }
}
