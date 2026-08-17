namespace Content.Client.Localization;

/// <summary>
/// Interface for UI controllers and windows that need to
/// refresh their texts when the language changes.
/// </summary>
public interface ILanguageRefreshable
{
    /// <summary>
    /// Called when the language changes. The controller/window should
    /// update all localized texts and recreate windows.
    /// </summary>
    void OnLanguageChanged();
}
