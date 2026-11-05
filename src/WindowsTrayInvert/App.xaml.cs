using System.Windows;
using System.Windows.Threading;
using WindowsTrayInvert.Config;
using WindowsTrayInvert.Localization;
using WindowsTrayInvert.Promotion;
using WindowsTrayInvert.Startup;
using WindowsTrayInvert.Tray;
using WindowsTrayInvert.UI;
using WindowsTrayInvert.UI.Helpers;

namespace WindowsTrayInvert;

/// <summary>
/// Tray-resident app. Started with <c>--tray</c> (autostart) it stays in the tray; started by hand it also opens
/// the window. A second launch signals the running instance to show its window and exits.
/// </summary>
public partial class App : Application
{
    /// <summary>Name shown to the user (window title, tray tooltip, dialogs).</summary>
    public const string DisplayName = "Windows Tray Invert";

    private const string InstanceName = @"Local\WindowsTrayInvert";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private RegistryWatcher? _watcher;
    private TrayIconHost? _trayIcon;
    private MainWindow? _window;
    private PromotionEnforcer? _enforcer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, InstanceName + ".Mutex", out bool isFirst);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!isFirst)
        {
            _showEvent.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        HorizontalScrollHelper.Register();
        MiddleClickScrollHelper.Register();

        AppConfig config = new(AppConfig.DefaultFilePath);
        Exception? loadError = null;
        try
        {
            config.Load();
        }
        catch (Exception ex)
        {
            loadError = ex;
        }
        LocalizationManager loc = LocalizationManager.Instance;
        loc.Culture = LocalizationManager.Resolve(config.Language);
        if (loadError is not null)
        {
            MessageBox.Show(loc.Format("ConfigLoadFailed", config.FilePath, loadError.Message),
                DisplayName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        NotifyIconStore store = new();
        _enforcer = new PromotionEnforcer(store, config);
        AutoStartManager autoStart = new(Environment.ProcessPath!);
        _window = new MainWindow(store, config, _enforcer, autoStart);
        _trayIcon = new TrayIconHost(ShowWindow, ExitApp);

        _enforcer.Apply();
        _watcher = new RegistryWatcher(NotifyIconStore.KeyPath, TimeSpan.FromMilliseconds(300));
        _watcher.Changed += (_, _) => OnRegistryChanged();
        _watcher.Failed += (_, ex) => Dispatcher.BeginInvoke(() => MessageBox.Show(
            loc.Format("WatcherStopped", ex.Message), DisplayName, MessageBoxButton.OK, MessageBoxImage.Error));
        _watcher.Start();

        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, -1, false);

        if (!e.Args.Contains(AutoStartManager.TrayArgument, StringComparer.OrdinalIgnoreCase))
            ShowWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _watcher?.Dispose();
        _trayIcon?.Dispose();
        _showEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Runs on the watcher thread.</summary>
    private void OnRegistryChanged()
    {
        try
        {
            _enforcer!.Apply();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Apply failed: {ex}");
        }
        Dispatcher.BeginInvoke(() =>
        {
            if (_window!.IsVisible)
                _window.Refresh();
        });
    }

    private void ShowWindow()
    {
        if (_window is null)
            return;
        _window.Refresh();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApp()
    {
        _window?.CloseForExit();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
