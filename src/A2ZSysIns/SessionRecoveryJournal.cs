using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace A2ZSysIns
{
    internal static class SessionRecoveryJournal
    {
        private static readonly object Sync = new object();
        private static readonly string JournalDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2Z-System-Inspector");
        private static readonly string JournalPath = Path.Combine(JournalDirectory, "recovery-journal.log");
        private static string _sessionId;

        public static string PathName => JournalPath;

        public static void Begin(string sessionId, string portableLogPath)
        {
            lock (Sync)
            {
                Directory.CreateDirectory(JournalDirectory);
                _sessionId = sessionId;
                Append("SESSION_START", sessionId, DateTime.UtcNow.ToString("o"));
                Append("PORTABLE_LOG", sessionId, portableLogPath ?? "");
            }
        }

        public static void Write(string action, string detail)
        {
            lock (Sync)
            {
                try { Append(action, _sessionId ?? "NO_SESSION", detail ?? ""); } catch { }
            }
        }

        public static void WriteForSession(string sessionId, string action, string detail)
        {
            lock (Sync)
            {
                try { Append(action, sessionId ?? "NO_SESSION", detail ?? ""); } catch { }
            }
        }

        public static void RecordOwnedResource(string kind, string id, string detail)
        {
            Write("OWNED_RESOURCE", kind + "|" + id + "|" + (detail ?? ""));
        }

        public static void RecordCleanup(string kind, string id, string status, string detail)
        {
            Write("CLEANUP_RESOURCE", kind + "|" + id + "|" + status + "|" + (detail ?? ""));
        }

        public static void RecordCleanupForSession(string sessionId, string kind, string id, string status, string detail)
        {
            WriteForSession(sessionId, "CLEANUP_RESOURCE", kind + "|" + id + "|" + status + "|" + (detail ?? ""));
        }

        public static string GetOwnedResourceDetail(string sessionId, string kind, string id)
        {
            try
            {
                var prefix = kind + "¦" + id + "¦";
                var entry = ReadEntries().LastOrDefault(x => x[2] == sessionId && x[1] == "OWNED_RESOURCE" && x[3].StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                return entry == null ? null : entry[3].Substring(prefix.Length);
            }
            catch { return null; }
        }

        public static void Complete()
        {
            Write("SESSION_COMPLETE", DateTime.UtcNow.ToString("o"));
        }

        public static string LastKnownPortableLog()
        {
            try
            {
                if (!File.Exists(JournalPath)) return null;
                return File.ReadLines(JournalPath).Reverse()
                    .Select(Parse)
                    .Where(x => x != null && x.Length >= 4 && x[1] == "PORTABLE_LOG")
                    .Select(x => x[3])
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }
            catch { return null; }
        }

        public static bool HasIncompleteSession(out string sessionId, out string portableLog)
        {
            sessionId = null;
            portableLog = null;
            try
            {
                if (!File.Exists(JournalPath)) return false;
                var lines = ReadEntries();
                var start = lines.LastOrDefault(x => x[1] == "SESSION_START");
                if (start == null) return false;
                var currentSession = start[2];
                sessionId = currentSession;
                var completed = lines.Any(x => x[1] == "SESSION_COMPLETE" && x[2] == currentSession);
                portableLog = lines.LastOrDefault(x => x[1] == "PORTABLE_LOG" && x[2] == currentSession)?[3];
                return !completed;
            }
            catch { return false; }
        }

        public static bool TryGetUnresolvedPawnIoCleanup(out string sessionId, out string status, out string portableLog)
        {
            sessionId = null;
            status = null;
            portableLog = null;
            try
            {
                if (!File.Exists(JournalPath)) return false;
                var entries = ReadEntries();
                var sessions = entries.Where(x => x[1] == "SESSION_START").Select(x => x[2]).Distinct().Reverse();
                foreach (var candidate in sessions)
                {
                    if (candidate == _sessionId) continue;
                    var related = entries.Where(x => x[2] == candidate).ToList();
                    var owned = related.Any(x => x[1] == "OWNED_RESOURCE" && x[3].IndexOf("driver¦PawnIO-2.2.0", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!owned) continue;

                    var cleanup = related.Where(x => x[1] == "CLEANUP_RESOURCE").ToList();
                    var driverLast = cleanup.LastOrDefault(x => x[3].StartsWith("driver¦PawnIO-2.2.0¦", StringComparison.OrdinalIgnoreCase));
                    var serviceLast = cleanup.LastOrDefault(x => x[3].StartsWith("service¦PawnIO¦", StringComparison.OrdinalIgnoreCase));
                    var candidates = new[] { driverLast, serviceLast }.Where(x => x != null).ToList();
                    if (candidates.Count == 0) continue;

                    var unresolved = candidates.Select(x => new { Entry = x, Status = CleanupStatus(x[3]) })
                        .FirstOrDefault(x => IsUnresolvedCleanupStatus(x.Status));
                    if (unresolved == null) continue;

                    sessionId = candidate;
                    status = unresolved.Status;
                    portableLog = related.LastOrDefault(x => x[1] == "PORTABLE_LOG")?[3];
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        public static bool PreviousIncompleteSessionProvesPawnIoOwnership(out string sessionId)
        {
            sessionId = null;
            try
            {
                if (!File.Exists(JournalPath)) return false;
                var entries = ReadEntries();
                var sessions = entries.Where(x => x[1] == "SESSION_START").Select(x => x[2]).Distinct().Reverse();
                foreach (var candidate in sessions)
                {
                    if (candidate == _sessionId) continue;
                    var related = entries.Where(x => x[2] == candidate).ToList();
                    var completed = related.Any(x => x[1] == "SESSION_COMPLETE");
                    var owned = related.Any(x => x[1] == "OWNED_RESOURCE" && x[3].IndexOf("driver¦PawnIO-2.2.0", StringComparison.OrdinalIgnoreCase) >= 0);
                    var verified = related.Any(x => x[1] == "CLEANUP_RESOURCE" &&
                        (x[3].IndexOf("driver¦PawnIO-2.2.0¦VERIFIED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         x[3].IndexOf("service¦PawnIO¦VERIFIED", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!completed && owned && !verified)
                    {
                        sessionId = candidate;
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static string CleanupStatus(string detail)
        {
            var parts = (detail ?? "").Split('¦');
            return parts.Length >= 3 ? parts[2] : "UNKNOWN";
        }

        private static bool IsUnresolvedCleanupStatus(string value)
        {
            return value == "REBOOT_REQUIRED" || value == "PENDING_OR_RESIDUE" || value == "RESIDUE_DETECTED" ||
                   value == "FAILED" || value == "POST_REBOOT_RESIDUE" || value == "POST_REBOOT_VERIFY_FAILED";
        }

        private static List<string[]> ReadEntries()
        {
            return File.ReadAllLines(JournalPath).Select(Parse).Where(x => x != null && x.Length >= 4).ToList();
        }

        private static void Append(string type, string session, string detail)
        {
            Directory.CreateDirectory(JournalDirectory);
            var line = DateTime.UtcNow.ToString("o") + "|" + Clean(type) + "|" + Clean(session) + "|" + Clean(detail);
            File.AppendAllText(JournalPath, line + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string[] Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            return line.Split(new[] { '|' }, 4);
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "¦");
        }
    }
}
