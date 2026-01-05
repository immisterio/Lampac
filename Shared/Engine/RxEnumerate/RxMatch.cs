using System.Text.RegularExpressions;

namespace Shared.Engine.RxEnumerate
{
    public ref struct RxMatch
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly List<Range> _ranges = new List<Range>(100);

        public RxMatch(string pattern, ReadOnlySpan<char> html, int skip, RegexOptions options = RegexOptions.CultureInvariant)
        {
            _html = html;
            int i = 0;

            foreach (var match in Regex.EnumerateMatches(html, pattern, options))
            {
                if (i++ < skip)
                    continue;

                int start = match.Index;
                int end = start + match.Length;
                _ranges.Add(new Range(start, end));
            }
        }

        public int Count => _ranges.Count;

        public RowEnumerable Rows() => new RowEnumerable(_html, _ranges);

        public RxRow First() => new RowEnumerable(_html, _ranges).GetEnumerator().Current;
    }
}
