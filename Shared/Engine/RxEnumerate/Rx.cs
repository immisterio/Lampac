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
            var m = new RxMatch(pattern, html, 0, options);
            if (m.Count == 0)
                return null;

            return m.Rows().GetEnumerator().Current.Match(pattern, index, false, options);
        }
    }
}
