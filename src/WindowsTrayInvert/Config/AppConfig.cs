using System.Text.Json;
using WindowsTrayInvert.Config.Enums;
using WindowsTrayInvert.Config.Models;

namespace WindowsTrayInvert.Config;

/// <summary>
/// App settings persisted as JSON: hide list, theme, language. The hide list holds <c>NotifyIconSettings</c> subkey
/// ids, so icons sharing one exe (several explorer.exe icons) are toggled separately.
/// Thread-safe: the watcher thread reads while the UI thread may edit.
/// </summary>
public sealed class AppConfig
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly HashSet<string> _hiddenIds = new(StringComparer.OrdinalIgnoreCase);
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
            _hiddenIds.Clear();
            _hiddenIds.UnionWith(data.HiddenIds ?? []);
            _theme = Enum.IsDefined(data.Theme) ? data.Theme : ThemeMode.System;
            _language = data.Language;
        }
    }

    /// <summary>Whether the icon with this subkey id should be on the taskbar (not in the hide list).</summary>
    public bool IsShown(string id)
    {
        lock (_lock)
            return !_hiddenIds.Contains(id);
    }

    /// <returns><c>true</c> if the hide list changed (and was saved).</returns>
    public bool SetShown(string id, bool shown)
    {
        lock (_lock)
        {
            bool changed = shown ? _hiddenIds.Remove(id) : _hiddenIds.Add(id);
            if (!changed)
                return false;
        }
        Save();
        return true;
    }

    /// <summary>Drops ids whose subkey was deleted (orphan cleanup), so the list does not grow forever.</summary>
    public void Forget(IEnumerable<string> ids)
    {
        lock (_lock)
        {
            int before = _hiddenIds.Count;
            _hiddenIds.ExceptWith(ids);
            if (_hiddenIds.Count == before)
                return;
        }
        Save();
    }

    private void Save()
    {
        string json;
        lock (_lock)
        {
            json = JsonSerializer.Serialize(new ConfigData
            {
                HiddenIds = _hiddenIds.Order(StringComparer.OrdinalIgnoreCase).ToList(),
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
