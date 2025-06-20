using System.Text.Json;

namespace Shared.Engine.CORE
{
    public class BookmarkCache<T> where T : class, new()
    {
        #region BookmarkCache
        string path;
        string md5user;

        public BookmarkCache(string path, string md5user)
        {
            this.path = path;
            this.md5user = md5user;
        }

        static Dictionary<string, List<T>> db = new Dictionary<string, List<T>>();

        string pathfile => $"cache/bookmarks/{path}/{md5user.Substring(0, 2)}/{md5user.Substring(2)}";
        #endregion

        #region Read
        public List<T> Read()
        {
            try
            {
                string key = $"{path}:{md5user}";

                if (db.TryGetValue(key, out List<T>? val))
                    return val;

                if (File.Exists(pathfile))
                {
                    val = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(pathfile));
                    if (val != null)
                    {
                        db.TryAdd(key, val);
                        return val;
                    }
                }
            }
            catch { }

            return new List<T>();
        }
        #endregion

        #region Write
        public void Write(List<T> val)
        {
            try
            {
                Directory.CreateDirectory($"cache/bookmarks/{path}");
                Directory.CreateDirectory($"cache/bookmarks/{path}/{md5user.Substring(0, 2)}");
                File.WriteAllText(pathfile, JsonSerializer.Serialize(val));
            }
            catch { }
        }
        #endregion
    }
}
