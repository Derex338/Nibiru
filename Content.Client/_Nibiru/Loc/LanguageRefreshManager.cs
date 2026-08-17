using System.Collections.Generic;

namespace Content.Client.Localization;

/// <summary>
/// Static registry of components that need to be updated when the language changes.
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
