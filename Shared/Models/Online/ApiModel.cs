namespace Shared.Models.Online
{
    public class ApiModel
    {
        public string title { get; set; }

        public string stream_url { get; set; }

        public List<(string link, string quality)> streams { get; set; } = new List<(string link, string quality)>();

        public List<ApiModel> submenu { get; set; }

        /// <summary>
        /// voice
        /// season
        /// episode
        /// </summary>
        public string type { get; set; }
    }
}
