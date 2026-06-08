namespace Content.Shared._Nibiru.EntityInspector;

/// <summary>
/// Помечает компонент как отображаемый в окне инспектора сущностей (Entity Inspector).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InspectableComponentAttribute : Attribute
{
    /// <summary>
    /// Отображаемое имя компонента в окне инспектора.
    /// Если не задано, будет использовано имя класса без суффикса «Component».
    /// </summary>
    public string? DisplayName { get; }

    public InspectableComponentAttribute(string? displayName = null)
    {
        DisplayName = displayName;
    }
}
