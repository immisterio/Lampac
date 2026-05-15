using Shared.Models.Base;

namespace SakhTV;

public class ModuleConf : BaseSettings
{
    public ModuleConf(string plugin, string host = null)
    {
        this.plugin = plugin;
        this.host = host;
    }

    public string APP_VERSION { get; set; }

    public string app_id { get; set; }

    public string release { get; set; }

    public string userAgent { get; set; }
}
