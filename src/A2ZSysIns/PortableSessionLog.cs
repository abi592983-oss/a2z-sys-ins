using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace A2ZSysIns
{
    internal static class PortableSessionLog
    {
        private static readonly object Sync = new object();
        private static string _root;
        private static string _path;
        private static string _sessionId;
        private static bool _initialized;

        public static string Root => _root;
        public static string PathName => _path;
        public static string SessionId => _sessionId;
        public static bool IsInitialized => _initialized;

        public static bool TryGetAutomaticRoot(out string root, out string reason)
        {
            root = null; reason = null;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (CanWrite(baseDir) && !LooksLikeTemporaryExtraction(baseDir))
            {
                root = baseDir;
                reason = "Writable application origin.";
                return true;
            }

            try
            {
                var removable = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                    .Select(d => d.RootDirectory.FullName)
                    .Where(CanWrite).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (removable.Count == 1)
                {
                    root = removable[0];
                    reason = "Single writable removable drive detected.";
                    return true;
                }
                reason = removable.Count == 0
                    ? "Application origin is temporary/unwritable and no unique removable destination was detected."
                    : "Application origin is temporary/unwritable and multiple removable destinations were detected.";
            }
            catch (Exception ex) { reason = "Destination detection failed: " + ex.Message; }
            return false;
        }

        public static void Initialize(string selectedRoot = null)
        {
            lock (Sync)
            {
                if (_initialized) return;
                string reason;
                if (string.IsNullOrWhiteSpace(selectedRoot))
                {
                    if (!TryGetAutomaticRoot(out selectedRoot, out reason))
                        throw new InvalidOperationException("A log destination must be selected by the technician. " + reason);
                }
                if (!Directory.Exists(selectedRoot) || !CanWrite(selectedRoot))
                    throw new IOException("The selected log destination is not writable: " + selectedRoot);

                _sessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _root = Path.GetFullPath(selectedRoot);
                var dir = Path.Combine(_root, "A2Z-Inspector-Logs");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "A2Z-Inspector-UNKNOWN-" + _sessionId + ".log");
                AppendRaw("A2Z SYSTEM INSPECTOR PORTABLE SESSION LOG");
                AppendRaw("SESSION|" + _sessionId);
                AppendRaw("STARTED_LOCAL|" + DateTime.Now.ToString("o"));
                AppendRaw("STARTED_UTC|" + DateTime.UtcNow.ToString("o"));
                AppendRaw("PROCESS_BASE|" + AppDomain.CurrentDomain.BaseDirectory);
                AppendRaw("LOG_ROOT|" + _root);
                AppendRaw("NOTE|This log is intentionally retained for validation and recovery diagnostics.");
                _initialized = true;
                SessionRecoveryJournal.Begin(_sessionId, _path);
            }
        }

        public static void Write(string action, string response)
        {
            try
            {
                if (!_initialized) return;
                lock (Sync)
                {
                    AppendRaw(DateTime.UtcNow.ToString("o") + "|EVENT|" + OneLine(action) + "|" + OneLine(response));
                    SessionRecoveryJournal.Write(action, response);
                }
            }
            catch { }
        }

        public static void SetDeviceIdentity(string manufacturer, string model, string serial)
        {
            try
            {
                if (!_initialized) return;
                lock (Sync)
                {
                    var identity = SafeName(string.Join("-", new[] { manufacturer, model, serial }.Where(x => !string.IsNullOrWhiteSpace(x) && x != "N/A")));
                    if (string.IsNullOrWhiteSpace(identity)) identity = "UNKNOWN";
                    var dir = Path.GetDirectoryName(_path);
                    var desired = Path.Combine(dir, "A2Z-Inspector-" + identity + "-" + _sessionId + ".log");
                    if (!string.Equals(_path, desired, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(desired)) desired = Path.Combine(dir, "A2Z-Inspector-" + identity + "-" + _sessionId + "-2.log");
                        File.Move(_path, desired);
                        _path = desired;
                        SessionRecoveryJournal.Write("PORTABLE_LOG_RENAMED", _path);
                    }
                    AppendRaw("DEVICE|" + OneLine(manufacturer) + "|" + OneLine(model) + "|" + OneLine(serial));
                }
            }
            catch (Exception ex) { Write("Portable log identity rename failed", ex.Message); }
        }

        public static void RecordOwnedResource(string kind, string id, string detail = null)
        {
            Write("OWNED_RESOURCE", kind + "|" + id + "|" + (detail ?? ""));
            SessionRecoveryJournal.RecordOwnedResource(kind, id, detail);
        }

        public static void RecordCleanup(string kind, string id, string status, string detail = null)
        {
            Write("CLEANUP_RESOURCE", kind + "|" + id + "|" + status + "|" + (detail ?? ""));
            SessionRecoveryJournal.RecordCleanup(kind, id, status, detail);
        }

        public static void Complete()
        {
            Write("SESSION_COMPLETE", DateTime.UtcNow.ToString("o"));
            SessionRecoveryJournal.Complete();
        }

        public static IEnumerable<string> EnumerateExistingLogs()
        {
            try
            {
                if (!_initialized) return new string[0];
                var dir = Path.Combine(_root, "A2Z-Inspector-Logs");
                return Directory.Exists(dir) ? Directory.GetFiles(dir, "A2Z-Inspector-*.log").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : new string[0];
            }
            catch { return new string[0]; }
        }

        private static bool LooksLikeTemporaryExtraction(string folder)
        {
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
            var full = Path.GetFullPath(folder).TrimEnd('\\');
            return full.StartsWith(temp + "\\", StringComparison.OrdinalIgnoreCase)
                || full.IndexOf("Temporary Internet Files", StringComparison.OrdinalIgnoreCase) >= 0
                || full.IndexOf("TempState", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool CanWrite(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
                var test = Path.Combine(folder, ".a2z-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (File.Create(test)) { }
                File.Delete(test);
                return true;
            }
            catch { return false; }
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
            foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            value = Regex.Replace(value, @"\s+", " ").Trim().Replace(' ', '_');
            return value.Length > 120 ? value.Substring(0, 120) : value;
        }

        private static string OneLine(string value)
        {
            return (value ?? "").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "¦");
        }

        private static void AppendRaw(string line)
        {
            File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}
