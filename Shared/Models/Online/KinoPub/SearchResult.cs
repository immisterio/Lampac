using Shared.Models.Templates;

namespace Shared.Models.Online.KinoPub
{
    public class SearchResult
    {
        public int id { get; set; }

        public SimilarTpl? similars { get; set; }
    }
}
