using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace A2ZSysIns
{
    internal sealed class InspectionProgressTracker
    {
        private readonly Stopwatch _total = Stopwatch.StartNew();
        private readonly Dictionary<string, double> _expectedSeconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "System", 8 }, { "Resources", 8 }, { "Events", 12 }, { "Storage", 35 },
            { "Advanced", 18 }, { "Sensors", 18 }, { "Integrity", 150 }, { "Assessment", 8 }
        };
        private string _stage;
        private Stopwatch _stageClock;
        private DateTime _lastActivityUtc;
        private double _completedWeight;

        public void Begin(string stage)
        {
            _stage = stage;
            _stageClock = Stopwatch.StartNew();
            _lastActivityUtc = DateTime.UtcNow;
        }

        public void Activity() { _lastActivityUtc = DateTime.UtcNow; }

        public void Complete(string stage)
        {
            Activity();
            if (_stageClock != null && _stageClock.Elapsed.TotalSeconds > 0.5)
                _expectedSeconds[stage] = Math.Max(2, _expectedSeconds.ContainsKey(stage) ? (_expectedSeconds[stage] * 0.65 + _stageClock.Elapsed.TotalSeconds * 0.35) : _stageClock.Elapsed.TotalSeconds);
            _completedWeight += Weight(stage);
        }

        public ProgressSnapshot Snapshot()
        {
            var idle = DateTime.UtcNow - _lastActivityUtc;
            var stalled = _stageClock != null && idle.TotalSeconds >= Math.Max(20, Expected(_stage) * 0.35);
            var stageElapsed = _stageClock == null ? 0 : _stageClock.Elapsed.TotalSeconds;
            var stageExpected = Expected(_stage);
            var stageFraction = stalled ? Math.Min(0.92, stageElapsed / Math.Max(stageExpected, 1)) : Math.Min(0.92, stageElapsed / Math.Max(stageExpected, 1));
            var done = Math.Min(0.98, (_completedWeight + Weight(_stage) * stageFraction) / 100.0);
            var remaining = stalled ? (TimeSpan?)null : TimeSpan.FromSeconds(Math.Max(0, RemainingExpected() - Math.Min(stageElapsed, stageExpected)));
            return new ProgressSnapshot
            {
                Percent = (int)Math.Round(done * 100),
                Elapsed = _total.Elapsed,
                Remaining = remaining,
                IsWaiting = stalled,
                WaitingFor = idle,
                Stage = _stage
            };
        }

        private double RemainingExpected()
        {
            return Math.Max(0, 100 - _completedWeight - Weight(_stage)) * 1.2 + Expected(_stage);
        }

        private double Expected(string stage) { return stage != null && _expectedSeconds.ContainsKey(stage) ? _expectedSeconds[stage] : 15; }
        private static double Weight(string stage)
        {
            switch (stage)
            {
                case "System": return 8; case "Resources": return 8; case "Events": return 10; case "Storage": return 20;
                case "Advanced": return 12; case "Sensors": return 14; case "Integrity": return 20; case "Assessment": return 8;
                default: return 0;
            }
        }

        public static string Format(TimeSpan value)
        {
            if (value.TotalHours >= 1) return ((int)value.TotalHours).ToString(CultureInfo.InvariantCulture) + "h " + value.Minutes + "m";
            if (value.TotalMinutes >= 1) return ((int)value.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m " + value.Seconds + "s";
            return Math.Max(0, value.Seconds) + "s";
        }
    }

    internal sealed class ProgressSnapshot
    {
        public int Percent;
        public TimeSpan Elapsed;
        public TimeSpan? Remaining;
        public bool IsWaiting;
        public TimeSpan WaitingFor;
        public string Stage;
    }
}
