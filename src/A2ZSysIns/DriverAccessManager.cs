using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace A2ZSysIns
{
    internal sealed class SensorDriverStatus
    {
        public string Backend { get; set; }
        public bool Installed { get; set; }
        public Version Version { get; set; }
        public string Detail { get; set; }
        public string BundledInstaller { get; set; }
    }

    internal static class DriverAccessManager
    {
        public static readonly Version RecommendedPawnIoVersion = new Version(2, 2, 0);
        private const string ExpectedSha256 = "1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032";
        private static readonly object Sync = new object();
        private static bool _ownedPawnIo;
        private static HashSet<string> _baselinePawnIoInf = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static SensorDriverStatus DetectPawnIo()
        {
            var status = new SensorDriverStatus
            {
                Backend = "PawnIO",
                Installed = false,
                Detail = "PawnIO was not detected in the Windows uninstall registry.",
                BundledInstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "pawnio", "2.2.0", "PawnIO_setup.exe")
            };

            Version version;
            string raw;
            if (TryReadVersion(RegistryView.Registry64, out raw) || TryReadVersion(RegistryView.Registry32, out raw))
            {
                status.Installed = true;
                if (Version.TryParse(raw, out version)) status.Version = version;
                status.Detail = status.Version == null
                    ? "PawnIO is installed, but its version could not be parsed. Inspector will not modify this pre-existing installation."
                    : status.Version < RecommendedPawnIoVersion
                        ? "PawnIO " + status.Version + " is pre-existing and older than 2.2.0. Inspector will not upgrade or remove it."
                        : "PawnIO " + status.Version + " is pre-existing and will be left unchanged.";
            }

            if (!File.Exists(status.BundledInstaller)) status.BundledInstaller = null;
            return status;
        }

        public static bool EnsureModernPawnIo(InspectionReport report, out string detail)
        {
            lock (Sync)
            {
                var current = DetectPawnIo();
                EvidenceEngine.Log(report, "PawnIO detection", current.Detail);
                PortableSessionLog.Write("PawnIO detection", current.Detail);
                SessionRecoveryJournal.Write("PAWNIO_DETECTION", current.Detail);

                if (current.Installed)
                {
                    detail = current.Detail;
                    return true;
                }

                if (!SupportsPawnIo22())
                {
                    detail = "PawnIO 2.2 temporary deployment is supported only on Windows 10 1809 or later. No driver was installed.";
                    EvidenceEngine.Log(report, "PawnIO deployment skipped", detail);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(current.BundledInstaller) || !File.Exists(current.BundledInstaller))
                {
                    detail = "Bundled PawnIO 2.2.0 installer is missing. No driver was installed.";
                    EvidenceEngine.Log(report, "PawnIO deployment failed", detail);
                    return false;
                }
                var hash = Sha256(current.BundledInstaller);
                if (!string.Equals(hash, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    detail = "Bundled PawnIO installer failed SHA-256 verification. No driver was installed.";
                    EvidenceEngine.Log(report, "PawnIO deployment blocked", detail + " Actual=" + hash);
                    return false;
                }

                _baselinePawnIoInf = FindPawnIoOemInf();
                var ownership = "PawnIO 2.2.0|installerSha256=" + hash + "|baselineInf=" + string.Join(",", _baselinePawnIoInf);
                PortableSessionLog.RecordOwnedResource("planned-driver-install", "PawnIO-2.2.0", ownership);
                SessionRecoveryJournal.RecordOwnedResource("planned-driver-install", "PawnIO-2.2.0", ownership);
                EvidenceEngine.Log(report, "PawnIO temporary install planned", ownership);

                var exit = RunInstaller(current.BundledInstaller, "-install -silent", report, "install");
                if (exit != 0 && exit != 3010)
                {
                    detail = "PawnIO installer returned exit code " + exit + ".";
                    EvidenceEngine.Log(report, "PawnIO temporary install failed", detail);
                    return false;
                }
                if (exit == 3010)
                {
                    detail = "PawnIO installation requested a reboot. Inspector will clean up the temporary deployment and will not use it this run.";
                    _ownedPawnIo = true;
                    PortableSessionLog.RecordOwnedResource("driver", "PawnIO-2.2.0", "installed; reboot-required");
                    SessionRecoveryJournal.RecordOwnedResource("driver", "PawnIO-2.2.0", "installed; reboot-required");
                    CleanupOwnedPawnIo(report, "install requested reboot");
                    return false;
                }

                var after = DetectPawnIo();
                if (!after.Installed)
                {
                    detail = "PawnIO installer returned success but the installation could not be verified.";
                    EvidenceEngine.Log(report, "PawnIO verification failed", detail);
                    return false;
                }

                _ownedPawnIo = true;
                PortableSessionLog.RecordOwnedResource("driver", "PawnIO-2.2.0", "temporary Inspector-owned deployment");
                SessionRecoveryJournal.RecordOwnedResource("driver", "PawnIO-2.2.0", "temporary Inspector-owned deployment");
                detail = "PawnIO " + (after.Version == null ? "2.2.0" : after.Version.ToString()) + " temporarily installed and verified.";
                EvidenceEngine.Log(report, "PawnIO temporary install verified", detail);
                return true;
            }
        }

        public static void CleanupOwnedPawnIo(InspectionReport report, string reason)
        {
            lock (Sync)
            {
                if (!_ownedPawnIo)
                {
                    PortableSessionLog.Write("PawnIO cleanup skipped", "No Inspector-owned PawnIO deployment is active. " + reason);
                    return;
                }

                var installer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "pawnio", "2.2.0", "PawnIO_setup.exe");
                EvidenceEngine.Log(report, "PawnIO cleanup started", reason);
                PortableSessionLog.RecordCleanup("driver", "PawnIO-2.2.0", "STARTED", reason);
                SessionRecoveryJournal.RecordCleanup("driver", "PawnIO-2.2.0", "STARTED", reason);

                try
                {
                    if (File.Exists(installer) && string.Equals(Sha256(installer), ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        RunInstaller(installer, "-uninstall -silent", report, "uninstall");
                    else
                        EvidenceEngine.Log(report, "PawnIO uninstall warning", "Verified bundled installer unavailable; continuing with owned DriverStore cleanup verification.");

                    var currentInf = FindPawnIoOemInf();
                    foreach (var inf in currentInf.Except(_baselinePawnIoInf, StringComparer.OrdinalIgnoreCase).ToArray())
                    {
                        var code = RunProcess(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe"),
                            "/delete-driver " + inf + " /uninstall", out var output);
                        EvidenceEngine.Log(report, "PawnIO DriverStore cleanup", inf + " Exit=" + code + " " + output);
                    }

                    var remainingNewInf = FindPawnIoOemInf().Except(_baselinePawnIoInf, StringComparer.OrdinalIgnoreCase).ToArray();
                    var registryPresent = DetectPawnIo().Installed;
                    var servicePresent = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO") != null;
                    var clean = !registryPresent && !servicePresent && remainingNewInf.Length == 0;
                    var verification = "uninstallRegistry=" + registryPresent + "; serviceKey=" + servicePresent +
                        "; newDriverStoreInf=" + string.Join(",", remainingNewInf);
                    PortableSessionLog.RecordCleanup("driver", "PawnIO-2.2.0", clean ? "VERIFIED" : "RESIDUE_DETECTED", verification);
                    SessionRecoveryJournal.RecordCleanup("driver", "PawnIO-2.2.0", clean ? "VERIFIED" : "RESIDUE_DETECTED", verification);
                    EvidenceEngine.Log(report, "PawnIO cleanup verification", (clean ? "VERIFIED: " : "RESIDUE DETECTED: ") + verification);
                    if (clean) _ownedPawnIo = false;
                }
                catch (Exception ex)
                {
                    PortableSessionLog.RecordCleanup("driver", "PawnIO-2.2.0", "FAILED", ex.ToString());
                    SessionRecoveryJournal.RecordCleanup("driver", "PawnIO-2.2.0", "FAILED", ex.ToString());
                    EvidenceEngine.Log(report, "PawnIO cleanup exception", ex.ToString());
                }
            }
        }

        public static void CleanupOnApplicationExit()
        {
            if (!_ownedPawnIo) return;
            var report = new InspectionReport { InspectionId = "exit-cleanup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") };
            CleanupOwnedPawnIo(report, "application exit");
        }

        private static int RunInstaller(string exe, string args, InspectionReport report, string operation)
        {
            var exit = RunProcess(exe, args, out var output);
            EvidenceEngine.Log(report, "PawnIO " + operation + " process", "Exit=" + exit + " " + output);
            PortableSessionLog.Write("PawnIO " + operation + " process", "Exit=" + exit + " " + output);
            SessionRecoveryJournal.Write("PAWNIO_" + operation.ToUpperInvariant(), "Exit=" + exit + " " + output);
            return exit;
        }

        private static int RunProcess(string exe, string args, out string output)
        {
            using (var p = new Process { StartInfo = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } })
            {
                p.Start();
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } throw new TimeoutException(Path.GetFileName(exe) + " timed out."); }
                System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { stdout, stderr }, 5000);
                output = (stdout.Result + " " + stderr.Result).Trim();
                return p.ExitCode;
            }
        }

        private static bool SupportsPawnIo22()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int build;
                    var raw = Convert.ToString(key == null ? null : key.GetValue("CurrentBuildNumber"));
                    return int.TryParse(raw, out build) && build >= 17763;
                }
            }
            catch { return false; }
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

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool TryReadVersion(RegistryView view, out string version)
        {
            version = null;
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))
                {
                    if (key == null) return false;
                    version = Convert.ToString(key.GetValue("DisplayVersion"));
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
