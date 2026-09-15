using System;
using System.Windows;
using System.Windows.Controls;

namespace A2ZSysIns
{
    public partial class MainWindow
    {
        static MainWindow()
        {
            EventManager.RegisterClassHandler(typeof(MainWindow), Button.ClickEvent, new RoutedEventHandler(HandleScanSetupNavigation), true);
        }

        private static void HandleScanSetupNavigation(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var window = button == null ? null : Window.GetWindow(button) as MainWindow;
            if (window == null || window.TechnicianBox == null) return;

            var content = button.Content == null ? string.Empty : button.Content.ToString();
            var isScanCommand = content.IndexOf("Scan Again", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                content.IndexOf("Start Inspection", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isScanCommand || !string.IsNullOrWhiteSpace(window.TechnicianBox.Text)) return;

            window.Tabs.SelectedIndex = 1;
            window.TechnicianBox.Focus();
            e.Handled = true;
        }
    }
}
