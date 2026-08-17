using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Component for an animal that has latched onto a target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalGrabbedComponent : Component
{
    /// <summary>
    /// Target Entity being grabbed
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField]
    public float TargetSlowdownMultiplier = 0.6f;

    /// <summary>
    /// Damage per second while animal is grabbing target.
    /// </summary>
    [DataField]
    public DamageSpecifier? TickDamage;

    /// <summary>
    /// Accumulator for tick damage.
    /// </summary>
    [ViewVariables]
    public float DamageAccumulator;

    /// <summary>
    /// Damage interval in seconds.
    /// </summary>
    [DataField]
    public float DamageInterval = 1f;

    /// <summary>
    /// Accumulator for shake effect.
    /// </summary>
    [ViewVariables]
    public float ShakeAccumulator;

    /// <summary>
    /// Shake interval in seconds.
    /// </summary>
    [DataField]
    public float ShakeInterval = 0.25f;

    /// <summary>
    /// Shake amplitude in tiles.
    /// </summary>
    [DataField]
    public float ShakeAmplitude = 0.4f;

    /// <summary>
    /// Current shake direction (+1 or -1).
    /// </summary>
    [ViewVariables]
    public float ShakeDirection = 1f;

    /// <summary>
    /// DoAfter duration for detaching (seconds).
    /// </summary>
    [DataField]
    public float DetachDuration = 3f;
}

[Serializable, NetSerializable]
public sealed partial class NibiruAnimalDetachDoAfterEvent : SimpleDoAfterEvent
{
}
