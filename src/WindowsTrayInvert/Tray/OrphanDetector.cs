using WindowsTrayInvert.Tray.Models;

namespace WindowsTrayInvert.Tray;

/// <summary>
/// Finds tray entries whose executable no longer exists (old app versions, uninstalled apps).
/// Conservative on purpose: anything it cannot check for sure (network path, drive not ready, unexpanded
/// known folder) is NOT an orphan, so a temporarily missing drive never gets its entries removed.
/// </summary>
public sealed class OrphanDetector
{
    public bool IsOrphan(NotifyIconEntry entry)
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
