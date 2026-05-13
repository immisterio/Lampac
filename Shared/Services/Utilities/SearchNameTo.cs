using System.Globalization;

namespace Shared.Services.Utilities;

public static class SearchNameTo
{
    enum MatchOp
    {
        Equals,
        Contains,
        StartsWith,
        EndsWith
    }

    #region static
    [ThreadStatic]
    static char[] _threadCharBuffer;
    const int _threadCharSize = 512; // 512 символов

    static readonly CultureInfo _cultureInfoSearchName = CultureInfo.GetCultureInfo("ru-RU");
    #endregion

    public static bool Equals(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, comparisonType, MatchOp.Equals);

    public static bool Contains(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, comparisonType, MatchOp.Contains);

    public static bool StartsWith(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, comparisonType, MatchOp.StartsWith);

    public static bool EndsWith(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, comparisonType, MatchOp.EndsWith);

    #region Convert
    public static string Convert(ReadOnlySpan<char> val, string empty = null)
    {
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
    static bool Invoke(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType, MatchOp op)
    {
        if (name.Length == 0 || search.Length == 0)
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
                    normalized.Equals(search, comparisonType),

                MatchOp.Contains =>
                    normalized.Contains(search, comparisonType),

                MatchOp.StartsWith =>
                    normalized.StartsWith(search, comparisonType),

                MatchOp.EndsWith =>
                    normalized.EndsWith(search, comparisonType),

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
    static ReadOnlySpan<char> NormalizeTo(Span<char> buffer, ReadOnlySpan<char> name)
    {
        int written = 0;

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            // Быстро пропускаем whitespace и прочие явные разделители
            if (char.IsWhiteSpace(c))
                continue;

            // Оставляем только латиницу/кириллицу/цифры
            // (a-zA-Zа-яА-Я0-9Ёё)
            bool ok =
                (c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= 'А' && c <= 'Я') ||
                (c >= 'а' && c <= 'я') ||
                c is 'Ё' or 'ё';

            if (!ok)
                continue;

            // lower
            c = char.ToLower(c, _cultureInfoSearchName);

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
