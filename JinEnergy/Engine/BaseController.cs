using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using System.Web;

namespace JinEnergy.Engine
{
    public class BaseController : ComponentBase
    {
        public static Task<dynamic> OnError(string msg)
        {
            AppInit.JSRuntime?.InvokeVoidAsync("console.log", "BWA", msg);
            return Task.FromResult<dynamic>(new { success = false, msg });
        }

        public static string? arg(string name, string args)
        {
            string val = Regex.Match(args ?? "", $"{name}=([^&]+)").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(val))
                return null;

            return HttpUtility.UrlDecode(val);
        }
    }
}
