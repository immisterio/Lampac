using Newtonsoft.Json.Linq;

namespace Shared.Models.AppConf
{
    public class KitConf
    {
        public bool enable { get; set; }

        public bool absolute { get; set; }

        public string path { get; set; }

        public string eval_path { get; set; }

        public bool IsAllUsersPath { get; set; }

        public int cacheToSeconds { get; set; }

        public int configCheckIntervalSeconds { get; set; }

        public bool rhub_fallback { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, JObject> allUsers { get; set; }
    }


    public record KitConfEvalPath(string path, string uid);


    public class KitCacheEntry
    {
        public JObject init { get; set; }
        public string infile { get; set; }
        public DateTime lockTime { get; set; }
        public DateTime lastWriteTimeUtc { get; set; }
    }
}
