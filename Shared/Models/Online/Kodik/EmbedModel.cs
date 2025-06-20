using Shared.Model.Templates;

namespace Shared.Model.Online.Kodik
{
    public class EmbedModel
    {
        public SimilarTpl? stpl { get; set; }

        public List<Result>? result { get; set; }
    }
}
