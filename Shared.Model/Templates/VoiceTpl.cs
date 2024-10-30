﻿using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Model.Templates
{
    public class VoiceTpl
    {
        List<(string name, bool active, string link)> data = new List<(string, bool, string)>();

        public VoiceTpl() { }

        public VoiceTpl(int capacity) { data.Capacity = capacity; }

        public void Append(string? name, bool active, string link)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add((name, active, link));
        }

        public string ToHtml()
        {
            if (data.Count == 0)
                return string.Empty;

            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            foreach (var i in data)
                html.Append("<div class=\"videos__button selector " + (i.active ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + i.link + "\"}'>" + i.name + "</div>");

            return html.ToString() + "</div>";
        }

        public string ToJson()
        {
            if (data.Count == 0)
                return "[]";

            var html = new StringBuilder();

            foreach (var i in data)
                html.Append($"{{\"method\":\"link\", \"url\":\"{i.link}\", \"active\": {i.active.ToString().ToLower()}, \"name\":\"{i.name.Replace("\"", "%22")?.Replace("'", "%27")}\"}},");

            return "[" + Regex.Replace(html.ToString(), ",$", "") + "]";
        }
    }
}
