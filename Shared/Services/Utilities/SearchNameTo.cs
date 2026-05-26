using System.Runtime.CompilerServices;

namespace Shared.Services.Utilities;

public static class SearchNameTo
{
    const int _stackCharSize = 256;

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

        Span<char> _stbf = val.Length > _stackCharSize
            ? Span<char>.Empty
            : stackalloc char[val.Length];

        BufferCharPool _bufferChar = null;
        if (_stbf == Span<char>.Empty)
            _bufferChar = new BufferCharPool(val.Length);

        Span<char> buffer = _bufferChar != null
            ? _bufferChar.Span
            : _stbf;

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

        Span<char> _stbf = name.Length > _stackCharSize
            ? Span<char>.Empty
            : stackalloc char[name.Length];

        BufferCharPool _bufferChar = null;
        if (_stbf == Span<char>.Empty)
            _bufferChar = new BufferCharPool(name.Length);

        Span<char> buffer = _bufferChar != null
            ? _bufferChar.Span
            : _stbf;

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
