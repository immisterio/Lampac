using Microsoft.AspNetCore.Http;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public static ResponseTimeStatistics GetResponseTimeStatsLastMinute()
        {
            var now = DateTime.UtcNow;
            CleanupResponseTimes(now);

            double sum = 0;
            int count = 0;
            var durations = new List<double>(ResponseTimes.Count);

            foreach (var item in ResponseTimes)
            {
                sum += item.durationMs;
                count++;
                durations.Add(item.durationMs);
            }

            if (count == 0)
            {
                return new ResponseTimeStatistics
                {
                    Average = 0,
                    PercentileAverages = InitializePercentileDictionary()
                };
            }

            durations.Sort();

            return new ResponseTimeStatistics
            {
                Average = sum / count,
                PercentileAverages = CalculatePercentileAverages(durations)
            };
        }

        static Dictionary<int, double> CalculatePercentileAverages(List<double> sortedDurations)
        {
            const int bucketCount = 10;
            var result = InitializePercentileDictionary();

            int total = sortedDurations.Count;
            int baseSize = total / bucketCount;
            int remainder = total % bucketCount;
            int currentIndex = 0;

            for (int i = 1; i <= bucketCount; i++)
            {
                int key = i * 10;
                int bucketSize = baseSize + (i <= remainder ? 1 : 0);

                if (bucketSize > 0)
                {
                    result[key] = AverageRange(sortedDurations, currentIndex, bucketSize);
                    currentIndex += bucketSize;
                }
            }

            return result;
        }

        static Dictionary<int, double> InitializePercentileDictionary()
        {
            var dict = new Dictionary<int, double>();
            for (int i = 1; i <= 10; i++)
                dict[i * 10] = 0;

            return dict;
        }

        static double AverageRange(List<double> sortedDurations, int startIndex, int length)
        {
            if (length <= 0)
                return 0;

            double total = 0;
            for (int i = 0; i < length; i++)
                total += sortedDurations[startIndex + i];

            return total / length;
        }

        public class ResponseTimeStatistics
        {
            public double Average { get; set; }

            public Dictionary<int, double> PercentileAverages { get; set; } = new();
        }
    }
}
