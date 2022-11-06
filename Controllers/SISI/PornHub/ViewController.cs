using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.PornHub
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("phub/vidosik.m3u8")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.PornHub.enable)
                return OnError("disable");

            string memKey = $"phub:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out string hls))
            {
                string html = await HttpClient.Get($"{AppInit.conf.PornHub.host}/view_video.php?viewkey={goni}", httpversion: 2, timeoutSeconds: 8, addHeaders: new List<(string name, string val)>()
                {
                    ("accept-language", "ru-RU,ru;q=0.9"),
                    ("sec-ch-ua", "\"Chromium\";v=\"94\", \"Google Chrome\";v=\"94\", \";Not A Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36"),
                });

                if (html == null)
                    return OnError("html");

                foreach (string l in getDirectLinks(html))
                {
                    if (l.Contains("urlset/master.m3u8"))
                        hls = l;
                }

                if (string.IsNullOrWhiteSpace(hls))
                    return OnError("hls");

                memoryCache.Set(memKey, hls, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return Redirect($"{AppInit.Host(HttpContext)}/proxy/{hls}");
        }


        #region getDirectLinks
        List<string> getDirectLinks(string pageCode)
        {
            List<string> vars = new List<string>();
            var getmediaLinks = new List<string>();

            string mainParamBody = Regex.Match(pageCode, "var player_mp4_seek = \"[^\"]+\";[\n\r\t ]+(// var[^\n\r]+[\n\r\t ]+)?([^\n\r]+)").Groups[2].Value.Trim();
            mainParamBody = Regex.Replace(mainParamBody, "/\\*.*?\\*/", "");
            mainParamBody = mainParamBody.Replace("\" + \"", "");


            MatchCollection varMc = Regex.Matches(mainParamBody, "var ([^=]+)=([^;]+);");
            foreach (Match currVar in varMc)
            {
                string name = currVar.Groups[1].Value;
                string param = currVar.Groups[2].Value.Replace("\"", "").Replace(" + ", "");
                vars.Add(name + ";" + param);
            }

            MatchCollection qualMc = Regex.Matches(mainParamBody, "var media_([0-9]+)=(.*?);", RegexOptions.Singleline);
            foreach (Match m in qualMc)
            {
                string link = "";
                string[] parts = m.Groups[2].Value.Replace(" ", "").Split('+');
                foreach (string curr in parts)
                {
                    string line = vars.Find(x => x.StartsWith(curr));
                    string newVal = line.Split(';')[1];
                    link += newVal;
                }

                getmediaLinks.Add(link);
            }

            return getmediaLinks;
        }
        #endregion
    }
}
