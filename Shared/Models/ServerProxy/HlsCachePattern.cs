namespace Shared.Models.ServerProxy
{
    public class HlsCachePattern
    {
        /// <summary>
        /// match
        /// replace
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// match[index]
        /// </summary>
        public int index { get; set; }

        public string pattern { get; set; }

        public string replacement { get; set; }
    }
}
