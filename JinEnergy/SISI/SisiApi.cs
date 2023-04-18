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
                    title = "pornhub.com",
                    playlist_url = "phub",
                    AppInit.PornHub.enable
                },
                new
                {
                    title = "hqporner.com",
                    playlist_url = "hqr",
                    AppInit.HQporner.enable
                },
                new
                {
                    title = "spankbang.com",
                    playlist_url = "sbg",
                    AppInit.Spankbang.enable
                },
                new
                {
                    title = "eporner.com",
                    playlist_url = "epr",
                    AppInit.Eporner.enable
                },
                new
                {
                    title = "porntrex.com",
                    playlist_url = "ptx",
                    AppInit.Porntrex.enable
                },
                new
                {
                    title = "ebalovo.porn",
                    playlist_url = "elo",
                    AppInit.Ebalovo.enable
                },
                new
                {
                    title = "xhamster.com",
                    playlist_url = "xmr",
                    AppInit.Xhamster.enable
                },
                new
                {
                    title = "xvideos.com",
                    playlist_url = "xds",
                    AppInit.Xvideos.enable
                },
                new
                {
                    title = "xnxx.com",
                    playlist_url = "xnx",
                    AppInit.Xnxx.enable
                },
                new
                {
                    title = "bongacams.com",
                    playlist_url = "bgs",
                    AppInit.BongaCams.enable
                },
                new
                {
                    title = "chaturbate.com",
                    playlist_url = "chu",
                    AppInit.Chaturbate.enable
                }
            };

            return Task.FromResult(channels.Where(i => i.enable).ToList());
        }
    }
}
