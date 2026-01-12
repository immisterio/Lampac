using System.Text.RegularExpressions;

namespace Shared.Engine.RxEnumerate
{
    public ref struct RxSplit
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly List<Range> _ranges = new List<Range>(100);
        private readonly string _pattern;

        public RxSplit(string pattern, ReadOnlySpan<char> html, int skip, RegexOptions options = RegexOptions.CultureInvariant)
        {
            _html = html;
            _pattern = pattern;
            int i = 0;

            foreach (Range r in Regex.EnumerateSplits(html, pattern, options))
            {
                if (i++ < skip)
                    continue;
                _ranges.Add(r);
            }
        }

        public int Count => _ranges.Count;

        public RowEnumerable Rows() => new RowEnumerable(_html, _ranges, _pattern);

        public RxRow this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_ranges.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return new RxRow(_html, _ranges[index], _pattern);
            }
        }
    }
}
