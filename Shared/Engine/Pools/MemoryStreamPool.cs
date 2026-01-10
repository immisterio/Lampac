using System.Collections.Concurrent;

namespace Shared.Engine.Pools
{
    public static class MemoryStreamPool
    {
        static readonly ConcurrentBag<MemoryStream> _pool = new();

        public static int Count => _pool.Count;

        public static int Bytes => _pool.Sum(i => i.Capacity);


        public static MemoryStream Rent()
        {
            if (_pool.TryTake(out var memory))
                return memory;

            return new MemoryStream(PoolInvk.bufferSize);
        }

        public static void Return(MemoryStream memory)
        {
            memory.SetLength(0);
            memory.Position = 0;

            _pool.Add(memory);
        }
    }

    public static class MemoryStreamSmallPool
    {
        static readonly ConcurrentBag<MemoryStream> _pool = new();

        public static int Count => _pool.Count;

        public static int Bytes => _pool.Sum(i => i.Capacity);


        public static MemoryStream Rent()
        {
            if (_pool.TryTake(out var memory))
                return memory;

            return new MemoryStream(PoolInvk.rentChunk);
        }

        public static void Return(MemoryStream memory)
        {
            memory.SetLength(0);
            memory.Position = 0;

            _pool.Add(memory);
        }
    }
}
