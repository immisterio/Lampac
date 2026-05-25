using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Middlewares;

public class ResponseAvgStatistics
{
    private readonly RequestDelegate _next;

    public ResponseAvgStatistics(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        long startTimestamp = ResponseStatisticsTracker.StartRequest();

        try
        {
            await _next(context);
        }
        finally
        {
            ResponseStatisticsTracker.CompleteRequest(startTimestamp, context);
        }
    }
}


public static class ResponseStatisticsTracker
{
    public record ResponseTimeStatistics(double average, List<(double durationMs, string path)> averages);

    record ResponseModel(DateTime timestamp, double durationMs, string path);

    static int activeHttpRequests;

    public static int ActiveHttpRequests
        => Volatile.Read(ref activeHttpRequests);

    static readonly ConcurrentQueue<ResponseModel> ResponseTimes = new();
    static readonly Timer CleanupTimer = new Timer(CleanupResponseTimes, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromSeconds(1);

    internal static long StartRequest()
    {
        Interlocked.Increment(ref activeHttpRequests);
        return Stopwatch.GetTimestamp();
    }

    internal static void CompleteRequest(long startTimestamp, HttpContext context)
    {
        Interlocked.Decrement(ref activeHttpRequests);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        if (elapsed < SlowRequestThreshold)
            return;

        ResponseTimes.Enqueue(new ResponseModel(
            DateTime.UtcNow,
            elapsed.TotalMilliseconds,
            context.Request.Path.Value
        ));
    }

    public static ResponseTimeStatistics GetResponseTimeStatsLastMinute()
    {
        var now = DateTime.UtcNow;

        double sum = 0;
        int count = 0;
        var durations = new List<(double durationMs, string path)>(ResponseTimes.Count);

        foreach (var item in ResponseTimes)
        {
            sum += item.durationMs;
            count++;
            durations.Add((item.durationMs, item.path));
        }

        return new ResponseTimeStatistics(count == 0 ? 0 : (sum / count), durations);
    }

    static void CleanupResponseTimes(object state)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-60);

        while (ResponseTimes.TryPeek(out var oldest) && oldest.timestamp < cutoff)
            ResponseTimes.TryDequeue(out _);
    }
}
