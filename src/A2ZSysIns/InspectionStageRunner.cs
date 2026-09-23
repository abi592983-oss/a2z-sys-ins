using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace A2ZSysIns
{
    internal static class InspectionStageRunner
    {
        public static async Task<InspectionStageResult> RunAsync(InspectionReport report, string name, Action action, CancellationToken cancellation, IProgress<InspectionStageResult> progress)
        {
            var stage = new InspectionStageResult { Name = name, StartedAt = DateTime.Now, Status = "INDETERMINATE" };
            report.Stages.Add(stage); progress?.Report(stage);
            var timer = Stopwatch.StartNew(); var before = report.Measurements.Count + report.DiagnosticLog.Count;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                await Task.Run(action, cancellation).ConfigureAwait(false);
                stage.Status = HasLimitation(report, before) ? "WARNING" : "PASS";
                stage.Reason = stage.Status == "PASS" ? "Stage completed." : "Stage completed with unavailable or limited evidence; see measurements and diagnostic log.";
            }
            catch (OperationCanceledException)
            {
                stage.Status = "SKIPPED"; stage.Reason = "Cancelled before stage completion.";
                throw;
            }
            catch (Exception ex)
            {
                // A collector is non-fatal by default. Preserve the exception and continue.
                stage.Status = "ERROR"; stage.Reason = "Stage failed; subsequent independent stages continued.";
                stage.Error = ex.ToString();
                report.Limitations.Add(name + ": " + ex.GetBaseException().Message);
                EvidenceEngine.Log(report, "Stage failed", name + "\n" + ex);
            }
            finally
            {
                timer.Stop(); stage.FinishedAt = DateTime.Now; stage.DurationMilliseconds = timer.ElapsedMilliseconds;
                stage.EvidenceCount = Math.Max(0, report.Measurements.Count + report.DiagnosticLog.Count - before);
                progress?.Report(stage);
            }
            return stage;
        }

        public static void Preflight(InspectionReport report)
        {
            report.System["Inspector version"] = typeof(InspectionStageRunner).Assembly.GetName().Version.ToString();
            report.System["Inspector build"] = typeof(InspectionStageRunner).Assembly.GetName().Version.ToString();
            report.System["Session ID"] = report.InspectionId;
            report.System["Process architecture"] = Environment.Is64BitProcess ? "x64" : "x86";
            report.System["Operating system"] = Environment.OSVersion.VersionString;
            report.System["System time"] = DateTime.Now.ToString("o");
            report.IsAdministrator = IsAdministrator();
            EvidenceEngine.Record(report, "Preflight / elevation", "Windows principal", report.IsAdministrator ? "Observed" : "Unavailable", report.IsAdministrator ? "Administrator token detected." : "Not elevated; protected interfaces may be unavailable.");
            try
            {
                var root = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                var drive = DriveInfo.GetDrives().FirstOrDefault(x => x.IsReady && string.Equals(x.Name, root, StringComparison.OrdinalIgnoreCase));
                if (drive == null) EvidenceEngine.Record(report, "Preflight / application disk space", "DriveInfo", "Unavailable", "Application drive could not be measured.");
                else EvidenceEngine.Record(report, "Preflight / application disk space", "DriveInfo", "Observed", "Free space: " + Collectors.FormatBytes(drive.AvailableFreeSpace));
            }
            catch (Exception ex) { EvidenceEngine.Record(report, "Preflight / application disk space", "DriveInfo", "Unavailable", ex.GetBaseException().Message); }
            var smartctl = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "smartctl.exe");
            EvidenceEngine.Record(report, "Preflight / smartctl", "Packaged component", File.Exists(smartctl) ? "Observed" : "Unavailable", File.Exists(smartctl) ? "Bundled smartctl.exe available." : "Bundled smartctl.exe is missing; storage fallback paths may still be attempted.");
        }

        private static bool HasLimitation(InspectionReport report, int before)
        {
            return report.Measurements.Skip(Math.Min(before, report.Measurements.Count)).Any(x => x != null &&
                (string.Equals(x.Status, "Unavailable", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsAdministrator()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }
    }
}
