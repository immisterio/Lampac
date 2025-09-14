using LiteDB;

namespace TorrServer
{
    public class WhoseHashModel
    {
        [BsonId]
        public string id { get; set; }

        public string ip { get; set; }

        public string uid { get; set; }

        public string hash { get; set; }
    }
}
