using System.Collections.Concurrent;
using System.Text;

namespace Shared.Engine.Pools
{
    public static class StringBuilderPool
    {
        static readonly ConcurrentBag<StringBuilder> _pool = new();

        public static int Count => _pool.Count;

        public static int Bytes => _pool.Sum(i => i.Capacity) * 2; // 1 char == 2 byte


        public static StringBuilder Rent()
        {
            if (_pool.TryTake(out var sb))
                return sb;

            return new StringBuilder(PoolInvk.rentLargeChunk);
        }

        public static void Return(StringBuilder sb)
        {
            sb.Clear();
            _pool.Add(sb);
        }
    }
}
