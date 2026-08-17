namespace Content.Shared._Nibiru.EntityInspector;

/// <summary>
/// Marks a component to be displayed in the entity inspector window (Entity Inspector).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InspectableComponentAttribute : Attribute
{
    /// <summary>
    /// Display name of the component in inspector window.
    /// If not specified, the name will be taken from the class name without the "Component" suffix.
    /// </summary>
    public string? DisplayName { get; }

    public InspectableComponentAttribute(string? displayName = null)
    {
        DisplayName = displayName;
    }
}
