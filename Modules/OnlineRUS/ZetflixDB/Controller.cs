using Microsoft.AspNetCore.Mvc;
using Shared;
using System;
using System.Buffers.Text;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ZetflixDB;

public class ZetflixDBController : BaseOnlineController
{
    public ZetflixDBController() : base(ModInit.conf) { }

    [HttpGet]
    [Route("lite/zetflixdb")]
    async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, bool rjson = false)
    {
        if (kinopoisk_id == 0)
            return OnError();

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        string encodeKp = Encode(kinopoisk_id);
        if (encodeKp == null)
            return OnError("encodeKp");

        string uri = $"{init.apihost}/embed/AO/kinopoisk/{encodeKp}/";
        string args = $"?uri={EncryptQuery(uri)}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&rjson={rjson}";

        return LocalRedirect(accsArgs("/lite/videodb" + args));
    }

    static string Encode(long kinopoisk_id)
    {
        // long kinopoisk_id = 19 digits
        Span<byte> numberBytes = stackalloc byte[20];

        if (!Utf8Formatter.TryFormat(kinopoisk_id, numberBytes, out int numberLen))
            return null;

        Span<byte> base64Bytes = stackalloc byte[32];

        Base64.EncodeToUtf8(
            numberBytes.Slice(0, numberLen),
            base64Bytes,
            out _,
            out int base64Len);

        // trim '='
        while (base64Len > 0 && base64Bytes[base64Len - 1] == (byte)'=')
            base64Len--;

        // reverse inplace
        base64Bytes.Slice(0, base64Len).Reverse();

        return Encoding.UTF8.GetString(base64Bytes.Slice(0, base64Len));
    }
}
