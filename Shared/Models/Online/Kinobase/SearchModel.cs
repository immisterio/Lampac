using Shared.Models.Templates;

namespace Shared.Models.Online.Kinobase
{
    public class SearchModel
    {
        public string link { get; set; }

        public SimilarTpl? similar { get; set; }
    }
}
