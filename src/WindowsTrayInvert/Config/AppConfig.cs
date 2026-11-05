using System.Text.Json;
using WindowsTrayInvert.Config.Enums;
using WindowsTrayInvert.Config.Models;
using WindowsTrayInvert.Tray.Helpers;

namespace WindowsTrayInvert.Config;

/// <summary>
/// App settings persisted as JSON: hide list, theme, language. Paths are stored normalized and matched
/// case-insensitively. Thread-safe: the watcher thread reads while the UI thread may edit.
/// </summary>
public sealed class AppConfig
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly HashSet<string> _hiddenPaths = new(StringComparer.OrdinalIgnoreCase);
    private ThemeMode _theme = ThemeMode.System;
    private string? _language;

    public AppConfig(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsTrayInvert", "config.json");

    public string FilePath => _filePath;

    public ThemeMode Theme
    {
        get { lock (_lock) return _theme; }
        set { lock (_lock) _theme = value; Save(); }
    }

    public string? Language
    {
        get { lock (_lock) return _language; }
        set { lock (_lock) _language = value; Save(); }
    }

    public void Load()
    {
        ConfigData data = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(_filePath), _jsonOptions) ?? new ConfigData()
            : new ConfigData();

        lock (_lock)
        {
            Fill(_hiddenPaths, data.HiddenPaths);
            _theme = Enum.IsDefined(data.Theme) ? data.Theme : ThemeMode.System;
            _language = data.Language;
        }
    }

    /// <summary>Whether the icon of this exe should be on the taskbar (not in the hide list).</summary>
    public bool IsShown(string normalizedPath)
    {
        lock (_lock)
            return !_hiddenPaths.Contains(normalizedPath);
    }

    /// <returns><c>true</c> if the hide list changed (and was saved).</returns>
    public bool SetShown(string path, bool shown)
    {
        string normalized = KnownFolderPathHelper.Normalize(path);
        lock (_lock)
        {
            bool changed = shown ? _hiddenPaths.Remove(normalized) : _hiddenPaths.Add(normalized);
            if (!changed)
                return false;
        }
        Save();
        return true;
    }

    private static void Fill(HashSet<string> target, IEnumerable<string> paths)
    {
        target.Clear();
        foreach (string path in paths)
            target.Add(KnownFolderPathHelper.Normalize(path));
    }

    private void Save()
    {
        string json;
        lock (_lock)
        {
            json = JsonSerializer.Serialize(new ConfigData
            {
                HiddenPaths = _hiddenPaths.Order(StringComparer.OrdinalIgnoreCase).ToList(),
                Theme = _theme,
                Language = _language,
            }, _jsonOptions);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        string tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
