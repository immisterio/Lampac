using Newtonsoft.Json;
using System.Collections;
using System.IO.Compression;

namespace Shared.Engine
{
    public static class JsonHelper
    {
        #region ListReader
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
        #endregion

        #region IEnumerableReader
        public static IEnumerable<T> IEnumerableReader<T>(string filePath)
        {
            if (!File.Exists(filePath))
                return Enumerable.Empty<T>();

            return new JsonItemEnumerable<T>(filePath);
        }
        #endregion


        #region [Codex AI] JsonItemEnumerable<T>
        private class JsonItemEnumerable<T> : IEnumerable<T>
        {
            readonly string filePath;

            public JsonItemEnumerable(string filePath)
            {
                this.filePath = filePath;
            }

            public IEnumerator<T> GetEnumerator() => new JsonItemEnumerator(filePath);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private class JsonItemEnumerator : IEnumerator<T>
            {
                readonly string filePath;
                readonly JsonSerializer serializer = new JsonSerializer();

                FileStream fileStream;
                GZipStream gzipStream;
                StreamReader reader;
                JsonTextReader jsonReader;

                public JsonItemEnumerator(string filePath)
                {
                    this.filePath = filePath;
                    Initialize();
                }

                public T Current { get; private set; }

                object IEnumerator.Current => Current;

                void Initialize()
                {
                    try
                    {
                        fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                        reader = new StreamReader(gzipStream);
                        jsonReader = new JsonTextReader(reader);
                    }
                    catch
                    {
                        Dispose();
                    }
                }

                public bool MoveNext()
                {
                    if (jsonReader == null)
                        return false;

                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType == JsonToken.StartObject)
                        {
                            try
                            {
                                Current = serializer.Deserialize<T>(jsonReader);
                                return true;
                            }
                            catch { }
                        }
                    }

                    Current = default;
                    return false;
                }

                public void Reset() => throw new NotSupportedException();

                public void Dispose()
                {
                    jsonReader?.Close();
                    reader?.Dispose();
                    gzipStream?.Dispose();
                    fileStream?.Dispose();
                }
            }
        }
        #endregion
    }
}
