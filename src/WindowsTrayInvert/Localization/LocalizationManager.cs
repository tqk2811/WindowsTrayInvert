using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace WindowsTrayInvert.Localization;

/// <summary>
/// Runtime-switchable UI strings from <c>Localization/Strings*.resx</c>. XAML binds to the indexer (via
/// <see cref="TrExtension"/>); changing <see cref="Culture"/> raises <c>Item[]</c> so every binding updates at once.
/// A singleton because XAML markup extensions need a static source.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static readonly IReadOnlyList<CultureInfo> SupportedCultures = [new("en"), new("vi")];

    private static readonly ResourceManager _resources =
        new("WindowsTrayInvert.Localization.Strings", typeof(LocalizationManager).Assembly);

    private CultureInfo _culture = new("en");

    private LocalizationManager() { }

    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after <see cref="Culture"/> changes, for text set from code (tray menu...).</summary>
    public event EventHandler? CultureChanged;

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (_culture.Name == value.Name)
                return;
            _culture = value;
            CultureInfo.CurrentUICulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Missing keys show as <c>[key]</c> so they are visible instead of blank.</summary>
    public string this[string key] => _resources.GetString(key, _culture) ?? $"[{key}]";

    public string Format(string key, params object[] args) => string.Format(_culture, this[key], args);

    /// <summary>Saved culture name if supported, otherwise the Windows display language, otherwise English.</summary>
    public static CultureInfo Resolve(string? savedName)
    {
        CultureInfo? saved = SupportedCultures.FirstOrDefault(x => x.Name == savedName);
        if (saved is not null)
            return saved;
        string system = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        return SupportedCultures.FirstOrDefault(x => x.TwoLetterISOLanguageName == system) ?? SupportedCultures[0];
    }
}
