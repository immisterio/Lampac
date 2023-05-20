using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KinobaseController : BaseController
    {
        [JSInvokable("lite/kinobase")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");

            if (string.IsNullOrEmpty(arg.title) || arg.year == 0)
                return OnError("year");

            var oninvk = new KinobaseInvoke
            (
               null,
               AppInit.Kinobase.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Kinobase.corsHost(ongettourl)),
               (url, data) => JsHttpClient.Post(AppInit.Kinobase.corsHost(url), data),
               streamfile => streamfile
            );

            var content = await InvokeCache(arg.id, $"kinobase:view:{arg.title}:{arg.year}", () => oninvk.Embed(arg.title, arg.year, uri => JSRuntime.InvokeAsync<string?>("eval", evalcode(uri))));
            if (content == null)
                return OnError("content");

            return oninvk.Html(content, arg.title, arg.year, s);
        }


        static string evalcode(string uri)
        {
            return @"(async function () {
              var vod_url;
              var vod_script = await httpReq('" + uri + @"', false, {dataType: 'text'});

              var XMLHttpRequest = function () { 
                 this.open = function (method, url) {
	                vod_url = url;
                 };
                 this.send = function () {};
              };
  
              eval(vod_script);
              return vod_url;
            })();";
        }
    }
}
