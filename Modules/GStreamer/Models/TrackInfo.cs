namespace GStreamer.Models;

public sealed class TrackInfo
{
    public int Index { get; set; }
    public string PadName { get; set; }

    public string Type { get; set; }
    public string CapsName { get; set; }

    public string Title { get; set; }
    public string Language { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Channels { get; set; }
    public int? Rate { get; set; }
}