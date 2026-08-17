namespace Content.Shared._Nibiru.EntityInspector;

/// <summary>
/// Data of one field for Entity Inspector window.
/// </summary>
/// <param name="DisplayName">Display name (or localization key).</param>
/// <param name="Value">Current field value.</param>
/// <param name="Detail">Detailed description for the right panel (or localization key). Null — do not show.</param>
/// <param name="RequireOwnership">If true, the field is displayed only to the entity owner (or the one holding it).</param>
public sealed record InspectableFieldData(
    string  DisplayName,
    object? Value,
    string? Detail = null,
    int     Order  = 0,
    bool    RequireOwnership = false
);

/// <summary>
/// Implement this interface on a component to make it display in the Entity Inspector window.
/// </summary>
public interface IInspectableComponent
{
    /// <summary>
    /// Component name in section header (or Fluent localization key).
    /// If empty string - name is taken from type name without «Component» suffix.
    /// </summary>
    string InspectorDisplayName { get; }

    /// <summary>
    /// Returns list of fields to display in the inspector.
    /// Called every time the window opens.
    /// </summary>
    IEnumerable<InspectableFieldData> GetInspectableFields();
}
