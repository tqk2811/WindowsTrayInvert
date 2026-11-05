using System.Drawing;
using System.Windows.Forms;
using WindowsTrayInvert.Localization;

namespace WindowsTrayInvert.UI;

/// <summary>The app's own tray icon (WinForms <see cref="NotifyIcon"/>): double-click opens the window.</summary>
public sealed class TrayIconHost : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly EventHandler _onCultureChanged;

    public TrayIconHost(Action openWindow, Action exit)
    {
        _icon = LoadIcon();

        ContextMenuStrip menu = new();
        ToolStripItem openItem = menu.Items.Add("", null, (_, _) => openWindow());
        openItem.Font = new Font(menu.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(new ToolStripSeparator());
        ToolStripItem exitItem = menu.Items.Add("", null, (_, _) => exit());

        void ApplyTexts()
        {
            openItem.Text = LocalizationManager.Instance["TrayOpen"];
            exitItem.Text = LocalizationManager.Instance["TrayExit"];
        }
        ApplyTexts();
        _onCultureChanged = (_, _) => ApplyTexts();
        LocalizationManager.Instance.CultureChanged += _onCultureChanged;

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = App.DisplayName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                openWindow();
        };
    }

    /// <summary>Small-size frame of the embedded app.ico, so the tray does not downscale the 256px one.</summary>
    private static Icon LoadIcon()
    {
        var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (info is null)
            return (Icon)SystemIcons.Application.Clone();
        using Stream stream = info.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= _onCultureChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
