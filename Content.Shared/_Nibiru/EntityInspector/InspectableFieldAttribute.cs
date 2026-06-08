namespace Content.Shared._Nibiru.EntityInspector;

/// <summary>
/// Помечает поле или свойство компонента для отображения в окне инспектора сущностей.
/// Значение показывается inline в левой колонке. При наличии <see cref="Detail"/> —
/// при клике в правой панели появится расширенная информация.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class InspectableFieldAttribute : Attribute
{
    /// <summary>
    /// Отображаемое имя поля. Если не задано, берётся имя члена класса.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Подробное описание, которое будет показано в правой панели при клике на поле.
    /// Если не задано, правая панель показывает полное строковое представление значения.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Порядок отображения поля внутри секции компонента (меньше — выше).
    /// </summary>
    public int Order { get; set; } = 0;

    public InspectableFieldAttribute(string? displayName = null)
    {
        DisplayName = displayName;
    }
}
