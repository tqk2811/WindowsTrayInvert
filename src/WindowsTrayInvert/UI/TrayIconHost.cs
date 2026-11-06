using System.Runtime.InteropServices;
using System.Windows.Interop;
using WindowsTrayInvert.Localization;

namespace WindowsTrayInvert.UI;

/// <summary>
/// The app's own tray icon through <c>Shell_NotifyIcon</c> directly (no WinForms, so the idle process does not load
/// System.Windows.Forms/System.Drawing). Double-click opens the window; right-click shows a native menu.
/// Owns its hidden message window and icon handle; re-adds the icon when Explorer restarts.
/// The same window receives the "show" request of a second launch (<see cref="SignalRunningInstance"/>), so the app
/// needs no thread-pool wait for it.
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private const int CallbackMessage = 0x8000 + 1; // WM_APP + 1
    private const uint IconId = 1;
    private const string ShowMessageName = "WindowsTrayInvert.Show";
    private static readonly IntPtr HwndBroadcast = 0xFFFF;
    private const uint CommandOpen = 1;
    private const uint CommandExit = 2;

    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2;
    private const uint NifMessage = 0x1, NifIcon = 0x2, NifTip = 0x4;
    private const int WmLButtonDblClk = 0x0203, WmRButtonUp = 0x0205, WmNull = 0;
    // Legacy (no NIM_SETVERSION) keyboard activation: Win+B, arrows, then Enter/Space.
    private const int NinSelect = 0x0400, NinKeySelect = 0x0401;
    private const uint MfString = 0x0, MfSeparator = 0x800;
    private const uint TpmRightButton = 0x2, TpmReturnCmd = 0x100, TpmNoNotify = 0x80;

    private readonly Action _openWindow;
    private readonly Action _exit;
    private readonly HwndSource _messageWindow;
    private readonly IntPtr _icon;
    private readonly bool _ownsIcon;
    private readonly int _taskbarCreatedMessage;
    private readonly int _showMessage;
    private bool _disposed;

    public TrayIconHost(Action openWindow, Action exit)
    {
        _openWindow = openWindow;
        _exit = exit;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        _showMessage = RegisterWindowMessage(ShowMessageName);
        (_icon, _ownsIcon) = LoadIcon();

        // Top-level but never shown (message-only windows do not receive the TaskbarCreated broadcast).
        _messageWindow = new HwndSource(new HwndSourceParameters(nameof(TrayIconHost)) { Width = 0, Height = 0, WindowStyle = 0 });
        _messageWindow.AddHook(WndProc);
        AddIcon();
    }

    /// <summary>
    /// Asks the running instance to open its window. A registered message posted to every top-level window: only the
    /// running instance's hidden window knows it, everyone else ignores it.
    /// </summary>
    public static void SignalRunningInstance()
        => PostMessage(HwndBroadcast, RegisterWindowMessage(ShowMessageName), IntPtr.Zero, IntPtr.Zero);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        NotifyIconData data = CreateData(0);
        Shell_NotifyIcon(NimDelete, ref data);
        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
        if (_ownsIcon)
            DestroyIcon(_icon);
    }

    private void AddIcon()
    {
        NotifyIconData data = CreateData(NifMessage | NifIcon | NifTip);
        if (!Shell_NotifyIcon(NimAdd, ref data))
            Shell_NotifyIcon(NimModify, ref data); // already there (e.g. TaskbarCreated raced with startup)
    }

    private NotifyIconData CreateData(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _messageWindow.Handle,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = App.DisplayName,
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _taskbarCreatedMessage)
        {
            AddIcon();
            return IntPtr.Zero;
        }
        if (msg != CallbackMessage && msg != _showMessage)
            return IntPtr.Zero;

        handled = true;
        try
        {
            if (msg == _showMessage)
            {
                _openWindow();
                return IntPtr.Zero;
            }
            switch ((int)lParam)
            {
                case WmLButtonDblClk:
                case NinSelect:
                case NinKeySelect:
                    _openWindow();
                    break;
                case WmRButtonUp:
                    ShowMenu();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Exceptions must not unwind through the window procedure: report and keep the tray alive.
            System.Windows.MessageBox.Show(ex.ToString(), App.DisplayName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        return IntPtr.Zero;
    }

    /// <summary>Built per opening, so it always has the current language and holds nothing while closed.</summary>
    private void ShowMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;
        uint command;
        try
        {
            LocalizationManager loc = LocalizationManager.Instance;
            AppendMenu(menu, MfString, CommandOpen, loc["TrayOpen"]);
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, CommandExit, loc["TrayExit"]);
            SetMenuDefaultItem(menu, CommandOpen, 0);

            GetCursorPos(out Point point);
            // Foreground + WM_NULL afterwards: the documented way to make the menu close when clicking elsewhere.
            SetForegroundWindow(_messageWindow.Handle);
            command = TrackPopupMenuEx(menu, TpmRightButton | TpmReturnCmd | TpmNoNotify, point.X, point.Y, _messageWindow.Handle, IntPtr.Zero);
            PostMessage(_messageWindow.Handle, WmNull, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            DestroyMenu(menu);
        }

        if (command == CommandOpen)
            _openWindow();
        else if (command == CommandExit)
            _exit();
    }

    /// <summary>Small (tray-size) frame of the exe's own icon; falls back to the stock application icon.</summary>
    private static (IntPtr Icon, bool Owned) LoadIcon()
    {
        IntPtr[] small = [IntPtr.Zero];
        if (Environment.ProcessPath is { } path && ExtractIconEx(path, 0, null, small, 1) > 0 && small[0] != IntPtr.Zero)
            return (small[0], true);
        return (LoadIcon(IntPtr.Zero, (IntPtr)32512), false); // IDI_APPLICATION, shared: never destroyed
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, uint fByPos);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
