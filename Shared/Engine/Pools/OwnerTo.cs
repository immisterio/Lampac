using System.Text;

namespace Shared.Engine.Utilities
{
    public static class OwnerTo
    {
        static object _lock = new();

        // 10 MB (char = 2 байта)
        static int maxchars = 5 * 1024 * 1024;
        static char[] _buffer = new char[1024 * 1024];

        static readonly int[] sizechars =
        {
            2 * 1024 * 1024,
            3 * 1024 * 1024,
            4 * 1024 * 1024,
            maxchars
        };

        public static void Span(Stream ms, Encoding encoding, Action<ReadOnlySpan<char>> spanAction)
        {
            try
            {
                lock (_lock)
                {
                    int charCount = encoding.GetMaxCharCount((int)ms.Length);
                    if (charCount > maxchars)
                        throw new ArgumentException("large");

                    if (charCount > _buffer.Length)
                    {
                        for (int i = 0; i < sizechars.Length; i++)
                        {
                            if (sizechars[i] >= charCount)
                                _buffer = new char[sizechars[i]];
                        }
                    }

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
