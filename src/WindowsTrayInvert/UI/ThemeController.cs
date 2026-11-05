using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WindowsTrayInvert.Config.Enums;

namespace WindowsTrayInvert.UI;

/// <summary>Applies <see cref="ThemeMode"/> to the app; <see cref="ThemeMode.System"/> keeps following Windows live.</summary>
public sealed class ThemeController
{
    private readonly Window _window;
    private bool _watching;

    public ThemeController(Window window)
    {
        _window = window;
    }

    public void Apply(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
        {
            ApplicationThemeManager.ApplySystemTheme();
            if (!_watching && PresentationSource.FromVisual(_window) is not null)
            {
                SystemThemeWatcher.Watch(_window, WindowBackdropType.Mica, true);
                _watching = true;
            }
            return;
        }

        if (_watching)
        {
            SystemThemeWatcher.UnWatch(_window);
            _watching = false;
        }
        ApplicationThemeManager.Apply(
            mode == ThemeMode.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.Mica, true);
    }
}
