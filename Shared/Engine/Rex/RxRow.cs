using System.Text.RegularExpressions;

namespace Shared.Engine.RxEnumerate
{
    public readonly ref struct RxRow
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly int _start;
        private readonly int _end;
        private readonly string _pattern;

        internal RxRow(ReadOnlySpan<char> html, Range range, string pattern)
        {
            _html = html;
            _pattern = pattern;
            _start = range.Start.GetOffset(html.Length);
            _end = range.End.GetOffset(html.Length);
        }

        public int Start => _start;
        public int End => _end;

        public ReadOnlySpan<char> Span => _html.Slice(_start, _end - _start);

        public override string ToString() => new string(Span);

        private bool TryGetFirstMatchSpan(string pattern, RegexOptions options, out ReadOnlySpan<char> matchSpan)
        {
            var e = Regex.EnumerateMatches(Span, pattern, options);
            if (!e.MoveNext())
            {
                matchSpan = default;
                return false;
            }

            var vm = e.Current; // ValueMatch: Index + Length (относительно Span)
            matchSpan = Span.Slice(vm.Index, vm.Length);

            if (matchSpan.IsEmpty)
                return false;

            return true;
        }

        public GroupCollection Groups(RegexOptions options = RegexOptions.CultureInvariant)
            => Groups(_pattern, options);

        public GroupCollection Groups(string pattern, RegexOptions options = RegexOptions.CultureInvariant)
        {
            if (!TryGetFirstMatchSpan(pattern, options, out var matchSpan))
                return System.Text.RegularExpressions.Match.Empty.Groups;

            string segmentText = new string(matchSpan);
            var m = Regex.Match(segmentText, pattern, options);
            return m.Groups;
        }

        public string Match(string pattern, int index = 1, bool trim = false, RegexOptions options = RegexOptions.CultureInvariant)
        {
            if (!TryGetFirstMatchSpan(pattern, options, out var matchSpan))
                return null;

            string segmentText = new string(matchSpan);
            var m = Regex.Match(segmentText, pattern, options);

            if (!m.Success || index < 0 || index >= m.Groups.Count)
                return null;

            string value = m.Groups[index].Value;
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return trim ? value.Trim() : value;
        }

        public string Match(string pattern, string name, bool trim = false, RegexOptions options = RegexOptions.CultureInvariant)
        {
            if (!TryGetFirstMatchSpan(pattern, options, out var matchSpan))
                return null;

            string segmentText = new string(matchSpan);
            var m = Regex.Match(segmentText, pattern, options);

            if (!m.Success)
                return null;

            string value = m.Groups[name]?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return trim ? value.Trim() : value;
        }

        public bool Contains(string value, StringComparison comparison = StringComparison.Ordinal)
        {
            return Span.IndexOf(value.AsSpan(), comparison) >= 0;
        }

        public bool Contains(ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal)
        {
            return Span.IndexOf(value, comparison) >= 0;
        }
    }
}
