using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public sealed class InspectionStageRunnerTests
    {
        [TestMethod]
        public void CollectorException_IsRecorded_AndDoesNotPreventNextStage()
        {
            var report = new InspectionReport { InspectionId = "stage-test" };
            var failed = InspectionStageRunner.RunAsync(report, "Sensors", () => { throw new InvalidOperationException("provider unavailable"); }, CancellationToken.None, null).GetAwaiter().GetResult();
            var next = InspectionStageRunner.RunAsync(report, "Storage", () => EvidenceEngine.Record(report, "Storage", "fixture", "Observed", "fixture evidence"), CancellationToken.None, null).GetAwaiter().GetResult();

            Assert.AreEqual("ERROR", failed.Status);
            Assert.IsTrue(failed.SubsequentStagesMayContinue);
            Assert.AreEqual("PASS", next.Status);
            Assert.IsTrue(report.Limitations.Exists(x => x.Contains("Sensors")));
            Assert.AreEqual(2, report.Stages.Count);
        }

        [TestMethod]
        public void CancelledStage_IsNotReportedAsHealthy()
        {
            var report = new InspectionReport { InspectionId = "cancel-test" };
            var source = new CancellationTokenSource(); source.Cancel();
            try
            {
                InspectionStageRunner.RunAsync(report, "Storage", () => { }, source.Token, null).GetAwaiter().GetResult();
                Assert.Fail("Cancellation should propagate to the session controller.");
            }
            catch (OperationCanceledException) { }

            Assert.AreEqual(1, report.Stages.Count);
            Assert.AreEqual("SKIPPED", report.Stages[0].Status);
            Assert.IsFalse(string.Equals(report.Stages[0].Status, "PASS", StringComparison.Ordinal));
        }
    }
}
