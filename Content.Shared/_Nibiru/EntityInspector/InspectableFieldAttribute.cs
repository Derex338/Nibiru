namespace Content.Shared._Nibiru.EntityInspector;

/// <summary>
/// Marks a field or property of a component for display in the Entity Inspector window.
/// The value is displayed inline in the left column. If <see cref="Detail"/> is specified —
/// clicking it will show detailed information in the right panel.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class InspectableFieldAttribute : Attribute
{
    /// <summary>
    /// Display name of the field. If not specified, the name will be taken from the class name.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Detailed description that will be shown in the right panel when the field is clicked.
    /// If not specified, the right panel will show the full string representation of the value.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Order of the field within the component section (smaller = higher).
    /// </summary>
    public int Order { get; set; } = 0;

    public InspectableFieldAttribute(string? displayName = null)
    {
        DisplayName = displayName;
    }
}
