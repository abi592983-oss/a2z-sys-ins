using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace A2ZSysIns
{
    internal static class PawnIoResidueCleaner
    {
        public static void VerifyPreviousCleanupAfterRestart()
        {
            string previousSession, previousStatus, previousLog;
            if (!SessionRecoveryJournal.TryGetUnresolvedPawnIoCleanup(out previousSession, out previousStatus, out previousLog)) return;
            if (string.IsNullOrWhiteSpace(previousSession) || !JournalProvesOwnership(previousSession)) return;

            try
            {
                string queryOutput;
                var scmPresent = QueryService(out queryOutput);
                var serviceKey = ServiceKeyPresent();
                var uninstallRegistry = UninstallRegistrationPresent();
                var baselineInf = BaselinePawnIoInf(previousSession);
                var currentInf = FindPawnIoOemInf();
                var unexpectedInf = currentInf.Except(baselineInf, StringComparer.OrdinalIgnoreCase).ToArray();
                var clean = !scmPresent && !serviceKey && !uninstallRegistry && unexpectedInf.Length == 0;
                var state = "previousStatus=" + previousStatus +
                    "; scmService=" + scmPresent +
                    "; serviceKey=" + serviceKey +
                    "; uninstallRegistry=" + uninstallRegistry +
                    "; unexpectedDriverStoreInf=" + string.Join(",", unexpectedInf) +
                    "; previousLog=" + (previousLog ?? "");

                if (clean)
                {
                    PortableSessionLog.Write("Previous PawnIO cleanup verified after restart", "Session=" + previousSession + "; " + state);
                    SessionRecoveryJournal.RecordCleanupForSession(previousSession, "driver", "PawnIO-2.2.0", "VERIFIED_AFTER_REBOOT",
                        "First subsequent Inspector launch found no Inspector-added PawnIO SCM service, service key, uninstall registration, or DriverStore package. " + state);
                    SessionRecoveryJournal.RecordCleanupForSession(previousSession, "service", "PawnIO", "VERIFIED_AFTER_REBOOT",
                        "PawnIO service is absent after Windows restart. " + state);
                }
                else
                {
                    PortableSessionLog.Write("Previous PawnIO cleanup NOT verified after restart", "Session=" + previousSession + "; " + state + "; scQuery=" + OneLine(queryOutput));
                    SessionRecoveryJournal.RecordCleanupForSession(previousSession, "driver", "PawnIO-2.2.0", "POST_REBOOT_RESIDUE",
                        "A previous Inspector-owned PawnIO cleanup remains unresolved. Automatic deletion is not attempted here because the machine may have changed since the previous session. " + state);
                }
            }
            catch (Exception ex)
            {
                PortableSessionLog.Write("Previous PawnIO post-restart verification exception", ex.ToString());
                SessionRecoveryJournal.RecordCleanupForSession(previousSession, "driver", "PawnIO-2.2.0", "POST_REBOOT_VERIFY_FAILED", ex.ToString());
            }
        }

        public static void CleanupCurrentSessionServiceResidue()
        {
            var session = PortableSessionLog.SessionId;
            if (string.IsNullOrWhiteSpace(session)) return;
            if (!JournalProvesOwnership(session)) return;
            CleanupServiceResidue("current session " + session);
        }

        public static void RecoverPreviousIncompleteSessionServiceResidue()
        {
            string previousSession, previousLog;
            if (!SessionRecoveryJournal.HasIncompleteSession(out previousSession, out previousLog)) return;
            if (string.IsNullOrWhiteSpace(previousSession) || !JournalProvesOwnership(previousSession)) return;
            CleanupServiceResidue("previous incomplete session " + previousSession);
        }

        private static void CleanupServiceResidue(string reason)
        {
            try
            {
                string queryOutput;
                var exists = QueryService(out queryOutput);
                if (!ServiceKeyPresent() && !exists)
                {
                    PortableSessionLog.Write("PawnIO service residue cleanup", "No PawnIO service residue detected for " + reason + ".");
                    return;
                }

                PortableSessionLog.Write("PawnIO service residue cleanup", "Ownership proven; attempting SCM deletion for " + reason + ".");
                PortableSessionLog.Write("PawnIO service state before delete", queryOutput);

                string output;
                int deleteExit = RunSc("delete PawnIO", out output);
                PortableSessionLog.Write("PawnIO sc delete", "Exit=" + deleteExit + " " + output);

                for (var i = 0; i < 20; i++)
                {
                    if (!QueryService(out queryOutput) && !ServiceKeyPresent())
                    {
                        PortableSessionLog.RecordCleanup("service", "PawnIO", "VERIFIED", "SCM service and service registry key absent after ownership-gated deletion.");
                        SessionRecoveryJournal.RecordCleanup("service", "PawnIO", "VERIFIED", "SCM service and service registry key absent after ownership-gated deletion.");
                        return;
                    }
                    Thread.Sleep(250);
                }

                var stillExists = QueryService(out queryOutput);
                var keyPresent = ServiceKeyPresent();
                var runningKernelDriver = stillExists &&
                    queryOutput.IndexOf("KERNEL_DRIVER", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    queryOutput.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;

                var state = "scExists=" + stillExists + "; serviceKey=" + keyPresent + "; query=" + OneLine(queryOutput);
                if (runningKernelDriver)
                {
                    PortableSessionLog.RecordCleanup("service", "PawnIO", "REBOOT_REQUIRED",
                        state + ". PawnIO remains loaded as a non-stoppable kernel driver; service deletion has been requested and final unload requires Windows restart.");
                    SessionRecoveryJournal.RecordCleanup("service", "PawnIO", "REBOOT_REQUIRED", state);
                }
                else
                {
                    PortableSessionLog.RecordCleanup("service", "PawnIO", "PENDING_OR_RESIDUE",
                        state + ". Windows may still have the service marked for deletion until handles close or the system restarts.");
                    SessionRecoveryJournal.RecordCleanup("service", "PawnIO", "PENDING_OR_RESIDUE", state);
                }
            }
            catch (Exception ex)
            {
                PortableSessionLog.RecordCleanup("service", "PawnIO", "FAILED", ex.ToString());
                SessionRecoveryJournal.RecordCleanup("service", "PawnIO", "FAILED", ex.ToString());
            }
        }

        private static bool JournalProvesOwnership(string session)
        {
            try
            {
                var path = SessionRecoveryJournal.PathName;
                if (!File.Exists(path)) return false;
                foreach (var line in File.ReadLines(path))
                {
                    var parts = line.Split(new[] { '|' }, 4);
                    if (parts.Length < 4) continue;
                    if (!string.Equals(parts[2], session, StringComparison.Ordinal)) continue;
                    if (!string.Equals(parts[1], "OWNED_RESOURCE", StringComparison.Ordinal)) continue;
                    if (parts[3].IndexOf("driver¦PawnIO-2.2.0", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static HashSet<string> BaselinePawnIoInf(string session)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var detail = SessionRecoveryJournal.GetOwnedResourceDetail(session, "planned-driver-install", "PawnIO-2.2.0") ?? "";
            var marker = "baselineInf=";
            var index = detail.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return result;
            var raw = detail.Substring(index + marker.Length);
            var delimiter = raw.IndexOf('¦');
            if (delimiter >= 0) raw = raw.Substring(0, delimiter);
            foreach (var item in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) result.Add(item.Trim());
            return result;
        }

        private static HashSet<string> FindPawnIoOemInf()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var infDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
                foreach (var path in Directory.GetFiles(infDir, "oem*.inf"))
                {
                    try
                    {
                        var text = File.ReadAllText(path);
                        if (text.IndexOf("PawnIO", StringComparison.OrdinalIgnoreCase) >= 0) result.Add(Path.GetFileName(path));
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        private static bool UninstallRegistrationPresent()
        {
            return UninstallRegistrationPresent(RegistryView.Registry64) || UninstallRegistrationPresent(RegistryView.Registry32);
        }

        private static bool UninstallRegistrationPresent(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))
                    return key != null;
            }
            catch { return true; }
        }

        private static bool ServiceKeyPresent()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO"))
                    return key != null;
            }
            catch { return true; }
        }

        private static bool QueryService(out string output)
        {
            var exit = RunSc("query PawnIO", out output);
            if (exit == 0) return true;
            return output.IndexOf("1060", StringComparison.OrdinalIgnoreCase) < 0 &&
                   output.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string OneLine(string value)
        {
            return (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static int RunSc(string args, out string output)
        {
            var sc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe");
            using (var p = new Process
            {
                StartInfo = new ProcessStartInfo(sc, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            })
            {
                p.Start();
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(15000))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("sc.exe timed out while processing: " + args);
                }
                output = (stdout + " " + stderr).Trim();
                return p.ExitCode;
            }
        }
    }
}
