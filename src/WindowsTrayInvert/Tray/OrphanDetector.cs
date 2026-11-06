using WindowsTrayInvert.Tray.Enums;
using WindowsTrayInvert.Tray.Models;

namespace WindowsTrayInvert.Tray;

/// <summary>
/// Finds stale tray entries: the executable no longer exists (old app versions, uninstalled apps), or Explorer
/// has no icon image for it (needs an entry read with display data).
/// Conservative on purpose: anything it cannot check for sure (network path, drive not ready, unexpanded
/// known folder) is NOT an orphan, so a temporarily missing drive never gets its entries removed.
/// </summary>
public sealed class OrphanDetector
{
    public OrphanReason GetReason(NotifyIconEntry entry)
    {
        if (IsFileMissing(entry))
            return OrphanReason.MissingFile;
        if (entry.IconSnapshot is null || entry.IconSnapshot.Length == 0)
            return OrphanReason.MissingIcon;
        return OrphanReason.None;
    }

    private static bool IsFileMissing(NotifyIconEntry entry)
    {
        string? path = entry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('{') || path.StartsWith(@"\\"))
            return false;
        if (!Path.IsPathFullyQualified(path))
            return false;

        try
        {
            string? root = Path.GetPathRoot(path);
            if (root is null || !new DriveInfo(root).IsReady)
                return false;
            return !File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
