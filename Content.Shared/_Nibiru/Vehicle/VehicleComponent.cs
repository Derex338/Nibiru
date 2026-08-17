using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Component for entities that can be controlled via Strap (horses, vehicles)
/// Works automatically when attached to a StrapComponent
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RideableComponent : Component
{
    /// <summary>
    /// Locomotion type of the vehicle
    /// </summary>
    [DataField, AutoNetworkedField]
    public RideableLocomotionType LocomotionType = RideableLocomotionType.Legs;

    /// <summary>
    /// Can the rider control the vehicle if it's dead
    /// </summary>
    [DataField]
    public bool CanMoveWhenDead = false;

    /// <summary>
    /// Sprite state when rider is on the vehicle
    /// </summary>
    [DataField]
    public string? MountedState;

    /// <summary>
    /// Base sprite state of the vehicle
    /// </summary>
    [DataField]
    public string? BaseState;

    [DataField]
    public bool NeedSeddle = true;
}

/// <summary>
/// Locomotion type of the vehicle
/// </summary>
[Serializable, NetSerializable]
public enum RideableLocomotionType : byte
{
    /// <summary>
    /// Locomotion on legs (horses, animals)
    /// </summary>
    Legs,

    /// <summary>
    /// Locomotion on wheels (bicycles, cars)
    /// </summary>
    Wheels,

    /// <summary>
    /// Locomotion on tracks (tanks)
    /// </summary>
    Tracks,

    /// <summary>
    /// Locomotion by flight (flying mounts)
    /// </summary>
    Flight
}

[Serializable, NetSerializable]
public enum RideableVisuals : byte
{
    Mounted,
    Dead
}
