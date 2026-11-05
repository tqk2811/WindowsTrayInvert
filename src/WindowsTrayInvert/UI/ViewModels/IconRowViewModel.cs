using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsTrayInvert.Tray.Models;

namespace WindowsTrayInvert.UI.ViewModels;

/// <summary>
/// One icon card. The toggle shows <see cref="IsShown"/> (like Settings); turning it off adds the exe to the
/// hide list through the owner callback.
/// </summary>
public sealed class IconRowViewModel : INotifyPropertyChanged
{
    private readonly Action<IconRowViewModel, bool> _onHiddenChanged;
    private bool _isHidden;
    private NotifyIconEntry _entry;
    private ImageSource? _image;
    private bool _isOrphan;

    public IconRowViewModel(NotifyIconEntry entry, bool isHidden, bool isOrphan, Action<IconRowViewModel, bool> onHiddenChanged)
    {
        _entry = entry;
        _isHidden = isHidden;
        _isOrphan = isOrphan;
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

    /// <summary>The executable no longer exists on disk (see <see cref="Tray.OrphanDetector"/>).</summary>
    public bool IsOrphan => _isOrphan;

    public bool CanToggle => _entry.ExecutablePath is not null;

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
    public void Update(NotifyIconEntry entry, bool isHidden, bool isOrphan)
    {
        if (_isOrphan != isOrphan)
        {
            _isOrphan = isOrphan;
            OnPropertyChanged(nameof(IsOrphan));
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
