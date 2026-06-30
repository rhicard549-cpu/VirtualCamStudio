using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using System.IO;

namespace VirtualCamStudio
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string _logPath;

        public App()
        {
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "VirtualCamStudio_Error.log");

            // Clear old log
            try { File.Delete(_logPath); } catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Handle unhandled exceptions FIRST
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            base.OnStartup(e);

            LogStartup("Application starting...");
            LogStartup("Exception handlers registered");

            try
            {
                LogStartup("About to create MainWindow...");
                LogStartup($"Current thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

                MainWindow mainWindow;

                try
                {
                    LogStartup("Calling new MainWindow()...");
                    mainWindow = new MainWindow();
                    LogStartup("MainWindow constructor returned successfully!");
                }
                catch (Exception ex)
                {
                    LogStartup($"EXCEPTION in MainWindow constructor:");
                    LogStartup($"Type: {ex.GetType().FullName}");
                    LogStartup($"Message: {ex.Message}");
                    LogStartup($"Stack Trace:\n{ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        LogStartup($"Inner Exception: {ex.InnerException.GetType().FullName}");
                        LogStartup($"Inner Message: {ex.InnerException.Message}");
                        LogStartup($"Inner Stack:\n{ex.InnerException.StackTrace}");
                    }

                    MessageBox.Show(
                        $"FATAL ERROR in MainWindow constructor:\n\n{ex.GetType().Name}\n{ex.Message}\n\nSee Desktop\\VirtualCamStudio_Error.log",
                        "Cannot Start Application",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    Shutdown(1);
                    return;
                }

                LogStartup($"MainWindow created. Type: {mainWindow.GetType().FullName}");
                LogStartup($"Visibility: {mainWindow.Visibility}");
                LogStartup($"WindowState: {mainWindow.WindowState}");

                LogStartup("Calling mainWindow.Show()...");
                mainWindow.Show();
                LogStartup($"Show() completed. Visibility now: {mainWindow.Visibility}");

                LogStartup("Calling mainWindow.Activate()...");
                mainWindow.Activate();
                LogStartup($"Activate() completed. IsActive: {mainWindow.IsActive}");

                LogStartup("Startup complete!");
            }
            catch (Exception ex)
            {
                LogStartup($"EXCEPTION in OnStartup: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Failed to start application:\n\n{ex.Message}\n\nCheck Desktop\\VirtualCamStudio_Error.log",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string error = $"Unhandled UI Exception: {e.Exception.GetType().FullName}\nMessage: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
            LogStartup(error);

            MessageBox.Show(
                $"A critical error occurred:\n\n{e.Exception.Message}\n\nThe application will now close.\n\nCheck Desktop\\VirtualCamStudio_Error.log for details.",
                "VirtualCam Studio - Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
            Shutdown(1);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                string error = $"Unhandled Domain Exception: {ex.GetType().FullName}\nMessage: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                LogStartup(error);

                MessageBox.Show(
                    $"A fatal error occurred:\n\n{ex.Message}\n\nCheck Desktop\\VirtualCamStudio_Error.log for details.",
                    "VirtualCam Studio - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            string error = $"Unhandled Task Exception: {e.Exception.GetType().FullName}\nMessage: {e.Exception.Message}\n\nStack Trace:\n{e.Exception}";
            LogStartup(error);
            e.SetObserved();
        }

        private void LogStartup(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(_logPath, $"[{timestamp}] {message}\n");
            }
            catch (Exception ex)
            {
            }
        }
    }
}
