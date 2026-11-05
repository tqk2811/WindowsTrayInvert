using Microsoft.Win32;

namespace WindowsTrayInvert.Startup;

/// <summary>Start-with-Windows via <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
public sealed class AutoStartManager
{
    public const string TrayArgument = "--tray";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsTrayInvert";

    private readonly string _command;

    public AutoStartManager(string executablePath)
    {
        _command = $"\"{executablePath}\" {TrayArgument}";
    }

    /// <summary>True only if the Run entry points at this exe (a stale entry from another location counts as off).</summary>
    public bool IsEnabled
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return string.Equals(key?.GetValue(ValueName) as string, _command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, _command, RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
