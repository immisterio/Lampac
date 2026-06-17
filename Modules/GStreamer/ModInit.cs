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
        modpath = initspace.path;

        updateConf();
        EventListener.UpdateInitFile += updateConf;

        foreach (var m in conf.limit_map)
            CoreInit.conf.WAF.limit_map.Insert(0, m);

        InitGst();
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
            segment_seconds = 6,
            aac_bitrate = 256000,
            video_bitrate = 5000,
            PATH = @"C:\Program Files\gstreamer\1.0\mingw_x86_64",
            inactiveMinutes = 5,
            limit_map = new List<WafLimitRootMap>()
            {
                new("^/gst/", new WafLimitMap { limit = 50, second = 1 })
            }
        });
    }


    static void InitGst()
    {
        SetupGStreamer();

        Gst.Module.Initialize();
        GstApp.Module.Initialize();

        var gstArgs = Array.Empty<string>();
        Gst.Functions.Init(ref gstArgs);
    }

    static void SetupGStreamer()
    {
        Environment.SetEnvironmentVariable(
            "GST_REGISTRY",
            Path.Combine(
                AppContext.BaseDirectory,
                "cache",
                "gstreamer-registry.bin"
            ),
            EnvironmentVariableTarget.Process
        );

        if (!OperatingSystem.IsWindows())
            return;

        var gstBin = Path.Combine(conf.PATH, "bin");
        if (!Directory.Exists(gstBin))
            return;

        var currentPath = Environment.GetEnvironmentVariable("PATH");

        Environment.SetEnvironmentVariable(
            "PATH",
            string.IsNullOrEmpty(currentPath)
                ? gstBin
                : $"{gstBin}{Path.PathSeparator}{currentPath}",
            EnvironmentVariableTarget.Process
        );

        //Environment.SetEnvironmentVariable(
        //    "GST_DEBUG",
        //    "souphttpsrc:6,matroskademux:5,h264parse:4," +
        //    "hlssink3:4,splitmuxsink:4,mpegtsmux:4,*:2",
        //    EnvironmentVariableTarget.Process
        //);
    }
}
