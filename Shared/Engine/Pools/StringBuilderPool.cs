using System.Collections.Concurrent;
using System.Text;

namespace Shared.Engine.Pools
{
    public static class StringBuilderPool
    {
        static readonly ConcurrentBag<StringBuilder> _pool = new();

        public static int Count => _pool.Count;

        public static int GC { get; private set; }

        static int rentMax => PoolInvk.Rent(1024 * 1024);


        public static StringBuilder Rent()
        {
            if (_pool.TryTake(out var sb))
                return sb;

            return new StringBuilder(rentMax);
        }

        public static void Return(StringBuilder sb)
        {
            if (rentMax >= sb.Capacity)
            {
                sb.Clear();
                _pool.Add(sb);
            }
            else
            {
                GC++;
            }
        }
    }
}
