using System;
using System.Windows;

namespace A2ZSysIns
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            PortableSessionLog.Initialize();
            PortableSessionLog.Write("Application startup", "Portable diagnostic flight recorder initialized.");
            AppDomain.CurrentDomain.UnhandledException += (s, x) => PortableSessionLog.Write("Unhandled AppDomain exception", Convert.ToString(x.ExceptionObject));
            DispatcherUnhandledException += (s, x) => PortableSessionLog.Write("Unhandled dispatcher exception", x.Exception == null ? "Unknown" : x.Exception.ToString());
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            PortableSessionLog.Write("Application exit", "ExitCode=" + e.ApplicationExitCode + "; cleanup lifecycle completed/attempted. Diagnostic log intentionally retained.");
            base.OnExit(e);
        }
    }
}
