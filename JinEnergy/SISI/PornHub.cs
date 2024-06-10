using JinEnergy.Engine;
using JinEnergy.Model;
using Microsoft.JSInterop;
using Shared.Engine.SISI;
using Shared.Model.Online;

namespace JinEnergy.SISI
{
    public class PornHubController : BaseController
    {
        [JSInvokable("phub")]
        public static ValueTask<ResultModel> Index(string args) => result(args, "phub");

        [JSInvokable("phubgay")]
        public static ValueTask<ResultModel> Gay(string args) => result(args, "phubgay");

        [JSInvokable("phubsml")]
        public static ValueTask<ResultModel> Shemale(string args) => result(args, "phubsml");


        static List<HeadersModel> headers = HeadersModel.Init
        (
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
            ("cookie", "atatusScript=hide; cookiesBannerSeen=1; quality=720; ss=324931217390739148; accessAgeDisclaimerPH=1; expiredEnterModalShown=1; cookieConsent=3; bs=i1d2ps3ezjn4jisfyphx0ind2j14mey8; tj_UUID=ChAh7CIK5WBIgqEu8fQCDEP_EgwIkuiFqAYQgNqaiQE=; tj_UUID_v2=ChAh7CIK5WBIgqEu8fQCDEP_EgwIkuiFqAYQgNqaiQE=; d_fs=1; d_uidb=5e4fbea7-4e67-a0ec-0a19-44c12627a3f6; d_uid=5e4fbea7-4e67-a0ec-0a19-44c12627a3f6; d_uidb=5e4fbea7-4e67-a0ec-0a19-44c12627a3f6; __s=6505552D-42FE722901BB97AD-71CDF29; __l=6505552D-42FE722901BB97AD-71CDF29; RNLBSERVERID=ded7295; hasVisited=1; fg_18d9418ca9b3bf3c5cad39676821aeec=19552.100000; fg_f916a4d27adf4fc066cd2d778b4d388e=99810.100000; fg_0d2ec4cbd943df07ec161982a603817e=52466.100000; platform=pc; fg_9951ce1ac4434b4ac312a1334fa77d82=77324.100000; fg_1f595a21748e9a93d04690b86a079a2a=53822.100000; fg_5067492b57c1c879a31bcb2ab72b7a3b=42380.100000; fg_05e293c2afb5036639bfd3a79bcb824c=69399.100000; ua=22210ca73bf1af2ec2eace74a96ee356; fg_fa3f0973fd973fca3dfabc86790b408b=30674.100000; _gid=GA1.2.1186566113.1696674808; etavt={\"64fffc67ec506\":\"1_23_2_NA|1\",\"64fa0594e5377\":\"1_23_2_NA|0\"}; _ga_B39RFFWGYY=GS1.1.1696674807.10.1.1696674809.0.0.0; _ga=GA1.1.554019270.1694848300")
        );


        async static ValueTask<ResultModel> result(string args, string plugin)
        {
            var init = AppInit.PornHub.Clone();

            string? search = parse_arg("search", args);
            string? model = parse_arg("model", args);
            string? sort = parse_arg("sort", args);
            int c = int.Parse(parse_arg("c", args) ?? "0");
            int pg = int.Parse(parse_arg("pg", args) ?? "1");

            refresh: string? html = await PornHubTo.InvokeHtml(init.corsHost(), plugin, search, model, sort, c, null, pg, url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, headers)));

            var playlist = PornHubTo.Playlist("phub/vidosik", "phub", html, IsModel_page: !string.IsNullOrEmpty(model), onplaylist: pl =>
            {
                pl.picture = rsizehost(pl.picture);
                return pl;
            });

            if (playlist.Count == 0 && IsRefresh(init))
                goto refresh;

            return OnResult(string.IsNullOrEmpty(model) ? PornHubTo.Menu(null, plugin, search, sort, c) : null, playlist, total_pages: PornHubTo.Pages(html));
        }


        [JSInvokable("phub/vidosik")]
        async public static ValueTask<ResultModel> Stream(string args)
        {
            var init = AppInit.PornHub.Clone();

            refresh: var stream_links = await PornHubTo.StreamLinks("phub/vidosik", "phub", init.corsHost(), parse_arg("vkey", args), url => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, headers)));

            if (stream_links == null && IsRefresh(init))
                goto refresh;

            if (bool.Parse(parse_arg("related", args) ?? "false"))
                return OnResult(null, stream_links?.recomends, total_pages: 1);

            return OnResult(init, stream_links);
        }
    }
}
