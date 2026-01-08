using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public class HtmlParse
    {
        public List<HtmlRowParse> nodes { get; private set; } = new List<HtmlRowParse>();

        public HtmlParse(string html, string xpathNodes) 
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var _nodes = doc.DocumentNode?.SelectNodes(xpathNodes);
            if (_nodes == null)
                return;

            foreach (var node in _nodes)
                nodes.Add(new HtmlRowParse(node));
        }

        public static List<HtmlRowParse> Nodes(string html, string xpathNodes)
        {
            return new HtmlParse(html, xpathNodes).nodes;
        }
    }


    public class HtmlRowParse
    {
        public HtmlNode row { get; private set; }

        public HtmlRowParse(HtmlNode node)
        {
            row = node;
        }

        #region SelectText
        public string SelectText(string xpath, string attribute = null, string[] attributes = null)
        {
            string value = null;

            if (string.IsNullOrEmpty(xpath) && (!string.IsNullOrEmpty(attribute) || attributes != null))
            {
                if (attributes != null)
                {
                    foreach (var attr in attributes)
                    {
                        string attrValue = row.GetAttributeValue(attr, null);
                        if (!string.IsNullOrWhiteSpace(attrValue))
                        {
                            value = attrValue;
                            break;
                        }
                    }
                }
                else
                {
                    value = row.GetAttributeValue(attribute, null);
                }
            }
            else
            {
                var inNode = row.SelectSingleNode(xpath);
                if (inNode != null)
                {
                    if (attributes != null)
                    {
                        foreach (var attr in attributes)
                        {
                            string attrValue = inNode.GetAttributeValue(attr, null);
                            if (!string.IsNullOrWhiteSpace(attrValue))
                            {
                                value = attrValue;
                                break;
                            }
                        }
                    }
                    else
                    {
                        value = (!string.IsNullOrEmpty(attribute) ? inNode.GetAttributeValue(attribute, null) : inNode.InnerText);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value?.Trim();
        }
        #endregion

        #region SelectHtml
        public string SelectHtml(string xpath)
        {
            var inNode = row.SelectSingleNode(xpath);
            if (inNode != null)
            {
                string html = inNode.InnerHtml;
                if (string.IsNullOrWhiteSpace(html))
                    return null;

                return inNode.InnerHtml;
            }

            return null;
        }
        #endregion

        #region Regex
        public string Regex(string xpath, string pattern, int index = 1, RegexOptions options = RegexOptions.IgnoreCase)
        {
            string html = SelectHtml(pattern);
            if (string.IsNullOrWhiteSpace(html))
                return null;

            string res = System.Text.RegularExpressions.Regex.Match(html, pattern, options).Groups[index].Value;
            if (string.IsNullOrWhiteSpace(res))
                return null;

            return res.Trim();
        }

        public string Regex(string xpath, string pattern, string groupName, RegexOptions options = RegexOptions.IgnoreCase)
        {
            string html = SelectHtml(pattern);
            if (string.IsNullOrWhiteSpace(html))
                return null;

            string res = System.Text.RegularExpressions.Regex.Match(html, pattern, options).Groups[groupName].Value;
            if (string.IsNullOrWhiteSpace(res))
                return null;

            return res.Trim();
        }


        public string Regex(string pattern, int index = 1, RegexOptions options = RegexOptions.IgnoreCase)
        {
            string res = System.Text.RegularExpressions.Regex.Match(row.InnerHtml, pattern, options).Groups[index].Value;
            if (string.IsNullOrWhiteSpace(res))
                return null;

            return res.Trim();
        }

        public string Regex(string pattern, string groupName, RegexOptions options = RegexOptions.IgnoreCase)
        {
            string res = System.Text.RegularExpressions.Regex.Match(row.InnerHtml, pattern, options).Groups[groupName].Value;
            if (string.IsNullOrWhiteSpace(res))
                return null;

            return res.Trim();
        }
        #endregion
    }
}
