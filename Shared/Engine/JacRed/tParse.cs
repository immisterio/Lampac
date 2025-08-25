using System.Globalization;
using System.Text.RegularExpressions;

namespace Shared.Engine.JacRed
{
    public static class tParse
    {
        #region BytesToString
        public static string BytesToString(long byteCount)
        {
            string[] suf = { "Byt", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0 " + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString().Replace(",", ".") + " " + suf[place];
        }
        #endregion

        #region ParseCreateTime
        public static DateTime ParseCreateTime(string line, string format)
        {
            line = Regex.Replace(line, " янв\\.? ", ".01.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " февр?\\.? ", ".02.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " март?\\.? ", ".03.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " апр\\.? ", ".04.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " май\\.? ", ".05.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " июнь?\\.? ", ".06.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " июль?\\.? ", ".07.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " авг\\.? ", ".08.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " сент?\\.? ", ".09.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " окт\\.? ", ".10.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " нояб?\\.? ", ".11.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " дек\\.? ", ".12.", RegexOptions.IgnoreCase);

            line = Regex.Replace(line, " янв(аря?)? ", ".01.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " фев(раля?)? ", ".02.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " марта? ", ".03.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " апр(еля?)? ", ".04.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " ма(й|я)? ", ".05.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " июн(ь|я)? ", ".06.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " июл(ь|я)? ", ".07.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " авг(устa?)? ", ".08.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " сент(ябр(я|ь)?)? ", ".09.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " окт(ябр(я|ь)?)? ", ".10.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " ноя(бр(я|ь)?)? ", ".11.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " дек(абр(я|ь)?)? ", ".12.", RegexOptions.IgnoreCase);

            line = Regex.Replace(line, " январ(ь|я)?\\.? ", ".01.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " феврал(ь|я)?\\.? ", ".02.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " марта?\\.? ", ".03.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " апрел(ь|я)?\\.? ", ".04.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " май?я?\\.? ", ".05.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " июн(ь|я)?\\.? ", ".06.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " июл(ь|я)?\\.? ", ".07.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " августа?\\.? ", ".08.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " сентябр(ь|я)?\\.? ", ".09.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " октябр(ь|я)?\\.? ", ".10.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " ноябр(ь|я)?\\.? ", ".11.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " декабр(ь|я)?\\.? ", ".12.", RegexOptions.IgnoreCase);

            line = Regex.Replace(line, " Jan ", ".01.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Feb ", ".02.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Mar ", ".03.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Apr ", ".04.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " May ", ".05.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Jun ", ".06.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Jul ", ".07.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Aug ", ".08.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Sep ", ".09.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Oct ", ".10.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Nov ", ".11.", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, " Dec ", ".12.", RegexOptions.IgnoreCase);

            if (Regex.IsMatch(line, "^[0-9]\\."))
                line = $"0{line}";

            DateTime.TryParseExact(line.ToLower(), format, new CultureInfo("ru-RU"), DateTimeStyles.None, out DateTime createTime);
            return createTime;
        }
        #endregion
    }
}
