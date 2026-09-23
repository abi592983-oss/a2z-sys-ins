using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public sealed class ProcessExecutionServiceTests
    {
        private static string Cmd => Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

        [TestMethod]
        public void CapturesStandardOutput_StandardError_AndExitCode()
        {
            var result = ProcessExecutionService.RunAsync(new ProcessExecutionRequest
            {
                FileName = Cmd, Arguments = "/c echo out& echo err 1>&2& exit /b 7", TimeoutMilliseconds = 5000
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Started);
            Assert.AreEqual(7, result.ExitCode);
            StringAssert.Contains(result.StandardOutput, "out");
            StringAssert.Contains(result.StandardError, "err");
            Assert.IsTrue(result.LastOutputAt.HasValue);
        }

        [TestMethod]
        public void Cancellation_TerminatesOnlyStartedChildProcess()
        {
            var started = new ManualResetEventSlim(false);
            var cancel = new CancellationTokenSource();
            var task = ProcessExecutionService.RunAsync(new ProcessExecutionRequest
            {
                FileName = Cmd, Arguments = "/c ping -n 30 127.0.0.1 > nul", TimeoutMilliseconds = 60000,
                Started = id => started.Set()
            }, cancel.Token);
            Assert.IsTrue(started.Wait(5000), "The owned child process did not start.");
            cancel.Cancel();
            try { task.GetAwaiter().GetResult(); Assert.Fail("Cancellation must propagate."); }
            catch (OperationCanceledException) { }
        }
    }
}
