using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using Wpf.Ui.Controls;
using System.Diagnostics;
using System.Globalization;
using WindowsTrayInvert.Config;
using WindowsTrayInvert.Config.Enums;
using WindowsTrayInvert.Localization;
using WindowsTrayInvert.Promotion;
using WindowsTrayInvert.Startup;
using WindowsTrayInvert.Tray;
using WindowsTrayInvert.UI.ViewModels;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace WindowsTrayInvert.UI;

/// <summary>
/// Management window in Windows 11 Settings style. Closing really closes it (the app owns a new one per opening),
/// so it unhooks everything it attached to long-lived objects in <see cref="OnClosed"/>.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly NotifyIconStore _store;
    private static readonly ThemeMode[] _themeCycle = [ThemeMode.System, ThemeMode.Light, ThemeMode.Dark];

    private readonly AppConfig _config;
    private readonly PromotionEnforcer _enforcer;
    private readonly AutoStartManager _autoStart;
    private readonly ThemeController _theme;
    private readonly OrphanDetector _orphans = new();
    private readonly ObservableCollection<IconRowViewModel> _rows = [];
    private readonly ListCollectionView _view;
    private bool _closed;

    public MainWindow(NotifyIconStore store, AppConfig config, PromotionEnforcer enforcer, AutoStartManager autoStart)
    {
        _store = store;
        _config = config;
        _enforcer = enforcer;
        _autoStart = autoStart;
        _theme = new ThemeController(this);
        InitializeComponent();

        LanguageBox.ItemsSource = LocalizationManager.SupportedCultures;
        LanguageBox.SelectedItem = LocalizationManager.SupportedCultures
            .First(x => x.Name == LocalizationManager.Instance.Culture.Name);
        LocalizationManager.Instance.CultureChanged += OnCultureChanged;

        _view = new ListCollectionView(_rows)
        {
            Filter = MatchesSearch,
            CustomSort = Comparer<IconRowViewModel>.Create(
                (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name)),
        };
        IconList.ItemsSource = _view;

        // System mode needs the window handle to watch Windows theme changes, so apply again once loaded.
        _theme.Apply(_config.Theme);
        Loaded += (_, _) => _theme.Apply(_config.Theme);
        UpdateThemeButton();
        Refresh();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        UpdateThemeButton();
        Refresh(); // orphan count text is built in code
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        // Here, not in OnClosed: unwatching needs the window handle, which is gone by then.
        if (!e.Cancel)
            _theme.Detach();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        LocalizationManager.Instance.CultureChanged -= OnCultureChanged;
        base.OnClosed(e);
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeMode next = _themeCycle[(Array.IndexOf(_themeCycle, _config.Theme) + 1) % _themeCycle.Length];
        _theme.Apply(next);
        TrySave(() => _config.Theme = next);
        UpdateThemeButton();
    }

    private void UpdateThemeButton()
    {
        (SymbolRegular symbol, string key) = _config.Theme switch
        {
            ThemeMode.Light => (SymbolRegular.WeatherSunny24, "ThemeLight"),
            ThemeMode.Dark => (SymbolRegular.WeatherMoon24, "ThemeDark"),
            _ => (SymbolRegular.DarkTheme24, "ThemeSystem"),
        };
        ThemeButton.Icon = new SymbolIcon(symbol);
        ThemeButton.ToolTip = LocalizationManager.Instance[key];
        System.Windows.Automation.AutomationProperties.SetName(ThemeButton, LocalizationManager.Instance[key]);
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is not CultureInfo culture || culture.Name == LocalizationManager.Instance.Culture.Name)
            return;
        LocalizationManager.Instance.Culture = culture;
        TrySave(() => _config.Language = culture.Name);
    }

    private void TrySave(Action save)
    {
        try
        {
            save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, LocalizationManager.Instance["ErrorSaveConfig"],
                MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>Re-reads the registry, keeping existing rows (and scroll position) where possible.</summary>
    public void Refresh()
    {
        AutoStartToggle.IsChecked = _autoStart.IsEnabled;

        var byId = _rows.ToDictionary(x => x.Id);
        HashSet<string> seen = [];
        foreach (var entry in _store.ReadAll())
        {
            seen.Add(entry.Id);
            bool hidden = entry.ExecutablePath is not null && !_config.IsShown(entry.ExecutablePath);
            bool orphan = _orphans.IsOrphan(entry);
            if (byId.TryGetValue(entry.Id, out var row))
                row.Update(entry, hidden, orphan);
            else
                _rows.Add(new IconRowViewModel(entry, hidden, orphan, OnHiddenChanged));
        }
        foreach (var stale in _rows.Where(x => !seen.Contains(x.Id)).ToList())
            _rows.Remove(stale);

        int orphanCount = _rows.Count(x => x.IsOrphan);
        OrphanText.Text = orphanCount == 0
            ? LocalizationManager.Instance["OrphanNone"]
            : LocalizationManager.Instance.Format("OrphanCount", orphanCount);
        OrphanButton.IsEnabled = orphanCount > 0;

        RefreshView();
    }

    private async void OnOrphanCleanClick(object sender, RoutedEventArgs e)
    {
        var orphans = _rows.Where(x => x.IsOrphan).OrderBy(x => x.ExecutablePath, StringComparer.OrdinalIgnoreCase).ToList();
        if (orphans.Count == 0)
            return;

        LocalizationManager loc = LocalizationManager.Instance;
        // One checkbox per entry, all checked by default; Remove is disabled while nothing is checked.
        var dialog = new ContentDialog(DialogHost)
        {
            Title = loc["OrphanConfirmTitle"],
            PrimaryButtonText = loc["Remove"],
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = loc["Cancel"],
            DialogMaxWidth = 640,
        };
        StackPanel list = new();
        List<(CheckBox Box, IconRowViewModel Row)> choices = [];
        foreach (var row in orphans)
        {
            CheckBox box = new()
            {
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = row.ExecutablePath,
                Content = new StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock { Text = row.Name, TextTrimming = TextTrimming.CharacterEllipsis },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = row.ExecutablePath,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                        },
                    },
                },
            };
            // Checked/Unchecked (not Click) so keyboard and accessibility toggles update the button too.
            box.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
            box.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = choices.Any(x => x.Box.IsChecked == true);
            choices.Add((box, row));
            list.Children.Add(box);
        }

        StackPanel content = new();
        content.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = loc["OrphanConfirmText"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });
        content.Children.Add(new System.Windows.Controls.ScrollViewer
        {
            MaxHeight = 300,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = list,
        });
        dialog.Content = content;

        // Closed while the dialog was open: do not act on a choice the user can no longer see.
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || _closed)
            return;

        try
        {
            foreach (var (box, row) in choices)
            {
                if (box.IsChecked == true)
                    _store.Delete(row.Id);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, loc["ErrorTitle"], MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        Refresh();
    }

    private void RefreshView()
    {
        _view.Refresh();
        EmptyText.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool MatchesSearch(object item)
    {
        string keyword = SearchBox.Text.Trim();
        if (keyword.Length == 0)
            return true;
        var row = (IconRowViewModel)item;
        return row.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
            || (row.ExecutablePath?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshView();

    private void OnHiddenChanged(IconRowViewModel row, bool hidden)
    {
        if (row.ExecutablePath is null)
            return;
        try
        {
            _config.SetShown(row.ExecutablePath, !hidden);
            _enforcer.Apply();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, LocalizationManager.Instance["ErrorSaveConfig"], MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        Refresh();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

    private void OnShowAllClick(object sender, RoutedEventArgs e) => _ = SetAllShownAsync(true);

    private void OnHideAllClick(object sender, RoutedEventArgs e) => _ = SetAllShownAsync(false);

    /// <summary>Bulk show/hide for the rows currently in the (filtered) list, after confirmation.</summary>
    private async Task SetAllShownAsync(bool shown)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        var targets = _view.Cast<IconRowViewModel>()
            .Where(x => x.CanToggle && x.IsShown != shown)
            .ToList();

        var dialog = new ContentDialog(DialogHost) { DialogMaxWidth = 520 };
        if (targets.Count == 0)
        {
            dialog.Title = shown ? loc["ShowAll"] : loc["HideAll"];
            dialog.Content = loc["BulkNothing"];
            dialog.CloseButtonText = "OK";
            await dialog.ShowAsync();
            return;
        }

        dialog.Title = loc.Format(shown ? "BulkShowTitle" : "BulkHideTitle", targets.Count);
        dialog.Content = new System.Windows.Controls.TextBlock { Text = loc["BulkText"], TextWrapping = TextWrapping.Wrap };
        dialog.PrimaryButtonText = loc["Apply"];
        dialog.PrimaryButtonAppearance = ControlAppearance.Primary;
        dialog.CloseButtonText = loc["Cancel"];
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || _closed)
            return;

        try
        {
            // Write the config for all rows, then enforce once (not once per row).
            foreach (var row in targets)
                _config.SetShown(row.ExecutablePath!, shown);
            _enforcer.Apply();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, loc["ErrorSaveConfig"], MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        Refresh();
    }

    /// <summary>Opens Explorer with the exe selected; if it no longer exists, opens the nearest existing folder.</summary>
    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not IconRowViewModel { ExecutablePath: { } path })
            return;
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            string? folder = Path.GetDirectoryName(path);
            while (folder is not null && !Directory.Exists(folder))
                folder = Path.GetDirectoryName(folder);
            if (folder is not null)
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, LocalizationManager.Instance["ErrorTitle"],
                MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _autoStart.SetEnabled(AutoStartToggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, LocalizationManager.Instance["ErrorTitle"], MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        AutoStartToggle.IsChecked = _autoStart.IsEnabled;
    }
}
