using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Attack phases "bite and leap back" for wolves.
/// </summary>
[Serializable, NetSerializable]
public enum LeapPhase : byte
{
    /// <summary>
    /// Waiting for target / pursuing target.
    /// </summary>
    Idle,

    /// <summary>
    /// Performs a bite (melee attack).
    /// </summary>
    Biting,

    /// <summary>
    /// Physical leap backward relative to body direction.
    /// </summary>
    Leaping,

    /// <summary>
    /// Cooldown after the jump before the next cycle.
    /// </summary>
    Cooldown,
}

/// <summary>
/// Hit-and-run attack component — for wolves and similar predators.
/// The animal approaches the target, bites, then sharply leaps backward
/// relative to its body rotation, waits, and repeats.
/// The leap is implemented via physics impulse, not standard navigation.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcHitAndRunAttackComponent : Component
{
    // Runtime-state

    /// <summary>
    /// Current phase of the attack cycle.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public LeapPhase Phase = LeapPhase.Idle;

    /// <summary>
    /// Leap backward vector (normalized, filled at the moment of bite).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2 LeapDirection;

    /// <summary>
    /// Internal timer for the current phase.
    /// </summary>
    [ViewVariables]
    public float Timer;

    // Settings (DataField)

    /// <summary>
    /// Backward leap speed (units/sec).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float LeapSpeed = 9f;

    /// <summary>
    /// Leap distance (units). Determines the duration of the Leaping phase.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float LeapDistance = 3f;

    /// <summary>
    /// Pause in seconds after landing before the next cycle.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float WaitDuration = 1.0f;

    /// <summary>
    /// Distance to the target at which the bite begins (transition to Biting).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float AttackRange = 1.5f;
}
