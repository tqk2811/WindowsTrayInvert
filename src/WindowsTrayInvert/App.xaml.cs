using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Markup;
using WindowsTrayInvert.Config;
using WindowsTrayInvert.Diagnostics;
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
/// The window and the WPF-UI resource dictionaries only exist while the window is open: closing it releases them
/// and trims the working set, so the idle tray process stays small.
/// </summary>
public partial class App : Application
{
    /// <summary>Name shown to the user (window title, tray tooltip, dialogs).</summary>
    public const string DisplayName = "Windows Tray Invert";

    private const string InstanceName = @"Local\WindowsTrayInvert";

    private Mutex? _instanceMutex;
    private RegistryWatcher? _watcher;
    private TrayIconHost? _trayIcon;
    private NotifyIconStore? _store;
    private AppConfig? _config;
    private PromotionEnforcer? _enforcer;
    private AutoStartManager? _autoStart;
    private MainWindow? _window;
    private ResourceDictionary[]? _uiDictionaries;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, InstanceName + ".Mutex", out bool isFirst);
        if (!isFirst)
        {
            TrayIconHost.SignalRunningInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        HorizontalScrollHelper.Register();
        MiddleClickScrollHelper.Register();

        _config = new AppConfig(AppConfig.DefaultFilePath);
        Exception? loadError = null;
        try
        {
            _config.Load();
        }
        catch (Exception ex)
        {
            loadError = ex;
        }
        LocalizationManager loc = LocalizationManager.Instance;
        loc.Culture = LocalizationManager.Resolve(_config.Language);
        if (loadError is not null)
        {
            MessageBox.Show(loc.Format("ConfigLoadFailed", _config.FilePath, loadError.Message),
                DisplayName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _store = new NotifyIconStore();
        _enforcer = new PromotionEnforcer(_store, _config);
        _autoStart = new AutoStartManager(Environment.ProcessPath!);
        _trayIcon = new TrayIconHost(ShowWindow, ExitApp);

        _enforcer.Apply();
        _watcher = new RegistryWatcher(NotifyIconStore.KeyPath, TimeSpan.FromMilliseconds(300));
        _watcher.Changed += (_, _) => OnRegistryChanged();
        _watcher.Failed += (_, ex) => Dispatcher.BeginInvoke(() => MessageBox.Show(
            loc.Format("WatcherStopped", ex.Message), DisplayName, MessageBoxButton.OK, MessageBoxImage.Error));
        _watcher.Start();

        if (e.Args.Contains(AutoStartManager.TrayArgument, StringComparer.OrdinalIgnoreCase))
            ScheduleTrim();
        else
            ShowWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        _trayIcon?.Dispose();
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
        Dispatcher.BeginInvoke(() => _window?.Refresh());
    }

    private void ShowWindow()
    {
        if (_exiting)
            return;
        if (_window is null)
        {
            LoadUiDictionaries();
            _window = new MainWindow(_store!, _config!, _enforcer!, _autoStart!);
            _window.Closed += OnWindowClosed;
        }
        else
        {
            _window.Refresh();
        }
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _window!.Closed -= OnWindowClosed;
        _window = null;
        if (_exiting)
            return;
        UnloadUiDictionaries();
        ScheduleTrim();
    }

    /// <summary>WPF-UI themes and control styles, merged only while the window is alive (they are large).</summary>
    private void LoadUiDictionaries()
    {
        if (_uiDictionaries is not null)
            return;
        // Start with the saved theme so a dark user does not see one light frame before ThemeController applies it.
        var theme = _config!.Theme == Config.Enums.ThemeMode.Dark
            || (_config.Theme == Config.Enums.ThemeMode.System
                && Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme() == Wpf.Ui.Appearance.SystemTheme.Dark)
            ? Wpf.Ui.Appearance.ApplicationTheme.Dark
            : Wpf.Ui.Appearance.ApplicationTheme.Light;
        _uiDictionaries = [new ThemesDictionary { Theme = theme }, new ControlsDictionary()];
        foreach (var dictionary in _uiDictionaries)
            Resources.MergedDictionaries.Add(dictionary);
    }

    private void UnloadUiDictionaries()
    {
        // The theme manager swaps the themes dictionary for a new instance, so clear everything merged, not only ours;
        // also the accent colors/brushes it writes straight into the root dictionary.
        Resources.MergedDictionaries.Clear();
        Resources.Clear();
        _uiDictionaries = null;
    }

    /// <summary>Trims once the dispatcher is idle, after the closed window's last messages are processed.</summary>
    private void ScheduleTrim()
        => Dispatcher.BeginInvoke(() =>
        {
            if (_window is null && !_exiting)
                MemoryTrimmer.Trim();
        }, DispatcherPriority.ApplicationIdle);

    private void ExitApp()
    {
        _exiting = true;
        _window?.Close();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
