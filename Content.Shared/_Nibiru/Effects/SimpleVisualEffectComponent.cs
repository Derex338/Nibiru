using System.Numerics;

namespace Content.Shared._Nibiru.Effects;

[RegisterComponent]
public sealed partial class SimpleVisualEffectComponent : Component
{
    /// <summary>
    /// How much the entity moves per second.
    /// </summary>
    [DataField]
    public Vector2 MoveRate = Vector2.Zero;

    /// <summary>
    /// How much the entity's scale increases per second.
    /// </summary>
    [DataField]
    public Vector2 ScaleRate = Vector2.Zero;

    /// <summary>
    /// The maximum scale the entity can reach.
    /// </summary>
    [DataField]
    public Vector2 MaxScale = new Vector2(5f, 5f);
}
