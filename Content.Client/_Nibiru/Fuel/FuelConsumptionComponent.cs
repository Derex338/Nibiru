using Content.Client.Light.EntitySystems;
using Content.Shared.Light.Components;
using Robust.Shared.Audio;
using Content.Shared._Nibiru.Fuel;

namespace Content.Client._Nibiru.Fuel;

/// <summary>
/// Component that represents a handheld expendable light which can be activated and eventually dies over time.
/// </summary>
[RegisterComponent]
public sealed partial class FuelConsumptionComponent : SharedFuelConsumptionComponent
{
    /// <summary>
    /// The icon state used by expendable lights when the they have been completely expended.
    /// </summary>
    [DataField("iconStateSpent")]
    public string? IconStateSpent;

    /// <summary>
    /// The icon state used by expendable lights while they are lit.
    /// </summary>
    [DataField("iconStateLit")]
    public string? IconStateLit;

    /// <summary>
    /// The sprite layer shader used while the expendable light is lit.
    /// </summary>
    [DataField("spriteShaderLit")]
    public string? SpriteShaderLit = null;

    /// <summary>
    /// The sprite layer shader used after the expendable light has burnt out.
    /// </summary>
    [DataField("spriteShaderSpent")]
    public string? SpriteShaderSpent = null;

    /// <summary>
    /// The sprite layer shader used after the expendable light has burnt out.
    /// </summary>
    [DataField("glowColorLit")]
    public Color? GlowColorLit = null;

    /// <summary>
    /// The sound that plays when the expendable light is lit.
    /// </summary>
    [Access(typeof(FuelConsumptionSystem))]
    public EntityUid? PlayingStream;
}

public enum FuelLightVisualLayers : byte
{
    Base = 0,
    Glow = 1,
    Overlay = 2,
}
