using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public static class StringConvert
    {
        #region FindStartText
        public static string FindStartText(in string data, string end, string start = null)
        {
            try
            {
                int endtIndex = data.IndexOf(end);
                if (endtIndex == -1)
                    return null;

                return data.AsSpan(0, endtIndex).ToString();
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region FindLastText
        public static string FindLastText(in string data, string start, string end = null)
        {
            try
            {
                int startIndex = data.IndexOf(start);
                if (startIndex == -1)
                    return null;

                var resSpan = data.AsSpan(startIndex);
                string res = resSpan.ToString();

                if (end == null)
                    return res;

                return FindStartText(res, end);
            }
            catch 
            {
                return null;
            }
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

            string result = Regex.Replace(val.ToLower(), "[^a-zA-Zа-яА-Я0-9Ёё]+", "").Replace("ё", "е").Replace("щ", "ш");
            if (string.IsNullOrWhiteSpace(result))
                return empty;

            return result;
        }
        #endregion
    }
}
