using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Shared.Engine.Utilities
{
    public static class JsonConvertPool
    {
        static readonly object _lockJson = new();

        static readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();

        static readonly StringBuilder _sb = new StringBuilder(PoolInvk.rentCharMax);


        public static string SerializeObject<T>(T value)
        {
            lock (_lockJson)
            {
                _sb.Clear();

                using (var sw = new StringWriter(_sb, CultureInfo.InvariantCulture))
                {
                    using (var jw = new JsonTextWriter(sw)
                    {
                        ArrayPool = NewtonsoftPool.Array
                    })
                    {
                        _serializer.Serialize(jw, value);
                        jw.Flush();

                        return _sb.ToString();
                    }
                }
            }
        }
    }
}
