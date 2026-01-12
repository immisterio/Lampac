namespace Shared.Engine.RxEnumerate
{
    public readonly ref struct RowEnumerable
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly List<Range> _ranges;
        private readonly string _pattern;

        public RowEnumerable(ReadOnlySpan<char> html, List<Range> ranges, string pattern)
        {
            _html = html;
            _ranges = ranges;
            _pattern = pattern;
        }

        public RowEnumerator GetEnumerator() => new RowEnumerator(_html, _ranges, _pattern);
    }

    public ref struct RowEnumerator
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly List<Range> _ranges;
        private int _index;
        private readonly string _pattern;
        public RxRow Current { get; private set; }

        public RowEnumerator(ReadOnlySpan<char> html, List<Range> ranges, string pattern)
        {
            _html = html;
            _ranges = ranges;
            _pattern = pattern;
            _index = -1;
            Current = default;
        }

        public bool MoveNext()
        {
            _index++;
            if (_index >= _ranges.Count)
                return false;

            Range r = _ranges[_index];
            Current = new RxRow(_html, r, _pattern);
            return true;
        }
    }
}
