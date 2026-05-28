using Microsoft.Playwright;

namespace Shared.PlaywrightCore;

public class PlaywrightBrowser : IDisposable
{
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext<PlaywrightBrowser>();

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

    #region IsCompleted / completionSource
    public bool IsCompleted
    {
        get
        {
            if (chromium != null)
                return chromium.IsCompleted;

            return firefox.IsCompleted;
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
    #endregion

    public Chromium chromium = null;

    public Firefox firefox = null;


    public PlaywrightBrowser(string priorityBrowser = null)
    {
        if (priorityBrowser == "firefox" && Firefox.Status != PlaywrightStatus.disabled)
        {
            firefox = new Firefox();
            return;
        }

        chromium = new Chromium();
    }

    #region SetFailedUrl
    public void SetFailedUrl(string url)
    {
        if (chromium != null)
        {
            chromium.failedUrl = url;
        }
        else
        {
            firefox.failedUrl = url;
        }
    }
    #endregion

    #region NewPageAsync
    async public Task<IPage> NewPageAsync(string plugin, IReadOnlyDictionary<string, string> headers = null, (string ip, string username, string password) proxy = default, bool keepopen = true, bool imitationHuman = false, bool deferredDispose = false)
    {
        try
        {
            if (chromium == null && firefox == null)
                return default;

            IPage page = default;

            if (chromium != null)
                page = await chromium.NewPageAsync(plugin, headers, proxy, keepopen: keepopen, imitationHuman: imitationHuman, deferredDispose: deferredDispose).ConfigureAwait(false);
            else
                page = await firefox.NewPageAsync(plugin, headers, proxy, keepopen: keepopen).ConfigureAwait(false);

            return page;
        }
        catch { return default; }
    }
    #endregion

    #region SetPageResult
    public void SetPageResult(string val)
    {
        try
        {
            if (chromium != null)
            {
                chromium.IsCompleted = true;
                chromium.completionSource.SetResult(val);
            }
            else
            {
                firefox.IsCompleted = true;
                firefox.completionSource.SetResult(val);
            }
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_tjv9tao1");
        }
    }
    #endregion

    #region WaitPageResult
    public Task<string> WaitPageResult(int seconds = 10)
    {
        try
        {
            if (chromium != null)
                return chromium.WaitPageResult(seconds);

            return firefox.WaitPageResult(seconds);
        }
        catch { return default; }
    }
    #endregion

    #region WaitForAnySelectorAsync
    public Task WaitForAnySelectorAsync(IPage page, params string[] selectors)
    {
        var tasks = selectors.Select(selector =>
            page.WaitForSelectorAsync(selector)
        ).ToArray();

        return Task.WhenAny(tasks);
    }
    #endregion

    #region ClearContinueAsync
    async public Task ClearContinueAsync(IRoute route, IPage page)
    {
        var cookies = await page.Context.CookiesAsync();
        if (cookies == null || cookies.Count == 0)
        {
            // нету куки, продолжаем
            await route.ContinueAsync();
            return;
        }

        var filteredCookies = cookies.Where(c => c.Name != "cf_clearance").Select(c => new Cookie
        {
            Name = c.Name,
            Value = c.Value,
            Domain = c.Domain,
            Path = c.Path,
            Expires = c.Expires,
            HttpOnly = c.HttpOnly,
            Secure = c.Secure,
            SameSite = c.SameSite
        }).ToList();

        if (filteredCookies.Count == cookies.Count)
        {
            // Если куки не содержат cf_clearance, продолжаем
            await route.ContinueAsync();
            return;
        }

        if (filteredCookies.Count == 0)
        {
            // после удаления cf_clearance не осталось других куки
            await page.Context.ClearCookiesAsync();
            await route.ContinueAsync();
            return;
        }

        await page.Context.ClearCookiesAsync();
        await page.Context.AddCookiesAsync(filteredCookies);

        await route.ContinueAsync();
    }
    #endregion

    #region Dispose
    public void Dispose()
    {
        chromium?.Dispose();
        firefox?.Dispose();
    }
    #endregion
}
