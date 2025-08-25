using Shared.Models.Templates;

namespace Shared.Models.Online.Filmix
{
    public class SearchResult
    {
        public int id { get; set; }

        public SimilarTpl? similars { get; set; }
    }
}
