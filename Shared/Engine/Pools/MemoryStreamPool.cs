using System.Collections.Concurrent;

namespace Shared.Engine.Pools
{
    public static class MemoryStreamPool
    {
        static readonly ConcurrentBag<MemoryStream> _pool = new();

        public static int Count => _pool.Count;

        public static int GC { get; private set; }


        public static MemoryStream Rent()
        {
            if (_pool.TryTake(out var memory))
                return memory;

            return new MemoryStream(PoolInvk.rentMax);
        }

        public static void Return(MemoryStream memory)
        {
            if (PoolInvk.rentMax >= memory.Capacity)
            {
                memory.SetLength(0);
                memory.Position = 0;

                _pool.Add(memory);
            }
            else
            {
                GC++;
            }
        }
    }
}
