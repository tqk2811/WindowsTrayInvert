namespace WindowsTrayInvert.Tray.Enums;

/// <summary>Why <see cref="OrphanDetector"/> treats an entry as stale.</summary>
public enum OrphanReason
{
    None,

    /// <summary>The executable no longer exists on disk.</summary>
    MissingFile,

    /// <summary>Explorer saved no icon image for it (<c>IconSnapshot</c> missing or empty).</summary>
    MissingIcon,
}
