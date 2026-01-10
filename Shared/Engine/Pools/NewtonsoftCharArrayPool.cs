using Newtonsoft.Json;
using System.Buffers;

namespace Shared.Engine.Utilities
{
    public static class NewtonsoftPool
    {
        public static readonly IArrayPool<char> Array = new NewtonsoftCharArrayPool();
    }

    public class NewtonsoftCharArrayPool : IArrayPool<char>
    {
        private readonly ArrayPool<char> _pool;

        public NewtonsoftCharArrayPool(ArrayPool<char> pool = null, bool clearOnReturn = false)
        {
            _pool = pool ?? ArrayPool<char>.Shared;
        }

        public char[] Rent(int minimumLength) => _pool.Rent(PoolInvk.Rent(minimumLength));

        public void Return(char[] array) => _pool.Return(array, clearArray: false);
    }
}
