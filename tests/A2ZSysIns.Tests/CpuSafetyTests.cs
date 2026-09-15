using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public class CpuSafetyTests
    {
        private static CpuSafetyMetrics Metrics(double temp, double load, double clock)
        {
            return new CpuSafetyMetrics
            {
                TemperatureC = temp,
                CpuLoadPercent = load,
                AverageCoreClockMHz = clock,
                MaximumCoreClockMHz = clock
            };
        }

        [TestMethod]
        public void SingleClockOutlierDoesNotAbort()
        {
            var monitor = new CpuStressSafetyMonitor();
            monitor.SetStressBaseline(new[] { 1300d, 1320d, 1310d, 1290d, 1310d });

            var normal = monitor.Evaluate(Metrics(55, 85, 1300), true);
            var outlier = monitor.Evaluate(Metrics(55, 85, 800), true);
            var recovered = monitor.Evaluate(Metrics(56, 85, 1310), true);

            Assert.IsFalse(normal.Abort);
            Assert.IsFalse(outlier.Abort, "A single 800 MHz sample must not be treated as a real clock collapse.");
            Assert.IsFalse(recovered.Abort);
        }

        [TestMethod]
        public void SustainedClockDropAborts()
        {
            var monitor = new CpuStressSafetyMonitor();
            monitor.SetStressBaseline(new[] { 3500d, 3520d, 3480d, 3510d, 3500d });

            Assert.IsFalse(monitor.Evaluate(Metrics(70, 95, 3400), true).Abort);
            Assert.IsFalse(monitor.Evaluate(Metrics(72, 95, 2400), true).Abort);
            Assert.IsFalse(monitor.Evaluate(Metrics(73, 95, 2400), true).Abort);
            var decision = monitor.Evaluate(Metrics(74, 95, 2400), true);

            Assert.IsTrue(decision.Abort);
            StringAssert.Contains(decision.Reason, "clock collapse");
        }

        [TestMethod]
        public void SustainedClockSurgeAborts()
        {
            var monitor = new CpuStressSafetyMonitor();
            monitor.SetStressBaseline(new[] { 2500d, 2520d, 2480d, 2510d, 2500d });

            Assert.IsFalse(monitor.Evaluate(Metrics(65, 95, 2500), true).Abort);
            Assert.IsFalse(monitor.Evaluate(Metrics(66, 95, 3900), true).Abort);
            var decision = monitor.Evaluate(Metrics(67, 95, 3900), true);

            Assert.IsTrue(decision.Abort);
            StringAssert.Contains(decision.Reason, "clock surge");
        }

        [TestMethod]
        public void HardTemperatureImmediatelyAborts()
        {
            var monitor = new CpuStressSafetyMonitor();
            var decision = monitor.Evaluate(Metrics(90, 95, 3500), true);
            Assert.IsTrue(decision.Abort);
            StringAssert.Contains(decision.Reason, "90");
        }

        [TestMethod]
        public void EarlyTemperatureLimitAbortsBeforeHardLimit()
        {
            var monitor = new CpuStressSafetyMonitor();
            var decision = monitor.Evaluate(Metrics(87, 95, 3500), true);
            Assert.IsTrue(decision.Abort);
            StringAssert.Contains(decision.Reason, "87");
        }

        [TestMethod]
        public void MissingClockDuringStressAborts()
        {
            var monitor = new CpuStressSafetyMonitor();
            var decision = monitor.Evaluate(new CpuSafetyMetrics { TemperatureC = 60, CpuLoadPercent = 95 }, true);
            Assert.IsTrue(decision.Abort);
            StringAssert.Contains(decision.Reason, "clock telemetry");
        }

        [TestMethod]
        public void IdleBoostDoesNotBecomeStressBaselineFailure()
        {
            var monitor = new CpuStressSafetyMonitor();
            monitor.SetBaseline(800);
            var idle = monitor.Evaluate(Metrics(45, 5, 800), false);
            Assert.IsFalse(idle.Abort);

            monitor.SetStressBaseline(new[] { 3400d, 3500d, 3450d, 3480d, 3460d });
            var boosted = monitor.Evaluate(Metrics(65, 95, 3600), true);
            Assert.IsFalse(boosted.Abort);
        }
    }
}