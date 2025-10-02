using Microsoft.AspNetCore.Http;
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

            if (trackStats)
                stopwatch = RequestStatisticsTracker.StartRequest();

            try
            {
                await _next(context);
            }
            finally
            {
                if (trackStats)
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
            double min = double.MaxValue;
            double max = double.MinValue;

            foreach (var item in ResponseTimes)
            {
                sum += item.durationMs;
                count++;
                if (item.durationMs < min)
                    min = item.durationMs;
                if (item.durationMs > max)
                    max = item.durationMs;
            }

            if (count == 0)
                return (0, 0, 0);

            return (sum / count, min, max);
        }
    }
}
