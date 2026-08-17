using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Heating;

/// <summary>
/// Component for surfaces that heat items (campfire, brazier, etc.)
/// Works with PlaceableSurfaceComponent to detect items on the surface
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HeatingSurfaceComponent : Component
{
    /// <summary>
    /// How fast items heat up in degrees per second
    /// </summary>
    [DataField]
    public float HeatingRate = 50f;

    /// <summary>
    /// Temperature at which items start to burn
    /// </summary>
    [DataField]
    public float BurnTemperature = 500f;

    /// <summary>
    /// Heating radius (if PlaceableSurface is not used)
    /// </summary>
    [DataField]
    public float HeatingRadius = 0.5f;

    /// <summary>
    /// Minimum source temperature to work
    /// </summary>
    [DataField]
    public float MinSourceTemperature = 3f;

    /// <summary>
    /// Only items on PlaceableSurface heat up
    /// </summary>
    [DataField]
    public bool RequirePlacedOnSurface = true;

    /// <summary>
    /// Sound made when item burns
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;

    /// <summary>
    /// Sound made while cooking/heating
    /// </summary>
    [DataField]
    public SoundSpecifier? CookingSound;

    /// <summary>
    /// Sound interval (seconds)
    /// </summary>
    [DataField]
    public float CookingSoundInterval = 3f;

    [DataField]
    public float CookingSoundTimer = 0f;
}

[Serializable, NetSerializable]
public enum HeatingSurfaceVisuals : byte
{
    IsHeating,
    HasItems
}
