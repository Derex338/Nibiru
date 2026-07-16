using System.Collections.Generic;

namespace Content.Client.Localization;

/// <summary>
/// Статический реестр компонентов, которые нужно обновлять при смене языка.
/// Альтернатива изменению движка — UIController'ы сами регистрируются через Initialize().
/// </summary>
public static class LanguageRefreshManager
{
    private static readonly List<ILanguageRefreshable> _refreshables = new();

    public static void Register(ILanguageRefreshable refreshable)
    {
        if (!_refreshables.Contains(refreshable))
            _refreshables.Add(refreshable);
    }

    public static void Unregister(ILanguageRefreshable refreshable)
    {
        _refreshables.Remove(refreshable);
    }

    public static void RefreshAll()
    {
        foreach (var r in _refreshables)
        {
            r.OnLanguageChanged();
        }
    }
}
