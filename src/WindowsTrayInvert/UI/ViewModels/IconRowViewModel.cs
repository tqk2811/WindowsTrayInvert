using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsTrayInvert.Tray.Enums;
using WindowsTrayInvert.Tray.Models;

namespace WindowsTrayInvert.UI.ViewModels;

/// <summary>
/// One icon card. The toggle shows <see cref="IsShown"/> (like Settings); turning it off adds this icon (subkey id) to the
/// hide list through the owner callback.
/// </summary>
public sealed class IconRowViewModel : INotifyPropertyChanged
{
    private readonly Action<IconRowViewModel, bool> _onHiddenChanged;
    private bool _isHidden;
    private NotifyIconEntry _entry;
    private ImageSource? _image;
    private OrphanReason _orphanReason;

    public IconRowViewModel(NotifyIconEntry entry, bool isHidden, OrphanReason orphanReason, Action<IconRowViewModel, bool> onHiddenChanged)
    {
        _entry = entry;
        _isHidden = isHidden;
        _orphanReason = orphanReason;
        _onHiddenChanged = onHiddenChanged;
        _image = DecodeImage(entry.IconSnapshot);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => _entry.Id;

    public string? ExecutablePath => _entry.ExecutablePath;

    public string Name => !string.IsNullOrWhiteSpace(_entry.InitialTooltip)
        ? _entry.InitialTooltip.Split('\n')[0].Trim().Trim('‪', '‬')
        : Path.GetFileNameWithoutExtension(_entry.ExecutablePath) ?? _entry.Id;

    public ImageSource? Image => _image;

    /// <summary>Stale entry (see <see cref="Tray.OrphanDetector"/>).</summary>
    public bool IsOrphan => _orphanReason != OrphanReason.None;

    public bool IsFileMissing => _orphanReason == OrphanReason.MissingFile;

    public bool IsIconMissing => _orphanReason == OrphanReason.MissingIcon;

    public bool CanOpenFolder => _entry.ExecutablePath is not null;

    public bool IsShown
    {
        get => !_isHidden;
        set
        {
            if (_isHidden == !value)
                return;
            _isHidden = !value;
            OnPropertyChanged();
            _onHiddenChanged(this, _isHidden);
        }
    }

    /// <summary>Refresh from a newer registry read without re-triggering the callback.</summary>
    public void Update(NotifyIconEntry entry, bool isHidden, OrphanReason orphanReason)
    {
        if (_orphanReason != orphanReason)
        {
            _orphanReason = orphanReason;
            OnPropertyChanged(nameof(IsOrphan));
            OnPropertyChanged(nameof(IsFileMissing));
            OnPropertyChanged(nameof(IsIconMissing));
        }
        if (!ReferenceEquals(entry.IconSnapshot, _entry.IconSnapshot)
            && !(entry.IconSnapshot ?? []).AsSpan().SequenceEqual(_entry.IconSnapshot ?? []))
        {
            _image = DecodeImage(entry.IconSnapshot);
            OnPropertyChanged(nameof(Image));
        }
        _entry = entry;
        OnPropertyChanged(nameof(Name));
        if (_isHidden != isHidden)
        {
            _isHidden = isHidden;
            OnPropertyChanged(nameof(IsShown));
        }
    }

    private static ImageSource? DecodeImage(byte[]? png)
    {
        if (png is null || png.Length == 0)
            return null;
        try
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(png);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null; // unknown snapshot format: show the placeholder glyph
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
