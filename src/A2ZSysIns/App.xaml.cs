using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace A2ZSysIns
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string previousSession, previousLog;
            var incomplete = SessionRecoveryJournal.HasIncompleteSession(out previousSession, out previousLog);
            string root, reason;
            if (!PortableSessionLog.TryGetAutomaticRoot(out root, out reason))
            {
                var message = incomplete
                    ? "A previous Inspector session did not record a clean completion.\n\nPrevious session: " + previousSession +
                      "\nPrevious log: " + (string.IsNullOrWhiteSpace(previousLog) ? "not recorded" : previousLog) +
                      "\n\nSelect the folder/USB drive where Inspector must keep this session's permanent diagnostic log. If the previous log is on another drive, select that drive so Inspector can preserve the recovery trail."
                    : "Inspector cannot reliably determine its original portable folder (this can happen when launched from inside a ZIP or from a temporary location).\n\nSelect the folder/USB drive where the permanent diagnostic log must be stored.";
                System.Windows.MessageBox.Show(message, "A2Z System Inspector — log location required", MessageBoxButton.OK, MessageBoxImage.Information);
                using (var picker = new FolderBrowserDialog { Description = "Select A2Z System Inspector log location", ShowNewFolderButton = true })
                {
                    if (picker.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedPath))
                    {
                        System.Windows.MessageBox.Show("Inspector will close because a reliable diagnostic-log location is required before inspection.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Shutdown(-2);
                        return;
                    }
                    root = picker.SelectedPath;
                }
            }
            else if (incomplete)
            {
                var answer = System.Windows.MessageBox.Show(
                    "A previous Inspector session did not record a clean completion.\n\nPrevious session: " + previousSession +
                    "\nPrevious log: " + (string.IsNullOrWhiteSpace(previousLog) ? "not recorded" : previousLog) +
                    "\n\nIs this the same log location you used previously?\n\nChoose No to select the previous log folder/USB drive manually.",
                    "A2Z System Inspector — recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer == MessageBoxResult.No)
                {
                    using (var picker = new FolderBrowserDialog { Description = "Select the previous/current Inspector log location", ShowNewFolderButton = false })
                    {
                        if (picker.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedPath))
                        {
                            System.Windows.MessageBox.Show("Inspector will close because recovery location selection was cancelled.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Warning);
                            Shutdown(-3);
                            return;
                        }
                        root = picker.SelectedPath;
                    }
                }
            }

            PortableSessionLog.Initialize(root);
            PortableSessionLog.Write("Application startup", "Portable diagnostic flight recorder initialized. Local recovery journal=" + SessionRecoveryJournal.PathName);
            if (incomplete) PortableSessionLog.Write("Previous incomplete session detected", "Session=" + previousSession + "; previousLog=" + previousLog);

            try { PawnIoResidueCleaner.RecoverPreviousIncompleteSessionServiceResidue(); }
            catch (Exception ex) { PortableSessionLog.Write("Previous-session PawnIO service recovery exception", ex.ToString()); }

            try { DriverAccessManager.RecoverPreviousOwnedPawnIo(); }
            catch (Exception ex) { PortableSessionLog.Write("Previous-session PawnIO recovery exception", ex.ToString()); }

            AppDomain.CurrentDomain.UnhandledException += (s, x) => PortableSessionLog.Write("Unhandled AppDomain exception", Convert.ToString(x.ExceptionObject));
            DispatcherUnhandledException += (s, x) => PortableSessionLog.Write("Unhandled dispatcher exception", x.Exception == null ? "Unknown" : x.Exception.ToString());
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { DriverAccessManager.CleanupOnApplicationExit(); }
            catch (Exception ex) { PortableSessionLog.Write("Application-exit driver cleanup exception", ex.ToString()); }

            try { PawnIoResidueCleaner.CleanupCurrentSessionServiceResidue(); }
            catch (Exception ex) { PortableSessionLog.Write("Application-exit PawnIO service cleanup exception", ex.ToString()); }

            PortableSessionLog.Write("Application exit", "ExitCode=" + e.ApplicationExitCode + "; cleanup lifecycle completed/attempted. Diagnostic log intentionally retained.");
            PortableSessionLog.Complete();
            base.OnExit(e);
        }
    }
}
