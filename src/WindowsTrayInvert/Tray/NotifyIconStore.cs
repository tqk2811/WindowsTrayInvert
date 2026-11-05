using Microsoft.Win32;
using WindowsTrayInvert.Tray.Helpers;
using WindowsTrayInvert.Tray.Models;

namespace WindowsTrayInvert.Tray;

/// <summary>
/// Reads and writes the per-icon settings Explorer keeps under <c>HKCU\Control Panel\NotifyIconSettings</c>.
/// Explorer watches this key itself, so a write takes effect without any broadcast.
/// </summary>
public sealed class NotifyIconStore
{
    public const string KeyPath = @"Control Panel\NotifyIconSettings";
    private const string IsPromotedName = "IsPromoted";

    /// <param name="includeDisplayData">
    /// Also read <see cref="NotifyIconEntry.InitialTooltip"/> and <see cref="NotifyIconEntry.IconSnapshot"/> (PNG
    /// bytes). Only the window needs them; enforcement runs on every registry change and skips them.
    /// </param>
    public IReadOnlyList<NotifyIconEntry> ReadAll(bool includeDisplayData = true)
    {
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (root is null)
            return [];

        List<NotifyIconEntry> entries = [];
        foreach (string id in root.GetSubKeyNames())
        {
            using RegistryKey? key = root.OpenSubKey(id);
            if (key is null)
                continue; // deleted between enumeration and open

            string? rawPath = key.GetValue("ExecutablePath") as string;
            entries.Add(new NotifyIconEntry
            {
                Id = id,
                RawExecutablePath = rawPath,
                ExecutablePath = rawPath is null ? null : KnownFolderPathHelper.Normalize(rawPath),
                InitialTooltip = includeDisplayData ? key.GetValue("InitialTooltip") as string : null,
                IsPromoted = key.GetValue(IsPromotedName) is int value ? value : null,
                IconSnapshot = includeDisplayData ? key.GetValue("IconSnapshot") as byte[] : null,
            });
        }
        return entries;
    }

    /// <summary>Removes the whole <c>NotifyIconSettings\&lt;id&gt;</c> subkey. Missing key is not an error.</summary>
    public void Delete(string id)
    {
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        root?.DeleteSubKeyTree(id, throwOnMissingSubKey: false);
    }

    /// <returns><c>false</c> if the subkey no longer exists.</returns>
    public bool SetPromoted(string id, bool promoted)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"{KeyPath}\{id}", writable: true);
        if (key is null)
            return false;

        key.SetValue(IsPromotedName, promoted ? 1 : 0, RegistryValueKind.DWord);
        return true;
    }
}
