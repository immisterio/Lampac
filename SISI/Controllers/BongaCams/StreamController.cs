//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Lampac.Engine;
//using Lampac.Engine.CORE;
//using Lampac.Model.SISI.BongaCams;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Caching.Memory;

//namespace SISI.Controllers.BongaCams
//{
//    public class StreamController : BaseController
//    {
//        [HttpGet]
//        [Route("bgs/potok.m3u8")]
//        async public Task<ActionResult> Index(string baba)
//        {
//            if (!AppInit.conf.BongaCams.enable)
//                return OnError("disable");

//            string memKey = $"bongacams:stream:{baba}";
//            if (memoryCache.TryGetValue(memKey, out string hls))
//                return Redirect(HostStreamProxy(AppInit.conf.BongaCams.streamproxy, hls));

//            var root = await HttpClient.Post<Amf>(
//                       $"{AppInit.conf.BongaCams.host}/tools/amf.php?x-country=ua&res=1061112?{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", $"method=getRoomData&args%5B%5D={baba}&args%5B%5D=&args%5B%5D=",
//                       useproxy: AppInit.conf.BongaCams.useproxy,
//                       addHeaders: new List<(string name, string val)>()
//            {
//                ("dnt", "1"),
//                //("referer", AppInit.conf.BongaCams.host),
//                ("sec-fetch-dest", "empty"),
//                ("sec-fetch-mode", "cors"),
//                ("sec-fetch-site", "same-origin"),
//                ("x-requested-with", "XMLHttpRequest"),
//                ("x-ab-split-group", "5645da7355b7d0ac0590e38a54d1d996f6754e425c1709e4420e1c68d90620315932e5bef13fd38e"),
//                //("cookie", "bonga20120608=dcf21cf81fc13991e8f999c26126a857; ts_type2=1; fv=ZGR5BGL0AGV2ZD==; uh=GH5AAJMvIy9bLzkmDaSZsyOTsxg6Zt==; sg=501; BONGA_REF=https%3A%2F%2Fwww.google.com%2F; reg_ver2=3; warning18=%5B%22ru_RU%22%5D; __ti=H4sIAAAAAAACAyWIOw6AIBBEr2KmJ9ldIcbZ05BIQa3BgnB3Fav3GcNhymQUXZKETYKaGLgrT8cBTt6lNjB-ev3LWB1teufK7FGVtb-dHxKMhapUAAAA; __asc=6527103917a758e97aa2f42fa81; __auc=6527103917a758e97aa2f42fa81; _ga=GA1.2.901307154.1625469917; _gid=GA1.2.1041270203.1625469917; _gat_gtag_UA_10874655_24=1; _gat_gtag_UA_10874655_62=1; tj0ffcjy9e=1802827793"),
//            });

//            if (string.IsNullOrWhiteSpace(root?.localData?.videoServerUrl))
//                return OnError("baba");

//            hls = $"http:{root.localData.videoServerUrl}/hls/stream_{baba}/public-aac/stream_{baba}/chunks.m3u8";
//            memoryCache.Set(memKey, hls, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 5));

//            return Redirect(HostStreamProxy(AppInit.conf.BongaCams.streamproxy, hls));
//        }
//    }
//}
