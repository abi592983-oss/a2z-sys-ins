using Microsoft.Win32;
using System;
using System.IO;

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
                    ? "PawnIO is installed, but its version could not be parsed."
                    : status.Version < RecommendedPawnIoVersion
                        ? "PawnIO " + status.Version + " is installed; 2.2.0 or newer is recommended for the modern LibreHardwareMonitor access path."
                        : "PawnIO " + status.Version + " is installed and meets the recommended version.";
            }

            if (!File.Exists(status.BundledInstaller))
                status.BundledInstaller = null;

            return status;
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
            catch
            {
                return false;
            }
        }
    }
}
