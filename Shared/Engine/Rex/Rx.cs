using System.Text.RegularExpressions;

namespace Shared.Engine.RxEnumerate
{
    public static class Rx
    {
        public static RxSplit Split(string pattern, ReadOnlySpan<char> html, int skip = 0, RegexOptions options = RegexOptions.CultureInvariant)
            => new RxSplit(pattern, html, skip, options);

        public static RxSplit Split(string pattern, string html, int skip = 0, RegexOptions options = RegexOptions.CultureInvariant)
            => new RxSplit(pattern, html.AsSpan(), skip, options);

        public static RxMatch Matches(string pattern, ReadOnlySpan<char> html, int skip = 0, RegexOptions options = RegexOptions.CultureInvariant)
            => new RxMatch(pattern, html, skip, options);

        public static RxMatch Matches(string pattern, string html, int skip = 0, RegexOptions options = RegexOptions.CultureInvariant)
            => new RxMatch(pattern, html.AsSpan(), skip, options);

        public static string Match(ReadOnlySpan<char> html, string pattern, int index = 1, RegexOptions options = RegexOptions.CultureInvariant)
        {
            if (html.IsEmpty)
                return null;

            var m = new RxMatch(pattern, html, 0, options);
            if (m.Count == 0)
                return null;

            return m[0].Match(pattern, index, false, options);
        }

        public static GroupCollection Groups(ReadOnlySpan<char> html, string pattern, RegexOptions options = RegexOptions.CultureInvariant)
        {
            if (html.IsEmpty)
                return null;

            var m = new RxMatch(pattern, html, 0, options);
            if (m.Count == 0)
                return null;

            return m[0].Groups(pattern, options);
        }
    }
}
