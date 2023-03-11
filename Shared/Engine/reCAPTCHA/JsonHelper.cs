using System.Globalization;
using Newtonsoft.Json;

namespace Shared.Engine.reCAPTCHA
{
    public class JsonHelper
    {
        public static string ExtractStr(dynamic json, string firstLevel, string secondLevel = null, bool silent = false)
        {
            try
            {
                object result = json[firstLevel];

                if (result != null && secondLevel != null)
                    result = json[firstLevel][secondLevel];

                if (result == null)
                    return null;

                return result.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string AsString(dynamic json)
        {
            return JsonConvert.SerializeObject(json, Formatting.Indented);
        }

        public static double? ExtractDouble(dynamic json, string firstLevel, string secondLevel = null)
        {
            double outDouble;
            string numberAsStr = ExtractStr(json, firstLevel, secondLevel);

            if (numberAsStr == null ||
                !double.TryParse(numberAsStr.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture,
                    out outDouble))
            {
                return null;
            }

            return outDouble;
        }

        public static int? ExtractInt(dynamic json, string firstLevel, string secondLevel = null, bool silent = false)
        {
            int outInt;
            string numberAsStr = JsonHelper.ExtractStr(json, firstLevel, secondLevel, silent);

            if (!int.TryParse(numberAsStr, out outInt))
                return null;

            return outInt;
        }
    }
}
