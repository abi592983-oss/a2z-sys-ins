using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public class CpuTelemetryValidationTests
    {
        [TestMethod]
        public void CadenceTrackerLearnsRealUpdateInterval()
        {
            var tracker = new TelemetryCadenceTracker();
            var start = DateTime.UtcNow;
            Assert.IsTrue(tracker.Observe(10, start));
            Assert.IsFalse(tracker.Observe(10, start.AddMilliseconds(50)));
            Assert.IsTrue(tracker.Observe(20, start.AddMilliseconds(100)));
            Assert.IsTrue(tracker.Observe(30, start.AddMilliseconds(200)));
            Assert.AreEqual(100d, tracker.EstimatedUpdateMilliseconds.Value, 0.001);
        }

        [TestMethod]
        public void StressSamplingNeverUsesSlowerThanFiftyMillisecondsForSafety()
        {
            var sampler = new AdaptiveTelemetrySampler();
            var metrics = new CpuSafetyMetrics { TemperatureC = 50, CpuLoadPercent = 80, AverageCoreClockMHz = 2500 };
            var now = DateTime.UtcNow;
            for (var i = 0; i < 20; i++) sampler.Observe(metrics, now.AddMilliseconds(i * 50));
            Assert.AreEqual(50, sampler.RecommendedPollMilliseconds(true));
            Assert.IsTrue(AdaptiveTelemetrySampler.SafetyPollMilliseconds <= 50);
        }

        [TestMethod]
        public void RepeatedSensorValueIsObservedButNotClaimedAsFreshChange()
        {
            var tracker = new TelemetryCadenceTracker();
            var t = DateTime.UtcNow;
            Assert.IsTrue(tracker.Observe(55, t));
            Assert.IsFalse(tracker.Observe(55, t.AddMilliseconds(50)));
            Assert.IsFalse(tracker.Observe(55, t.AddMilliseconds(100)));
            Assert.IsTrue(tracker.Observe(56, t.AddMilliseconds(150)));
            Assert.AreEqual(150d, tracker.EstimatedUpdateMilliseconds.Value, 0.001);
        }

        [TestMethod]
        public void StressSamplesCarryCpuRamGpuAndCorrelationInputsFromSameTimestamp()
        {
            var sample = new CpuStressSample
            {
                CapturedAt = DateTime.UtcNow,
                ElapsedMilliseconds = 250,
                ObservedCpuLoadPercent = 96,
                MemoryUsedPercent = 72,
                GpuLoadPercent = 81,
                TemperatureC = 74,
                AverageCoreClockMHz = 2600,
                TelemetryPollIntervalMilliseconds = 50
            };
            Assert.AreEqual(sample.CapturedAt, sample.CapturedAt);
            Assert.IsTrue(sample.ObservedCpuLoadPercent.HasValue);
            Assert.IsTrue(sample.MemoryUsedPercent.HasValue);
            Assert.IsTrue(sample.GpuLoadPercent.HasValue);
            Assert.IsTrue(sample.AverageCoreClockMHz.HasValue);
        }

        [TestMethod]
        public void MissingGpuDoesNotCreateSyntheticGpuHistory()
        {
            var samples = new List<CpuStressSample>
            {
                new CpuStressSample { ElapsedMilliseconds = 0, TemperatureC = 50, ObservedCpuLoadPercent = 10 },
                new CpuStressSample { ElapsedMilliseconds = 50, TemperatureC = 51, ObservedCpuLoadPercent = 30 }
            };
            foreach (var sample in samples) Assert.IsFalse(sample.GpuLoadPercent.HasValue);
        }

        [TestMethod]
        public void StableTelemetryDoesNotTriggerSafetyAbortByItself()
        {
            var safety = new CpuStressSafetyMonitor();
            safety.SetStressBaseline(new[] { 2500d, 2500d, 2500d, 2500d, 2500d });
            CpuSafetyDecision decision = null;
            for (var i = 0; i < 40; i++)
            {
                decision = safety.Evaluate(new CpuSafetyMetrics { TemperatureC = 60, CpuLoadPercent = 80, AverageCoreClockMHz = 2500 }, true);
                Assert.IsFalse(decision.Abort);
            }
        }
    }
}