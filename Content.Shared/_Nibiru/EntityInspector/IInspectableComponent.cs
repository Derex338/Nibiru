namespace Content.Shared._Nibiru.EntityInspector;

/// <summary>
/// Данные одного отображаемого поля для окна инспектора сущностей.
/// </summary>
/// <param name="DisplayName">Отображаемое имя (или ключ локализации).</param>
/// <param name="Value">Текущее значение поля.</param>
/// <param name="Detail">Подробное описание для правой панели (или ключ локализации). Null — не показывать.</param>
/// <param name="RequireOwnership">Если true, поле отображается только хозяину сущности (или тому, кто её держит).</param>
public sealed record InspectableFieldData(
    string  DisplayName,
    object? Value,
    string? Detail = null,
    int     Order  = 0,
    bool    RequireOwnership = false
);

/// <summary>
/// Реализуйте этот интерфейс на компоненте, чтобы он отображался в окне инспектора сущностей
/// </summary>
public interface IInspectableComponent
{
    /// <summary>
    /// Имя компонента в заголовке секции (или ключ Fluent-локализации).
    /// Если пустая строка — имя берётся из имени типа без суффикса «Component».
    /// </summary>
    string InspectorDisplayName { get; }

    /// <summary>
    /// Возвращает список полей для отображения в инспекторе.
    /// Вызывается каждый раз при открытии/обновлении окна.
    /// </summary>
    IEnumerable<InspectableFieldData> GetInspectableFields();
}
