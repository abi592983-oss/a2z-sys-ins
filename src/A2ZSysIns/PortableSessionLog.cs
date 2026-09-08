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

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized) return;
                _sessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _root = SelectWritableOrigin();
                var dir = System.IO.Path.Combine(_root, "A2Z-Inspector-Logs");
                Directory.CreateDirectory(dir);
                _path = System.IO.Path.Combine(dir, "A2Z-Inspector-UNKNOWN-" + _sessionId + ".log");
                AppendRaw("A2Z SYSTEM INSPECTOR PORTABLE SESSION LOG");
                AppendRaw("SESSION|" + _sessionId);
                AppendRaw("STARTED_LOCAL|" + DateTime.Now.ToString("o"));
                AppendRaw("STARTED_UTC|" + DateTime.UtcNow.ToString("o"));
                AppendRaw("PROCESS_BASE|" + AppDomain.CurrentDomain.BaseDirectory);
                AppendRaw("LOG_ROOT|" + _root);
                AppendRaw("NOTE|This log is intentionally retained for validation and recovery diagnostics.");
                _initialized = true;
            }
        }

        public static void Write(string action, string response)
        {
            try
            {
                Initialize();
                lock (Sync)
                {
                    AppendRaw(DateTime.UtcNow.ToString("o") + "|EVENT|" + OneLine(action) + "|" + OneLine(response));
                }
            }
            catch { }
        }

        public static void SetDeviceIdentity(string manufacturer, string model, string serial)
        {
            try
            {
                Initialize();
                lock (Sync)
                {
                    var identity = SafeName(string.Join("-", new[] { manufacturer, model, serial }.Where(x => !string.IsNullOrWhiteSpace(x) && x != "N/A")));
                    if (string.IsNullOrWhiteSpace(identity)) identity = "UNKNOWN";
                    var dir = System.IO.Path.GetDirectoryName(_path);
                    var desired = System.IO.Path.Combine(dir, "A2Z-Inspector-" + identity + "-" + _sessionId + ".log");
                    if (!string.Equals(_path, desired, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(desired)) desired = System.IO.Path.Combine(dir, "A2Z-Inspector-" + identity + "-" + _sessionId + "-2.log");
                        File.Move(_path, desired);
                        _path = desired;
                    }
                    AppendRaw("DEVICE|" + OneLine(manufacturer) + "|" + OneLine(model) + "|" + OneLine(serial));
                }
            }
            catch (Exception ex) { Write("Portable log identity rename failed", ex.Message); }
        }

        public static void RecordOwnedResource(string kind, string id, string detail = null)
        {
            Write("OWNED_RESOURCE", kind + "|" + id + "|" + (detail ?? ""));
        }

        public static void RecordCleanup(string kind, string id, string status, string detail = null)
        {
            Write("CLEANUP_RESOURCE", kind + "|" + id + "|" + status + "|" + (detail ?? ""));
        }

        public static IEnumerable<string> EnumerateExistingLogs()
        {
            try
            {
                Initialize();
                var dir = System.IO.Path.Combine(_root, "A2Z-Inspector-Logs");
                return Directory.Exists(dir) ? Directory.GetFiles(dir, "A2Z-Inspector-*.log").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : new string[0];
            }
            catch { return new string[0]; }
        }

        private static string SelectWritableOrigin()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (CanWrite(baseDir)) return baseDir;

            // When Explorer launches an EXE from a compressed ZIP it may materialize it in a temp folder.
            // Windows does not reliably expose the original ZIP path to the child process, so never guess it.
            // Prefer a writable removable drive only when there is exactly one plausible removable target.
            try
            {
                var removable = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                    .Select(d => d.RootDirectory.FullName.TrimEnd('\\'))
                    .Where(CanWrite).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (removable.Count == 1) return removable[0];
            }
            catch { }

            var fallback = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2Z-System-Inspector");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static bool CanWrite(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
                var test = System.IO.Path.Combine(folder, ".a2z-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (File.Create(test)) { }
                File.Delete(test);
                return true;
            }
            catch { return false; }
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
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
