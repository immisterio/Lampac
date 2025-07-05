using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public class HtmlCommon
    {
        HtmlNode row;

        public HtmlCommon(HtmlNode row)
        {
            this.row = row;
        }


        public string NodeValue(in string node, string attribute = null, string removeChild = null)
        {
            if (string.IsNullOrEmpty(node) && !string.IsNullOrEmpty(attribute))
            {
                return row.GetAttributeValue(attribute, null);
            }
            else
            {
                var inNode = row.SelectSingleNode(node);
                if (inNode != null)
                {
                    if (removeChild != null)
                        inNode.RemoveChild(inNode.SelectSingleNode(removeChild));

                    return (!string.IsNullOrEmpty(attribute) ? inNode.GetAttributeValue(attribute, null) : inNode.InnerText)?.Trim();
                }
            }

            return null;
        }


        public string Match(string pattern, int index = 1)
        {
            return new Regex(pattern, RegexOptions.IgnoreCase).Match(row.InnerHtml).Groups[index].Value.Trim();
        }


        public static int Integer(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            if (int.TryParse(Regex.Replace(value, "[^0-9]+", ""), out int result))
                return result;

            return 0;
        }
    }
}
