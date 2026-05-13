using System.Globalization;
using System.Text;

namespace Shared.Services.Utilities;

public static class SearchNameTo
{
    #region static
    [ThreadStatic]
    static char[] _threadCharBuffer;
    const int _threadCharSize = 512; // 512 символов

    static readonly CultureInfo _cultureInfoSearchName = CultureInfo.GetCultureInfo("ru-RU");
    #endregion

    public static bool Equals(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, (n, s) => n.Equals(s, comparisonType));

    public static bool Contains(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, (n, s) => n.Contains(s, comparisonType));

    public static bool StartsWith(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, (n, s) => n.StartsWith(s, comparisonType));

    public static bool EndsWith(ReadOnlySpan<char> name, ReadOnlySpan<char> search, StringComparison comparisonType = StringComparison.Ordinal)
        => Invoke(name, search, (n, s) => n.EndsWith(s, comparisonType));

    #region Convert
    public static string Convert(ReadOnlySpan<char> val, string empty = null)
    {
        var sb = Builder(val);

        if (sb.Length == 0)
            return empty;

        return sb.ToString();
    }
    #endregion

    #region Invoke
    public static bool Invoke(ReadOnlySpan<char> name, ReadOnlySpan<char> search, Func<ReadOnlySpan<char>, ReadOnlySpan<char>, bool> predicate)
    {
        if (name == ReadOnlySpan<char>.Empty || search == ReadOnlySpan<char>.Empty)
            return false;

        if (name.Length == 0 || search.Length == 0)
            return false;

        var sb = Builder(name);

        if (sb.Length == 0)
            return false;

        if (sb.Length > _threadCharSize)
        {
            using (var charBuf = new BufferCharPool(sb.Length))
            {
                Span<char> buffer = charBuf.Span;
                sb.CopyTo(0, buffer, sb.Length);

                return predicate.Invoke(buffer.Slice(0, sb.Length), search);
            }
        }
        else
        {
            char[] _threadBuffer = _threadCharBuffer ??= new char[_threadCharSize];
            Span<char> buffer = _threadBuffer;
            sb.CopyTo(0, buffer, sb.Length);

            return predicate.Invoke(buffer.Slice(0, sb.Length), search);
        }
    }
    #endregion

    #region Builder
    static StringBuilder Builder(ReadOnlySpan<char> name)
    {
        var sb = StringBuilderPool.ThreadInstance;

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

            // - lower
            c = char.ToLower(c, _cultureInfoSearchName);

            // - ё -> е
            // - щ -> ш
            if (c == 'ё') c = 'е';
            else if (c == 'щ') c = 'ш';

            sb.Append(c);
        }

        return sb;
    }
    #endregion
}
