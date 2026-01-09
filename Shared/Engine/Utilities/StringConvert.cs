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
        static readonly char[] _rentedSearchName = new char[PoolInvk.rentLargeChunk];

        static readonly CultureInfo _cultureInfoSearchName = CultureInfo.GetCultureInfo("ru-RU");

        public static string SearchName(ReadOnlySpan<char> val, string empty = null)
        {
            if (val.IsEmpty || val.Length == 0)
                return empty;

            lock (_rentedSearchName)
            {
                int n = 0;

                for (int i = 0; i < val.Length; i++)
                {
                    char c = val[i];

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
                    c = char.ToLower(c, _cultureInfoSearchName);

                    // - ё -> е
                    // - щ -> ш
                    if (c == 'ё') c = 'е';
                    else if (c == 'щ') c = 'ш';

                    _rentedSearchName[n++] = c;
                }

                if (n == 0)
                    return empty;

                return new string(_rentedSearchName, 0, n);
            }
        }
        #endregion
    }
}
