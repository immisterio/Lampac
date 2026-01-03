using System.Buffers;
using System.Globalization;

namespace Shared.Engine
{
    public static class StringConvert
    {
        #region FindStartText
        public static string FindStartText(string data, string end)
        {
            int endtIndex = data.IndexOf(end, StringComparison.Ordinal);
            if (endtIndex < 0)
                return null;

            return data.Substring(0, endtIndex);
        }
        #endregion

        #region FindLastText
        public static string FindLastText(string data, string start, string end = null)
        {
            if (data == null || start == null)
                return null;

            int startIndex = data.IndexOf(start, StringComparison.Ordinal);
            if (startIndex < 0)
                return null;

            if (end == null)
                return data.Substring(startIndex);

            int endIndex = data.IndexOf(end, startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
                return null;

            return data.Substring(startIndex, endIndex - startIndex);
        }
        #endregion

        #region Remove
        public static string Remove(string data, string start, string end)
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    int startIndex = data.IndexOf(start);
                    if (startIndex == 0)
                        break;


                    int endIndex = data.IndexOf(end);
                    if (endIndex == 0) {
                        data = data.Remove(startIndex);
                        break;
                    }
                    
                    data = data.Remove(startIndex, (endIndex - startIndex));
                }

                return data;
            }
            catch
            {
                return data;
            }
        }
        #endregion


        #region SearchName
        public static string SearchName(string val, string empty = null)
        {
            if (string.IsNullOrWhiteSpace(val))
                return empty;

            var s = val.AsSpan();

            // Верхняя граница — длина входа (после фильтрации будет <=)
            char[] rented = ArrayPool<char>.Shared.Rent(s.Length);
            int n = 0;

            try
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];

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
                    c = char.ToLower(c, CultureInfo.GetCultureInfo("ru-RU"));

                    // - ё -> е
                    // - щ -> ш
                    if (c == 'ё') c = 'е';
                    else if (c == 'щ') c = 'ш';

                    rented[n++] = c;
                }

                if (n == 0)
                    return empty;

                return new string(rented, 0, n);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
        #endregion
    }
}
