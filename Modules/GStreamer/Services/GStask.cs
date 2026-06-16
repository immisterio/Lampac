using Gst;
using GStreamer.Models;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace GStreamer.Services;

public class GStask
{
    #region GStask
    public const int segmentSeconds = 6;

    public System.DateTime lastActive { get; private set; } = System.DateTime.UtcNow;

    public SemaphoreSlim semaphore { get; } = new(1, 1);

    public int lastSentSegment = -1;

    public readonly ulong id;
    public readonly ProbeInfo probe;
    public readonly string sourceUrl;
    public int lastIndexSegment = -1;

    public byte[] initMp4 { get; private set; }
    (int index, bool complete, Segment seg) readySegment = (-1, false, default);

    Mp4BoxReader mp4Reader;

    Pipeline pipeline;
    Bus bus;
    Gst.Bin bin;
    GstApp.AppSink sink;

    public GStask(ProbeInfo probe, string sourceUrl, ulong id)
    {
        this.id = id;
        this.probe = probe;
        this.sourceUrl = sourceUrl;

        string pipelineArgs = CreatePipelineArgs(probe);
        pipeline = (Pipeline)Gst.Functions.ParseLaunch(pipelineArgs);
        bus = pipeline.GetBus();

        bin = pipeline;
        sink = (GstApp.AppSink)bin.GetByName("out");

        mp4Reader = new Mp4BoxReader(
           onInit: data =>
           {
               initMp4 = data;
           },
           onSegment: seg =>
           {
               readySegment.seg = seg;
               readySegment.complete = true;
           }
        );

        pipeline.SetState(State.Playing);
    }
    #endregion

    #region CreatePipelineArgs
    string CreatePipelineArgs(ProbeInfo probe, int audioIndex = 0)
    {
        var sb = new StringBuilder();

        long queueNs = 30 * 1_000_000_000L; // 30s
        const int maxQueueBytes = 64 * 1024 * 1024; // 64 MB

        sb.AppendLine($$"""
        souphttpsrc location="{{sourceUrl}}" is-live=false !
        queue2 use-buffering=true max-size-buffers=0 max-size-bytes={{maxQueueBytes}} max-size-time={{queueNs}} !
        matroskademux name=d
        """);

        if (probe.IsH264)
        {
            sb.AppendLine($$"""
            d.video_0 !
            queue max-size-buffers=0 max-size-bytes={{maxQueueBytes}} max-size-time={{queueNs}} !
            h264parse config-interval=-1 !
            h264timestamper !
            video/x-h264,stream-format=avc,alignment=au !
            mux.video_0
            """);
        }
        else if (probe.IsH265)
        {
            sb.AppendLine($$"""
            d.video_0 !
            queue max-size-buffers=0 max-size-bytes={{maxQueueBytes}} max-size-time={{queueNs}} !
            h265parse config-interval=-1 !
            h265timestamper !
            video/x-h265,stream-format=hvc1,alignment=au !
            mux.video_0
            """);
        }
        else if (probe.IsAV1)
        {
            sb.AppendLine($$"""
            d.video_0 !
            queue max-size-buffers=0 max-size-bytes={{maxQueueBytes}} max-size-time={{queueNs}} !
            av1parse !
            video/x-av1,stream-format=obu-stream,alignment=tu !
            mux.video_0
            """);
        }
        else if (probe.IsVP9)
        {
            sb.AppendLine($$"""
            d.video_0 !
            queue max-size-buffers=0 max-size-bytes={{maxQueueBytes}} max-size-time={{queueNs}} !
            vp9parse !
            video/x-vp9,alignment=frame !
            mux.video_0
            """);
        }
        else
        {
            throw new NotSupportedException("Unsupported video codec");
        }

        sb.AppendLine($$"""
        d.audio_{{audioIndex}} !
        queue max-size-buffers=0 max-size-bytes={{maxQueueBytes}} max-size-time={{queueNs}} !
        decodebin !
        audioconvert !
        audioresample !
        audio/x-raw,rate=48000,channels=2 !
        avenc_aac bitrate=128000 !
        aacparse !
        audio/mpeg,mpegversion=4,stream-format=raw,rate=48000,channels=2 !
        mux.audio_0
        """);

        sb.AppendLine($$"""
        mp4mux name=mux fragment-duration={{segmentSeconds * 1000}} streamable=true !
        appsink name=out emit-signals=false sync=false max-buffers=0 max-bytes={{maxQueueBytes}} drop=false
        """);

        return sb.ToString();
    }
    #endregion

