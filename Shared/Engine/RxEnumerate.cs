using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public readonly ref struct RxEnumerate
    {
        private readonly ReadOnlySpan<char> _html;
        private readonly string _splitPattern;
        private readonly int _skip;
        private readonly RegexOptions _options;

        public RxEnumerate(
            string delimiterLiteral,
            ReadOnlySpan<char> html,
            int skip = 0,
            RegexOptions options = RegexOptions.CultureInvariant)
        {
            _html = html;
            _splitPattern = delimiterLiteral;
            _skip = skip;
            _options = options;
        }

        public int Count()
        {
            int i = 0;
            int count = 0;

            foreach (Range _ in Regex.EnumerateSplits(_html, _splitPattern, _options))
            {
                if (i++ < _skip)
                    continue;

                count++;
            }

            return count;
        }

        public RowEnumerable Rows() => new RowEnumerable(_html, _splitPattern, _skip, _options);

        public readonly ref struct RowEnumerable
        {
            private readonly ReadOnlySpan<char> _html;
            private readonly string _splitPattern;
            private readonly int _skip;
            private readonly RegexOptions _options;

            public RowEnumerable(ReadOnlySpan<char> html, string splitPattern, int skip, RegexOptions options)
            {
                _html = html;
                _splitPattern = splitPattern;
                _skip = skip;
                _options = options;
            }

            public RowEnumerator GetEnumerator() => new RowEnumerator(_html, _splitPattern, _skip, _options);
        }

        public ref struct RowEnumerator
        {
            private readonly ReadOnlySpan<char> _html;
            private Regex.ValueSplitEnumerator _enumerator;
            private int _skip;
            private string _current;

            public RowEnumerator(ReadOnlySpan<char> html, string splitPattern, int skip, RegexOptions options)
            {
                _html = html;
                _enumerator = Regex.EnumerateSplits(html, splitPattern, options);
                _skip = skip;
                _current = null;
            }

            public string Current => _current!;

            public bool MoveNext()
            {
                while (_enumerator.MoveNext())
                {
                    if (_skip > 0)
                    {
                        _skip--;
                        continue;
                    }

                    Range r = _enumerator.Current;     // <-- это Range (например 14546..16406)
                    _current = _html[r].ToString();    // <-- вырезаем фрагмент и получаем string
                    return true;
                }

                return false;
            }
        }
    }
}
