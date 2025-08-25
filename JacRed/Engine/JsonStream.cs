using Newtonsoft.Json;
using System.IO.Compression;

namespace JacRed.Engine.CORE
{
    public static class JsonStream
    {
        #region Read
        public static T Read<T>(string path)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Error = (se, ev) => { ev.ErrorContext.Handled = true; }
                };

                var serializer = JsonSerializer.Create(settings);

                using (Stream file = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
                {
                    using (var sr = new StreamReader(file))
                    {
                        using (var jsonTextReader = new JsonTextReader(sr))
                        {
                            return serializer.Deserialize<T>(jsonTextReader);
                        }
                    }
                }
            }
            catch { return default; }
        }
        #endregion

        #region Write
        public static void Write(string path, object db)
        {
            try
            {
                //var settings = new JsonSerializerSettings()
                //{
                //    Formatting = Formatting.Indented
                //};

                var serializer = JsonSerializer.Create(); // settings

                using (var sw = new StreamWriter(new GZipStream(File.OpenWrite(path), CompressionMode.Compress)))
                {
                    using (var jsonTextWriter = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(jsonTextWriter, db);
                    }
                }
            }
            catch { }
        }
        #endregion
    }
}
