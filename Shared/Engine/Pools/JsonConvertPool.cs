using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Shared.Engine.Utilities
{
    public static class JsonConvertPool
    {
        static readonly ThreadLocal<JsonSerializer> _serializer = new ThreadLocal<JsonSerializer>(JsonSerializer.CreateDefault);

        static readonly ThreadLocal<StringBuilder> _cachedSb = new ThreadLocal<StringBuilder>(() => new StringBuilder(PoolInvk.rentMax / 2));

        public static int Count => _cachedSb.IsValueCreated ? _cachedSb.Values.Count : 0;


        public static string SerializeObject<T>(T value)
        {
            var sb = _cachedSb.Value;
            sb.Clear();

            using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
            {
                using (var jw = new JsonTextWriter(sw)
                {
                    ArrayPool = NewtonsoftPool.Array
                })
                {
                    _serializer.Value.Serialize(jw, value);
                    jw.Flush();

                    return sb.ToString();
                }
            }
        }
    }
}
