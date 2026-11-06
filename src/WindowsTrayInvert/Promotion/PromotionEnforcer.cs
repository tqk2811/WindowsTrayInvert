using WindowsTrayInvert.Config;
using WindowsTrayInvert.Promotion.Models;
using WindowsTrayInvert.Tray;

namespace WindowsTrayInvert.Promotion;

/// <summary>
/// Sets <c>IsPromoted</c> of every icon to what <see cref="AppConfig.IsShown"/> says (shown unless in the hide list).
/// Only writes values that differ, so its own writes settle the registry watcher instead of looping.
/// </summary>
public sealed class PromotionEnforcer
{
    private readonly NotifyIconStore _store;
    private readonly AppConfig _config;
    private readonly object _applyLock = new();

    public PromotionEnforcer(NotifyIconStore store, AppConfig config)
    {
        _store = store;
        _config = config;
    }

    /// <summary>Computes what <see cref="Apply"/> would change, without writing.</summary>
    public IReadOnlyList<PromotionChange> Plan()
    {
        List<PromotionChange> changes = [];
        foreach (var entry in _store.ReadAll(includeDisplayData: false))
        {
            bool promote = _config.IsShown(entry.Id);
            int desired = promote ? 1 : 0;
            if ((entry.IsPromoted ?? 0) != desired)
                changes.Add(new PromotionChange { Entry = entry, Promote = promote });
        }
        return changes;
    }

    /// <returns>The changes actually written.</returns>
    public IReadOnlyList<PromotionChange> Apply()
    {
        lock (_applyLock)
        {
            List<PromotionChange> applied = [];
            foreach (var change in Plan())
            {
                if (_store.SetPromoted(change.Entry.Id, change.Promote))
                    applied.Add(change);
            }
            return applied;
        }
    }
}
