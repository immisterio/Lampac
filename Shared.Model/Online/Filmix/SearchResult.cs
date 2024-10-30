using Shared.Model.Templates;

namespace Shared.Model.Online.Filmix
{
    public class SearchResult
    {
        public int id { get; set; }

        public SimilarTpl? similars { get; set; }
    }
}
