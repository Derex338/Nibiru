using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;

namespace Content.Shared._CE.ZLevels.Light.Components;

/// <summary>
///     Treats this entity as a light source that projects a ray in the sun's direction.
///     Works similarly to SunShadowCastComponent but for light.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SunLightRayCastComponent : Component
{
    [DataField]
    public Vector2[] Points = new[]
    {
        new Vector2(-0.5f, -0.5f),
        new Vector2(0.5f, -0.5f),
        new Vector2(0.5f, 0.5f),
        new Vector2(-0.5f, 0.5f),
    };
}
