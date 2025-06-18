using Shared.Model.Templates;

namespace Shared.Model.Online.KinoPub
{
    public class SearchResult
    {
        public int id { get; set; }

        public SimilarTpl? similars { get; set; }
    }
}
