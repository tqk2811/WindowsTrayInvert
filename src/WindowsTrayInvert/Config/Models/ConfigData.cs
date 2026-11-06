using System.Text.Json.Serialization;
using WindowsTrayInvert.Config.Enums;

namespace WindowsTrayInvert.Config.Models;

/// <summary>JSON shape of <c>config.json</c>.</summary>
public sealed class ConfigData
{
    /// <summary>Hidden icons, as <c>NotifyIconSettings</c> subkey ids.</summary>
    public List<string>? HiddenIds { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>Culture name (<c>en</c>, <c>vi</c>); <c>null</c> = follow the Windows display language.</summary>
    public string? Language { get; set; }

    /// <summary>Render on the CPU instead of the GPU: fewer threads and less memory; applied at startup.</summary>
    public bool SoftwareRendering { get; set; }
}
