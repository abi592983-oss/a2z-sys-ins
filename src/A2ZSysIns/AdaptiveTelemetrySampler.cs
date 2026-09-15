using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    internal sealed class TelemetryCadenceTracker
    {
        private readonly Queue<double> _intervals = new Queue<double>();
        private double? _lastValue;
        private DateTime? _lastChangedAt;
        public double? EstimatedUpdateMilliseconds { get; private set; }
        public DateTime? LastChangedAt { get { return _lastChangedAt; } }

        public bool Observe(double? value, DateTime capturedAt)
        {
            if (!value.HasValue) return false;
            var changed = !_lastValue.HasValue || Math.Abs(_lastValue.Value - value.Value) > 0.0001;
            if (changed)
            {
                if (_lastChangedAt.HasValue)
                {
                    var interval = (capturedAt - _lastChangedAt.Value).TotalMilliseconds;
                    if (interval >= 1 && interval <= 10000)
                    {
                        _intervals.Enqueue(interval);
                        while (_intervals.Count > 9) _intervals.Dequeue();
                        EstimatedUpdateMilliseconds = Median(_intervals);
                    }
                }
                _lastChangedAt = capturedAt; _lastValue = value.Value;
            }
            return changed;
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray(); if (sorted.Length == 0) return 0;
            var m = sorted.Length / 2; return sorted.Length % 2 == 0 ? (sorted[m - 1] + sorted[m]) / 2.0 : sorted[m];
        }
    }

    internal sealed class AdaptiveTelemetrySampler
    {
        internal const int SafetyPollMilliseconds = 50;
        internal const int MinimumPollMilliseconds = 25;
        internal const int MaximumPollMilliseconds = 1000;
        private readonly TelemetryCadenceTracker _cpuLoad = new TelemetryCadenceTracker();
        private readonly TelemetryCadenceTracker _cpuClock = new TelemetryCadenceTracker();
        private readonly TelemetryCadenceTracker _cpuTemperature = new TelemetryCadenceTracker();
        private readonly TelemetryCadenceTracker _memory = new TelemetryCadenceTracker();
        private readonly TelemetryCadenceTracker _gpuLoad = new TelemetryCadenceTracker();
        private readonly TelemetryCadenceTracker _gpuTemperature = new TelemetryCadenceTracker();

        public int Observe(CpuSafetyMetrics metrics, DateTime capturedAt, bool underStress)
        {
            _cpuLoad.Observe(metrics == null ? (double?)null : metrics.CpuLoadPercent, capturedAt);
            _cpuClock.Observe(metrics == null ? (double?)null : metrics.AverageCoreClockMHz, capturedAt);
            _cpuTemperature.Observe(metrics == null ? (double?)null : metrics.TemperatureC, capturedAt);
            _memory.Observe(metrics == null ? (double?)null : metrics.MemoryUsedPercent, capturedAt);
            _gpuLoad.Observe(metrics == null ? (double?)null : metrics.GpuLoadPercent, capturedAt);
            _gpuTemperature.Observe(metrics == null ? (double?)null : metrics.GpuTemperatureC, capturedAt);
            return RecommendedPollMilliseconds(underStress);
        }

        // During active stress, safety remains at 50 ms or faster. During preflight,
        // the fastest observed sensor cadence determines the next polling interval.
        public int RecommendedPollMilliseconds(bool underStress)
        {
            if (underStress) return SafetyPollMilliseconds;
            var estimates = new[] { _cpuLoad.EstimatedUpdateMilliseconds, _cpuClock.EstimatedUpdateMilliseconds, _cpuTemperature.EstimatedUpdateMilliseconds, _memory.EstimatedUpdateMilliseconds, _gpuLoad.EstimatedUpdateMilliseconds, _gpuTemperature.EstimatedUpdateMilliseconds }
                .Where(x => x.HasValue).Select(x => x.Value).ToArray();
            if (estimates.Length == 0) return SafetyPollMilliseconds;
            var fastest = estimates.Min();
            return (int)Math.Max(MinimumPollMilliseconds, Math.Min(MaximumPollMilliseconds, Math.Round(fastest / 2.0)));
        }

        public string DescribeFreshness(CpuSafetyMetrics metrics)
        {
            return "CPU load " + Describe(_cpuLoad) + "; clock " + Describe(_cpuClock) + "; CPU temp " + Describe(_cpuTemperature) + "; RAM " + Describe(_memory) + "; GPU load " + Describe(_gpuLoad) + "; GPU temp " + Describe(_gpuTemperature);
        }

        private static string Describe(TelemetryCadenceTracker tracker) => tracker.EstimatedUpdateMilliseconds.HasValue ? "~" + tracker.EstimatedUpdateMilliseconds.Value.ToString("0") + " ms" : "learning";
    }
}