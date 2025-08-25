namespace Shared.Models.Base
{
    public interface Icors
    {
        public string host { get; set; }

        public bool corseu { get; set; }

        public string webcorshost { get; set; }


        public string corsHost();

        public string cors(string uri);
    }
}
