using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KinobaseController : BaseController
    {
        [JSInvokable("lite/kinobase")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Kinobase.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            if (string.IsNullOrEmpty(arg.title) || arg.year == 0)
                return EmptyError("year");

            var oninvk = new KinobaseInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               (url, data) => JsHttpClient.Post(init.cors(url), data, httpHeaders(args, init)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            refresh: var content = await InvokeCache(arg.id, $"kinobase:view:{arg.title}:{arg.year}", () => oninvk.Embed(arg.title, arg.year, code => JSRuntime.InvokeAsync<string?>("eval", evalcode(code))));

            string html = oninvk.Html(content, arg.title, arg.year, s);
            if (string.IsNullOrEmpty(html) && IsRefresh(init, true))
                goto refresh;

            return html;
        }


        static string evalcode(string code)
        {
            return @"(function () {
              var vod_url, params, $ = {}; 
			  $.get = function (u, p) { if (u && u.startsWith('/vod/')) { vod_url = u; params = p; } }; 
			  
			  var XMLHttpRequest = function XMLHttpRequest() { this.open = function (m, u) { if (u && u.startsWith('/vod/')) { vod_url = u; } }; this.send = function () {}; }; 
			  
			  try 
			  { 
			      " + code + @"
			  } 
			  catch (e) {} 
			  
			  if (params) { 
			      for (var name in params) { 
			          vod_url = Lampa.Utils.addUrlComponent(vod_url, name + '=' + encodeURIComponent(params[name])); 
			      } 
			  }
  
			  return vod_url;
            })();";
        }
    }
}
