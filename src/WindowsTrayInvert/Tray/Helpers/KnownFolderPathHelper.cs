using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WindowsTrayInvert.Tray.Helpers;

/// <summary>
/// Expands paths like <c>{6D809377-6AF0-444B-8957-A3773F02200E}\App\app.exe</c> that Explorer stores in
/// <c>ExecutablePath</c>, and normalizes paths for case-insensitive comparison.
/// </summary>
public static partial class KnownFolderPathHelper
{
    private static readonly ConcurrentDictionary<Guid, string?> _folderCache = new();

    [GeneratedRegex(@"^\{(?<guid>[0-9A-Fa-f\-]{36})\}(?<rest>.*)$")]
    private static partial Regex KnownFolderPrefixRegex();

    public static string Expand(string path)
    {
        Match match = KnownFolderPrefixRegex().Match(path);
        if (!match.Success)
            return path;

        Guid guid = Guid.Parse(match.Groups["guid"].Value);
        string? folder = _folderCache.GetOrAdd(guid, GetKnownFolderPath);
        if (folder is null)
            return path;

        return folder.TrimEnd('\\') + match.Groups["rest"].Value;
    }

    /// <summary>Expanded, full, trimmed path; compare results with <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
    public static string Normalize(string path)
    {
        string expanded = Expand(path.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(expanded);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return expanded;
        }
    }

    private static string? GetKnownFolderPath(Guid folderId)
    {
        int hr = SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out IntPtr pathPtr);
        try
        {
            return hr == 0 ? Marshal.PtrToStringUni(pathPtr) : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
