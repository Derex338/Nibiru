namespace Content.Client.Localization;

/// <summary>
/// Интерфейс для UIController'ов и окон, которые должны
/// перезагружать свои тексты при смене языка.
/// </summary>
public interface ILanguageRefreshable
{
    /// <summary>
    /// Вызывается когда язык сменился. Контроллер/окно должно
    /// обновить все локализованные тексты и пересоздать окна.
    /// </summary>
    void OnLanguageChanged();
}
