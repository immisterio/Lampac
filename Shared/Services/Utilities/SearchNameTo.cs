using System.Runtime.CompilerServices;

namespace Shared.Services.Utilities;

public static class SearchNameTo
{
    [ThreadStatic]
    static char[] _threadCharBuffer;
    const int _threadCharSize = 512; // 512 символов

    enum MatchOp
    {
        Equals,
        Contains,
        StartsWith,
        EndsWith
    }

    public static bool Equals(ReadOnlySpan<char> name, ReadOnlySpan<char> searchNormalized)
        => Invoke(name, searchNormalized, MatchOp.Equals);

    public static bool Contains(ReadOnlySpan<char> name, ReadOnlySpan<char> searchNormalized)
        => Invoke(name, searchNormalized, MatchOp.Contains);

    public static bool StartsWith(ReadOnlySpan<char> name, ReadOnlySpan<char> searchNormalized)
        => Invoke(name, searchNormalized, MatchOp.StartsWith);

    public static bool EndsWith(ReadOnlySpan<char> name, ReadOnlySpan<char> searchNormalized)
        => Invoke(name, searchNormalized, MatchOp.EndsWith);

    #region Convert
    public static string Convert(ReadOnlySpan<char> val, string empty = null)
    {
        if (val.IsEmpty)
            return empty;

        BufferCharPool _bufferChar = null;
        char[] _threadBuffer = null;

        if (val.Length > _threadCharSize)
            _bufferChar = new BufferCharPool(val.Length);
        else
            _threadBuffer = _threadCharBuffer ??= new char[_threadCharSize];

        Span<char> buffer = _bufferChar != null
            ? _bufferChar.Span
            : _threadBuffer;

        try
        {
            ReadOnlySpan<char> normalized = NormalizeTo(buffer, val);
            if (normalized.Length == 0)
                return empty;

            return new string(normalized);
        }
        finally
        {
            if (_bufferChar != null)
                _bufferChar.Dispose();
        }
    }
    #endregion

    #region Invoke
    static bool Invoke(ReadOnlySpan<char> name, ReadOnlySpan<char> searchNormalized, MatchOp op)
    {
        if (name.IsEmpty || searchNormalized.IsEmpty)
            return false;

        // Если нормализованная строка для поиска длиннее, чем name, то совпадений быть не может
        if (searchNormalized.Length > name.Length)
            return false;

        BufferCharPool _bufferChar = null;
        char[] _threadBuffer = null;

        if (name.Length > _threadCharSize)
            _bufferChar = new BufferCharPool(name.Length);
        else
            _threadBuffer = _threadCharBuffer ??= new char[_threadCharSize];

        Span<char> buffer = _bufferChar != null
            ? _bufferChar.Span
            : _threadBuffer;

        try
        {
            ReadOnlySpan<char> normalized = NormalizeTo(buffer, name);
            if (normalized.Length == 0)
                return false;

            return op switch
            {
                MatchOp.Equals =>
                    normalized.Length == searchNormalized.Length &&
                    normalized.Equals(searchNormalized, StringComparison.Ordinal),

                MatchOp.Contains =>
                    normalized.Contains(searchNormalized, StringComparison.Ordinal),

                MatchOp.StartsWith =>
                    normalized.StartsWith(searchNormalized, StringComparison.Ordinal),

                MatchOp.EndsWith =>
                    normalized.EndsWith(searchNormalized, StringComparison.Ordinal),

                _ => false
            };
        }
        finally
        {
            if (_bufferChar != null)
                _bufferChar.Dispose();
        }
    }
    #endregion

    #region NormalizeTo
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> NormalizeTo(Span<char> buffer, ReadOnlySpan<char> name)
    {
        int written = 0;

        for (int i = 0; i < name.Length; i++)
        {
            char c = char.ToLowerInvariant(name[i]);

            // Оставляем только латиницу/кириллицу/цифры
            // (a-zа-я0-9Ёё)
            bool ok =
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'z') ||
                (c >= 'а' && c <= 'я') ||
                c is 'Ё' or 'ё';

            if (!ok)
                continue;

            // ё -> е
            // щ -> ш
            if (c == 'ё')
                c = 'е';
            else if (c == 'щ')
                c = 'ш';

            buffer[written++] = c;
        }

        return buffer.Slice(0, written);
    }
    #endregion
}
