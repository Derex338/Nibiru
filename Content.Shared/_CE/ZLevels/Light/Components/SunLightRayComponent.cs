using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Light.Components;

/// <summary>
///     Added to a map to enable sun light ray projection.
///     The rays will follow the same direction as SunShadowComponent if it exists.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SunLightRayComponent : Component
{
    /// <summary>
    ///     Intensity multiplier for the light rays.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity = 1.0f;

    /// <summary>
    ///     The color of the rays. If null, will use the sun color from LightCycleComponent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? Color;
}
