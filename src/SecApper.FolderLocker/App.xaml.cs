using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SecApper.FolderLocker;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static System.Threading.Mutex? _singleInstanceMutex;
    private static System.Threading.EventWaitHandle? _showWindowEvent;
    private const string MutexName = "SecApperFolderLockerSingleInstanceMutex";
    private const string EventName = "SecApperShowMainWindowEvent";

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new System.Threading.Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Signal the already running instance (in tray or background) to show its window
            try
            {
                using var showEvent = System.Threading.EventWaitHandle.OpenExisting(EventName);
                showEvent.Set();
            }
            catch
            {
            }

            Shutdown();
            return;
        }

        // Setup the event wait handle for future instances to signal us
        try
        {
            _showWindowEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, EventName);
            Task.Run(() =>
            {
                while (_showWindowEvent != null)
                {
                    try
                    {
                        _showWindowEvent.WaitOne();
                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow != null)
                            {
                                MainWindow.Show();
                                MainWindow.WindowState = WindowState.Normal;
                                MainWindow.Activate();
                            }
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }
        catch
        {
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _showWindowEvent?.Dispose(); } catch { }
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        try { _singleInstanceMutex?.Dispose(); } catch { }
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
        System.Windows.MessageBox.Show(
            $"An unexpected error occurred: {e.Exception.Message}\n\nDetails have been logged.",
            "SecApper Folder Locker",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogError("AppDomainUnhandledException", ex);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogError("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogError(string context, Exception ex)
    {
        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecApper", "FolderLocker", "logs");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "error.log");
            string entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] [{context}] {ex.GetType().FullName}: {ex.Message}\r\n{ex.StackTrace}\r\n\r\n";
            File.AppendAllText(logFile, entry);
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), entry);
            Console.Error.WriteLine(entry);
        }
        catch
        {
        }
    }
}
