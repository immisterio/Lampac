using JinEnergy.Engine;
using Microsoft.JSInterop;

namespace JinEnergy
{
    public class SisiApiController : BaseController
    {
        [JSInvokable("sisi")]
        public static Task<List<dynamic>> Index(string args)
        {
            var channels = new List<dynamic>()
            { 
                new
                {
                    title = "Закладки",
                    playlist_url = "https://bwa-cloud.apn.monster/sisi/bookmarks",
                    enable = true
                },
                new
                {
                    title = "pornhub.com",
                    playlist_url = AppInit.PornHub.overridehost ?? "phub",
                    AppInit.PornHub.enable
                },
                new
                {
                    title = "xvideos.com",
                    playlist_url = AppInit.Xvideos.overridehost ?? "xds",
                    AppInit.Xvideos.enable
                },
                new
                {
                    title = "xhamster.com",
                    playlist_url = AppInit.Xhamster.overridehost ?? "xmr",
                    AppInit.Xhamster.enable
                },
                new
                {
                    title = "ebalovo.porn",
                    playlist_url = AppInit.Ebalovo.overridehost ?? "elo",
                    AppInit.Ebalovo.enable
                },
                new
                {
                    title = "hqporner.com",
                    playlist_url = AppInit.HQporner.overridehost ?? "hqr",
                    AppInit.HQporner.enable
                },
                new
                {
                    title = "spankbang.com",
                    playlist_url = AppInit.Spankbang.overridehost ?? "sbg",
                    AppInit.Spankbang.enable
                },
                new
                {
                    title = "eporner.com",
                    playlist_url = AppInit.Eporner.overridehost ?? "epr",
                    AppInit.Eporner.enable
                },
                new
                {
                    title = "porntrex.com",
                    playlist_url = AppInit.Porntrex.overridehost ?? "ptx",
                    AppInit.Porntrex.enable
                },
                new
                {
                    title = "xnxx.com",
                    playlist_url = AppInit.Xnxx.overridehost ?? "xnx",
                    AppInit.Xnxx.enable
                },
                new
                {
                    title = "bongacams.com",
                    playlist_url = AppInit.BongaCams.overridehost ?? "bgs",
                    AppInit.BongaCams.enable
                },
                new
                {
                    title = "chaturbate.com",
                    playlist_url = AppInit.Chaturbate.overridehost ?? "chu",
                    AppInit.Chaturbate.enable
                }
            };

            return Task.FromResult(channels.Where(i => i.enable).ToList());
        }
    }
}
