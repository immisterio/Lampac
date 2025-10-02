using Microsoft.AspNetCore.Http;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class RequestStatistics
    {
        private readonly RequestDelegate _next;

        public RequestStatistics(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            bool trackStats = !(context.Request.Path.StartsWithSegments("/ws") || context.Request.Path.StartsWithSegments("/nws"));
            Stopwatch stopwatch = null;

            if (trackStats && AppInit.conf.openstat.enable)
                stopwatch = RequestStatisticsTracker.StartRequest();

            try
            {
                await _next(context);
            }
            finally
            {
                if (trackStats && AppInit.conf.openstat.enable)
                    RequestStatisticsTracker.CompleteRequest(stopwatch);
            }
        }
    }

    public static class RequestStatisticsTracker
    {
        static int activeHttpRequests;
        static readonly ConcurrentQueue<(DateTime timestamp, double durationMs)> ResponseTimes = new();

        public static int ActiveHttpRequests => Volatile.Read(ref activeHttpRequests);

        internal static Stopwatch StartRequest()
        {
            Interlocked.Increment(ref activeHttpRequests);
            return Stopwatch.StartNew();
        }

        internal static void CompleteRequest(Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return;

            stopwatch.Stop();
            AddResponseTime(stopwatch.Elapsed.TotalMilliseconds);
            Interlocked.Decrement(ref activeHttpRequests);
        }

        static void AddResponseTime(double durationMs)
        {
            var now = DateTime.UtcNow;
            ResponseTimes.Enqueue((now, durationMs));
            CleanupResponseTimes(now);
        }

        static void CleanupResponseTimes(DateTime now)
        {
            while (ResponseTimes.TryPeek(out var oldest) && (now - oldest.timestamp).TotalSeconds > 60)
                ResponseTimes.TryDequeue(out _);
        }

        public static (double avg, double min, double max) GetResponseTimeStatsLastMinute()
        {
            var now = DateTime.UtcNow;
            CleanupResponseTimes(now);

            double sum = 0;
            int count = 0;
            var durations = new System.Collections.Generic.List<double>();

            foreach (var item in ResponseTimes)
            {
                sum += item.durationMs;
                count++;
                durations.Add(item.durationMs);
            }

            if (count == 0)
                return (0, 0, 0);

            durations.Sort();

            int minSampleSize = Math.Min(100, durations.Count);
            int maxSampleSize = Math.Min(100, durations.Count);

            double minAvg = AverageRange(durations, 0, minSampleSize);
            double maxAvg = AverageRange(durations, durations.Count - maxSampleSize, maxSampleSize);

            return (sum / count, minAvg, maxAvg);
        }

        static double AverageRange(System.Collections.Generic.List<double> sortedDurations, int startIndex, int length)
        {
            if (length <= 0)
                return 0;

            double total = 0;
            for (int i = 0; i < length; i++)
                total += sortedDurations[startIndex + i];

            return total / length;
        }
    }
}
