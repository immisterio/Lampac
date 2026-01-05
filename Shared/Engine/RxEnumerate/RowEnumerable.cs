namespace Shared.Engine.RxEnumerate
{
    public readonly ref struct RowEnumerable
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly List<Range> _ranges;
        public RowEnumerable(ReadOnlySpan<char> html, List<Range> ranges)
        {
            _html = html;
            _ranges = ranges;
        }
        public RowEnumerator GetEnumerator() => new RowEnumerator(_html, _ranges);
    }

    public ref struct RowEnumerator
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly List<Range> _ranges;
        private int _index;
        public RxRow Current { get; private set; }

        public RowEnumerator(ReadOnlySpan<char> html, List<Range> ranges)
        {
            _html = html;
            _ranges = ranges;
            _index = -1;
            Current = default;
        }

        public bool MoveNext()
        {
            _index++;
            if (_index >= _ranges.Count)
                return false;

            Range r = _ranges[_index];
            Current = new RxRow(_html, r);
            return true;
        }
    }
}
