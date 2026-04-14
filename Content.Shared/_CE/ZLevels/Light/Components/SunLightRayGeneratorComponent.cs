using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Light.Components;

/// <summary>
///     Internal component used to track light ray entities on a grid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SunLightRayGeneratorComponent : Component
{
    [DataField]
    public Dictionary<Vector2i, EntityUid> Rays = new();
}