    #region UpdateLastActive
    public void UpdateLastActive()
    {
        lastActive = System.DateTime.UtcNow;
    }
    #endregion

    #region Seek
    public bool Seek(long seconds)
    {
        pipeline.SetState(State.Null);
        pipeline.Dispose();
        sink.Dispose();
        bus.Dispose();

        string pipelineArgs = CreatePipelineArgs(probe);
        pipeline = (Pipeline)Gst.Functions.ParseLaunch(pipelineArgs);
        bus = pipeline.GetBus();

        bin = pipeline;
        sink = (GstApp.AppSink)bin.GetByName("out");

        var ret = pipeline.SetState(State.Paused);
        if (ret == StateChangeReturn.Failure)
            return false;

        if (ret == StateChangeReturn.Async)
        {
            // ждём завершение команды в pipeline
            using var msg = bus.TimedPopFiltered(
                10_000_000_000UL,
                MessageType.AsyncDone | MessageType.Error
            );
        }

        bool ok = pipeline.SeekSimple(
            Format.Time,
            SeekFlags.Flush | SeekFlags.KeyUnit | SeekFlags.SnapBefore,
            seconds * 1_000_000_000L
        );

        if (!ok)
            return false;

        // После flushing seek тоже лучше дождаться ASYNC_DONE.
        using var flushing = bus.TimedPopFiltered(
            5_000_000_000UL,
            MessageType.AsyncDone | MessageType.Error
        );

        mp4Reader.ResetSegment();
        mp4Reader.SeekReset();
        readySegment = (-1, false, default);

        ret = pipeline.SetState(State.Playing);
        if (ret == StateChangeReturn.Failure)
            return false;

        return true;
    }
    #endregion

    #region GetSegment
    public Segment GetSegment(int index, CancellationToken ct)
    {
        if (index != -1 && readySegment.index == index)
            return readySegment.seg;

        mp4Reader.ResetSegment();
        readySegment = (-1, false, default);

        long start = Stopwatch.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(10);

        while (Stopwatch.GetElapsedTime(start) < timeout)
        {
            if (ct.IsCancellationRequested)
                return default;

            using (var err = bus.TimedPopFiltered(0, MessageType.Error))
            {
                if (err != null)
                    return default; // pipeline сдох
            }

            using (var eos = bus.TimedPopFiltered(0, MessageType.Eos))
            {
                if (eos != null)
                    return default; // конец потока
            }

            // 100 ms
            using (var sample = sink.TryPullSample(100_000_000UL))
            {
                var buffer = sample?.GetBuffer();
                nuint? size = buffer?.GetSize();

                if (buffer == null || size == 0)
                    continue;

                mp4Reader.Push(buffer, (int)size);

                if (readySegment.complete)
                {
                    readySegment.index = index;
                    return readySegment.seg;
                }
            }
        }

#warning pipeline ушел в себя и bus это не видит или реально таймаут, нужно отслеживать состояние pipeline
        return default;
    }
    #endregion

    #region Dispose
    public void Dispose()
    {
        if (pipeline == null)
            return;

        mp4Reader.Dispose();

        pipeline.SetState(State.Null);
        pipeline.Dispose();
        pipeline = null;

        sink.Dispose();
        sink = null;
        bus.Dispose();
        bus = null;

        semaphore.Dispose();
        initMp4 = null;
        bin = null;
    }
    #endregion
}
