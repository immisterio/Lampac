﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Shared.Model.Templates
{
    public class SimilarTpl
    {
        List<(string title, string year, string details, string link)> data = new List<(string, string, string, string)>();

        public SimilarTpl() { }

        public SimilarTpl(int capacity) { data.Capacity = capacity; }


        public string OnlineSplit => "{prestige-split}";


        public void Append(string? title, string year, string details, string link)
        {
            if (!string.IsNullOrEmpty(title))
                data.Add((title, year, details, link));
        }

        public string ToHtml()
        {
            if (data.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            foreach (var i in data) 
            {
                string datajson = JsonSerializer.Serialize(new
                {
                    method = "link",
                    url = i.link,
                    similar = true,
                    year = int.TryParse(i.year, out int _year) ? _year : 0,
                    i.details

                }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });

                datajson = datajson.Replace("{prestige-split}", "<span class=\\\"online-prestige-split\\\">●</span>");

                html.Append($"<div class=\"videos__item videos__season selector {(firstjson ? "focused" : "")}\" data-json='{datajson}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">{HttpUtility.HtmlEncode(i.title)}</div></div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }


        public string ToJson()
        {
            if (data.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(new 
            {
                type = "similar",
                data = data.Select(i => new 
                {
                    url = i.link,
                    details = i.details?.Replace("{prestige-split}", "<span class=\"online-prestige-split\">●</span>"),
                    i.title,
                    year = int.TryParse(i.year, out int _year) ? _year : 0
                })
            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        }
    }
}
