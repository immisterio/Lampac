using LiteDB;

namespace Shared.Models.Base
{
    public class UserSync
    {
        [BsonId]
        public string id { get; set; }

        public Dictionary<string, Dictionary<string, string>> timecodes { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    }
}
