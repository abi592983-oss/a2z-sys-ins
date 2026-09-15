using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    internal sealed class CpuSafetyMetrics
    {
        public double? TemperatureC;
        public double? CpuLoadPercent;
        public double? AverageCoreClockMHz;
        public double? MaximumCoreClockMHz;
        public double? FanRpm;
    }

    internal sealed class CpuSafetyDecision
    {
        public bool Abort;
        public string Reason;
        public string Assessment = "Observed";
        public bool SampleValid = true;
    }

    internal sealed class CpuStressSafetyMonitor
    {
        internal const double HardTemperatureC = 90.0;
        internal const double EarlyTemperatureC = 87.0;
        internal const double PreflightTemperatureC = 80.0;
        internal const double MinimumUsableClockMHz = 100.0;
        internal const int RequiredSustainedSamples = 3;
        internal const int HistoryLimit = 8;

        private readonly Queue<double> _clocks = new Queue<double>();
        private int _clockDropCount;
        private int _clockSurgeCount;
        private int _unchangedTelemetryCount;
        private CpuSafetyMetrics _last;
        private bool _stressBaselineSet;

        public double? BaselineClockMHz { get; private set; }

        public void SetBaseline(double clockMHz)
        {
            if (clockMHz >= MinimumUsableClockMHz && clockMHz < 10000)
                BaselineClockMHz = clockMHz;
        }

        // Idle CPU clocks are intentionally not treated as the stress baseline: power management
        // and boost can legitimately move them by large amounts. Establish this only after load begins.
        public void SetStressBaseline(IEnumerable<double> clocks)
        {
            var valid = clocks == null ? new double[0] : clocks.Where(IsUsableClock).ToArray();
            if (valid.Length == 0) return;
            BaselineClockMHz = Median(valid);
            _stressBaselineSet = true;
            _clocks.Clear();
            foreach (var value in valid) Add(_clocks, value);
            _clockDropCount = 0;
            _clockSurgeCount = 0;
        }

        public CpuSafetyDecision Evaluate(CpuSafetyMetrics metrics, bool underStress)
        {
            if (metrics == null)
                return AbortNow("Safety monitoring returned no telemetry.");

            if (!metrics.TemperatureC.HasValue || metrics.TemperatureC.Value <= 0 || metrics.TemperatureC.Value >= 150)
                return AbortNow("CPU temperature telemetry was lost or became invalid.");

            if (metrics.TemperatureC.Value >= HardTemperatureC)
                return AbortNow("CPU reached the 90 °C hard safety limit.");

            if (underStress && !metrics.AverageCoreClockMHz.HasValue)
                return AbortNow("CPU clock telemetry was lost during the stress test.");

            if (underStress && (!metrics.CpuLoadPercent.HasValue || metrics.CpuLoadPercent.Value < 0 || metrics.CpuLoadPercent.Value > 100))
                return AbortNow("CPU load telemetry was lost or became invalid during the stress test.");

            if (metrics.AverageCoreClockMHz.HasValue && !IsUsableClock(metrics.AverageCoreClockMHz.Value))
                return AbortNow("CPU clock telemetry became physically implausible.");

            if (metrics.CpuLoadPercent.HasValue && (metrics.CpuLoadPercent.Value < 0 || metrics.CpuLoadPercent.Value > 100))
                return AbortNow("CPU load telemetry became physically implausible.");

            if (metrics.TemperatureC.Value >= EarlyTemperatureC)
                return AbortNow("CPU temperature reached the 87 °C early-abort threshold.");

            if (metrics.AverageCoreClockMHz.HasValue)
            {
                Add(_clocks, metrics.AverageCoreClockMHz.Value);

                if (underStress && _stressBaselineSet && metrics.CpuLoadPercent.GetValueOrDefault() >= 70)
                {
                    var median = Median(_clocks);
                    var dropRatio = 1.0 - metrics.AverageCoreClockMHz.Value / Math.Max(1.0, median);
                    var surgeRatio = metrics.AverageCoreClockMHz.Value / Math.Max(1.0, median) - 1.0;
                    var baselineDrop = 1.0 - metrics.AverageCoreClockMHz.Value / Math.Max(1.0, BaselineClockMHz.Value);
                    var baselineSurge = metrics.AverageCoreClockMHz.Value / Math.Max(1.0, BaselineClockMHz.Value) - 1.0;

                    if (dropRatio >= 0.30 || baselineDrop >= 0.35) _clockDropCount++; else _clockDropCount = 0;
                    if (surgeRatio >= 0.50 || baselineSurge >= 0.60) _clockSurgeCount++; else _clockSurgeCount = 0;

                    if (_clockDropCount >= RequiredSustainedSamples)
                        return AbortNow("Sustained CPU clock collapse detected under load; possible thermal, power, or stability throttling.");
                    if (_clockSurgeCount >= 2)
                        return AbortNow("Sustained abnormal CPU clock surge detected under load; possible unstable frequency behavior.");
                }
            }

            if (underStress && metrics.CpuLoadPercent.HasValue && metrics.CpuLoadPercent.Value >= 70 && _last != null)
            {
                var sameClock = metrics.AverageCoreClockMHz.HasValue && _last.AverageCoreClockMHz.HasValue &&
                                 Math.Abs(metrics.AverageCoreClockMHz.Value - _last.AverageCoreClockMHz.Value) < 1.0;
                var sameLoad = metrics.CpuLoadPercent.HasValue && _last.CpuLoadPercent.HasValue &&
                               Math.Abs(metrics.CpuLoadPercent.Value - _last.CpuLoadPercent.Value) < 0.1;
                var sameTemp = metrics.TemperatureC.HasValue && _last.TemperatureC.HasValue &&
                               Math.Abs(metrics.TemperatureC.Value - _last.TemperatureC.Value) < 0.1;
                if (sameClock && sameLoad && sameTemp) _unchangedTelemetryCount++; else _unchangedTelemetryCount = 0;
                if (_unchangedTelemetryCount >= 12)
                    return AbortNow("CPU telemetry appears stale during active stress; safety monitoring can no longer be trusted.");
            }

            _last = metrics;
            return new CpuSafetyDecision
            {
                Assessment = metrics.TemperatureC.Value >= 85 ? "High temperature — closely monitored" : "Observed",
                SampleValid = true
            };
        }

        private CpuSafetyDecision AbortNow(string reason)
        {
            return new CpuSafetyDecision { Abort = true, Reason = reason, Assessment = "ABORT" };
        }

        private static bool IsUsableClock(double value) => value >= MinimumUsableClockMHz && value < 10000;

        private static void Add(Queue<double> queue, double value)
        {
            queue.Enqueue(value);
            while (queue.Count > HistoryLimit) queue.Dequeue();
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            if (sorted.Length == 0) return 0;
            var middle = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2.0 : sorted[middle];
        }
    }
}