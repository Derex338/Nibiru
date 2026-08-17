using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Phases of attack with a run-up for horned animals.
/// </summary>
[Serializable, NetSerializable]
public enum ChargePhase : byte
{
    /// <summary>
    /// Waiting for / chasing the target. Transition to WindUp occurs
    /// when the target enters the run-up range.
    /// </summary>
    Idle,

    /// <summary>
    /// The animal has stopped, turned to the target and is shaking.
    /// Lasts <see cref="NibiruNpcChargeAttackComponent.ShakeDuration"/> seconds.
    /// </summary>
    WindUp,

    /// <summary>
    /// Active run-up - flies in a straight line through physical impulse.
    /// Trembling removed. Damage to everything in the way through collisions.
    /// Stops when <see cref="NibiruNpcChargeAttackComponent.MaxDuration"/> expires
    /// or when colliding with a static object.
    /// </summary>
    Charging,

    /// <summary>
    /// Cooldown after the run-up before returning to pursuit.
    /// </summary>
    Cooldown,
}

/// <summary>
/// Component for attack with a run-up - for horned animals (goats, cows, etc.).
/// The animal enters the run-up range, stops, turns to the target,
/// begins to shake (WindUp), then runs in a straight line, dealing damage
/// to all entities in the path. The run-up stops when colliding with a solid
/// object or when the maximum time expires.
/// Movement is implemented through physical impulse, without NPC navigation.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcChargeAttackComponent : Component
{
    // Runtime-state

    /// <summary>
    /// Current phase of the run-up.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public ChargePhase Phase = ChargePhase.Idle;

    /// <summary>
    /// Normalized vector of the run-up direction.
    /// Fixed at the moment of entering WindUp and does not change until the end of the run-up.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2 Direction;

    /// <summary>
    /// Internal timer for the current phase.
    /// </summary>
    [ViewVariables]
    public float Timer;

    /// <summary>
    /// Set of entities that have already received damage in the current run-up.
    /// Cleared when entering WindUp.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> HitEntities = new();

    // Config (DataField)

    /// <summary>
    /// Duration of the shake before the run-up (seconds).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float ShakeDuration = 1.0f;

    /// <summary>
    /// Run-up speed (units per second).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Speed = 15f;

    /// <summary>
    /// Maximum duration of the run-up (seconds).
    /// If no collision with a wall occurs during this time, it stops by itself.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MaxDuration = 1.5f;

    /// <summary>
    /// Cooldown duration after the run-up (seconds).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float CooldownDuration = 5.0f;

    /// <summary>
    /// Minimum distance to the target for the start of WindUp.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MinChargeDistance = 3f;

    /// <summary>
    /// Maximum distance to the target for the start of WindUp.
    /// If the target is further away, the pursuit continues.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float MaxChargeDistance = 9f;

    /// <summary>
    /// Damage dealt to each entity upon collision during the run-up.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public DamageSpecifier? Damage;

    /// <summary>
    /// Knockback force applied to entities upon collision during the run-up.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float KnockbackForce = 6f;

    /// <summary>
    /// Whether to stop the run-up upon collision with a static object (wall).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool StopOnWallCollision = true;
}
