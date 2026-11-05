using WindowsTrayInvert.Tray.Models;

namespace WindowsTrayInvert.Promotion.Models;

/// <summary>An icon whose <c>IsPromoted</c> differs from what the hide list requires.</summary>
public sealed class PromotionChange
{
    public required NotifyIconEntry Entry { get; init; }

    public required bool Promote { get; init; }
}
