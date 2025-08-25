using LiteDB;
using Shared.Models.SISI.Base;

namespace Shared.Models.SISI
{
    public class User
    {
        [BsonId]
        public string Id { get; set; }

        public List<PlaylistItem> Bookmarks { get; set; } = new();
    }
}
