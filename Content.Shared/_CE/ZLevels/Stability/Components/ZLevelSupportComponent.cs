using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Stability.Components;

/// <summary>
/// This component indicates that an entity provides structural support for tiles on the level above.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelSupportComponent : Component
{
    /// <summary>
    /// How many tiles of connected structure this component can support.
    /// </summary>
    [DataField("radius")]
    public int Radius = 3;
}
