using System.Buffers;
using System.Threading;
using HtmlKit;

namespace Shared.Engine
{
    public enum HtmlSpanTargetType
    {
        Exact = 0,
        Contains = 1
    }

    public static class HtmlSpan
    {
        private static readonly ThreadLocal<State> _state = new(() => new State());

        public static ReadOnlySpan<char> Node(
            ReadOnlySpan<char> html,
            string element,
            string attribute,
            string target,
            HtmlSpanTargetType targetType)
        {
            if (html.IsEmpty)
                return ReadOnlySpan<char>.Empty;

            foreach (var span in Nodes(html, element, attribute, target, targetType))
                return span;

            return ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Возвращает все совпадения (в порядке обхода), как ReadOnlySpan<char> для foreach.
        /// Важно: совпадения НЕ пересекаются. Вложенные совпадения внутри уже возвращенного узла не выдаются.
        /// </summary>
        public static NodesEnumerable Nodes(
            ReadOnlySpan<char> html,
            string element,
            string attribute,
            string target,
            HtmlSpanTargetType targetType)
            => new NodesEnumerable(html, element, attribute, target, targetType);

        public readonly ref struct NodesEnumerable
        {
            private readonly ReadOnlySpan<char> _html;
            private readonly string _element;
            private readonly string _attribute;
            private readonly string _target;
            private readonly HtmlSpanTargetType _targetType;

            public NodesEnumerable(ReadOnlySpan<char> html, string element, string attribute, string target, HtmlSpanTargetType targetType)
            {
                _html = html;
                _element = element;
                _attribute = attribute;
                _target = target;
                _targetType = targetType;
            }

            public Enumerator GetEnumerator() => new Enumerator(_html, _element, _attribute, _target, _targetType);
        }

        public ref struct Enumerator
        {
            private readonly ReadOnlySpan<char> _html;
            private readonly string _element;
            private readonly string _attribute;
            private readonly string _target;
            private readonly HtmlSpanTargetType _targetType;

            private readonly bool _anyElement;

            private readonly State _st;
            private HtmlTokenizer _tokenizer;

            private bool _inCapture;
            private int _captureStartIndex;
            private int _depth;
            private string? _captureTagName;

            private ReadOnlySpan<char> _current;

            public ReadOnlySpan<char> Current => _current;

            public Enumerator(ReadOnlySpan<char> html, string element, string attribute, string target, HtmlSpanTargetType targetType)
            {
                _html = html;
                _element = element;
                _attribute = attribute;
                _target = target;
                _targetType = targetType;

                _anyElement = element.Length == 1 && element[0] == '*';

                _st = _state.Value!;
                _st.EnsureCapacity(html.Length);
                html.CopyTo(_st.Buffer);
                _st.Reader.Reset(_st.Buffer, html.Length);

                _tokenizer = new HtmlTokenizer(_st.Reader)
                {
                    DecodeCharacterReferences = false,
                    IgnoreTruncatedTags = true
                };

                _inCapture = false;
                _captureStartIndex = -1;
                _depth = 0;
                _captureTagName = null;
                _current = ReadOnlySpan<char>.Empty;
            }

            public bool MoveNext()
            {
                _current = ReadOnlySpan<char>.Empty;

                if (_html.IsEmpty)
                    return false;

                HtmlToken token;
                while (_tokenizer.ReadNextToken(out token))
                {
                    if (token.Kind != HtmlTokenKind.Tag)
                        continue;

                    var tag = (HtmlTagToken)token;

                    int endPos = _st.Reader.Position; // позиция сразу после '>'
                    int tagStart = LastIndexOf(_st.Buffer, '<', endPos - 1);
                    if (tagStart < 0)
                        continue;

                    if (!_inCapture)
                    {
                        if (tag.IsEndTag)
                            continue;

                        if (!_anyElement && !EqualsOrdinalIgnoreCase(tag.Name, _element))
                            continue;

                        if (!TagHasAttributeMatch(tag, _attribute, _target, _targetType))
                            continue;

                        // Начинаем захват узла
                        _inCapture = true;
                        _captureStartIndex = tagStart;
                        _captureTagName = tag.Name;

                        if (tag.IsEmptyElement)
                        {
                            // Самозакрывающийся: возвращаем сразу
                            _inCapture = false;
                            _captureTagName = null;
                            _depth = 0;

                            _current = _html.Slice(_captureStartIndex, endPos - _captureStartIndex);
                            return true;
                        }

                        _depth = 1;
                        continue;
                    }
                    else
                    {
                        // Дочитываем до закрытия захваченного тега с учетом вложенности одноименных тегов
                        if (_captureTagName is null)
                            return false;

                        if (!EqualsOrdinalIgnoreCase(tag.Name, _captureTagName))
                            continue;

                        if (tag.IsEndTag)
                        {
                            _depth--;
                            if (_depth == 0)
                            {
                                int start = _captureStartIndex;
                                int len = endPos - start;

                                _inCapture = false;
                                _captureStartIndex = -1;
                                _captureTagName = null;
                                _depth = 0;

                                _current = _html.Slice(start, len);
                                return true;
                            }
                        }
                        else
                        {
                            if (!tag.IsEmptyElement)
                                _depth++;
                        }
                    }
                }

                return false;
            }
        }

        private static bool TagHasAttributeMatch(HtmlTagToken tag, string attribute, string target, HtmlSpanTargetType targetType)
        {
            foreach (var a in tag.Attributes)
            {
                if (!EqualsOrdinalIgnoreCase(a.Name, attribute))
                    continue;

                var value = a.Value ?? string.Empty;

                return targetType switch
                {
                    HtmlSpanTargetType.Exact => string.Equals(value, target, StringComparison.Ordinal),
                    HtmlSpanTargetType.Contains => value.IndexOf(target, StringComparison.Ordinal) >= 0,
                    _ => false
                };
            }

            return false;
        }

        private static bool EqualsOrdinalIgnoreCase(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int LastIndexOf(char[] buffer, char c, int fromInclusive)
        {
            for (int i = fromInclusive; i >= 0; i--)
            {
                if (buffer[i] == c)
                    return i;
            }
            return -1;
        }

        private sealed class State
        {
            public char[] Buffer { get; private set; } = ArrayPool<char>.Shared.Rent(PoolInvk.rentLargeChunk);
            public ReusableCharArrayTextReader Reader { get; } = new();

            public void EnsureCapacity(int requiredLength)
            {
                if (Buffer.Length >= requiredLength)
                    return;

                var newBuf = ArrayPool<char>.Shared.Rent(PoolInvk.Rent(requiredLength));
                ArrayPool<char>.Shared.Return(Buffer, clearArray: false);
                Buffer = newBuf;
            }
        }

        private sealed class ReusableCharArrayTextReader : TextReader
        {
            private char[] _buffer = Array.Empty<char>();
            private int _length;
            private int _pos;

            public int Position => _pos;

            public void Reset(char[] buffer, int length)
            {
                _buffer = buffer;
                _length = length;
                _pos = 0;
            }

            public override int Peek() => _pos >= _length ? -1 : _buffer[_pos];

            public override int Read() => _pos >= _length ? -1 : _buffer[_pos++];

            public override int Read(char[] buffer, int index, int count)
            {
                if (_pos >= _length)
                    return 0;

                int remaining = _length - _pos;
                int toCopy = remaining < count ? remaining : count;

                Array.Copy(_buffer, _pos, buffer, index, toCopy);
                _pos += toCopy;
                return toCopy;
            }
        }
    }
}
