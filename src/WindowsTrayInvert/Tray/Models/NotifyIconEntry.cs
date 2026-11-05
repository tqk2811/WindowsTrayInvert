namespace WindowsTrayInvert.Tray.Models;

/// <summary>
/// One subkey of <c>HKCU\Control Panel\NotifyIconSettings</c>.
/// </summary>
public sealed class NotifyIconEntry
{
    /// <summary>Subkey name (numeric id assigned by Explorer).</summary>
    public required string Id { get; init; }

    /// <summary>Raw <c>ExecutablePath</c> value, may start with a known folder GUID.</summary>
    public string? RawExecutablePath { get; init; }

    /// <summary><see cref="RawExecutablePath"/> with the known folder GUID expanded.</summary>
    public string? ExecutablePath { get; init; }

    public string? InitialTooltip { get; init; }

    /// <summary><c>IsPromoted</c> value; <c>null</c> when the value does not exist (treated as hidden by Windows).</summary>
    public int? IsPromoted { get; init; }

    /// <summary>Last icon image Explorer saw, PNG bytes (observed on Win11 26300).</summary>
    public byte[]? IconSnapshot { get; init; }
}
