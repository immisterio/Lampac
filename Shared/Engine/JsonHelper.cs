using Newtonsoft.Json;
using System.IO.Compression;

namespace Shared.Engine
{
    public static class JsonHelper
    {
        public static List<T> ListReader<T>(string filePath, int capacity = 0)
        {
            var items = new List<T>(capacity);

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    using (var reader = new StreamReader(gzipStream))
                    {
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            var serializer = new JsonSerializer();
                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.StartObject)
                                {
                                    try
                                    {
                                        items.Add(serializer.Deserialize<T>(jsonReader));
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }

            return items;
        }
    }
}
