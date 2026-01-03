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
            if (!AppInit.conf.openstat.enable)
            {
                await _next(context);
                return;
            }

            Stopwatch stopwatch = RequestStatisticsTracker.StartRequest();

            try
            {
                await _next(context);
            }
            finally
            {
                RequestStatisticsTracker.CompleteRequest(stopwatch);
            }
        }
    }

    public static class RequestStatisticsTracker
    {
        static int activeHttpRequests;
        static readonly ConcurrentQueue<(DateTime timestamp, double durationMs)> ResponseTimes = new();

        static readonly Timer CleanupTimer = new Timer(CleanupResponseTimes, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        internal static Stopwatch StartRequest()
        {
            Interlocked.Increment(ref activeHttpRequests);
            return Stopwatch.StartNew();
        }

        internal static void CompleteRequest(Stopwatch stopwatch)
        {
            Interlocked.Decrement(ref activeHttpRequests);

            if (stopwatch == null)
                return;

            stopwatch.Stop();
            AddResponseTime(stopwatch.Elapsed.TotalMilliseconds);
        }

        static void AddResponseTime(double durationMs)
        {
            ResponseTimes.Enqueue((DateTime.UtcNow, durationMs));
        }

        static void CleanupResponseTimes(object state)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-60);

            while (ResponseTimes.TryPeek(out var oldest) && oldest.timestamp < cutoff)
                ResponseTimes.TryDequeue(out _);
        }


        #region openstat
        public static int ActiveHttpRequests => Volatile.Read(ref activeHttpRequests);

        public static ResponseTimeStatistics GetResponseTimeStatsLastMinute()
        {
            var now = DateTime.UtcNow;

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
        #endregion
    }
}
