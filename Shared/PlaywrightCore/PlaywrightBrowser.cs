using Microsoft.Playwright;
using Shared.Engine;
using Shared.Model.Base;
using Shared.Model.Online;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Shared.PlaywrightCore
{
    public class PlaywrightBrowser : IDisposable
    {
        public static PlaywrightStatus Status
        {
            get
            {
                if (Chromium.Status == PlaywrightStatus.NoHeadless || Firefox.Status != PlaywrightStatus.disabled)
                    return PlaywrightStatus.NoHeadless;

                if (Chromium.Status == PlaywrightStatus.headless)
                    return PlaywrightStatus.headless;

                return PlaywrightStatus.disabled;
            }
        }


        public Chromium chromium = null;

        public Firefox firefox = null;


        public PlaywrightBrowser(string priorityBrowser = null, PlaywrightStatus minimalAPI = PlaywrightStatus.NoHeadless)
        {
            if (string.IsNullOrEmpty(priorityBrowser))
                priorityBrowser = "chromium";

            if (priorityBrowser == "firefox" && Firefox.Status != PlaywrightStatus.disabled)
            {
                firefox = new Firefox();
                return;
            }

            if (priorityBrowser == "chromium")
            {
                if (Chromium.Status == PlaywrightStatus.NoHeadless || Chromium.Status == minimalAPI)
                {
                    chromium = new Chromium();
                    return;
                }
            }

            if (Chromium.Status != PlaywrightStatus.disabled)
            {
                if (Chromium.Status == PlaywrightStatus.NoHeadless || Chromium.Status == minimalAPI)
                {
                    chromium = new Chromium();
                    return;
                }
            }

            if (Firefox.Status != PlaywrightStatus.disabled)
            {
                firefox = new Firefox();
                return;
            }
        }


        public TaskCompletionSource<string> completionSource 
        {
            get
            {
                if (chromium != null)
                    return chromium.completionSource;

                return firefox.completionSource;
            }
        }


        public ValueTask<IPage> NewPageAsync(string plugin, Dictionary<string, string> headers = null, (string ip, string username, string password) proxy = default)
        {
            if (chromium == null && firefox == null)
                return default;

            if (chromium != null)
                return chromium.NewPageAsync(headers, proxy);

            return firefox.NewPageAsync(plugin, headers, proxy);
        }


        public ValueTask<string> WaitPageResult(int seconds = 10)
        {
            if (chromium != null)
                return chromium.WaitPageResult(seconds);

            return firefox.WaitPageResult(seconds);
        }


        public void Dispose()
        {
            chromium?.Dispose();
            firefox?.Dispose();
        }




        async public static ValueTask<string> Get(BaseSettings init, string url, List<HeadersModel> headers = null, (string ip, string username, string password) proxy = default, PlaywrightStatus minimalAPI = PlaywrightStatus.NoHeadless)
        {
            try
            {
                using (var browser = new PlaywrightBrowser(init.priorityBrowser, minimalAPI))
                {
                    var page = await browser.NewPageAsync(init.plugin, headers?.ToDictionary(), proxy);
                    if (page == null)
                        return null;

                    var response = await page.GotoAsync($"view-source:{url}");
                    if (response != null)
                    {
                        string result = await response.TextAsync();
                        PlaywrightBase.WebLog(response.Request, response, result, proxy);

                        return result;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
