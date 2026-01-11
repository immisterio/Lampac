using System.Text;

namespace Shared.Engine.Utilities
{
    public static class OwnerTo
    {
        static object _lock = new();

        static char[] _buffer = new char[PoolInvk.rentCharMax];

        public static void Span(Stream ms, Encoding encoding, Action<ReadOnlySpan<char>> spanAction)
        {
            try
            {
                lock (_lock)
                {
                    int charCount = encoding.GetMaxCharCount((int)ms.Length);
                    if (charCount > _buffer.Length)
                        return;

                    using (var reader = new StreamReader(ms, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                    {
                        int actual = reader.Read(_buffer, 0, _buffer.Length);

                        spanAction(_buffer.AsSpan(0, actual));
                    }
                }
            }
            catch { }
        }
    }
}
