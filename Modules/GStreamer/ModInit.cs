using GStreamer.Services;
using Shared;
using Shared.Models.AppConf;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace GStreamer;

public class ModInit : IModuleLoaded
{
    public static string modpath;
    public static ModuleConf conf;

    public void Loaded(InitspaceModel initspace)
    {
        InitGst();
        modpath = initspace.path;

        updateConf();
        EventListener.UpdateInitFile += updateConf;

        foreach (var m in conf.limit_map)
            CoreInit.conf.WAF.limit_map.Insert(0, m);
    }

    public void Dispose()
    {
        GService.Dispose();
        EventListener.UpdateInitFile -= updateConf;
    }

    void updateConf()
    {
        conf = ModuleInvoke.Init("gst", new ModuleConf()
        {
            limit_map = new List<WafLimitRootMap>()
            {
                new("^/gst/", new WafLimitMap { limit = 50, second = 1 })
            }
        });
    }



    static void InitGst()
    {
        SetupGStreamerWindows();

        Gst.Module.Initialize();
        GstApp.Module.Initialize();

        var gstArgs = Array.Empty<string>();
        Gst.Functions.Init(ref gstArgs);
    }


    static void SetupGStreamerWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var gstRoot = @"C:\Program Files\gstreamer\1.0\mingw_x86_64";
        var gstBin = Path.Combine(gstRoot, "bin");
        var gstPlugins = Path.Combine(
            gstRoot,
            "lib",
            "gstreamer-1.0"
        );

        if (!Directory.Exists(gstBin))
            throw new DirectoryNotFoundException(gstBin);

        var oldPath =
            Environment.GetEnvironmentVariable("PATH")
            ?? string.Empty;

        if (!oldPath.Contains(gstBin, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable(
                "PATH",
                gstBin + Path.PathSeparator + oldPath,
                EnvironmentVariableTarget.Process
            );
        }

        Environment.SetEnvironmentVariable(
            "GST_PLUGIN_PATH",
            gstPlugins,
            EnvironmentVariableTarget.Process
        );

        Environment.SetEnvironmentVariable(
            "GST_REGISTRY",
            Path.Combine(
                AppContext.BaseDirectory,
                "gstreamer-registry.bin"
            ),
            EnvironmentVariableTarget.Process
        );

        Environment.SetEnvironmentVariable(
            "GST_DEBUG",
            "souphttpsrc:6,matroskademux:5,h264parse:4," +
            "hlssink3:4,splitmuxsink:4,mpegtsmux:4,*:2",
            EnvironmentVariableTarget.Process
        );
    }
}
