using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Shared.Engine.Pools
{
    public static class StringBuilderPool
    {
        static readonly ConcurrentBag<StringBuilder> _pool = new();


        public static readonly StringBuilder EmptyHtml = new StringBuilder();

        public static readonly StringBuilder EmptyJsonObject = new StringBuilder("{}");

        public static readonly StringBuilder EmptyJsonArray = new StringBuilder("[]");


        public static int rent => 32 * 1024; // 1 char == 2 byte (64кб, ниже LOH лимита ~85кб)

        public static int FreeCont => _pool.Count;

        public static int RentNew;

        public static int GC;


        public static StringBuilder Rent()
        {
            if (_pool.TryTake(out var sb))
                return sb;

            Interlocked.Increment(ref RentNew);
            return new StringBuilder(rent);
        }

        public static void Return(StringBuilder sb)
        {
            if (sb == null)
                return;

            if (sb.Capacity > PoolInvk.rentCharMax)
            {
                Interlocked.Increment(ref GC);
            }
            else if (sb.Capacity >= rent)
            {
                sb.Clear();
                _pool.Add(sb);
            }
        }
    }
}
