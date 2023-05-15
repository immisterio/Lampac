using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class VoKino : BaseController
    {
        #region vokinotk
        [HttpGet]
        [Route("lite/vokinotk")]
        async public Task<ActionResult> Token(string login, string pass)
        {
            string html = string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                html = "Введите данные аккаунта <a href='http://vokino.tv'>vokino.tv</a> <br> <br><form method=\"get\" action=\"/lite/vokinotk\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Добавить устройство</button></form> ";
            }
            else
            {
                string deviceid = new string(DateTime.Now.ToBinary().ToString().Reverse().ToArray()).Substring(0, 8);
                var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.VoKino.host}/v2/auth?email={HttpUtility.UrlEncode(login)}&passwd={HttpUtility.UrlEncode(pass)}&deviceid={deviceid}");

                html = $"В init.conf для VoKino укажите token <br><br><b>{token_request.Value<string>("authToken")}</b>";
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/vokino")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title)
        {
            if (kinopoisk_id == 0 || string.IsNullOrWhiteSpace(AppInit.conf.VoKino.token))
                return Content(string.Empty);

            string memKey = $"vokino:{kinopoisk_id}";
            if (!memoryCache.TryGetValue(memKey, out JArray channels))
            {
                var root = await HttpClient.Get<JObject>($"{AppInit.conf.VoKino.host}/v2/list?name=%2B{kinopoisk_id}&token={AppInit.conf.VoKino.token}", timeoutSeconds: 8);
                if (root == null || !root.ContainsKey("channels"))
                    return Content(string.Empty);

                string id = root.Value<JArray>("channels")?.First?.Value<JObject>("details")?.Value<string>("id");
                if (string.IsNullOrWhiteSpace(id))
                    return Content(string.Empty);

                root = await HttpClient.Get<JObject>($"{AppInit.conf.VoKino.host}/v2/online/vokino?id={id}&inparse=true&token={AppInit.conf.VoKino.token}", timeoutSeconds: 8);
                if (root == null || !root.ContainsKey("channels"))
                    return Content(string.Empty);

                channels = root.Value<JArray>("channels");
                if (channels == null || channels.Count == 0)
                    return Content(string.Empty);

                memoryCache.Set(memKey, channels, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            foreach (var ch in channels)
            {
                string link = HostStreamProxy(AppInit.conf.VoKino.streamproxy, ch.Value<string>("stream_url"));
                string name = ch.Value<string>("quality_full");
                if (!string.IsNullOrWhiteSpace(name.Replace("2160p.", "")))
                {
                    name = name.Replace("2160p.", "4K ");
                    string size = ch.Value<JObject>("extra")?.Value<string>("size");
                    if (!string.IsNullOrWhiteSpace(size))
                        name += $" - {size}";
                }

                html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + (title ?? original_title) + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                firstjson = false;
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
